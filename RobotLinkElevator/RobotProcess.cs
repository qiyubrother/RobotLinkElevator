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
using ElevatorActions;
namespace RobotLinkElevator
{
    partial class Program
    {
        #region 启动WebSocket（机器人）服务
        static void RunWebSocketServer()
        {
            string url = _configuration["WebSocket:url"];
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            DebugHelper.PrintTraceMessage($"[机器人报文处理服务][{url}]WebSocketServerStarted");

            Task.Run(() => RunAcceptLoopAsync(listener));
        }
        #endregion

        #region [服务器|机器人]机器人通过WebSocket连接服务器，并向服务器发送SN
        static async Task RunAcceptLoopAsync(HttpListener listener)
        {
            while (true)
            {
                WebSocket webSocket = null;
                try
                {
                    var context = listener.GetContext();
                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.Close();
                        continue;
                    }
                    DebugHelper.PrintTraceMessage($"[WebSocket]Waiting Connect...");
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    webSocket = wsContext.WebSocket;

                    var allKeys = context.Request.QueryString.AllKeys;
                    var Parameters = new Dictionary<string, string>();
                    allKeys.All(s => { Parameters.Add(s, context.Request.QueryString.Get(s)); return true; });
                    var sb = new StringBuilder();
                    foreach(var k in Parameters)
                    {
                        sb.Append($"{k.Key}={k.Value}");
                    }

                    DebugHelper.PrintTraceMessage($"[WebSocket][QueryString]{sb}");

                    new Thread(() => { ProcessAcceptWebSocket(webSocket, Parameters); }).Start();

                    DebugHelper.PrintTraceMessage($"[WebSocket]Accepted.");
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"[RunAcceptLoopAsync]{ex.Message}");
                    // 异常之后清除WebSocket记录
                    try
                    {
                        RobotLinks.TryRemove(webSocket, out RobotWebSocketLinkInfo _);
                    }
                    catch { }
                }
            }
        }
        static void ProcessAcceptWebSocket(WebSocket webSocket, Dictionary<string, string> Parameters)
        {
            // 读取首次连接参数
            Parameters.TryGetValue("checksum", out string signature);
            Parameters.TryGetValue("username", out string username);
            Parameters.TryGetValue("curtime", out string curtime);
            Parameters.TryGetValue("robotsn", out string robotsn);
            Parameters.TryGetValue("robotmac", out string robotmac);
            Parameters.TryGetValue("nonce", out string nonce);

            #region 业务逻辑处理(Hold)
            // 将连接记录保存到数据库
            #endregion

            // 组织返回报文
            var firstMessage = new FirstMessageInfo
            {
                RRRR_STATUS = "SUCCESS",
                RobotSN = robotsn,
                ErrorInfo = "OK",
                ActionName = "FirstConnection_Back",
                ServerTime = DateTime.Now
            };
            // SHA验证
            if (MD5VerifyValidity(signature, username, curtime, robotsn, robotmac, nonce))
            {
                // 发送回文
                #region 业务逻辑处理(Hold)
                // 将首次连接回文保存到数据库
                #endregion
                webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(firstMessage))), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                // 填写加密方法不对回文
                firstMessage.RRRR_STATUS = "ERROR_JMFF";
                firstMessage.ErrorInfo = "加密方法不对";
                // 发送回文
                webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(firstMessage))), WebSocketMessageType.Text, true, CancellationToken.None);
                // 注销WebSocket资源
                webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "", CancellationToken.None);
                webSocket.Dispose();
                DebugHelper.PrintErrorMessage($"首次连接失败，加密方法不对。");
                return;
            }
            //Console.WriteLine($"robotsn:::{robotsn}");
            //Console.WriteLine($"robotsn:::{username}");
            //Console.WriteLine($"RobotHelper.GetUniqueRobotSN:::{RobotHelper.GetUniqueRobotSN(username, robotsn)}");
            var uniqueSN = RobotHelper.GetUniqueRobotSN(username, robotsn); // 将robotSN转换成UniqueRobotSN
            DebugHelper.PrintTraceMessage($"[WebSocket]robotsn({robotsn})->UniqueRobotSN({uniqueSN}).");

            // 添加当前WebSocket到连接管理队列
            RobotLinks.TryAdd(webSocket, new RobotWebSocketLinkInfo
            {
                WebSocket = webSocket,
                //SN = robotsn,
                SN = uniqueSN, 
                OnlineTime = DateTime.Now,
                HeartbeatTime = DateTime.Now,
            });
            // 首次连接认证通过后，异步接收客户端请求
            Task.Run(() => HandleConnectionAsync(webSocket));
            DebugHelper.PrintTraceMessage($"机器人::{robotsn}连接到服务器...");
        }
        #endregion

        #region [服务器|机器人]首次连接认证通过后，异步接收客户端请求
        static async Task HandleConnectionAsync(WebSocket webSocket)
        {
            while (webSocket.State != WebSocketState.Aborted
                && webSocket.State != WebSocketState.Closed
                && webSocket.State != WebSocketState.CloseReceived
                && webSocket.State != WebSocketState.CloseSent
                && webSocket.State != WebSocketState.None
                )
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    // 异步接收数据
                    var received = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    DebugHelper.PrintTraceMessage($"Trace.RawData(string)::{Encoding.UTF8.GetString(buffer, 0, received.Count)}");
                    #region 业务逻辑处理(Hold)
                    // 将接收的报文存储到数据库
                    #endregion
                    if (received.MessageType != WebSocketMessageType.Close)
                    {
                        // Echo anything we receive
                        #region 业务逻辑处理(Hold)
                        // 将发送的报文存储到数据库
                        #endregion
                        #region [服务器|机器人]接收机器人发来的数据，并将数据追加到RobotReceives队列。
                        var param = new RobotReceive { RawData = buffer, RawDataLength = received.Count };
                        string result = Encoding.UTF8.GetString(buffer, 0, received.Count).Trim();
                        try
                        {
                            var v = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
                            foreach (var p in v)
                            {
                                if (p.Key == "ActionName")
                                {
                                    param.Command = p.Value;
                                    continue;
                                }
                                param.Parameters.Add(p.Key, p.Value);
                            }
                            if (string.IsNullOrEmpty(param.Command))
                            {
                                DebugHelper.PrintTraceMessage($"Invalid ActionName.TxData:{result}");
                            }
                            param.WebSocket = webSocket;
                            RobotReceives.Enqueue(param);
                        }
                        catch (Exception ex)
                        {
                            DebugHelper.PrintErrorMessage($"ReceiveWebSocket:{ex.Message}");
                            DebugHelper.PrintDebugMessage(buffer, received.Count);
                            DebugHelper.PrintErrorMessage($"RawData(string)::{Encoding.UTF8.GetString(buffer, 0, received.Count)}");
                            
                            var robotSend = new RobotSend ();

                            try
                            {
                                robotSend.SN = RobotLinks[webSocket].SN;
                                robotSend.Command = "ErrorCommand_Back";
                                robotSend.Parameters.Add("RRRR_STATUS", "ERROR_TASKING");
                                robotSend.Parameters.Add("RobotSN", RobotLinks[webSocket].SN);
                                robotSend.Parameters.Add("ErrorInfo", "无效的请求。系统解析出错。");
                                robotSend.Parameters.Add("Timestamp", TimeHelp.ConvertDateTimeToInt(DateTime.Now).ToString());
                                RobotSends.Enqueue(robotSend);
                                var so = JsonConvert.SerializeObject(robotSend);
                                DebugHelper.PrintErrorMessage($"返回错误消息到机器人:{so}");
                            }
                            catch(Exception ex2)
                            {
                                DebugHelper.PrintErrorMessage($"ReceiveWebSocket.catch[异常处理模块再次出错]:{ex2.Message}");
                            }
                            continue;
                        }
                        #endregion
                    }
                    else
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);

                        webSocket.Dispose();
                        DebugHelper.PrintErrorMessage($"客户端异常, 退出。");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    DebugHelper.PrintErrorMessage($"[{DateTime.Now}][HandleConnectionAsync]疑客户端断开。[{ex.Message}] Sleep(500);");
                    Thread.Sleep(500);
                }
            }
        }
        #endregion
        
        #region [服务器|机器人]报文内容校验
        static bool MD5VerifyValidity(string signature, string username, string curtime, string robotsn, string robotmac, string nonce)
        {
            bool result = false;
            string[] ArrTmp = { username.ToUpper(), curtime.ToUpper(), robotsn.ToUpper(), robotmac.ToUpper(), nonce.ToUpper() };
            Array.Sort(ArrTmp);
            string tmpStr = string.Join("", ArrTmp);
            using (var md5 = MD5.Create())
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(tmpStr);
                byte[] md5Array = md5.ComputeHash(byteArray);
                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < md5Array.Length; i++)
                {
                    sBuilder.Append(md5Array[i].ToString("x2"));
                }
                string temp = sBuilder.ToString();

                if (temp.ToUpper() == signature.ToUpper())
                {
                    result = true;
                }
            }
            return result;
        }
        #endregion
    }
}
