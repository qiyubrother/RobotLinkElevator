using System;
using System.Collections.Generic;
using System.Text;

namespace ElevatorActions
{
    public class ActionRobotMovedTx : ActionBaseTx
    {
        /// <summary>
        /// 机器人已就位动作名
        /// </summary>
        public override string ActionName { get => "RobotMoved"; }

        /// <summary>
        /// 机器人位置 In | Out
        /// </summary>
        public string Location { get; set; }
        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp => ((long)(DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalSeconds).ToString();  // 相差秒数

    }

    public class ActionRobotMovedRx : ActionBaseRx
    {
        /// <summary>
        /// 机器人已就位动作名
        /// </summary>
        public override string ActionName { get => "RobotMoved_Back"; }
        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp => ((long)(DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalSeconds).ToString();  // 相差秒数

    }
}
