using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator
{
    /// <summary>
    /// 获取服务器时间
    /// </summary>
    public class GetServerDateTimeTx
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 4;
        public byte HeaderLen2 = 4;
        public byte HeaderEnd = 0x68;
        public byte C = 2;  // 命令号
        public byte A = 8;  // 机器人编号
        public byte CI = 0; // 附加控制命令
        public byte[] DATA = new byte[1];
        public byte CS = 0;
        public byte EndFlag = 0x16;

        /// <summary>
        /// 获取字节流
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 1 + (1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            buffer[6] = CI;
            buffer[7] = DATA[0]; // DATA AREA

            buffer[8] = CS;
            buffer[9] = EndFlag;

            return buffer;
        }

        /// <summary>
        /// 根据字节流构建实体类
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static GetServerDateTimeTx Create(byte[] bytes)
        {
            var obj = new GetServerDateTimeTx
            {
                HeaderStart = bytes[0],
                HeaderLen1 = bytes[1],
                HeaderLen2 = bytes[2],
                HeaderEnd = bytes[3],
                C = bytes[4],
                A = bytes[5],
                CI = bytes[6],
                DATA = new byte[1] { bytes[7] },
                CS = bytes[bytes.Length - 2],
                EndFlag = bytes[bytes.Length - 1]
            };
            return obj;
        }
    }

    /// <summary>
    /// 获取服务器时间
    /// </summary>
    public class GetServerDateTimeRx
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 9;
        public byte HeaderLen2 = 9;
        public byte HeaderEnd = 0x68;
        public byte C = 0x82;  // 命令号
        public byte A = 8;  // 机器人编号
        public byte CI = 0; // 附加控制命令
        public byte[] DATE = new byte[6];
        public byte CS = 0;
        public byte EndFlag = 0x16;

        /// <summary>
        /// 获取字节流
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 6 + (1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            buffer[6] = CI;

            Array.Copy(DATE, 0, buffer, 7, 6);

            buffer[13] = CS;
            buffer[14] = EndFlag;

            return buffer;
        }

        /// <summary>
        /// 根据字节流构建实体类
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static GetServerDateTimeRx Create(byte[] bytes)
        {
            var obj = new GetServerDateTimeRx
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

            return obj;
        }
    }
}
