using Domain.DataTypes;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UAParser;

namespace DataAccess.EFCore
{
    public class Utilities
    {
        public static string Hash(string input)
        {
            var md5 = MD5.Create();
            var sha256 = SHA256.Create();

            var inputByte = Encoding.UTF8.GetBytes(input);

            var result = md5.ComputeHash(sha256.ComputeHash(inputByte));

            return Convert.ToBase64String(result);
        }

        public static UserAgentData GetUserAgentData(StringValues userAgentHeader)
        {
            var uaParser = Parser.GetDefault();
            var clietnInfo = uaParser.Parse(userAgentHeader);

            return new UserAgentData
            {
                OS = clietnInfo.OS.ToString(),
                Browser = clietnInfo.UA.ToString()
            };
        }
    }
}
