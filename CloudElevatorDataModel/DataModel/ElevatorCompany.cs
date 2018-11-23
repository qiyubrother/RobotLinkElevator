using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace DataModule.Model
{
    [Table("ElevatorCompany")]
    public class ElevatorCompany
    {
        [Required, Key]
        public string ElevatorCompanyId { get; set; }

        [Required]
        public string Company { get; set; }
    }
}
