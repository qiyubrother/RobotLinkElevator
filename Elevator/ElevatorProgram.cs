using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static RobotLinkElevator.CodeHelper;
using Serilog;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Elevator
{
    /// <summary>
    /// 电梯模拟器
    /// </summary>
    class ElevatorProgram
    {
        static void Main(string[] args)
        {

            #region 系统初始化
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(@"logs\Elevator.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            #endregion
            do
            {
                try
                {
                    #region 初始化
                    var s = File.ReadAllText("ElevatorSetting.json");
                    var j = JsonConvert.DeserializeObject<JObject>(s);
                    var buffer = new byte[1024];
                    var ip = j["ServerIp"].ToString();
                    var port = Convert.ToInt32(j["ServerPort"].ToString());
                    var elevatorId = j["ElevatorId"].ToString();
                    var elevatorHeartBeatSecond = Convert.ToInt32(j["ElevatorHeartBeatSecond"].ToString()) * 1000;
                    var callElevatorCostSecond = Convert.ToInt32(j["CallElevatorCostSecond"].ToString()) * 1000;
                    Env.ElevatorNo = Convert.ToByte(j["ElevatorNo"].ToString());

                    Env.Init(); // 初始化环境变量

                    var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    clientSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true); // 立即发送，不等待
                    clientSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port)); //配置服务器IP与端口
                    DebugHelper.PrintTraceMessage($"[{clientSocket.Handle}]已经连接到服务程序...");
                    //Task.Run(() =>
                    //{
                    //    do
                    //    {
                    //        if (clientSocket.Poll(100, SelectMode.SelectError))
                    //        {
                    //            break;
                    //        }
                    //        else
                    //        {
                    //            var b = new byte[] {0x5a };
                    //            clientSocket.Send(b);
                    //            System.Threading.Thread.Sleep(elevatorHeartBeatSecond * 1000);
                    //        }
                    //    } while (true);
                    //});
                    #endregion
                    #region 发送ID
                    {
                        DebugHelper.PrintTraceMessage("发送ID命令");
                        var sendId = new SendID();
                        // ID
                        sendId.DATA = ConvertTo8421Bytes(elevatorId);
                        //sendId.DATA[0] = 1; // ID
                        //sendId.DATA[1] = 2; // ID
                        //sendId.DATA[2] = 3; // ID
                        //sendId.DATA[3] = 4; // ID
                        // 计算CS
                        sendId.SetCS();
                        var txBuffer = sendId.GetByteStream();
                        // 输出上送报文到屏幕
                        DebugHelper.PrintTxMessage(txBuffer);
                        // 发送数据
                        clientSocket.Send(txBuffer);
                        var rxBufferBig = new byte[20];
                        // 接收数据
                        var rxLen = clientSocket.Receive(rxBufferBig);
                        var rxBuffer = new byte[rxLen];
                        Array.Copy(rxBufferBig, rxBuffer, rxLen);
                        var rxSendID = SendID.Create(rxBuffer);

                        // 输出下送报文
                        DebugHelper.PrintRxMessage(rxBuffer);
                        // 存储确认ID
                        Env.ConfirmId = rxBuffer[rxBuffer.Length - 3];
                        DebugHelper.PrintTraceMessage($"ConfirmID:{Env.ConfirmId}");
                    }
                    #endregion
                    #region 获取服务器时间
                    if (true)
                    {
                        DebugHelper.PrintTraceMessage("获取服务器时间");
                        var tx = new GetServerDateTimeTx();
                        tx.DATA[0] = 0;
                        tx.CS = 10; // CS验证
                        var txBuffer = tx.GetByteStream();
                        // 输出上送报文到屏幕
                        DebugHelper.PrintTxMessage(txBuffer);
                        // 发送数据
                        clientSocket.Send(txBuffer);
                        var rxBufferBig = new byte[20];
                        // 接收数据
                        var rxLen = clientSocket.Receive(rxBufferBig);
                        var rxBuffer = new byte[rxLen];
                        Array.Copy(rxBufferBig, rxBuffer, rxLen);
                        // 输出下送报文
                        DebugHelper.PrintRxMessage(rxBuffer);
                    }
                    #endregion
                    #region 发送心跳包
                    if (false)
                    {
                        var getSvrDateTime = new GetServerDateTimeTx();
                        getSvrDateTime.DATA[0] = 0;
                        getSvrDateTime.CS = 10; // CS验证
                        var txBuffer = getSvrDateTime.GetByteStream();
                        // 输出上送报文到屏幕
                        DebugHelper.PrintTxMessage(txBuffer);
                        // 发送数据
                        clientSocket.Send(txBuffer);
                        var rxBufferBig = new byte[20];
                        // 接收数据
                        var rxLen = clientSocket.Receive(rxBufferBig);
                        var rxBuffer = new byte[rxLen];
                        Array.Copy(rxBufferBig, rxBuffer, rxLen);
                        var rxSendID = SendID.Create(rxBuffer);

                        rxSendID.C = rxBuffer[0];
                        rxSendID.A = rxBuffer[1];
                        rxSendID.CI = rxBuffer[2];
                        Array.Copy(rxBuffer, 4, rxSendID.DATA, 0, 6);
                        // 输出下送报文
                        DebugHelper.PrintRxMessage(rxBuffer);
                    }
                    #endregion
                    #region 接收线程
                    do
                    {
                        try
                        {
                            DebugHelper.PrintTraceMessage($"等待服务端指令...");
                            DebugHelper.PrintTraceMessage($"ConfirmId:{Env.ConfirmId}, CurrentFloor:{Env.CurrentFloor}, StartFloor:{Env.StartFloor}, TargetFloor:{Env.TargetFloor}, ElevatorNo:{Env.ElevatorNo}");
                            var tmpBuffer = new byte[1024];
                            byte[] rcvdBuffer;

                            #region 接收服务端数据，并作处理
                            if (clientSocket.Poll(100, SelectMode.SelectError))
                            {
                                DebugHelper.PrintErrorMessage($"连接失效，服务端已经断开。");
                                break;
                            }
                            var len = clientSocket.Receive(tmpBuffer);
                            rcvdBuffer = new byte[len];
                            Array.Copy(tmpBuffer, rcvdBuffer, len);
                            #region 收到0个数据，认为服务已断开
                            if (len == 0)
                            {
                                DebugHelper.PrintErrorMessage($"收到0字节数据，服务端已经断开。");
                                break; // 服务端断开
                            }
                            #endregion
                            var command = rcvdBuffer[4];

                            switch (command)
                            {
                                #region 呼叫电梯（收到上送报文） + 电梯到达（上送）
                                case 0x03:
                                    {
                                        DebugHelper.PrintTraceMessage(Env.FormatToString());
                                        DebugHelper.PrintTraceMessage($"[电梯到达（上送）][0x03]");
                                        #region 返回应答报文（回文）
                                        DebugHelper.PrintRxMessage(rcvdBuffer);
                                        var callElevator = CallElevatorTx.Create(rcvdBuffer);
                                        DebugHelper.PrintTraceMessage($"Start floor:{callElevator.Start}, End floor:{callElevator.End}");
                                        var rx = new CallElevatorRx();
                                        rx.HeaderStart = 0x68;
                                        rx.HeaderEnd = 0x68;
                                        rx.A = callElevator.A; // 机器人编号
                                        Env.StartFloor = callElevator.Start; // 起始楼层
                                        Env.TargetFloor = callElevator.End;  // 目标楼层
                                        #region 当前系统时间
                                        rx.DATE[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                                        rx.DATE[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                                        rx.DATE[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                                        rx.DATE[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                                        rx.DATE[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                                        rx.DATE[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                                        #endregion
                                        rx.NO = Env.ElevatorNo; // 返回电梯号
                                        #region 计算SHA
                                        {
                                            var sb = new byte[8];
                                            sb[0] = Env.ConfirmId;
                                            Array.Copy(rx.DATE, 0, sb, 1, 6);
                                            sb[7] = rx.NO;
                                            DebugHelper.PrintDebugMessage(sb);
                                            // 排序
                                            InsertSort(sb);
                                            DebugHelper.PrintDebugMessage(sb);
                                            var sha1 = SHA1.Create().ComputeHash(sb);
                                            var ln = Math.Min(sha1.Length, 20);
                                            for (var i = 0; i < ln; i++) rx.Sha1[i] = sha1[i];
                                        }
                                        #endregion
                                        var rxBuffer = rx.GetByteStream();
                                        #region 计算CS
                                        rxBuffer[rxBuffer.Length - 2] = CSCheckHelper.GetCS(rxBuffer);
                                        #endregion
                                        DebugHelper.PrintTraceMessage($"响应呼梯请求[0x83]...");
                                        DebugHelper.PrintTxMessage(rxBuffer);
                                        // 发送应答数据到服务器
                                        clientSocket.Send(rxBuffer);
                                        #endregion
                                        #region 电梯调度，等待30秒
                                        DebugHelper.PrintTraceMessage($"电梯调度，等待{callElevatorCostSecond / 1000}秒...");
                                        System.Threading.Thread.Sleep(callElevatorCostSecond);
                                        #endregion
                                        #region 发送【电梯到达】指令到服务器（上送）
                                        #region 组织数据
                                        var txElevatorArrived = new ElevatorArrivedTx();
                                        txElevatorArrived.A = callElevator.A; // 机器人编号
                                        txElevatorArrived.Floor = 1; // 测试数据，指定到达1层。
                                        txElevatorArrived.No = Env.ElevatorNo; // 返回电梯号，测试数据返回3
                                        txElevatorArrived.Point = 0; // 到达起始楼层
                                        // 当前系统时间
                                        txElevatorArrived.DATE[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                                        txElevatorArrived.DATE[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                                        txElevatorArrived.DATE[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                                        txElevatorArrived.DATE[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                                        txElevatorArrived.DATE[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                                        txElevatorArrived.DATE[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));

                                        #region 计算SHA
                                        {
                                            var sb = new byte[10];
                                            Array.Copy(txElevatorArrived.DATE, sb, 6);
                                            sb[6] = txElevatorArrived.No;
                                            sb[7] = txElevatorArrived.Floor;
                                            sb[8] = txElevatorArrived.Point;
                                            sb[9] = Env.ConfirmId;
                                            InsertSort(sb);
                                            var sha1 = SHA1.Create().ComputeHash(sb);
                                            var ln = Math.Min(sha1.Length, 20);
                                            for (var i = 0; i < ln; i++) txElevatorArrived.Sha1[i] = sha1[i];
                                        }
                                        #endregion
                                        // 获取字节流
                                        var txElevatorArrivedBuffer = txElevatorArrived.GetByteStream();
                                        #region 计算CS

                                        txElevatorArrivedBuffer[txElevatorArrivedBuffer.Length - 2] = CSCheckHelper.GetCS(txElevatorArrivedBuffer);
                                        #endregion
                                        DebugHelper.PrintTxMessage(txElevatorArrivedBuffer);
                                        #endregion
                                        DebugHelper.PrintTraceMessage($"电梯已经到达指定楼层，发送【电梯到达】指令到服务器[0x04]...");
                                        clientSocket.Send(txElevatorArrivedBuffer); // 发送
                                        Env.CurrentFloor = Env.StartFloor; // 标记当前楼层为起始层
                                        #endregion
                                        break;
                                    }
                                #endregion
                                #region 机器人已就位（收到上送报文）
                                case 0x05:
                                    {
                                        // 如果机器人已进入电梯，则：
                                        // 1、关门并行驶到目标楼层。
                                        // 2、发送电梯到达指定楼层报文，并开门。
                                        // 如果机器人已经出电梯，则：
                                        // 1、关门，任务结束。
                                        DebugHelper.PrintTraceMessage(Env.FormatToString());
                                        var rx = ElevatorArrivedRx.Create(rcvdBuffer);
                                        if (Env.CurrentFloor == Env.StartFloor)
                                        {
                                            // 机器人已经进入电梯，准备乘梯
                                            // rx.Delay 为延时关门时间（秒）
                                            DebugHelper.PrintRxMessage(rcvdBuffer);
                                            DebugHelper.PrintTraceMessage($"收到机器人已就位报文[进入电梯]，延时关门{rx.Delay}秒...");
                                            System.Threading.Thread.Sleep(1000 * rx.Delay);

                                            DebugHelper.PrintTraceMessage($"正在关门...");
                                            System.Threading.Thread.Sleep(1000);
                                            DebugHelper.PrintTraceMessage($"正在前往目标楼层[{Env.TargetFloor}]（{rx.Delay}秒）...");
                                            System.Threading.Thread.Sleep(rx.Delay * 1000);

                                            #region 发送【电梯到达】指令到服务器[回文]
                                            var txElevatorArrived = new ElevatorArrivedTx();
                                            txElevatorArrived.A = rx.A; // 机器人编号
                                            txElevatorArrived.Floor = Env.TargetFloor; // 测试数据，指定到达1层。
                                            txElevatorArrived.No = Env.ElevatorNo; // 返回电梯号
                                            txElevatorArrived.Point = 1; // 到达目标楼层
                                                                         // 当前系统时间
                                            txElevatorArrived.DATE[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                                            txElevatorArrived.DATE[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                                            txElevatorArrived.DATE[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                                            txElevatorArrived.DATE[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                                            txElevatorArrived.DATE[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                                            txElevatorArrived.DATE[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));

                                            #region 计算SHA
                                            {
                                                var sb = new byte[10];
                                                Array.Copy(txElevatorArrived.DATE, sb, 6);
                                                sb[6] = txElevatorArrived.No;
                                                sb[7] = txElevatorArrived.Floor;
                                                sb[8] = txElevatorArrived.Point;
                                                sb[9] = Env.ConfirmId;
                                                InsertSort(sb);
                                                var sha1 = SHA1.Create().ComputeHash(sb);
                                                var ln = Math.Min(sha1.Length, 20);
                                                for (var i = 0; i < ln; i++) txElevatorArrived.Sha1[i] = sha1[i];
                                            }
                                            #endregion
                                            // 获取字节流
                                            var txElevatorArrivedBuffer = txElevatorArrived.GetByteStream();

                                            #region 计算CS
                                            txElevatorArrivedBuffer[txElevatorArrivedBuffer.Length - 2] = CSCheckHelper.GetCS(txElevatorArrivedBuffer);
                                            #endregion
                                            DebugHelper.PrintTraceMessage($"电梯已经到达指定楼层，发送【电梯到达】指令到服务器...");
                                            DebugHelper.PrintTxMessage(txElevatorArrivedBuffer);
                                            clientSocket.Send(txElevatorArrivedBuffer); // 发送
                                            Env.CurrentFloor = Env.TargetFloor; // 标记当前楼层为目标层
                                            #endregion
                                        }
                                        else if (Env.CurrentFloor == Env.TargetFloor)
                                        {
                                            // 机器人已经离开电梯
                                            DebugHelper.PrintRxMessage(rcvdBuffer);
                                            DebugHelper.PrintTraceMessage($"收到机器人已就位报文[离开电梯]，延时关门{rx.Delay}秒...  -- 任务结束 --");

                                            Env.Init(); // 初始化环境变量
                                        }
                                        break;
                                    }
                                #endregion
                                #region 任务取消（收到上送报文）
                                case 0x06:
                                    {
                                        DebugHelper.PrintTraceMessage(Env.FormatToString());
                                        // 清楚当前会话中的SN.
                                        var tx = CancelTaskTx.Create(rcvdBuffer);
                                        DebugHelper.PrintTraceMessage($"收到任务取消报文,[机器人编号：{tx.A}]");
                                        DebugHelper.PrintRxMessage(rcvdBuffer);
                                        var rx = new CancelTaskRx();
                                        // 当前系统时间
                                        rx.DATE[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                                        rx.DATE[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                                        rx.DATE[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                                        rx.DATE[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                                        rx.DATE[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                                        rx.DATE[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));

                                        #region 计算SHA
                                        var sb = new byte[6];
                                        Array.Copy(rx.DATE, sb, 6);
                                        var sha1 = SHA1.Create().ComputeHash(sb);
                                        var ln = Math.Min(sha1.Length, 20);
                                        for (var i = 0; i < ln; i++) rx.Sha1[i] = sha1[i];
                                        #endregion
                                        // 获取字节流
                                        var rxBuffer = rx.GetByteStream();
                                        #region 计算CS
                                        rxBuffer[rxBuffer.Length - 2] = CSCheckHelper.GetCS(rxBuffer);
                                        #endregion
                                        DebugHelper.PrintTraceMessage($"[{DateTime.Now}][正在发送任务取消回文]...");
                                        DebugHelper.PrintTxMessage(rcvdBuffer);
                                        clientSocket.Send(rxBuffer); // 发送
                                        break;
                                    }
                                #endregion
                                #region 收到电梯已到达指定楼层的回文
                                case 0x84:
                                    {
                                        DebugHelper.PrintTraceMessage(Env.FormatToString());
                                        // 读取延时秒数，并执行延时。
                                        var elevatorArrivedRx = ElevatorArrivedRx.Create(rcvdBuffer);
                                        DebugHelper.PrintRxMessage(rcvdBuffer);
                                        // 解析延时关门秒数
                                        DebugHelper.PrintTraceMessage($"[{DateTime.Now}]收到收到电梯已到达指定楼层的回文，延时关门{elevatorArrivedRx.Delay}秒...");

                                        System.Threading.Thread.Sleep(elevatorArrivedRx.Delay * 1000);
                                        break;
                                    }
                                #endregion
                            }
                            #endregion
                        }
                        catch(SocketException se)
                        {
                            DebugHelper.PrintErrorMessage($"[{DateTime.Now}][Exception]{se.Message}");
                            break;
                        }
                        catch(Exception ex)
                        {
                            DebugHelper.PrintErrorMessage($"[{DateTime.Now}][Exception]{ex.Message}");
                            System.Threading.Thread.Sleep(2000);
                        }
                    } while (true);
                    #endregion
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"[{DateTime.Now}][出现异常]{ex.Message}...");
                }
                System.Threading.Thread.Sleep(3000);
                DebugHelper.PrintTraceMessage($"[{DateTime.Now}]尝试重新连接服务器...");
            } while (true);
        }

        public static string TransCommandCode(byte commandCode)
        {
            switch (commandCode)
            {
                case 1: return "Tx.发送ID";
                case 81: return "Rx.发送ID";
                case 2: return "Tx.获取时间";
                case 82: return "Rx.获取时间";
                case 3: return "Tx.呼叫电梯";
                case 83: return "Rx.呼叫电梯";
                case 4: return "Tx.电梯到达";
                case 84: return "Rx.电梯到达";
                case 5: return "Tx.机器人就位";
                case 85: return "Rx.机器人就位";
                case 6: return "Tx.取消任务";
                case 86: return "Rx.取消任务";
            }

            return commandCode.ToString();
        }

        public static void InsertSort(byte[] data)
        {
            byte temp;
            for (int i = 1; i < data.Length; i++)
            {
                temp = data[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    if (data[j] > temp)
                    {
                        data[j + 1] = data[j];
                        if (j == 0)
                        {
                            data[0] = temp;
                            break;
                        }
                    }
                    else
                    {
                        data[j + 1] = temp;
                        break;
                    }
                }
            }
        }
        public class Env
        {
            public static byte ElevatorNo = 0;
            public static byte ConfirmId = 0;
            public static byte StartFloor = 0;
            public static byte TargetFloor = 0;
            public static byte CurrentFloor = 0;

            public static void Init()
            {
                //ElevatorNo = 7;
                //ConfirmId = 0;
                StartFloor = 0;
                TargetFloor = 0;
                CurrentFloor = 0;
            }

            public static string FormatToString()=> $"ElevatorNo:{ElevatorNo}, StartFloor:{StartFloor}, TargetFloor:{TargetFloor}, CurrentFloor:{CurrentFloor}";
        }
    }










}
