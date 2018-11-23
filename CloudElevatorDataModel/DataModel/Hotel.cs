using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DataModule.Model
{
    [Table("Hotel")]
    public class Hotel
    {
        [Required, Key]
        public string HotelId { get; set; }
        [Required]
        public string HotelName { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string Comments { get; set; }
    }
}
