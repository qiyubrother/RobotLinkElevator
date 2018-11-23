using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataModule.Model;

namespace DataModelTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var dc = new DataContext();
            Console.WriteLine($"RobotCompanys:");
            Console.WriteLine($"----------------------------------------");
            foreach (var rc in dc.RobotCompanys)
            {
                Console.WriteLine($"SN:{rc.RobotCompanyID}, ElevatorCompanyId:{rc.Company}, CompanyAbbreviation:{rc.CompanyAbbreviation}, CompanyTag:{rc.CompanyTag}");
            }
            Console.WriteLine("");
            Console.WriteLine($"ElevatorCompanys:");
            Console.WriteLine($"----------------------------------------");
            foreach (var e in dc.ElevatorCompanys)
            {
                Console.WriteLine($"ElevatorCompanyId:{e.ElevatorCompanyId}, Company:{e.Company}");
            }
            Console.WriteLine("");
            Console.WriteLine($"Hotels:");
            Console.WriteLine($"----------------------------------------");
            foreach (var h in dc.Hotels)
            {
                Console.WriteLine($"HotelId:{h.HotelId}, HotelName:{h.HotelName}, City:{h.City}, Address:{h.Address}, Comments:{h.Comments}");
            }
            Console.WriteLine("");
            Console.WriteLine($"RobotMaps:");
            Console.WriteLine($"----------------------------------------");
            foreach (var m in dc.RobotMaps)
            {
                Console.WriteLine($"RobotID:{m.RobotSN}, UniqueRobotID:{m.UniqueRobotSN}, RobotCompanyId:{m.RobotCompanyId}");
            }
            Console.WriteLine("");
            Console.WriteLine($"ElevatorIdModules:");
            Console.WriteLine($"----------------------------------------");
            foreach (var e in dc.ElevatorIdMudules)
            {
                Console.WriteLine($"ElevatorId:{e.ElevatorId}, ModuleName:{e.ModuleName}, ElevatorCompanyId:{e.ElevatorCompanyId}");
            }
            Console.WriteLine("");
            Console.WriteLine($"HotelElevators:");
            Console.WriteLine($"----------------------------------------");
            foreach (var e in dc.HotelElevators)
            {
                var h = dc.Hotels.FirstOrDefault(x => x.HotelId == e.HotelId);
                if (h != null)
                {
                    Console.WriteLine($"HotelId:{e.HotelId}, Hotel:{h.HotelName}, ElevatorId:{e.ElevatorId}");
                }
            }
            Console.WriteLine($"HotelRobots:");
            Console.WriteLine($"----------------------------------------");
            foreach (var e in dc.HotelRobots)
            {
                var h = dc.Hotels.FirstOrDefault(x => x.HotelId == e.HotelId);
                if (h != null)
                {
                    Console.WriteLine($"HotelId:{e.HotelId}, Hotel:{h.HotelName}, UniqueRobotSN:{e.UniqueRobotSN}");
                }
            }

            Console.ReadKey();
        }
    }
}
