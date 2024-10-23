using Domain.DataTypes;
using Domain.DTO;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DataAccess.EFCore.Repositories.Service
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TokensService _tokensService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, IUnitOfWork unitOfWork, ILogger<AuthService> logger, TokensService tokensService)
        {
            _configuration = configuration;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _tokensService = tokensService;
        }

        // Method to generate JWT token for a user
        public async Task<string> GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
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
                expires: DateTime.Now.AddMinutes(1),  // Token expires in 5 minutes
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
        public async Task<UserData> LoginAsync(AuthDto request, UserAgentData userAgentData)
        {
            var getUser = await _unitOfWork.Users.FindAsync(u => u.Email.Equals(request.Email));
            var user = getUser.FirstOrDefault();
            if (user is null)
            {
                _logger.LogError("User with such email is not found!");
                throw new Exception("User with such email is not found!");
            }

            if (!user.PasswordHash.Equals(Utilities.Hash(request.Password)))
            {
                _logger.LogError("Invalid password!");
                throw new Exception("Invalid password!");
            }

            var userDto = new UserDto { Id = user.Id, Email = user.Email };

            _logger.LogInformation("Generate tokens");
            var tokens = _tokensService.GenerateTokens(userDto);

            _logger.LogInformation("Save refresh token");
            await _tokensService.SaveRefreshTokenAsync(user.Id, tokens.RefreshJwt, userAgentData);

            var userData = new UserData { UserDto = userDto, TokensData = tokens };

            return userData;
        }
        public async Task<string> LogoutAsync(string refreshToken)
        {
            _logger.LogInformation("Remove refresh token");
            var removedRefreshToken = await _tokensService.RemoveRefreshTokenAsync(refreshToken);
            return removedRefreshToken;
        }
        public async Task<UserData> RegistrationAsync(AuthDto request, UserAgentData userAgentData)
        {
            var userData = await _unitOfWork.Users.FindAsync(u => u.Email.Equals(request.Email));
            var user = userData.FirstOrDefault();
            if (user != null)
            {
                _logger.LogError("Error register, user with such an email already exists!");
                throw new Exception("Error register, user with such an email already exists!");
            }
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ArgumentException("Password cannot be null or empty.", nameof(request.Password));
            }
            var hashPassword = Utilities.Hash(request.Password);

            if (hashPassword == null)
            {
                throw new InvalidOperationException("Password hash cannot be null.");
            }

            _logger.LogInformation("Add User data");
            var userEntry = new User
            {
                Email = request.Email,
                PasswordHash = hashPassword
            };
            await _unitOfWork.Users.AddAsync(userEntry);

            await _unitOfWork.CompleteAsync();

            var addedUserId = userEntry.Id;
            var userDto = new UserDto { Id = addedUserId, Email = request.Email };

            _logger.LogInformation("Generate tokens");
            var tokens = _tokensService.GenerateTokens(userDto);

            _logger.LogInformation("Save refresh token");
            await _tokensService.SaveRefreshTokenAsync(addedUserId, tokens.RefreshJwt, userAgentData);

            var userDataEntitiest = new UserData { UserDto = userDto, TokensData = tokens };

            return userDataEntitiest;
        }
    }
}