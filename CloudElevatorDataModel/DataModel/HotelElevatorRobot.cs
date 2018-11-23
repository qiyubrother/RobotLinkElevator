using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataModule.Model
{
    [Table("HotelElevatorRobot")]
    public class HotelElevatorRobot
    {
        /// <summary>
        /// HotelID
        /// </summary>
        [Required]
        public string HotelId { get; set; }
        /// <summary>
        /// Elevator ID
        /// </summary>
        [Required]
        public string ElevatorId { get; set; }
        /// <summary>
        /// Unique RobotSN
        /// </summary>
        [Required]
        public string UniqueRobotSN { get; set; }
    }
}
