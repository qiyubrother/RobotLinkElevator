using System;
using System.Collections.Generic;
using System.Text;

namespace RobotLinkElevator
{
    public class ElevatorHelper
    {
        /// <summary>
        /// 字节流转十进制字符串
        /// </summary>
        /// <param name="data">字节流</param>
        /// <returns></returns>
        public static string ByteToDecString(byte[] data)
        {
            string d = "";
            foreach (byte b in data)
            {
                var s = Convert.ToString(b, 10).PadLeft(2, '0');
                d += s + " ";
            }
            // Sample data: "10 11 12 13 14 15 16 17 18 19 20"
            return d.TrimEnd(' ');
        }
        /// <summary>
        /// 字节流转十进制字符串
        /// </summary>
        /// <param name="data">字节流</param>
        /// <returns></returns>
        public static string ByteToHexString(byte[] data)
        {
            string d = "";
            foreach (byte b in data)
            {
                var s = Convert.ToString(b, 16).PadLeft(2, '0');
                d += s + " ";
            }
            // Sample data: "10 11 12 13 14 15 16 17 18 19 20"
            return d.TrimEnd(' ');
        }
        /// <summary>
        /// 字符串转字节流
        /// </summary>
        /// <param name="data">字符串</param>
        /// <returns></returns>
        public static byte[] DecStringToByte(string data)
        {
            var d = data.Trim();
            var arr = d.Split(' ');
            var b = new byte[arr.Length];
            for (var i = 0; i < arr.Length; i++)
            {
                b[i] = Convert.ToByte(arr[i]);
            }
            // Sample data: new byte[] {10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20}
            return b;
        }
    }
}
