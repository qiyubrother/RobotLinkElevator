using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace DataModule.Model
{
    [Table("RobotMap")]
    public class RobotMap
    {
        [Required, Key]
        public string UniqueRobotSN { get; set; }
        [Required]
        public string RobotSN { get; set; }
        [Required]
        public string RobotCompanyId { get; set; }
    }
}
