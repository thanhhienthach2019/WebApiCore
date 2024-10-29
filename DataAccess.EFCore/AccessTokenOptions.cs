using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.EFCore
{
    public class AccessTokenOptions
    {
        public const string ISSUER = "_hien_key/authServer"; 
        public const string AUDIENCE = "_hien_key/authClient"; 
        private const string KEY = "hienKeyToken/k64zY35P6gVRuHpKHkF4uHNs5YI/5EgEN3NNY0tncXU="; 
        public const int LIFETIME = 30; 

        public static SymmetricSecurityKey GetSymmetricSecurityKey() => new SymmetricSecurityKey(Encoding.ASCII.GetBytes(KEY));
    }
}
