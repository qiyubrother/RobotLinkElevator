using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator
{
    public class CancelTaskTx
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 32;
        public byte HeaderLen2 = 32;
        public byte HeaderEnd = 0x68;
        public byte C = 0x86;  // 命令号
        public byte A = 8;  // 机器人编号
        public byte CI = 1; // 附加控制命令
        public byte[] DATE = new byte[6];
        public byte No = 0; // 电梯编号
        public byte Start = 0; // 起始楼层
        public byte End = 0;   // 目标楼层
        public byte[] Sha1 = new byte[20];
        public byte CS = 0;
        public byte EndFlag = 0x16;

        /// <summary>
        /// 获取字节流
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 6 + 1 + 1 + 1 + 20 + (1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            buffer[6] = CI;
            Array.Copy(DATE, 0, buffer, 7, 6);
            buffer[13] = No;
            buffer[14] = Start;
            buffer[15] = End;
            Array.Copy(Sha1, 0, buffer, 16, 20);
            buffer[buffer.Length - 2] = CS;
            buffer[buffer.Length - 1] = EndFlag;

            return buffer;
        }
        /// <summary>
        /// 根据字节流构建实体类
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static CancelTaskTx Create(byte[] bytes)
        {
            var obj = new CancelTaskTx
            {
                HeaderStart = bytes[0],
                HeaderLen1 = bytes[1],
                HeaderLen2 = bytes[2],
                HeaderEnd = bytes[3],
                C = bytes[4],
                A = bytes[5],
                CI = bytes[6],
                No = bytes[13],
                Start = bytes[14],
                End = bytes[15],
                CS = bytes[bytes.Length - 2],
                EndFlag = bytes[bytes.Length - 1]
            };

            for (var i = 0; i < 6; i++) obj.DATE[i] = bytes[i + 7];
            for (var i = 0; i < 20; i++) obj.Sha1[i] = bytes[i + 16];

            return obj;
        }
    }

    public class CancelTaskRx
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 29;
        public byte HeaderLen2 = 29;
        public byte HeaderEnd = 0x68;
        public byte C = 6;  // 命令号
        public byte A = 8;  // 机器人编号
        public byte CI = 1; // 附加控制命令
        public byte[] DATE = new byte[6];
        public byte[] Sha1 = new byte[20];
        public byte CS = 0;
        public byte EndFlag = 0x16;
        /// <summary>
        /// 获取字节流
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 6 + 20 + (1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            buffer[6] = CI;
            Array.Copy(DATE, 0, buffer, 7, 6);
            Array.Copy(Sha1, 0, buffer, 13, 20);
            buffer[buffer.Length - 2] = CS;
            buffer[buffer.Length - 1] = EndFlag;

            return buffer;
        }
        /// <summary>
        /// 根据字节流构建实体类
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static CancelTaskRx Create(byte[] bytes)
        {
            var obj = new CancelTaskRx
            {
                HeaderStart = bytes[0],
                HeaderLen1 = bytes[1],
                HeaderLen2 = bytes[2],
                HeaderEnd = bytes[3],
                C = bytes[4],
                A = bytes[5],
                CI = bytes[6],
                CS = bytes[bytes.Length - 2],
                EndFlag = bytes[bytes.Length - 1]
            };

            for (var i = 0; i < 6; i++) obj.DATE[i] = bytes[i + 7];
            for (var i = 0; i < 20; i++) obj.Sha1[i] = bytes[i + 13];

            return obj;
        }
    }
}
