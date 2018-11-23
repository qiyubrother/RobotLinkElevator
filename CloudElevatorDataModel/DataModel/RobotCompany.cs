using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataModule.Model
{
    [Table("RobotCompany")]
    public class RobotCompany
    {
        [Required, Key]
        public string RobotCompanyID { get; set; }
        [Required]
        public string Company { get; set; }
        [Required]
        public string CompanyAbbreviation { get; set; }
        [Required]
        public string CompanyTag { get; set; }
    }
}
