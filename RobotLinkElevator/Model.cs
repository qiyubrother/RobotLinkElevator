using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace RobotLinkElevator
{
    #region 函数参数定义（机器人 | 电梯）
    class LinkBaseData
    {
        public string SN { get; set; }
        public DateTime OnlineTime { get; set; }
        public DateTime HeartbeatTime { get; set; }
    }
    class ElevatorSocketLinkInfo : LinkBaseData
    {
        public Socket Socket { get; set; }
        public byte ConfirmId { get; set; }
    }
    class RobotWebSocketLinkInfo : LinkBaseData
    {
        public WebSocket WebSocket { get; set; }
        public string ElevatorModuleName { get; set; } = string.Empty;

    }
    #endregion
    #region 电梯收发数据定义
    class ElevatorData
    {
        public byte Command { get; set; }
        public byte Address { get; set; }
        public byte Append { get; set; }
        /// <summary>
        /// 电梯Data区字节数组
        /// </summary>
        public byte[] Data { get; set; }
    }
    class ElevatorReceive : ElevatorData
    {
        public Socket Socket { get; set; }
    }
    class ElevatorSend : ElevatorData
    {
        public string SN { get; set; }
    }
    #endregion
    #region 机器人收发数据类型定义
    class RobotData
    {
        public string Command { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
    class RobotReceive : RobotData
    {
        public WebSocket WebSocket { get; set; }
        public byte[] RawData;
        public int RawDataLength;
    }
    class RobotSend : RobotData
    {
        public string SN { get; set; }
    }
    #endregion
    #region 机器人首次连接认证数据结构定义
    public class FirstMessageInfo
    {
        public string RRRR_STATUS { get; set; }

        public string RobotSN { get; set; }

        public DateTime ServerTime { get; set; }

        public string ActionName { get; set; }

        public string ErrorInfo { get; set; }
    }
    #endregion
    #region 电梯呼叫任务数据定义
    public class CallElevatorTask
    {
        public bool IsCanceled { get; set; } = false;
        public string RobotSN { get; set; }
        public int RobotId { get; set; }
        /// <summary>
        /// 电梯编号
        /// </summary>
        public string ElevatorSN { get; set; }
        /// <summary>
        /// 服务器时间
        /// </summary>
        public DateTime TaskTime { get; set; } = DateTime.Now;
        /// <summary>
        /// 机器人所在楼层
        /// </summary>
        public int RobotStartFloor { get; set; }
        /// <summary>
        /// 机器人目标楼层
        /// </summary>
        public int RobotEndFloor { get; set; }

        public int ElevatorFloor { get; set; }
        public int ElevatorNumber { get; set; }
        /// <summary>
        /// 延迟时间
        /// </summary>
        public int Delay { get; set; }
        /// <summary>
        /// 任务类型
        /// </summary>
        public TaskPoint TaskPoint { get; set; }

        /// <summary>
        /// 电梯模块名
        /// </summary>
        public string ModuleName { get; set; }
        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp { get; set; }
    }
    #endregion
    #region 任务类型定义
    public enum TaskPoint
    {
        /// <summary>
        /// 取消任务
        /// </summary>
        CancelTask,
        /// <summary>
        /// 呼叫电梯
        /// </summary>
        CallElevator,
        /// <summary>
        /// 返回电梯编号
        /// </summary>
        ReturnNumber,
        /// <summary>
        /// 到达出发楼层
        /// </summary>
        StartArrived,
        /// <summary>
        /// 返回延迟
        /// </summary>
        ReturnDelay,
        /// <summary>
        /// 已经进入电梯
        /// </summary>
        InElevator,
        /// <summary>
        /// 到达目标楼层
        /// </summary>
        EndArrived,
        /// <summary>
        /// 已经出电梯
        /// </summary>
        OutElevator
    }
    #endregion
}
