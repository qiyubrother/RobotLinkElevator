using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using ElevatorActions;
namespace RobotLinkElevator
{
    public class ReturnAction
    {
        public static ActionBaseRx Get(WebSocket webSocket, ActionBaseTx a)
        {
            if (a is ActionGetServerTimeTx)
            {
                var tx = a as ActionGetServerTimeTx;
                return new ActionGetServerTimeRx { ErrorInfo = "OK", RobotSN = "", RRRR_STATUS = "SUCCESS" };
            }
            else if (a is ActionCallElevatorTx)
            {
                var tx = a as ActionCallElevatorTx;
                if (webSocket.State == WebSocketState.Closed
                    || webSocket.State == WebSocketState.CloseReceived
                    || webSocket.State == WebSocketState.CloseSent)
                {
                    return new ActionCallElevatorRx { ErrorInfo = "ERROR_TASKING", RobotSN = String.Empty, RRRR_STATUS = "ERROR_OFFLINE", Number = string.Empty };
                }
                if (Program.RobotLinks.TryGetValue(webSocket, out RobotWebSocketLinkInfo rl))
                {
                    return new ActionCallElevatorRx { ErrorInfo = "OK", RobotSN = rl.SN, RRRR_STATUS = "SUCCESS" };
                }
                else
                {
                    return new ActionCallElevatorRx { ErrorInfo = "SYSTEM_ERROR", RobotSN = String.Empty, RRRR_STATUS = "SYSTEM_ERROR", Number = string.Empty};
                }
            }
            return null;
        }
    }
}
