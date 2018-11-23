using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataModule.Model
{
    [Table("ElevatorIdModule")]
    public class ElevatorIdModule
    {
        [Required, Key]
        public string ElevatorId { get; set; }
        [Required]
        public string ElevatorCompanyId { get; set; }
        [Required]
        public string ModuleName { get; set; }

    }
}
