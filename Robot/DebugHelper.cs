using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Robot
{
    public class DebugHelper
    {
        public static void PrintTxMessage(string s)
        {
            s = $"[{DateTime.Now}]Tx::{s}";

            Parallel.Invoke(() => {
                var fc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(s);
                Console.ForegroundColor = fc;
            }, () => {
                Log.Information(s);
            });
        }

        public static void PrintRxMessage(string s)
        {
            s = $"[{DateTime.Now}]Rx::{s}";

            Parallel.Invoke(() => {
                var fc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(s);
                Console.ForegroundColor = fc;
            }, () => {
                Log.Information(s);
            });
        }

        public static void PrintDebugMessage(byte[] buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
            {
                sb.Append($"{Convert.ToString(b, 16).PadLeft(2, '0').ToUpper()} ");
            }
            var s = $"[{DateTime.Now}]{sb}";

            Parallel.Invoke(() => {
                var fc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine(s);
                Console.ForegroundColor = fc;
            }, () => {
                Log.Information(s);
            });
        }

        public static void PrintTraceMessage(string s)
        {
            Parallel.Invoke(() => {
                var fc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now}][Trace]::{s}");
                Console.ForegroundColor = fc;
            }, () => {
                Log.Information(s);
            });
        }

        public static void PrintErrorMessage(string s)
        {
            Parallel.Invoke(() => {
                var fc = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now}][Error]::{s}");
                Console.ForegroundColor = fc;
            }, () => {
                Log.Information(s);
            });
        }
    }
}
