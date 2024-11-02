using Domain.DataTypes;
using Domain.DTO;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.EFCore.Repositories.Service
{
    public class TokensService : ITokensService
    {
        private readonly ILogger<TokensService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        public TokensService(ILogger<TokensService> logger, IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }
        #region Public Methods
        public async Task SaveRefreshTokenAsync(
            Guid userId,
            string refreshToken,
            UserAgentData userAgentData, string DeviceFingerprint)
        {
            var userToken = await _unitOfWork.Tokens
                                             .GetTokenByUserAndUserAgentAsync(userId, userAgentData, DeviceFingerprint);

            if (userToken is not null)
            {
                var now = DateTime.UtcNow;

                userToken.RefreshToken = refreshToken;
                userToken.Created = now;
                userToken.Expired = now.AddDays(7);//AddMinutes(userToken.LifeTime)

                await _unitOfWork.CompleteAsync();
            }
            else
            {
                await _unitOfWork.Tokens.AddTokenAsync(userId, refreshToken, userAgentData.OS, userAgentData.Browser, DeviceFingerprint);
                await _unitOfWork.CompleteAsync();
            }
        }
        public TokensData GenerateTokens(UserDto user)
        {
            _logger.LogInformation("Generate refresh token");
            var refreshToken = GenerateRefreshToken();

            _logger.LogInformation("Generate access token");
            var accessJwt = GenerateAccessToken(user);

            return new TokensData { AccessJwt = accessJwt, RefreshJwt = refreshToken };
        }
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(randomNumber);
            }

            string base64Token = Convert.ToBase64String(randomNumber);

            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(base64Token));
                string hashedToken = Convert.ToBase64String(hashBytes);
                return hashedToken;
            }
        }
        public async Task<string> RemoveRefreshTokenAsync(string refreshToken)
        {
            var tokenData = await _unitOfWork.Tokens.FindAsync(t => t.RefreshToken.Equals(refreshToken));
            var token = tokenData.FirstOrDefault();
            if (token == null)
            {
                _logger.LogError("In database refresh token is not found!");
                throw new Exception("In database refresh token is not found!");
            }

            await _unitOfWork.Tokens.RemoveAsync(token);
            await _unitOfWork.CompleteAsync();

            return refreshToken;
        }
        public async Task RemoveExpiredTokensAsync()
        {
            var tokens = await _unitOfWork.Tokens.GetExpiredTokensAsync();
            if (tokens.Any())
            {
                await _unitOfWork.Tokens.RemoveRangeAsync(tokens);
                await _unitOfWork.CompleteAsync();
            }
        }
        public async Task<UserData?> RefreshAsync(TokensData tokens, UserAgentData userAgentData, string deviceFingerprint)
        {
            if (!await ValidateAccessTokenAsync(tokens.AccessJwt))
            {
                _logger.LogError("Invalid access token!");
                throw new Exception("Invalid access token!");
            }

            if (!await ValidateRefreshTokenAsync(tokens.RefreshJwt))
            {
                _logger.LogError("Invalid refresh token!");
                throw new Exception("Invalid refresh token!");
            }

            var user = await _unitOfWork.Tokens
                                 .GetUserByRefreshTokenAndUserAgentAsync(tokens, userAgentData, deviceFingerprint);

            if (user is null)
            {
                _logger.LogError("In database refresh token is not found!");
                throw new Exception("In database refresh token is not found!");
            }

            var userDto = new UserDto { Id = user.Id, Email = user.Email };        

            return new UserData { UserDto = userDto };
        }

        private string GenerateAccessToken(UserDto user)
        {
            var now = DateTime.UtcNow;
            var expires = now.Add(TimeSpan.FromMinutes(AccessTokenOptions.LIFETIME));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),                              
            };

            var jwt = new JwtSecurityToken(
                issuer: AccessTokenOptions.ISSUER, 
                audience: AccessTokenOptions.AUDIENCE, 
                claims: claims, 
                expires: expires, 
                signingCredentials: new SigningCredentials(AccessTokenOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256) // Chữ ký
            );

            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            return encodedJwt;
        }
        private async Task<bool> ValidateAccessTokenAsync(string accessToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var securityToken = (JwtSecurityToken)tokenHandler.ReadToken(accessToken);
            var claimValue = securityToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            var claimsPrincipal = tokenHandler.ValidateToken(accessToken, GetTokenValidationParameters(), out var validatedToken);

            var claims = claimsPrincipal.Claims.ToList();

            var userIdClaim = claims.FirstOrDefault(c => c.Type.Equals(ClaimTypes.NameIdentifier));
            if (userIdClaim is null)
            {
                return false;
            }

            var userEmailClaim = claims.FirstOrDefault(c => c.Type.Equals(ClaimTypes.Email));
            if (userEmailClaim is null)
            {
                return false;
            }

            var userExist = await _unitOfWork.Users.AnyAsync(Guid.Parse(userIdClaim.Value), userEmailClaim.Value);
            if (!userExist)
            {
                return false;
            }

            return true;
        }       

        public static TokenValidationParameters GetTokenValidationParameters(bool validateLifetime = false) => new()
       {
           ValidateIssuer = true,
           ValidIssuer = AccessTokenOptions.ISSUER,

           ValidateAudience = true,
           ValidAudience = AccessTokenOptions.AUDIENCE,

           ClockSkew = TimeSpan.Zero,
           ValidateLifetime = validateLifetime,

           ValidateIssuerSigningKey = true,
           IssuerSigningKey = AccessTokenOptions.GetSymmetricSecurityKey()
       };
        private async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
        {
            var tokenFind = await _unitOfWork.Tokens.FindAsync(t => t.RefreshToken.Equals(refreshToken));
            var token = tokenFind.FirstOrDefault();
            if (token is null)
            {
                return false;
            }

            if (DateTime.UtcNow >= token.Expired)
            {
                return false;
            }

            return true;
        }
        #endregion
    }
}
