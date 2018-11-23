using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator
{
    /// <summary>
    /// 发送电梯ID
    /// </summary>
    public class SendID
    {
        public byte HeaderStart = 0x68;
        public byte HeaderLen1 = 13;
        public byte HeaderLen2 = 13;
        public byte HeaderEnd = 0x68;
        public byte C = 1;  // 命令号
        public byte A = 8;  
        public byte CI = 0; // 附加控制命令
        public byte[] DATA = new byte[10];
        public byte CS = 0;
        public byte End = 0x16;

        /// <summary>
        /// 获取字节流
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteStream()
        {
            var buffer = new byte[(4 + 1 + 1 + 1) + 10 + (1 + 1)];
            buffer[0] = HeaderStart;
            buffer[1] = HeaderLen1;
            buffer[2] = HeaderLen2;
            buffer[3] = HeaderEnd;

            buffer[4] = C;
            buffer[5] = A;
            Array.Copy(DATA, 0, buffer, 7, 10);
            buffer[17] = CS;
            buffer[18] = End;

            return buffer;
        }

        /// <summary>
        /// 根据字节流构建实体类
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static SendID Create(byte[] bytes)
        {
            var obj = new SendID
            {
                HeaderStart = bytes[0],
                HeaderLen1 = bytes[1],
                HeaderLen2 = bytes[2],
                HeaderEnd = bytes[3],
                C = bytes[4],
                A = bytes[5],
                CI = bytes[6],
                CS = bytes[bytes.Length - 2],
                End = bytes[bytes.Length - 1]
            };
            Array.Copy(bytes, 7, obj.DATA, 0, 6);
            return obj;
        }

        public void SetCS()
        {
            var data = new byte[3 + 10];
            data[0] = C;
            data[1] = A;
            data[2] = CI;
            for(var i = 3; i< data.Length; i++)
            {
                data[i] = DATA[i - 3];
            }
            CS = GetCS(GetByteStream());
        }
        private byte GetCS(byte[] data)
        {
            byte cs = 0x00;
            int i = 0;
            while (i < data[1])
            {
                cs += data[i + 4];
                i++;
            }
            return cs;
        }
    }
}
