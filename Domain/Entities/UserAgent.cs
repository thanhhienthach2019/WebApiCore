using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class UserAgent
    {
        [Required]
        public Guid Id { get; set; }

        [ForeignKey("Id")]
        public Token Token { get; set; }

        [Required]
        public string OS { get; set; }

        [Required]
        public string Browser { get; set; }
        [Required]
        public string DeviceFingerprint { get; set; }
    }
}
