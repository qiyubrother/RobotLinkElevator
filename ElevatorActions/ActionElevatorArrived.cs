using System;
using System.Collections.Generic;
using System.Text;

namespace ElevatorActions
{
    public class ActionElevatorArrivedRx : ActionBaseTx
    {
        public override string ActionName { get => "ElevatorArrived_Back"; }
        /// <summary>
        /// 延时关门时间（秒）
        /// </summary>
        public byte Delay { get; set; }
        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp => ((long)(DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalSeconds).ToString();  // 相差秒数

    }
}
