using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataModule.Model
{
    [Table("HotelElevator")]
    public class HotelElevator
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
    }
}
