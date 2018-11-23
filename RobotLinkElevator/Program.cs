using ElevatorActions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using SocketCommunicationFoundation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static RobotLinkElevator.CodeHelper;
using DataModule.Model;
using Newtonsoft.Json.Linq;

namespace RobotLinkElevator
{
    partial class Program
    {
        static readonly IConfigurationBuilder ConfigurationBuilder = new ConfigurationBuilder();
        static IConfigurationRoot _configuration;
        //static bool isTest = true;

        static ConcurrentQueue<ElevatorReceive> ElevatorReceives { get; set; } = new ConcurrentQueue<ElevatorReceive>();
        static ConcurrentQueue<ElevatorSend> ElevatorSends { get; set; } = new ConcurrentQueue<ElevatorSend>();
        static ConcurrentDictionary<Socket, ElevatorSocketLinkInfo> ElevatorLinks { get; set; } = new ConcurrentDictionary<Socket, ElevatorSocketLinkInfo>();

        static ConcurrentQueue<RobotReceive> RobotReceives { get; set; } = new ConcurrentQueue<RobotReceive>();
        static ConcurrentQueue<RobotSend> RobotSends { get; set; } = new ConcurrentQueue<RobotSend>();
        public static ConcurrentDictionary<WebSocket, RobotWebSocketLinkInfo> RobotLinks { get; set; } = new ConcurrentDictionary<WebSocket, RobotWebSocketLinkInfo>();

        static List<CallElevatorTask> CallElevatorTasks { get; set; } = new List<CallElevatorTask>();
        static ConcurrentQueue<CallElevatorTask> CallElevatorStatuses { get; set; } = new ConcurrentQueue<CallElevatorTask>();

        static void Main(string[] args)
        {
            #region 系统初始化
            _configuration = ConfigurationBuilder
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile(cfg =>
                {
                    cfg.Path = "appsettings.json";
                    cfg.ReloadOnChange = true;
                    cfg.Optional = true;
                })
                .Build();

            //Log.Logger = new LoggerConfiguration()
            //    .ReadFrom.Configuration(_configuration)
            //    .CreateLogger();

            #region 以天为单位创建滚动日志
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(@"logs\RobotLinkElevator.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            #endregion
            var jo = JsonConvert.DeserializeObject<JObject>(System.IO.File.ReadAllText("appsettings.json"));
            var inspectionMicroSecond = Convert.ToInt32(jo["System"]["InspectionMicroSecond"].ToString());

            #endregion
            #region 启动电梯相关处理线程
            Task.Factory.StartNew(ProcessSendSocket);
            Task.Factory.StartNew(ProcessReceiveSocket);
            Task.Factory.StartNew(ProcessCallElevator);
            Task.Factory.StartNew(()=> {
                // 巡检连接状态
                do
                {
                    #region 巡检电梯连接
                    var obsElevatorList = new List<KeyValuePair<Socket, ElevatorSocketLinkInfo>>();
                    foreach (var d in ElevatorLinks)
                    {
                        var ts = DateTime.Now - d.Value.HeartbeatTime;
                        // 2分钟没有心跳或者SN 为空，断开。
                        if (ts.Minutes > 1 || ts.Hours > 0 || ts.Days > 0 || string.IsNullOrEmpty(d.Value.SN))
                        {
                            obsElevatorList.Add(d);
                        }
                        try
                        {
                            if (d.Value.Socket.Poll(100, SelectMode.SelectError))
                            {
                                obsElevatorList.Add(d);
                            }
                            else if (d.Key.Poll(100, SelectMode.SelectError))
                            {
                                obsElevatorList.Add(d);
                            }
                        }
                        catch (Exception)
                        {
                            obsElevatorList.Add(d);
                            DebugHelper.PrintErrorMessage($"连接异常，移除当前连接.");
                        }
                    }
                    obsElevatorList.ForEach(x =>
                    {
                        ElevatorLinks.TryRemove(x.Key, out ElevatorSocketLinkInfo _);
                        try
                        {
                            x.Key.Close();
                        }
                        catch { }
                    });
                    obsElevatorList.Clear();
                    #endregion
                    #region 巡检机器人连接
                    var obsRobotList = new List<KeyValuePair<WebSocket, RobotWebSocketLinkInfo>>();
                    foreach (var d in RobotLinks)
                    {
                        var ts = DateTime.Now - d.Value.HeartbeatTime;
                        // 2分钟没有心跳，断开。
                        if (ts.Days > 0 || ts.Hours > 0 || ts.Minutes > 2)
                        {
                            obsRobotList.Add(d);
                        }
                        else if (d.Value.WebSocket.State == WebSocketState.Aborted 
                        || d.Value.WebSocket.State == WebSocketState.Closed)
                        {
                            obsRobotList.Add(d);
                        }
                    }
                    obsRobotList.ForEach(x =>
                    {
                        RobotLinks.TryRemove(x.Key, out RobotWebSocketLinkInfo _);
                    });
                    obsRobotList.Clear();
                    #endregion
                    #region 输出所有有效的连接
                    DebugHelper.PrintTraceMessage($"Total ElevatorLinks:{ElevatorLinks.Count}");
                    if (ElevatorLinks.Count > 0)
                    {
                        foreach (var d in ElevatorLinks)
                        {
                            DebugHelper.PrintTraceMessage($"Handle:{d.Key.Handle}, ElevatorId:{d.Value.SN}, Heartbeat:{d.Value.HeartbeatTime}");
                        }
                    }

                    DebugHelper.PrintTraceMessage($"Total RobotLinks:{RobotLinks.Count}");
                    if (RobotLinks.Count > 0)
                    {
                        foreach (var d in RobotLinks)
                        {
                            DebugHelper.PrintTraceMessage($"RobotSN:{d.Value.SN}, Heartbeat:{d.Value.HeartbeatTime}");
                        }
                    }
                    #endregion
                    Thread.Sleep(inspectionMicroSecond); // 每隔x毫秒做一次检查
                } while (true);
            });
            Task.Run(() => RunSocketServer());
            #endregion
            #region 启动机器人相关线程
            Task.Factory.StartNew(ProcessSendWebSocket);
            Task.Factory.StartNew(ProcessReceiveWebSocket);
            Task.Run(() => RunWebSocketServer());
            #endregion
            Console.ReadKey();
        }

        void CommandProcess(ElevatorData ed)
        {
            //var method = new MainWindow().GetType().GetMethod("Command" + ed.Command);
            //if (method != null)
            //{
            //    object o = method.Invoke(this, new object[] { ed.Data });
            //    Convert.ToString(o);
            //}
        }
        public static void ElevatorCommand_01(ElevatorSocketLinkInfo elevatorLink, ElevatorData elevatorData)
        { 
            try
            {
                Array.Reverse(elevatorData.Data);

                string sn = "";
                foreach (var d in elevatorData.Data)
                {
                    sn += Convert.ToString(d, 16).PadLeft(2, '0') + "";
                }

                DebugHelper.PrintTraceMessage($"[ElevatorCommand_01]Socket New Link:{sn}");

                var v = ElevatorLinks.LastOrDefault(e => e.Value.SN == sn);
                if (!elevatorLink.Socket.Equals(v.Key) && v.Key != null)
                {
                    ElevatorLinks.TryRemove(v.Key, out _);
                }

                byte ConfirmId = Convert.ToByte(new Random().Next(1, 250));
                //if (isTest)
                //{
                //    ConfirmId = 0xDA;
                //}

                elevatorLink.SN = sn;
                elevatorLink.ConfirmId = ConfirmId;

                //配置响应数据
                ElevatorCommand_81(sn, ConfirmId);

                DebugHelper.PrintTraceMessage($"Socket New Link:{sn}{ConfirmId}");
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"ElevatorCommand_01:{ex.Message}");
            }            
        }

        /// <summary>
        /// 接收电梯的报文
        /// </summary>
        /// <param name="st"></param>
        /// <param name="data"></param>
        static void ReceiveSocket(Socket st, byte[] data)
        {
            string d = "";
            foreach (byte b in data)
            {
                var s = Convert.ToString(b, 16).PadLeft(2, '0');
                d += s + " ";
            }
            d = d.TrimEnd(' ');
            DebugHelper.PrintTraceMessage($"ReceiveSocket:{d},{st.RemoteEndPoint}");

            try
            {
                var elevatorReceive = new ElevatorReceive();
                if (data.Length == 1 && data[0] == 0x5a)
                {
                    // 心跳包
                    elevatorReceive.Data = data;
                }
                else
                {
                    if (CheckDataRule(data))
                    {
                        var sb = new StringBuilder();
                        foreach (var b in data)
                        {
                            sb.Append($"{Convert.ToString(b, 16).PadLeft(2, '0').ToUpper()} ");
                        }
                        DebugHelper.PrintTraceMessage($"[接收电梯报文][电梯]数据校验通过[CS校验 | 数据长度 | 数据头格式][CheckDataRule]::[{sb}]");
                        elevatorReceive = DataAnalysis(data);
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        foreach (var b in data)
                        {
                            sb.Append($"{Convert.ToString(b, 16).PadLeft(2, '0').ToUpper()} ");
                        }
                        DebugHelper.PrintErrorMessage($"[接收电梯报文][电梯]数据校验失败[CS校验 | 数据长度 | 数据头格式][CheckDataRule]::[{sb}]");
                        elevatorReceive.Command = 0;
                    }
                }
                elevatorReceive.Socket = st;
                ElevatorReceives.Enqueue(elevatorReceive);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"ReceiveSocket:{ex.Message}");
            }
        }
        /// <summary>
        /// 组织数据发给电梯
        /// </summary>
        /// <param name="st"></param>
        /// <param name="ed"></param>
        /// <param name="confirmId"></param>
        /// <returns></returns>
        static bool SendSocket(Socket st, ElevatorData ed, byte confirmId)
        {
            int dataLength = 0;
            try
            {
                dataLength = ed.Data.Length;
                if (ed.Append == 0x01)
                {
                    dataLength += 20;
                }
                byte[] data = new byte[dataLength + 9];
                data[0] = data[3] = 0x68;
                data[1] = data[2] = Convert.ToByte(3 + dataLength);
                data[4] = ed.Command;
                data[5] = ed.Address;
                data[6] = ed.Append;
                for (int i = 0; i < ed.Data.Length; i++)
                {
                    data[i + 7] = ed.Data[i];
                }
                if (ed.Append == 0x01)
                {
                    byte[] temp = new byte[ed.Data.Length + 1];
                    Array.Copy(ed.Data, temp, ed.Data.Length);
                    temp[temp.Length - 1] = confirmId;

                    Array.Sort(temp);
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    var sign = sha.ComputeHash(temp);
                    Array.Copy(sign, 0, data, 7 + ed.Data.Length, 20);
                }
                byte cs = 0;
                for (int i = 4; i < data.Length - 2; i++)
                {
                    cs += data[i];
                }
                data[data.Length - 2] = cs;
                data[data.Length - 1] = 0x16;

                st.Send(data);
                DebugHelper.PrintRxMessage(data);

                string d = "";
                foreach (byte b in data)
                {
                    var s = Convert.ToString(b, 16).PadLeft(2, '0');
                    d += s + " ";
                }
                DebugHelper.PrintTraceMessage($"SendSocket:{d},{st.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"[SendSocket]Exception:{ex.Message}");

                return false;
            }
            return true;
        }

        static void ProcessReceiveSocket()
        {
            while (true)
            {
                try
                {
                    if (ElevatorReceives.Count > 0)
                    {
                        Task.Run(() =>
                        {
                            ElevatorReceive param = null;
                            try
                            {
                                // 尝试移除并返回并发队列开头处的对象。
                                if (ElevatorReceives.TryDequeue(out param))
                                {
                                    if (!ElevatorLinks.TryGetValue(param.Socket, out ElevatorSocketLinkInfo el))
                                    {
                                        el = new ElevatorSocketLinkInfo();
                                        el.Socket = param.Socket;
                                        el.OnlineTime = DateTime.Now;

                                        ElevatorLinks.TryAdd(param.Socket, el);
                                    }

                                    el.HeartbeatTime = DateTime.Now;
                                    var c = Convert.ToString(param.Command, 16);
                                    var cmd = c.Length == 1 ? $"0{c}" : c;
                                    var method = new Program().GetType().GetMethod("ElevatorCommand_" + cmd);

                                    if (method != null)
                                    {
                                        try
                                        {
                                            object o = method.Invoke(null, new object[] { el, param });
                                            DebugHelper.PrintTraceMessage($"[ProcessReceiveSocket][调用反射方法成功][ElevatorCommand_{cmd}]...");
                                        }
                                        catch (Exception ex3)
                                        {
                                            DebugHelper.PrintErrorMessage($"[ProcessReceiveSocket][调用反射方法失败][ElevatorCommand_{cmd}][{ex3.Message}]...");
                                        }
                                    }
                                    else
                                    {
                                        if (param.Data.Length == 1 && param.Data[0] == 0x5a)
                                        {
                                            ElevatorSend ed = new ElevatorSend();
                                            ed.SN = el.SN;
                                            ed.Command = 0;
                                            ed.Data = new byte[1] { 0x5a };
                                            // 发送心跳包回文
                                            ElevatorSends.Enqueue(ed);
                                        }
                                        else
                                        {
                                            DebugHelper.PrintErrorMessage($"[ProcessReceiveSocket][反射失败][ElevatorCommand_{cmd}]...");
                                            try
                                            {
                                                ElevatorSend ed = new ElevatorSend();
                                                ed.SN = el.SN;
                                                ed.Command = 0;
                                                ed.Data = new byte[1] { 0x5a };
                                                ElevatorSends.Enqueue(ed);
                                            }
                                            catch (Exception ex)
                                            {
                                                DebugHelper.PrintErrorMessage("ProcessReceiveSocket.Exception:" + ex.Message);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugHelper.PrintErrorMessage($"ProcessReceiveSocket:{ex.Message}");
                                if (param != null)
                                {
                                    RemoveElevatorLink(param.Socket);
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"ProcessReceiveSocket End:{ex.Message}");
                }
                Thread.Sleep(1);
            }
        }
        static void ProcessSendSocket()
        {
            while (true)
            {
                try
                {
                    if (ElevatorSends.Count > 0)
                    {
                        ElevatorSend elevatorSend = null;
                        // 从队列中取出一条数据
                        ElevatorSends.TryDequeue(out elevatorSend);
                        
                        Task.Run(() =>
                        {
                            try
                            {
                                var v = ElevatorLinks.LastOrDefault(e => e.Value.SN == elevatorSend.SN);
                                var s = v.Key;
                                var c = v.Value.ConfirmId;
                                if (s != null && s.Connected)
                                {
                                    if (elevatorSend.Data.Length == 1)
                                    {
                                        // 处理心跳报文
                                        var b = BitConverter.GetBytes(s.Handle.ToInt32());
                                        var data = new byte[5];
                                        data[0] = elevatorSend.Data[0];
                                        data[1] = b[0];
                                        data[2] = b[1];
                                        data[3] = b[2];
                                        data[4] = b[3];
                                        elevatorSend.Data = data;
                                        s.Send(elevatorSend.Data);
                                        DebugHelper.PrintTraceMessage($"Handle:{s.Handle}, Hex:{ElevatorHelper.ByteToHexString(data)}");
                                        DebugHelper.PrintTraceMessage($"SendSocket:{ElevatorHelper.ByteToHexString(data)},{s.RemoteEndPoint}");
                                    }
                                    else
                                    {
                                        // 组织报文，并转发给电梯
                                        var b = SendSocket(s, elevatorSend, c);
                                        if (b)
                                        {
                                            DebugHelper.PrintTraceMessage($"[ProcessSendSocket][{elevatorSend.Command}][ElevatorSN:{elevatorSend.SN}][转发成功]...");
                                        }
                                        else
                                        {
                                            DebugHelper.PrintErrorMessage($"[ProcessSendSocket][{elevatorSend.Command}][ElevatorSN:{elevatorSend.SN}][转发失败]...");
                                        }
                                    }

                                }
                                else
                                {
                                    DebugHelper.PrintErrorMessage($"Connection Error.");
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugHelper.PrintErrorMessage($"ProcessSendSocket:{ex.Message}");
                                DebugHelper.PrintErrorMessage($"{ex.StackTrace}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"ProcessSendSocket End:{ex.Message}");
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// 发送数据到机器人
        /// </summary>
        /// <param name="ws"></param>
        /// <param name="rd"></param>
        static void SendWebSocket(WebSocket ws, RobotData rd)
        {
            try
            {
                rd.Parameters.Add("ActionName", rd.Command);
                string s = JsonConvert.SerializeObject(rd.Parameters);
                byte[] buffer = Encoding.UTF8.GetBytes(s);
                ws.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                DebugHelper.PrintDebugMessage(buffer, buffer.Length);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"SendWebSocket:{ex.Message}");
            }
        }

        /// <summary>
        /// 处理所有机器人发来的消息（连接除外）
        /// </summary>
        static void ProcessReceiveWebSocket()
        {
            while (true)
            {
                try
                {
                    if (RobotReceives.Count > 0)
                    {
                        RobotReceives.TryDequeue(out RobotReceive param);

                        Task.Run(() =>
                        {
                            try
                            {
                                if (RobotLinks.TryGetValue(param.WebSocket, out RobotWebSocketLinkInfo rl))
                                {
                                    rl.HeartbeatTime = DateTime.Now;
                                    if (param.Command.ToLower() == "HeartBeat".ToLower())
                                    {
                                        // 回复心跳报文
                                        RobotSend robotSend = new RobotSend();
                                        robotSend.Command = "HeartBeat_Callback";
                                        robotSend.SN = rl.SN;
                                        RobotSends.Enqueue(robotSend);
                                    }
                                    else
                                    {
                                        var cmd = param.Command.ToLower();
                                        switch (cmd)
                                        {
                                            case "callelevator":
                                                RobotCommand_CallElevator(rl, param);
                                                DebugHelper.PrintTraceMessage($"Call RobotCommand_CallElevator Successful.");
                                                break;
                                            case "canceltask":
                                                RobotCommand_CancelTask(rl, param);
                                                DebugHelper.PrintTraceMessage($"Call RobotCommand_CancelTask Successful.");
                                                break;
                                            case "elevatorarrived_back":
                                                RobotCommand_ElevatorArrived_Back(rl, param);
                                                DebugHelper.PrintTraceMessage($"Call RobotCommand_ElevatorArrived_Back Successful.");
                                                break;
                                            case "robotmoved":
                                                RobotCommand_RobotMoved(rl, param);
                                                DebugHelper.PrintTraceMessage($"Call RobotCommand_RobotMoved Successful.");
                                                break;
                                            case "servertime":
                                                RobotCommand_ServerTime(rl, param);
                                                DebugHelper.PrintTraceMessage($"Call RobotCommand_ServerTime Successful.");
                                                break;
                                            default:
                                                {
                                                    var method = new Program().GetType().GetMethod("RobotCommand_" + param.Command);
                                                    if (method != null)
                                                    {
                                                        object o = method.Invoke(null, new object[] { rl, param });
                                                        DebugHelper.PrintTraceMessage($"Default.Call RobotCommand_{param.Command} Successful.");
                                                    }
                                                    else
                                                    {
                                                        DebugHelper.PrintErrorMessage($"Call RobotCommand_{ param.Command} Error.");
                                                    }
                                                }
                                                break;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugHelper.PrintErrorMessage($"ProcessReceiveWebSocket:{ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"ProcessReceiveWebSocket End:{ex.Message}");
                }
                Thread.Sleep(1);
            }
        }
        static void ProcessSendWebSocket()
        {
            while (true)
            {
                try
                {
                    if (RobotSends.Count > 0)
                    {
                        RobotSend robotSend = null;
                        RobotSends.TryDequeue(out robotSend);
                        Task.Run(() =>
                        {
                            try
                            {
                                var s = RobotLinks.LastOrDefault(e => e.Value.SN == robotSend.SN).Key;
                                if (s != null)
                                {
                                    SendWebSocket(s, robotSend);
                                }
                                else
                                {
                                    DebugHelper.PrintErrorMessage($"[ProcessSendWebSocket]没有找到指定机器人[sn={robotSend.SN}]");
                                }
                            }
                            catch (Exception ex)
                            {
                                DebugHelper.PrintErrorMessage($"ProcessSendWebSocket:{ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"ProcessSendWebSocket End:{ex.Message}");
                }
                Thread.Sleep(1);
            }
        }
        #region 机器人呼叫电梯
        public static void RobotCommand_CallElevator(RobotWebSocketLinkInfo robotLink, RobotData robotData)
        {
            bool result = false;
            if (robotData.Parameters == null || robotData.Parameters.Count < 2 || !robotData.Parameters.ContainsKey("Start") || !robotData.Parameters.ContainsKey("End"))
            {
                DebugHelper.PrintErrorMessage($"[ParametersException]Parameters less.");
                return;
            }
            try
            {
                result = robotData.Parameters.TryGetValue("Start", out string start);
                if (!result)
                {
                    DebugHelper.PrintErrorMessage($"[ParametersException]Invalid parameter 'Start'.");
                    return;
                }
                result = robotData.Parameters.TryGetValue("End", out string end);
                if (!result)
                {
                    DebugHelper.PrintErrorMessage($"[ParametersException]Invalid parameter 'End'.");
                    return;
                }
                result = robotData.Parameters.TryGetValue("ModuleName", out string moduleName);
                if (!result)
                {
                    DebugHelper.PrintErrorMessage($"[ParametersException]Invalid parameter 'ModuleName'.");
                    return;
                }
                result = robotData.Parameters.TryGetValue("Timestamp", out string timestamp);
                if (!result)
                {
                    DebugHelper.PrintErrorMessage($"[ParametersException]Invalid parameter 'Timestamp'.");
                    return;
                }
                // 服务器向电梯发送【呼叫电梯指令】
                CallElevator(robotLink.SN, Convert.ToInt32(start), Convert.ToInt32(end), moduleName);
                robotLink.ElevatorModuleName = moduleName;
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"[Exception]RobotCommand_CallElevator:{ex.Message}");

                #region 回复错误消息（无效的呼梯）
                var robotSend = new RobotSend();
                try
                {
                    robotSend.SN = RobotLinks[robotLink.WebSocket].SN;
                    robotSend.Command = "ErrorCommand_Back";
                    robotSend.Parameters.Add("RRRR_STATUS", "ERROR_TASKING");
                    robotSend.Parameters.Add("RobotSN", RobotLinks[robotLink.WebSocket].SN);
                    robotSend.Parameters.Add("ErrorInfo", "无效的呼梯。系统解析出错。");
                    robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                    RobotSends.Enqueue(robotSend);
                }
                catch (Exception ex2)
                {
                    DebugHelper.PrintErrorMessage($"ReceiveWebSocket.catch[异常处理模块再次出错]:{ex2.Message}");
                }
                #endregion
                return;
            }
        }
        #endregion
        public static void RobotCommand_ElevatorArrived_Back(RobotWebSocketLinkInfo robotLink, RobotData robotData)
        {
            string delay;
            bool result = false;
            if (robotData.Parameters == null || robotData.Parameters.Count < 1 || !robotData.Parameters.ContainsKey("Delay"))
            {
                DebugHelper.PrintErrorMessage($"[RobotCommand_ElevatorArrived_Back]Parameters less.");
                return;
            }
            try
            {
                result = robotData.Parameters.TryGetValue("Delay", out delay);
                if (!result)
                {
                    DebugHelper.PrintErrorMessage($"[RobotCommand_ElevatorArrived_Back]Parameters.Delay error.");
                    return;
                }

                ReturnDelay(robotLink.SN, Convert.ToInt32(delay));
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"[RobotCommand_ElevatorArrived_Back]{ex.Message}.");
                return;
            }
        }
        public static void RobotCommand_RobotMoved(RobotWebSocketLinkInfo robotLink, RobotData robotData)
        {
            string location;
            bool result = false;
            if (robotData.Parameters == null || robotData.Parameters.Count < 1 || !robotData.Parameters.ContainsKey("Location"))
            {
                DebugHelper.PrintErrorMessage($"[RobotCommand_RobotMoved]Parameters.Location less.");
                return;
            }
            try
            {
                result = robotData.Parameters.TryGetValue("Location", out location);
                if (!result)
                {
                    DebugHelper.PrintErrorMessage($"[RobotCommand_RobotMoved]Parameters.Location error.");
                    return;
                }
                if (location == "In")
                {
                    DebugHelper.PrintTraceMessage($"InElevator.");
                    InElevator(robotLink.SN, robotLink.ElevatorModuleName);
                } 
                else if(location == "Out")
                {
                    DebugHelper.PrintTraceMessage($"OutElevator.");
                    OutElevator(robotLink.SN, robotLink.ElevatorModuleName);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"RobotCommand_RobotMoved:{ex.Message}");
                #region 回复错误消息（无效的电梯到达点位指令）
                var robotSend = new RobotSend();
                try
                {
                    robotSend.SN = RobotLinks[robotLink.WebSocket].SN;
                    robotSend.Command = "ErrorCommand_Back";
                    robotSend.Parameters.Add("RRRR_STATUS", "ERROR_TASKING");
                    robotSend.Parameters.Add("RobotSN", RobotLinks[robotLink.WebSocket].SN);
                    robotSend.Parameters.Add("ErrorInfo", "无效的电梯到达点位指令。系统解析出错。");
                    robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                    RobotSends.Enqueue(robotSend);
                }
                catch (Exception ex2)
                {
                    DebugHelper.PrintErrorMessage($"ReceiveWebSocket.catch[异常处理模块再次出错]:{ex2.Message}");
                }
                #endregion
                return;
            }
        }
        public static void RobotCommand_CancelTask(RobotWebSocketLinkInfo robotLink, RobotData robotData)
        {
            try
            {
                CancelTask(robotLink.SN);
                DebugHelper.PrintTraceMessage($"CancelTask({robotLink.SN})");
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage($"RobotCommand_CancelTask:" + ex.Message);
                return;
            }
        }
        public static void RobotCommand_ServerTime(RobotWebSocketLinkInfo robotLink, RobotReceive robotData)
        {
            
            var tx = JsonConvert.DeserializeObject<ActionGetServerTimeTx>(Encoding.UTF8.GetString(robotData.RawData, 0, robotData.RawDataLength));
            var rx = ReturnAction.Get(robotLink.WebSocket, tx);
            rx.RobotSN = robotLink.SN;
            var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(rx));
            robotLink.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);

            DebugHelper.PrintDebugMessage(buffer);
        }
        /// <summary>
        /// 任务取消
        /// </summary>
        /// <param name="robotSn"></param>
        static void CancelTask(string robotSn)
        {
            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                RobotSN = robotSn,
                TaskPoint = TaskPoint.CancelTask, // 任务取消
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });
        }
        /// <summary>
        /// 服务器向电梯发出【呼叫电梯指令】
        /// </summary>
        /// <param name="robotSn"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        static void CallElevator(string robotSn, int start, int end, string moduleName)
        {
            var r = GetElevatorSn(robotSn, moduleName);
            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                RobotSN = robotSn,
                //RobotId = r.robotId,
                ModuleName = moduleName,
                RobotStartFloor = start,
                RobotEndFloor = end,
                TaskPoint = TaskPoint.CallElevator, // 呼叫电梯任务
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });
        }
        /// <summary>
        /// 返回电梯号
        /// </summary>
        /// <param name="elevatorSn"></param>
        /// <param name="robotId"></param>
        /// <param name="number"></param>
        static void ReturnNumber(string elevatorSn, int robotId, int number)
        {
            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                ElevatorSN = elevatorSn,
                RobotId = robotId,
                ElevatorNumber = number,
                TaskPoint = TaskPoint.ReturnNumber,
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });            
        }
        /// <summary>
        /// 电梯已到达
        /// </summary>
        /// <param name="elevatorSn"></param>
        /// <param name="robotId"></param>
        /// <param name="number"></param>
        /// <param name="floor"></param>
        /// <param name="point"></param>
        static void ElevatorArrived(string elevatorSn, int robotId, int number, int floor, int point)
        {
            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                ElevatorSN = elevatorSn,
                RobotId = robotId,
                ElevatorNumber = number,
                ElevatorFloor = floor,
                TaskPoint = point == 0 ? TaskPoint.StartArrived : TaskPoint.EndArrived,
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });
        }
        /// <summary>
        /// 电梯延时
        /// </summary>
        /// <param name="robotSn"></param>
        /// <param name="delay"></param>
        static void ReturnDelay(string robotSn, int delay)
        {
            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                RobotSN = robotSn,
                Delay = delay,
                //RobotId = r.robotId,
                TaskPoint = TaskPoint.ReturnDelay,
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });
        }
        /// <summary>
        /// 已经进入电梯
        /// </summary>
        /// <param name="robotSn"></param>
        static void InElevator(string robotSn, string moduleName)
        {
            var r = GetElevatorSn(robotSn, moduleName);

            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                RobotSN = robotSn,
                ElevatorSN = r.elevatorSn,
                TaskPoint = TaskPoint.InElevator,
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });
        }
        /// <summary>
        /// 已经离开电梯
        /// </summary>
        /// <param name="robotSn"></param>
        static void OutElevator(string robotSn, string moduleName)
        {
            var r = GetElevatorSn(robotSn, moduleName);
            CallElevatorStatuses.Enqueue(new CallElevatorTask
            {
                RobotSN = robotSn,
                ElevatorSN = r.elevatorSn,
                //RobotId = r.robotId,
                TaskPoint = TaskPoint.OutElevator,
                Timestamp = TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString()
            });
        }
        /// <summary>
        /// 处理呼叫电梯指令
        /// </summary>
        static void ProcessCallElevator()
        {
            while (true)
            {
                try
                {
                    CallElevatorTask callElevatorTask = null;
                    RobotSend robotSend = null;
                    if(CallElevatorStatuses.TryDequeue(out callElevatorTask))
                    {
                        if (!callElevatorTask.IsCanceled)
                        {
                            switch (callElevatorTask.TaskPoint)
                            {
                                #region TaskPoint.CancelTask
                                case TaskPoint.CancelTask:
                                    robotSend = new RobotSend();
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.RobotSN == callElevatorTask.RobotSN);
                                        if (temp == null)
                                        {
                                            try
                                            {
                                                robotSend.SN = callElevatorTask.RobotSN;
                                                robotSend.Command = "CancelTask_Back";
                                                robotSend.Parameters.Add("RRRR_STATUS", "ERROR_TASKING");
                                                robotSend.Parameters.Add("RobotSN", callElevatorTask.RobotSN);
                                                robotSend.Parameters.Add("ErrorInfo", "当前任务为空");
                                                robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                                RobotSends.Enqueue(robotSend);
                                                DebugHelper.PrintTraceMessage($"[{DateTime.Now}][ProcessCallElevator][取消任务][ERROR_TASKING][当前任务为空]执行成功...");
                                            }
                                            catch(Exception ex1)
                                            {
                                                DebugHelper.PrintErrorMessage($"[{DateTime.Now}][ProcessCallElevator][取消任务][ERROR_TASKING]::{ex1.Message}");
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                ElevatorCommand_06(temp.ElevatorSN, temp.RobotId, temp.ElevatorNumber, temp.RobotStartFloor, temp.RobotEndFloor);
                                                robotSend.SN = callElevatorTask.RobotSN;
                                                robotSend.Command = "CancelTask_Back";
                                                robotSend.Parameters.Add("RRRR_STATUS", "SUCCESS");
                                                robotSend.Parameters.Add("RobotSN", callElevatorTask.RobotSN);
                                                robotSend.Parameters.Add("ErrorInfo", "OK");
                                                RobotSends.Enqueue(robotSend);
                                                CallElevatorTasks.Remove(temp);
                                                DebugHelper.PrintTraceMessage($"[{DateTime.Now}][ProcessCallElevator][取消任务][ERROR_TASKING]执行成功...");

                                                continue;
                                            }
                                            catch(Exception ex2)
                                            {
                                                DebugHelper.PrintErrorMessage($"[{DateTime.Now}][ProcessCallElevator][取消任务][SUCCESS]::{ex2.Message}");
                                            }

                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.CancelTask:{ex.Message}");
                                    }
                                    break;
                                #endregion
                                #region TaskPoint.CallElevator
                                case TaskPoint.CallElevator:
                                    robotSend = new RobotSend();
                                    #region 处理异常
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.RobotSN == callElevatorTask.RobotSN);
                                        if (temp != null)
                                        {
                                            robotSend.SN = callElevatorTask.RobotSN;
                                            robotSend.Command = "CallElevator_Back";
                                            robotSend.Parameters.Add("RRRR_STATUS", "ERROR_TASKING");
                                            robotSend.Parameters.Add("RobotSN", callElevatorTask.RobotSN);
                                            robotSend.Parameters.Add("ErrorInfo", "有未完成任务");
                                            robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                            RobotSends.Enqueue(robotSend);
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.CallElevator:{ex.Message}");
                                    }

                                    var v = GetElevatorSn(callElevatorTask.RobotSN, callElevatorTask.ModuleName);
                                    if (v.errorCode != "0")
                                    {
                                        robotSend.SN = callElevatorTask.RobotSN;
                                        robotSend.Command = "CallElevator_Back";
                                        robotSend.Parameters.Add("RRRR_STATUS", "ERROR_OFFLINE");
                                        robotSend.Parameters.Add("RobotSN", callElevatorTask.RobotSN);
                                        robotSend.Parameters.Add("ErrorInfo", v.errorMessage);
                                        robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                        RobotSends.Enqueue(robotSend);
                                        continue;
                                    }
                                    callElevatorTask.ElevatorSN = v.elevatorSn;
                                    #endregion
                                    //呼叫电梯
                                    ElevatorCommand_03(callElevatorTask.ElevatorSN, callElevatorTask.RobotId, 
                                        callElevatorTask.RobotStartFloor, callElevatorTask.RobotEndFloor);
                                    CallElevatorTasks.Add(callElevatorTask);
                                    break;
                                #endregion
                                #region TaskPoint.ReturnNumber
                                case TaskPoint.ReturnNumber:
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.ElevatorSN == callElevatorTask.ElevatorSN && c.RobotId == callElevatorTask.RobotId);
                                        if (temp != null)
                                        {
                                            temp.ElevatorNumber = callElevatorTask.ElevatorNumber;
                                            temp.TaskPoint = callElevatorTask.TaskPoint;
                                            robotSend = new RobotSend();
                                            robotSend.SN = temp.RobotSN;
                                            robotSend.Command = "CallElevator_Back";
                                            robotSend.Parameters.Add("RRRR_STATUS", "SUCCESS");
                                            robotSend.Parameters.Add("RobotSN", temp.RobotSN);
                                            robotSend.Parameters.Add("ErrorInfo", "OK");
                                            robotSend.Parameters.Add("Number", callElevatorTask.ElevatorNumber.ToString());
                                            robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                            RobotSends.Enqueue(robotSend);
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.ReturnNumber:" + ex.Message);
                                    }
                                    break;
                                #endregion
                                #region TaskPoint.StartArrived
                                case TaskPoint.StartArrived:
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.ElevatorSN == callElevatorTask.ElevatorSN && c.RobotId == callElevatorTask.RobotId);
                                        if (temp != null)
                                        {
                                            temp.ElevatorNumber = callElevatorTask.ElevatorNumber;
                                            temp.TaskPoint = callElevatorTask.TaskPoint;
                                            robotSend = new RobotSend();
                                            robotSend.SN = temp.RobotSN;
                                            robotSend.Command = "ElevatorArrived";
                                            robotSend.Parameters.Add("RobotSN", temp.RobotSN);
                                            robotSend.Parameters.Add("FloorNumber", callElevatorTask.ElevatorFloor.ToString());
                                            robotSend.Parameters.Add("Point", "Start");
                                            robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                            RobotSends.Enqueue(robotSend);

                                            //Console.WriteLine("向机器人发送电梯到达指令!!!");
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.StartArrived:" + ex.Message);
                                    }
                                    break;
                                #endregion
                                #region TaskPoint.ReturnDelay
                                case TaskPoint.ReturnDelay:
                                    robotSend = new RobotSend();
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.RobotSN == callElevatorTask.RobotSN);
                                        if (temp != null)
                                        {
                                            temp.Delay = callElevatorTask.Delay;
                                            temp.TaskPoint = callElevatorTask.TaskPoint;
                                            if (CheckElevatorStatus(callElevatorTask.RobotSN))
                                            {
                                                robotSend.SN = callElevatorTask.RobotSN;
                                                robotSend.Command = "CallElevator_Back";
                                                robotSend.Parameters.Add("RRRR_STATUS", "ERROR_TASKING");
                                                robotSend.Parameters.Add("RobotSN", callElevatorTask.RobotSN);
                                                robotSend.Parameters.Add("ErrorInfo", "有未完成任务");
                                                robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                                //RobotSends.Enqueue(robotSend);
                                                continue;
                                            }
                                            ElevatorCommand_84(temp.ElevatorSN, temp.RobotId, callElevatorTask.Delay);
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.ReturnDelay:" + ex.Message);
                                    }
                                    break;
                                #endregion
                                #region TaskPoint.InElevator
                                case TaskPoint.InElevator:
                                    robotSend = new RobotSend();
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.ElevatorSN == callElevatorTask.ElevatorSN && c.RobotId == callElevatorTask.RobotId);

                                        if (temp != null)
                                        {
                                            //temp.ElevatorNumber = callElevatorTask.ElevatorNumber;
                                            //temp.TaskPoint = callElevatorTask.TaskPoint;
                                            //robotSend = new RobotSend();
                                            //robotSend.SN = temp.RobotSN;
                                            //robotSend.Command = "RobotMoved_Back";
                                            //robotSend.Parameters.Add("RRRR_STATUS", "SUCCESS");
                                            //robotSend.Parameters.Add("RobotSN", temp.RobotSN);
                                            //robotSend.Parameters.Add("ErrorInfo", "OK");
                                            //robotSend.Parameters.Add("Number", callElevatorTask.ElevatorNumber.ToString());
                                            //RobotSends.Enqueue(robotSend);
                                            ElevatorCommand_05(temp.ElevatorSN, temp.RobotId, callElevatorTask.Delay);
                                            continue;
                                        }
                                        else
                                        {
                                            DebugHelper.PrintErrorMessage($"TaskPoint.InElevator error.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.InElevator:{ex.Message}");
                                    }
                                    break;
                                #endregion
                                #region TaskPoint.EndArrived
                                case TaskPoint.EndArrived:
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.ElevatorSN == callElevatorTask.ElevatorSN && c.RobotId == callElevatorTask.RobotId);
                                        if (temp != null)
                                        {
                                            temp.ElevatorNumber = callElevatorTask.ElevatorNumber;
                                            temp.TaskPoint = callElevatorTask.TaskPoint;
                                            robotSend = new RobotSend();
                                            robotSend.SN = temp.RobotSN;
                                            robotSend.Command = "ElevatorArrived";
                                            robotSend.Parameters.Add("RobotSN", temp.RobotSN);
                                            robotSend.Parameters.Add("FloorNumber", callElevatorTask.ElevatorFloor.ToString());
                                            robotSend.Parameters.Add("Point", "End");
                                            robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                            RobotSends.Enqueue(robotSend);
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.EndArrived:{ex.Message}");
                                    }
                                    break;
                                #endregion
                                #region TaskPoint.OutElevator
                                case TaskPoint.OutElevator:
                                    robotSend = new RobotSend();
                                    try
                                    {
                                        var temp = CallElevatorTasks.LastOrDefault(c => c.ElevatorSN == callElevatorTask.ElevatorSN && c.RobotId == callElevatorTask.RobotId);
                                        //var temp = CallElevatorTasks.LastOrDefault(c => c.RobotSN == callElevatorTask.RobotSN);
                                        if (temp != null)
                                        {
                                            temp.TaskPoint = callElevatorTask.TaskPoint;
                                            if (temp != null)
                                            {
                                                temp.ElevatorNumber = callElevatorTask.ElevatorNumber;
                                                temp.TaskPoint = callElevatorTask.TaskPoint;
                                                robotSend = new RobotSend();
                                                robotSend.SN = temp.RobotSN;
                                                robotSend.Command = "RobotMoved_Back";
                                                robotSend.Parameters.Add("RRRR_STATUS", "SUCCESS");
                                                robotSend.Parameters.Add("RobotSN", temp.RobotSN);
                                                robotSend.Parameters.Add("ErrorInfo", "OK");
                                                robotSend.Parameters.Add("Number", callElevatorTask.ElevatorNumber.ToString());
                                                robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                                RobotSends.Enqueue(robotSend);
                                            }

                                            ElevatorCommand_05(temp.ElevatorSN, temp.RobotId, temp.ElevatorNumber);
                                            CallElevatorTasks.Remove(temp);
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        DebugHelper.PrintErrorMessage($"TaskPoint.OutElevator:{ex.Message}");
                                    }
                                    break;
                                #endregion
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"ProcessCallElevator:{ex.Message}");
                }
                Thread.Sleep(10);
            }
        }

        static (string elevatorSn, byte confirmId, string errorCode, string errorMessage) GetElevatorSn(string uniqueRobotSn, string moduleName)
        {
            string elevatorSn = null;
            byte confirmId = 0;
            string rtnCode = "0";
            string rtnMessage = "OK";

            try
            {
                var db = new DataContext();
                if (string.IsNullOrEmpty(moduleName))
                {
                    // 酒店只有一部电梯
                    var h = db.HotelRobots.FirstOrDefault(x => x.UniqueRobotSN == uniqueRobotSn);
                    if (h != null)
                    {
                        var es = db.HotelElevators.Where(x => x.HotelId == h.HotelId);
                        if (es.Count() > 1)
                        {
                            // Error, more elevators.
                            // 出错
                            DebugHelper.PrintErrorMessage($"GetElevatorSn({uniqueRobotSn}, {moduleName}):More elevators, database error.");
                            rtnCode = "-1";
                            rtnMessage = "数据库配置错误。More elevators。";

                        }
                        else if (es.Count() == 0)
                        {
                            // Error, no elevator.
                            DebugHelper.PrintErrorMessage($"GetElevatorSn({uniqueRobotSn}, {moduleName}):No data, database error.");
                            rtnCode = "-2";
                            rtnMessage = "数据库配置错误。No data。";
                        }
                        else
                        {
                            #region 巡检电梯连接
                            var obsElevatorList = new List<KeyValuePair<Socket, ElevatorSocketLinkInfo>>();
                            foreach (var d in ElevatorLinks)
                            {
                                var ts = DateTime.Now - d.Value.HeartbeatTime;
                                // 2分钟没有心跳或者SN 为空，断开。
                                if (ts.Minutes > 1 || ts.Hours > 0 || ts.Days > 0 
                                    || string.IsNullOrEmpty(d.Value.SN)
                                    )
                                {
                                    obsElevatorList.Add(d);
                                }
                                try
                                {
                                    if (d.Value.Socket.Poll(100, SelectMode.SelectError))
                                    {
                                        obsElevatorList.Add(d);
                                    }
                                    else if (d.Key.Poll(100, SelectMode.SelectError))
                                    {
                                        obsElevatorList.Add(d);
                                    }
                                }
                                catch (Exception)
                                {
                                    obsElevatorList.Add(d);
                                    DebugHelper.PrintErrorMessage($"连接异常，移除当前连接.");
                                }
                            }
                            obsElevatorList.ForEach(x =>
                            {
                                ElevatorLinks.TryRemove(x.Key, out ElevatorSocketLinkInfo _);
                                try
                                {
                                    x.Key.Close();
                                }
                                catch { }
                            });
                            obsElevatorList.Clear();
                            #endregion
                            #region 巡检机器人连接
                            var obsRobotList = new List<KeyValuePair<WebSocket, RobotWebSocketLinkInfo>>();
                            foreach (var d in RobotLinks)
                            {
                                var ts = DateTime.Now - d.Value.HeartbeatTime;
                                // 2分钟没有心跳，断开。
                                if (ts.Days > 0 || ts.Hours > 0 || ts.Minutes > 2)
                                {
                                    obsRobotList.Add(d);
                                }
                                else if (d.Value.WebSocket.State == WebSocketState.Aborted
                                || d.Value.WebSocket.State == WebSocketState.Closed)
                                {
                                    obsRobotList.Add(d);
                                }
                            }
                            obsRobotList.ForEach(x =>
                            {
                                RobotLinks.TryRemove(x.Key, out RobotWebSocketLinkInfo _);
                            });
                            obsRobotList.Clear();
                            #endregion

                            var e = es.First();
                            elevatorSn = e.ElevatorId;
                            var el = ElevatorLinks.Values.FirstOrDefault(x => x.SN == elevatorSn);
                            if (el != null)
                            {
                                confirmId = el.ConfirmId;
                            }
                            else
                            {
                                // Error 
                                // 出错
                                DebugHelper.PrintErrorMessage($"GetElevatorSn({uniqueRobotSn}):elevatorSn:{elevatorSn}.No data, database error..");
                                DebugHelper.PrintTraceMessage("Valid ElevatorLinks.SN:");
                                foreach (var x in ElevatorLinks)
                                {
                                    DebugHelper.PrintTraceMessage($"Elevator.SN::{x.Value.SN}");
                                }

                                rtnCode = "-3";
                                rtnMessage = $"电梯已离线。";
                            }
                        }
                    }
                    else
                    {
                        // Error.
                        DebugHelper.PrintErrorMessage($"GetElevatorSn({uniqueRobotSn}, {moduleName}):No hotel data, database error.");
                        rtnCode = "-4";
                        rtnMessage = "数据库配置错误。No hotel data。";
                    }

                }
                else
                {
                    // 酒店有多部电梯
                    // Error.
                    DebugHelper.PrintErrorMessage($"GetElevatorSn({uniqueRobotSn}, {moduleName}):Invalid moduleName, The hotel has more elevators. database error.");
                    rtnCode = "-5";
                    rtnMessage = "数据库配置错误。Invalid moduleName, The hotel has more elevators。";
                }
            }
            catch(Exception ex)
            {
                rtnCode = "-1";
                rtnMessage = $"系统异常(GetElevatorSn({uniqueRobotSn}, {moduleName}))。{ex.Message}";
                DebugHelper.PrintErrorMessage($"{rtnCode}, {rtnMessage}");
            }

            return (elevatorSn, confirmId, rtnCode, rtnMessage);
        }
        static bool CheckRobotStatus(string robotSn)
        {
            bool result = false;
            return result;
        }
        static bool CheckElevatorStatus(string elevatorSn)
        {
            bool result = false;
            return result;
        }
    }
}
