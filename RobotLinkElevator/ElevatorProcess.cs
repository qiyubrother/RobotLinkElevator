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

namespace RobotLinkElevator
{
    partial class Program
    {
        #region 启动Socket（电梯）服务
        static void RunSocketServer()
        {
            string[] temp = _configuration["Socket:ip"].Split('.');
            byte[] ip = new byte[4];
            if (temp.Length != 4)
            {
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                ip[i] = Convert.ToByte(temp[i]);
            }

            Server.Startup(
                new IPEndPoint(new IPAddress(ip), Convert.ToInt32(_configuration["Socket:port"])),
                ReceivedSocketDataCallBack);
            DebugHelper.PrintTraceMessage($"[{DateTime.Now}][电梯报文处理服务][{_configuration["Socket:ip"]}:{_configuration["Socket:port"]}]SocketServerStarted");
        }
        static void ReceivedSocketDataCallBack(object o)
        {
            var param = o as SocketCallbackEventArgs;
            {
                ReceiveSocket(param.Socket, param.Buffer);
            }
        }
        #endregion

        #region 电梯向服务器发送“发送ID”指令
        public static void ElevatorCommand_81(string sn, byte confirmId)
        {
            try
            {
                ElevatorSend elevatorSend = new ElevatorSend();
                elevatorSend.SN = sn;
                elevatorSend.Command = 0x81;
                elevatorSend.Data = new byte[7];
                elevatorSend.Data[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                elevatorSend.Data[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                elevatorSend.Data[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                elevatorSend.Data[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                elevatorSend.Data[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                elevatorSend.Data[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                elevatorSend.Data[6] = confirmId;
                ElevatorSends.Enqueue(elevatorSend);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_81:" + ex.Message);
            }
        }
        #endregion

        #region 电梯向服务器发送“获取当前系统时间”指令
        public static void ElevatorCommand_02(ElevatorSocketLinkInfo elevatorLink, ElevatorData elevatorData)
        {
            try
            {
                ElevatorCommand_82(elevatorLink.SN);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_02:" + ex.Message);
            }
        }
        public static void ElevatorCommand_82(string sn)
        {
            try
            {
                ElevatorSend ed = new ElevatorSend();
                ed.SN = sn;
                ed.Command = 0x82;
                ed.Data = new byte[6];
                ed.Data[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                ed.Data[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                ed.Data[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                ed.Data[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                ed.Data[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                ed.Data[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                ElevatorSends.Enqueue(ed);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_82:" + ex.Message);
            }
        }
        #endregion

        #region 服务器向电梯发送“呼叫电梯”指令
        public static void ElevatorCommand_03(string sn, int robotId, int start, int end)
        {
            try
            {
                DebugHelper.PrintTraceMessage("服务器向电梯发送“呼叫电梯”指令...");
                ElevatorSend elevatorSend = new ElevatorSend();
                elevatorSend.SN = sn;
                elevatorSend.Command = 0x03;
                elevatorSend.Address = Convert.ToByte(robotId);
                elevatorSend.Append = 0x01;
                elevatorSend.Data = new byte[8];
                elevatorSend.Data[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                elevatorSend.Data[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                elevatorSend.Data[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                elevatorSend.Data[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                elevatorSend.Data[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                elevatorSend.Data[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                elevatorSend.Data[6] = Convert.ToByte(start);
                elevatorSend.Data[7] = Convert.ToByte(end);
                ElevatorSends.Enqueue(elevatorSend);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_03:" + ex.Message);
            }
        }
        public static void ElevatorCommand_83(ElevatorSocketLinkInfo elevatorLink, ElevatorData elevatorData)
        {
            // 机器人ID
            byte robotId = elevatorData.Address;
            // 取得电梯编号
            byte number = elevatorData.Data[6];

            try
            {
                DebugHelper.PrintTraceMessage("服务器向电梯发送“呼叫电梯”指令::回文...");
                byte[] data1 = new byte[7];
                Array.Copy(elevatorData.Data, data1, 7);
                byte[] data2 = new byte[20];
                Array.Copy(elevatorData.Data, 7, data2, 0, 20);
                var b = CheckDataSign(data1, elevatorLink.ConfirmId, data2);
                if (b)
                {
                    ReturnNumber(elevatorLink.SN, robotId, number);
                }
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_83:" + ex.Message);
            }
        }
        #endregion

        #region 电梯向服务器发送“电梯到达出发楼层 | 电梯到达目标楼层”指令
        public static void ElevatorCommand_04(ElevatorSocketLinkInfo elevatorLink, ElevatorData elevatorData)
        {
            DebugHelper.PrintTraceMessage("电梯向服务器发送“电梯到达出发楼层 | 电梯到达目标楼层”指令[0x04]...");
            byte robotId = elevatorData.Address;
            byte number = elevatorData.Data[6];
            byte floor = elevatorData.Data[7];
            byte point = elevatorData.Data[8];

            try
            {
                byte[] data1 = new byte[9];
                Array.Copy(elevatorData.Data, data1, 9);
                byte[] data2 = new byte[20];
                Array.Copy(elevatorData.Data, 9, data2, 0, 20);
                var b = CheckDataSign(data1, elevatorLink.ConfirmId, data2);
                if (b)
                {
                    DebugHelper.PrintTraceMessage($"[电梯][电梯到达出发楼层][CheckDataSign]数据校验通过...");
                    ElevatorArrived(elevatorLink.SN, robotId, number, floor, point);
                }
                else
                {
                    DebugHelper.PrintTraceMessage($"[电梯][电梯到达出发楼层][CheckDataSign]数据校验失败...");
                }
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_04:" + ex.Message);
            }
        }
        public static void ElevatorCommand_84(string sn, int robotId, int delay)
        {
            try
            {
                DebugHelper.PrintTraceMessage("电梯向服务器发送“电梯到达出发楼层 | 电梯到达目标楼层”指令::回文...");
                ElevatorSend elevatorSend = new ElevatorSend();
                elevatorSend.SN = sn;
                elevatorSend.Command = 0x84;
                elevatorSend.Address = Convert.ToByte(robotId);
                elevatorSend.Append = 0x01;
                elevatorSend.Data = new byte[7];
                elevatorSend.Data[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                elevatorSend.Data[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                elevatorSend.Data[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                elevatorSend.Data[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                elevatorSend.Data[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                elevatorSend.Data[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                elevatorSend.Data[6] = Convert.ToByte(delay);
                ElevatorSends.Enqueue(elevatorSend);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_84:" + ex.Message);
            }
        }
        #endregion

        #region 服务器向电梯发送“机器人已进入电梯 | 已出电梯”指令
        public static void ElevatorCommand_05(string sn, int robotId, int elevatorNumber)
        {
            try
            {
                DebugHelper.PrintTraceMessage("服务器向电梯发送“机器人已进入电梯 | 已出电梯”指令[0x05]::组织数据并添加到发送队列...");
                ElevatorSend elevatorSend = new ElevatorSend();
                elevatorSend.SN = sn;
                elevatorSend.Command = 0x05;
                elevatorSend.Address = Convert.ToByte(robotId);
                elevatorSend.Append = 0x01;
                elevatorSend.Data = new byte[7];
                elevatorSend.Data[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                elevatorSend.Data[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                elevatorSend.Data[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                elevatorSend.Data[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                elevatorSend.Data[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                elevatorSend.Data[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                elevatorSend.Data[6] = Convert.ToByte(elevatorNumber);
                ElevatorSends.Enqueue(elevatorSend);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_05:" + ex.Message);
            }
        }
        #endregion

        #region 服务器向电梯发送“任务取消”指令
        public static void ElevatorCommand_06(string sn, int robotId, int elevatorNumber, int start, int end)
        {
            try
            {
                DebugHelper.PrintTraceMessage("服务器向电梯发送“任务取消”指令[0x06]::组织数据并添加到发送队列...");
                ElevatorSend elevatorSend = new ElevatorSend();
                elevatorSend.SN = sn;
                elevatorSend.Command = 0x06;
                elevatorSend.Address = Convert.ToByte(robotId);
                elevatorSend.Append = 0x01;
                elevatorSend.Data = new byte[9];
                elevatorSend.Data[0] = Convert.ToByte(DecToBcd(DateTime.Now.Year - 2000));
                elevatorSend.Data[1] = Convert.ToByte(DecToBcd(DateTime.Now.Month));
                elevatorSend.Data[2] = Convert.ToByte(DecToBcd(DateTime.Now.Day));
                elevatorSend.Data[3] = Convert.ToByte(DecToBcd(DateTime.Now.Hour));
                elevatorSend.Data[4] = Convert.ToByte(DecToBcd(DateTime.Now.Minute));
                elevatorSend.Data[5] = Convert.ToByte(DecToBcd(DateTime.Now.Second));
                elevatorSend.Data[6] = Convert.ToByte(elevatorNumber);
                elevatorSend.Data[7] = Convert.ToByte(start);
                elevatorSend.Data[8] = Convert.ToByte(end);
                ElevatorSends.Enqueue(elevatorSend);

                DebugHelper.PrintDebugMessage(elevatorSend.Data);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_06:" + ex.Message);
            }
        }

        public static void ElevatorCommand_85(ElevatorSocketLinkInfo elevatorLink, ElevatorData elevatorData)
        {
            // byte robotId = elevatorData.Address;

        }
        public static void ElevatorCommand_86(ElevatorSocketLinkInfo elevatorLink, ElevatorData elevatorData)
        {
            byte robotId = elevatorData.Address;

            try
            {
                DebugHelper.PrintTraceMessage("服务器向电梯发送“任务取消”指令::回文...");
                byte[] data1 = new byte[6];
                Array.Copy(elevatorData.Data, data1, 6);
                byte[] data2 = new byte[20];
                Array.Copy(elevatorData.Data, 6, data2, 0, 20);
                var b = CheckDataSign(data1, elevatorLink.ConfirmId, data2);
            }
            catch (Exception ex)
            {
                DebugHelper.PrintErrorMessage("ElevatorCommand_86:" + ex.Message);
            }
        }
        #endregion

        #region 电梯上送数据CS校验
        static bool CheckDataRule(byte[] data)
        {
            bool result = false;

            if (data == null || data.Length < 10)
            {
                //DebugHelper.PrintErrorMessage($"Invalid data length.");
                return result;
            }

            if (data[0] != 0x68 || data[data.Length - 1] != 0x16)
            {
                //DebugHelper.PrintErrorMessage($"Invalid data header. not 0x68.");
                return result;
            }
                
            if (data[0] != data[3] || data[1] != data[2] || data[1] != (data.Length - 6))
            {
                //DebugHelper.PrintErrorMessage($"Invalid data header. Length error.");
                return result;
            }

            byte cs = 0x00;
            int i = 0;
            while (i < data[1])
            {
                cs += data[i + 4];
                i++;
            }
            if (cs != data[data.Length - 2])
            {
                DebugHelper.PrintErrorMessage($"Invalid data header. cs error.");

                return result;
            }

            result = true;

            return result;
        }
        #endregion

        #region 电梯上送数据验证(SHA1)
        static bool CheckDataSign(byte[] data, byte id, byte[] signData)
        {
            bool result = false;

            if (data == null || signData == null)
            {
                DebugHelper.PrintErrorMessage(data == null ? "CheckDataSign.Error::data == null" : "CheckDataSign.Error::signData == null");
                return result;
            }

            byte[] bData = new byte[data.Length + 1];
            Array.Copy(data, bData, data.Length);
            bData[data.Length] = id;
            Array.Sort(bData);

            SHA1 sha = new SHA1CryptoServiceProvider();
            var sign = sha.ComputeHash(bData);
            if (sign.Length != signData.Length)
            {
                DebugHelper.PrintErrorMessage("CheckDataSign.Error::sign.Length != signData.Length");
                return result;
            }
            for (int i = 0; i < sign.Length; i++)
            {
                if (sign[i] != signData[i])
                {
                    DebugHelper.PrintErrorMessage("CheckDataSign.Error::sign[i] != signData[i]");
                    return result;
                }
            }
            result = true;

            return result;
        }
        #endregion

        #region 电梯上送数据格式化成ElevatorReceive类
        static ElevatorReceive DataAnalysis(byte[] data)
        {
            ElevatorReceive er = new ElevatorReceive();
            er.Command = data[4];
            er.Address = data[5];
            er.Append = data[6];
            er.Data = new byte[data[1] - 3];
            Array.Copy(data, 7, er.Data, 0, data[1] - 3);
            return er;
        }
        #endregion

        static void RemoveElevatorLink(Socket s)
        {
            ElevatorLinks.TryRemove(s, out _);
            DebugHelper.PrintTraceMessage($"ElevatorLinks removed socket {s.Handle}");
        }
        static void AddElevatorLink(Socket s)
        {
            ElevatorLinks.TryRemove(s, out _);
            DebugHelper.PrintTraceMessage($"ElevatorLinks added socket {s.Handle}");
        }
    }
}
