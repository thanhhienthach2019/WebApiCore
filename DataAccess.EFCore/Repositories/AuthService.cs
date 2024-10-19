using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DataAccess.EFCore.Repositories
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;

        public AuthService(IConfiguration configuration, IUnitOfWork unitOfWork)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
        }

        // Method to generate JWT token for a user
        public async Task<string> GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username)
            };

            var keyValue = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(keyValue))
            {
                throw new Exception("Jwt:Key is not configured in appsettings.json.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyValue));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(60),  // Token expires in 5 minutes
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Method to generate a refresh token and save it to the database
        public async Task<string> GenerateRefreshToken(User user, int timeExpires)
        {
            var refreshToken = Guid.NewGuid().ToString();
            var expirationTime = DateTime.Now.AddDays(timeExpires); // Refresh token expires in the specified number of days

            user.RefreshToken = refreshToken;
            user.ExpirationDate = expirationTime;
            user.IsActiveToken = true;

            try
            {
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.CompleteAsync();  // Confirm changes in the database
            }
            catch (Exception ex)
            {
                throw new Exception("Could not save refresh token", ex);
            }

            // Return the new refresh token
            return refreshToken;
        }

        // Method to refresh JWT token using a valid refresh token
        public async Task<string> RefreshToken(string refreshToken)
        {
            var storedUser = await _unitOfWork.Users.FindAsync(u => u.RefreshToken == refreshToken);

            var user = storedUser.FirstOrDefault();

            if (user == null || user.ExpirationDate < DateTime.Now)
            {
                throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
            }

            return await GenerateJwtToken(user);
        }

        // Method to validate the two-factor authentication code
        public async Task<bool> ValidateTwoFactorRegisterCodeAsync(User user, string code)
        {
            return user.TwoFactorCodeRegister == code && user.TwoFactorRegisterExpiryTime > DateTime.Now;
        }
        public async Task<bool> ValidateTwoFactorLoginCodeAsync(User user, string code)
        {
            return user.TwoFactorCodeLogin == code && user.TwoFactorLoginExpiryTime > DateTime.Now;
        }
    }
}