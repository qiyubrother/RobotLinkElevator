using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator
{
    public class CSCheckHelper
    {
        public static byte GetCS(byte[] data)
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
