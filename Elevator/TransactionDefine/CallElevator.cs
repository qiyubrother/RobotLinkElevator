using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator
{

    public class CallElevatorTx
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 31;
        public byte HeaderLen2 = 31;
        public byte HeaderEnd = 0x68;
        public byte C = 2;  // 命令号
        public byte A = 8;  // 机器人编号
        public byte CI = 1; // 附加控制命令
        public byte[] DATA = new byte[6];
        public byte Start = 0;
        public byte End = 0;
        public byte[] Sha1 = new byte[20];
        public byte CS = 0;
        public byte EndFlag = 0x16;

        /// <summary>
        /// 获取字节流
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 6 + 1 + 1 + 20 + ( 1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            buffer[6] = CI;
            Array.Copy(DATA, 0, buffer, 7, 6);
            buffer[13] = Start;
            buffer[14] = End;
            Array.Copy(Sha1, 0, buffer, 15, 20);

            buffer[buffer.Length - 2] = CS;
            buffer[buffer.Length - 1] = EndFlag;

            return buffer;
        }
        /// <summary>
        /// 根据字节流构建实体类
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static CallElevatorTx Create(byte[] bytes)
        {
            var obj = new CallElevatorTx
            {
                HeaderStart = bytes[0],
                HeaderLen1 = bytes[1],
                HeaderLen2 = bytes[2],
                HeaderEnd = bytes[3],
                C = bytes[4],
                A = bytes[5],
                CI = bytes[6],
                Start = bytes[13],
                End = bytes[14],
                CS = bytes[bytes.Length - 2], 
                EndFlag = bytes[bytes.Length - 1]
            };

            for (var i = 0; i < 6; i++) obj.DATA[i] = bytes[i + 7];
            for (var i = 0; i < 20; i++) obj.Sha1[i] = bytes[i + 15];

            return obj;
        }
    }


    public class CallElevatorRx
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 30;
        public byte HeaderLen2 = 30;
        public byte HeaderEnd = 0x68;
        public byte C = 0x83;  // 命令号
        public byte A = 8;  // 机器人编号
        public byte CI = 1; // 附加控制命令
        public byte[] DATE = new byte[6];
        public byte NO = 0;
        public byte[] Sha1 = new byte[20];
        public byte CS = 0;
        public byte EndFlag = 0x16;

        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 6 + 1 + 20 + (1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            buffer[6] = CI;
            for (var i = 0; i < 6; i++) buffer[i + 7] = DATE[i];

            buffer[13] = NO;
            for (var i = 0; i < 20; i++) buffer[i + 14] = Sha1[i];

            buffer[buffer.Length - 2] = CS;
            buffer[buffer.Length - 1] = EndFlag;

            return buffer;
        }
    }
}
