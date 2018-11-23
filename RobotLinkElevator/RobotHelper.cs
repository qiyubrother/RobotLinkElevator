using System;
using System.Collections.Generic;
using System.Text;
using DataModule.Model;
using System.Linq;
namespace RobotLinkElevator
{
    public class RobotHelper
    {
        /// <summary>
        /// 根据RobotSN,RobotCompanyTag查询唯一的机器人ID
        /// </summary>
        /// <param name="robotCompanyTag">机器人连接WebSocket时传递的username</param>
        /// <param name="robotSN">机器人连接WebSocket时传递的robotsn</param>
        /// <returns></returns>
        public static string GetUniqueRobotSN(string robotCompanyTag, string robotSN)
        {
            var dc = new DataContext();
            var tag = dc.RobotCompanys.FirstOrDefault(x => x.CompanyTag.ToLower() == robotCompanyTag.ToLower());
            if (tag != null)
            {
                var ll = dc.RobotMaps.ToList();
                var r = dc.RobotMaps.FirstOrDefault(x => x.RobotSN == robotSN && x.RobotCompanyId.ToLower() == tag.RobotCompanyID.ToLower());
                if (r != null)
                {
                    return r.UniqueRobotSN;
                }
                else
                {
                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
