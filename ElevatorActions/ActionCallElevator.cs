using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ElevatorActions
{
    /// <summary>
    /// 呼叫电梯上送报文
    /// </summary>
    public class ActionCallElevatorTx : ActionBaseTx
    {
        /// <summary>
        /// 呼叫电梯动作名
        /// </summary>
        public override string ActionName { get => "CallElevator"; }
        /// <summary>
        /// 起始楼层
        /// </summary>
        public string Start { get; set; }
        /// <summary>
        /// 终止楼层
        /// </summary>
        public string End { get; set; }
        /// <summary>
        /// 电梯模块名
        /// </summary>
        public string ModuleName { get; set; }
        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp => ((long)(DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalSeconds).ToString();  // 相差秒数

    }
    /// <summary>
    /// 呼叫电梯下送报文
    /// </summary>
    public class ActionCallElevatorRx : ActionBaseRx
    {
        /// <summary>
        /// 返回操作指令名称  CallElevator_Back
        /// </summary>
        public override string ActionName { get => "CallElevator_Back"; }
        /// <summary>
        /// 电梯编号
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// 电梯模块名
        /// </summary>
        public string ModuleName { get; set; }
        /// <summary>
        /// 时间戳
        /// </summary>
        public string Timestamp => ((long)(DateTime.Now - TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1))).TotalSeconds).ToString();  // 相差秒数

    }
}
