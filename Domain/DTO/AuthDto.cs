using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTO
{
    public class AuthDto
    {
        [JsonRequired]
        [EmailAddress]
        [JsonProperty("Email")]
        public string Email { get; set; }

        [JsonRequired]
        [JsonProperty("Password")]
        [MinLength(4)]
        public string Password { get; set; }

        [JsonRequired]               
        public string Fingerprint { get; set; }
    }
}
