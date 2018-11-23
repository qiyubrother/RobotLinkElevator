using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ElevatorActions;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.IO;
namespace Robot
{
    class RobotProgram
    {
        static void Main(string[] args)
        {
            // Robot.exe --SN=NH1001 --User=Newhuman --Mac=00016C06A630 --Url=192.168.0.6:7080

            #region 系统初始化
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(@"logs\Robot.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            #endregion

            var webSocket = new ClientWebSocket();
            #region 变量定义

            var j = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("RobotSetting.json"));
            var userName = j["CompanyTag"].ToString();
            var curTime = DateTime.Now.ToFileTimeUtc().ToString();
            var robotSn = j["RobotSN"].ToString();
            var robotMac = j["RobotMac"].ToString();
            var nonce = j["Nonce"].ToString();
            var fromFloorNo = j["FromFloorNo"].ToString();
            var toFloorNo = j["ToFloorNo"].ToString();
            var moduleName = j["ModuleName"].ToString();
            var robotInElevatorSecond = Convert.ToInt32(j["RobotInElevatorSecond"].ToString()) * 1000;
            var robotOutElevatorSecond = Convert.ToInt32(j["RobotOutElevatorSecond"].ToString()) * 1000;
            var robotHeartBeatSecond = Convert.ToInt32(j["RobotHeartBeatSecond"].ToString()) * 1000;
            // 延迟关门时间（秒）
            var elevatorOpenDoorSecond = Convert.ToInt32(j["ElevatorOpenDoorSecond"].ToString()) * 1000;
            Console.WriteLine($"robotSn:{robotSn}");
            var taskEndExit = Convert.ToBoolean(j["TaskEndExit"].ToString());
            var checkSum = CodeHelper.GetSignature(userName, curTime, robotSn, robotMac, nonce);
            var timeout = Convert.ToInt32(j["TaskTimeoutSecond"].ToString()) * 1000;
            var url = $"{j["Url"].ToString()}?username={userName}&curtime={curTime}&robotsn={robotSn}&robotmac={robotMac}&nonce={nonce}&checksum={checkSum}";
            #endregion
            #region 机器人连接到云端服务器
            {
                #region 发送【连接到云端服务器】请求
                var buffer = new byte[1024];
                DebugHelper.PrintTxMessage(url);
                do
                {
                    try
                    {
                        DebugHelper.PrintTraceMessage($"[机器人]请求连接到服务程序...");

                        if (webSocket.State == WebSocketState.Closed)
                        {
                            webSocket.Dispose();
                            webSocket = new ClientWebSocket();
                        }
                        webSocket.ConnectAsync(new Uri(url), CancellationToken.None).Wait();
                        break;
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"连接到服务器程序失败，等待30秒后尝试重新连接...");
                        DebugHelper.PrintErrorMessage($"[Exception]{ex.Message}");
                        System.Threading.Thread.Sleep(timeout);
                    }
                } while (true);
                #endregion
                #region 接收返回结果
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                rst.Wait();
                if (rst.Result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                    DebugHelper.PrintRxMessage(msg);
                }
                else
                {
                    // 错误的消息类型
                    DebugHelper.PrintErrorMessage("[机器人][连接到服务器]错误的消息类型...");
                    return;
                }
                #endregion
            }
            #endregion
            #region 获取服务器时间
            {
                #region 变量定义
                var buffer = new byte[1024];
                #endregion
                #region 发送【获取服务器时间】请求
                DebugHelper.PrintTraceMessage($"[机器人]获取服务器时间...");
                var tx = JsonConvert.SerializeObject(new ActionGetServerTimeTx());
                DebugHelper.PrintTxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                #endregion
                #region 接收返回结果
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (rst.Wait(timeout))
                {
                    if (rst.Result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                        DebugHelper.PrintRxMessage(msg);
                    }
                    else
                    {
                        // 错误的消息类型
                        DebugHelper.PrintErrorMessage($"[机器人][获取服务器时间]错误的消息类型...");
                        return;
                    }
                }
                else
                {
                    DebugHelper.PrintErrorMessage($"[机器人][获取服务器时间]超时，忽略...");
                }
                #endregion
            }
            #endregion
            #region 发送心跳包
            SendHeartbeat(webSocket);
            #endregion
            #region 取消所有任务
            {
                var buffer = new byte[1024];
                #region 发送【取消所有任务】请求
                DebugHelper.PrintTraceMessage($"[机器人]取消所有任务...");
                var tx = JsonConvert.SerializeObject(new ActionCancelTaskTx());
                DebugHelper.PrintTxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                #endregion
                #region 接收返回结果
                DebugHelper.PrintTraceMessage($"等待接收取消任务的回文（最长{timeout / 1000}秒）...");
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (rst.Wait(timeout)) // 最长等待3秒
                {
                    if (rst.Result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                        DebugHelper.PrintRxMessage(msg);
                    }
                    else
                    {
                        // 错误的消息类型
                        DebugHelper.PrintErrorMessage($"[{DateTime.Now}][机器人][{rst.Result.MessageType}][取消所有任务]错误的消息类型...");
                        return;
                    }
                }
                else
                {
                    DebugHelper.PrintErrorMessage($"[机器人][取消所有任务]等待超时，放弃等待...");
                }
                #endregion
            }
            #endregion
            #region 呼叫电梯
            {
                #region 变量定义
                var buffer = new byte[1024];
                var startFloor = fromFloorNo; // 起始楼层
                var endFloor = toFloorNo;   // 目标楼层
                #endregion
                #region 发送【呼叫电梯】请求
                var tx = JsonConvert.SerializeObject(new ActionCallElevatorTx { Start = startFloor, End = endFloor, ModuleName = moduleName });
                DebugHelper.PrintTraceMessage($"[机器人]呼叫电梯...");
                DebugHelper.PrintTxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                s.Wait();
                #endregion
                #region 接收呼叫电梯回文
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                rst.Wait();
                if (rst.Result.MessageType == WebSocketMessageType.Text)
                {
                    DebugHelper.PrintTraceMessage($"[机器人]呼叫电梯回文...");
                    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                    DebugHelper.PrintRxMessage(msg);
                }
                else
                {
                    // 错误的消息类型
                    DebugHelper.PrintErrorMessage($"[机器人][呼叫电梯]错误的消息类型...");
                    return;
                }
                #endregion
            }
            #endregion
            #region 等待电梯到达指令
            {
                var buffer = new byte[1024];
                DebugHelper.PrintTraceMessage($"[机器人][等待电梯到达指令]...");
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                rst.Wait();
                if (rst.Result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                    DebugHelper.PrintRxMessage(msg);
                }
                else
                {
                    // 错误的消息类型
                    DebugHelper.PrintErrorMessage($"[机器人][等待电梯到达指令]错误的消息类型...");
                    return;
                }
                #region 发送电梯到达回文
                var tx = JsonConvert.SerializeObject(new ActionElevatorArrivedRx { ActionName = "ElevatorArrived_Back", Delay = 16 });
                DebugHelper.PrintTraceMessage($"[机器人]发送电梯到达回文...");
                DebugHelper.PrintTxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                #endregion
            }
            #endregion
            #region 进入电梯
            DebugHelper.PrintTraceMessage($"[机器人]正在进入电梯电梯（等待{robotInElevatorSecond / 1000}秒）...");
            Thread.Sleep(robotInElevatorSecond);
            #endregion
            #region 已经进入电梯
            {
                #region 变量定义
                var buffer = new byte[1024];
                #endregion
                #region 发送[机器人已进入电梯]指令
                DebugHelper.PrintTraceMessage($"[机器人]机器人已进入电梯...");
                var tx = JsonConvert.SerializeObject(new ActionRobotMovedTx { Location = "In" });
                DebugHelper.PrintTxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                #endregion
                #region 接收返回结果
                //Console.WriteLine("[-- 02 --]");
                //var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                //rst.Wait();
                //Console.WriteLine("[-- 03 --]");
                //if (rst.Result.MessageType == WebSocketMessageType.Text)
                //{
                //    Console.WriteLine($"[{DateTime.Now}][机器人]机器人已进入电梯回文...");
                //    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                //    OutRx(msg);
                //}
                //else
                //{
                //    // 错误的消息类型
                //    Console.WriteLine($"[{DateTime.Now}][机器人][已经进入电梯]错误的消息类型...");
                //    Console.ReadKey();
                //    return;
                //}
                #endregion
                #region 发送心跳包
                SendHeartbeat(webSocket);
                #endregion
            }
            #endregion
            #region 等待电梯到达指令(目标楼层)
            {
                var buffer = new byte[1024];
                DebugHelper.PrintTraceMessage($"[机器人][等待电梯到达指令][目标楼层][死等！]...");
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                rst.Wait();
                if (rst.Result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                    DebugHelper.PrintRxMessage(msg);
                }
                else
                {
                    // 错误的消息类型
                    DebugHelper.PrintErrorMessage($"[{DateTime.Now}][机器人][等待电梯到达指令][目标楼层]错误的消息类型...");
                    return;
                }
                #region 发送电梯到达回文
                DebugHelper.PrintTraceMessage($"要求电梯延迟关门{elevatorOpenDoorSecond}秒...");
                var tx = JsonConvert.SerializeObject(new ActionElevatorArrivedRx { ActionName = "ElevatorArrived_Back", Delay = (byte)elevatorOpenDoorSecond });
                DebugHelper.PrintTraceMessage($"[{DateTime.Now}][机器人]发送电梯到达目标楼层指令回文...");
                DebugHelper.PrintRxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                //DebugHelper.PrintTraceMessage($"[{DateTime.Now}][机器人][等待电梯到达回文，最长等待2秒]");
                //rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                // 等待回文，超时则报错。模拟器回文不处理。
                //rst.Wait(2000);
                //if (rst.Result.MessageType == WebSocketMessageType.Text)
                //{
                //    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                //    DebugHelper.PrintRxMessage(msg);
                //}
                //else
                //{
                //    // 错误的消息类型
                //    DebugHelper.PrintErrorMessage($"[{DateTime.Now}][机器人][等待电梯到达回文]错误...");
                //    return;
                //}
                #endregion
            }
            #endregion
            #region 发送心跳包
            SendHeartbeat(webSocket);
            #endregion
            #region 走出电梯时间
            DebugHelper.PrintTraceMessage($"[{DateTime.Now}][机器人]正在走出电梯电梯（等待{robotOutElevatorSecond / 1000}秒）...");
            Thread.Sleep(robotOutElevatorSecond);
            #endregion
            #region 机器人已走出电梯（上送）
            {
                #region 变量定义
                var buffer = new byte[1024];
                #endregion

                #region 发送【机器人已走出电梯】请求
                DebugHelper.PrintTraceMessage($"[机器人]机器人已走出电梯...");
                var tx = JsonConvert.SerializeObject(new ActionRobotMovedTx { Location = "Out" });
                DebugHelper.PrintTxMessage(tx);
                var s = webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(tx)), WebSocketMessageType.Text, true, CancellationToken.None);
                s.Wait();
                #endregion
                #region 接收【机器人已走出电梯】返回结果
                var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (rst.Wait(timeout))
                {
                    if (rst.Result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
                        DebugHelper.PrintRxMessage(msg);
                        DebugHelper.PrintTraceMessage("收到[机器人]机器人已走出电梯[回文]...  -- 乘梯任务完成 --");
                    }
                    else
                    {
                        // 错误的消息类型
                        DebugHelper.PrintErrorMessage($"[机器人][机器人已走出电梯]错误的消息类型（忽略）...");
                    }
                }
                else
                {
                    DebugHelper.PrintErrorMessage("[机器人][机器人已走出电梯]超时，放弃.");
                }

                #endregion
            }
            #endregion

            if (taskEndExit)
            {
                return;
            }
            else
            {
                var exitFlag = false;
                Console.WriteLine("按任意键退出。现在只发送心跳...");
                Task.Run(() =>
                {
                    do
                    {
                        var buffer = new byte[1024];
                        var jo = new JObject();
                        jo["ActionName"] = " Heartbeat";
                        webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jo.ToString())), WebSocketMessageType.Text, true, CancellationToken.None);
                        var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        rst.Wait(2000);
                        Thread.Sleep(robotHeartBeatSecond * 1000);
                    } while (!exitFlag);
                });
                Console.ReadKey();
                exitFlag = true;
                Thread.Sleep(200);
            }

        }

        static async void StartReceiving(ClientWebSocket client)
        {
            while (client.State != WebSocketState.Closed
                && client.State != WebSocketState.CloseReceived
                && client.State != WebSocketState.CloseSent)
            {
                var array = new byte[1024];
                var result = await client.ReceiveAsync(new ArraySegment<byte>(array), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = Encoding.UTF8.GetString(array, 0, result.Count);
                    var fc = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Rx][{DateTime.Now}]{msg}");
                    Console.ForegroundColor = fc;
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now}]{result.MessageType.ToString()}");
                }
            }
            Console.WriteLine($"[{DateTime.Now}]Exit StartReceiving thread.");
        }

        static void SendHeartbeat(ClientWebSocket webSocket)
        {
            //var jo = new JObject();
            //jo["ActionName"] = "Heartbeat";
            //{
            //    string msg = jo.ToString();
            //    DebugHelper.PrintTxMessage(msg);
            //    webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
            //}
            //var buffer = new byte[1024];
            //DebugHelper.PrintTraceMessage($"[机器人][等待心跳包回文][死等！]...");
            //var rst = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            //rst.Wait();
            //if (rst.Result.MessageType == WebSocketMessageType.Text)
            //{
            //    string msg = Encoding.UTF8.GetString(buffer, 0, rst.Result.Count);
            //    DebugHelper.PrintRxMessage(msg);
            //}
            //else
            //{
            //    // 错误的消息类型
            //    DebugHelper.PrintErrorMessage($"[{DateTime.Now}][机器人][等待电梯到达指令][目标楼层]错误的消息类型...");
            //    return;
            //}
        }
    }


}
