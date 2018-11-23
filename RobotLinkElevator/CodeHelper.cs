using System;
using System.Collections.Generic;
using System.Text;

namespace RobotLinkElevator
{
    public class CodeHelper
    {
        /// <summary>
        /// 将十进制码转为BCD码
        /// </summary>
        /// <param name="val">十进制码</param>
        /// <returns></returns>
        public static int DecToBcd(int val)
        {
            return ((val / 10 * 16) + (val % 10));
        }

        /// <summary>
        /// 将BCD码转为十进制码
        /// </summary>
        /// <param name="data">BCD码</param>
        /// <returns></returns>
        static byte BCDToByte(byte data)
        {
            int result = 0;
            result += (10 * (data >> 4));
            result += data & 0xf;

            return Convert.ToByte(result);
        }

        static byte[] BCDToByte(byte[] data)
        {
            var rst = new byte[data.Length];
            for(var i = 0; i < data.Length; i++)
            {
                rst[i] = Convert.ToByte(data[i]);
            }

            return rst;
        }

        static byte TransFloorCode(string floorName)
        {
            switch (floorName)
            {
                case "B1": return 0x3e;
                case "B2": return 0x3f;
                case "B3": return 0x40;
                case "B4": return 0x41;
                default:return (byte)Convert.ToInt16(floorName);
            }
        }

        /// <summary>
        /// 8421码的字符串转为byte[]
        /// 例如："180921" => {0x18, 0x09, 0x21}
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] ConvertTo8421Bytes(string str)
        {
            if (str.Length % 2 != 0)
            {
                return null;
            }
            var b = new byte[str.Length / 2];
            var j = 0;
            for (var i = 0; i < str.Length; i += 2, j++)
            {
                var s = str.Substring(j * 2, 2);
                try
                {
                    b[j] = Convert.ToByte(s, 16);
                }
                catch(Exception exx)
                {
                    Console.WriteLine(exx.Message);
                }
            }
            Array.Reverse(b);

            //Action<byte, byte> trans = (a1, a2) =>
            //{
            //    var c = a1;
            //    a1 = a2;
            //    a2 = c;
            //};


            //trans(b[0], b[9]);
            //trans(b[1], b[8]);
            //trans(b[2], b[7]);
            //trans(b[3], b[6]);
            //trans(b[4], b[5]);

            // 转会原型
            //for (var i = 0; i < b.Length; i++)
            //{
            //    Console.Write(Convert.ToString(b[i], 16).PadLeft(2, '0') + " ");
            //}
            return b;
        }
        
    }
}
