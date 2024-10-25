using Domain.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DataTypes
{
    public class UserData
    {
        public UserDto UserDto { get; set; }
        public TokensData TokensData { get; set; }        
        public string? TwoFactorCodeLogin { get; set; }
        public string? DeviceFingerprint { get; set; }
        public DateTime? TwoFactorLoginExpiryTime { get; set; }
    }
}
