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
            UserAgentData userAgentData)
        {
            var userToken = await _unitOfWork.Tokens
                                             .GetTokenByUserAndUserAgentAsync(userId, userAgentData);

            if (userToken is not null)
            {
                var now = DateTime.UtcNow;

                userToken.RefreshToken = refreshToken;
                userToken.Created = now;
                userToken.Expired = now.AddMinutes(userToken.LifeTime);

                await _unitOfWork.CompleteAsync();
            }
            else
            {
                await _unitOfWork.Tokens.AddTokenAsync(userId, refreshToken, userAgentData.OS, userAgentData.Browser);
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
            var randomNumber = new byte[32];

            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
        public async Task<string> RemoveRefreshTokenAsync(string refreshToken)
        {
            var tokenData = await _unitOfWork.Tokens.FindAsync(t => t.RefreshToken.Equals(refreshToken));
            var token = tokenData.FirstOrDefault();
            if (token == null)
            {
                _logger.LogError("In database refresh token is not found!");
                throw new Exception("Не удалось найти рефреш-токен в базе данных!");
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
        public async Task<UserData?> RefreshAsync(
            TokensData tokens,
            UserAgentData userAgentData)
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
                                 .GetUserByRefreshTokenAndUserAgentAsync(tokens, userAgentData);

            if (user is null)
            {
                _logger.LogError("In database refresh token is not found!");
                throw new Exception("In database refresh token is not found!");
            }

            var userDto = new UserDto { Id = user.Id, Email = user.Email };

            _logger.LogInformation("Generate tokens");
            var tokensDto = GenerateTokens(userDto);

            return new UserData { UserDto = userDto, TokensData = tokensDto };
        }

        private string GenerateAccessToken(UserDto user)
        {
            var now = DateTime.UtcNow;
            var expires = now.Add(TimeSpan.FromSeconds(AccessTokenOptions.LIFETIME));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email)
            };

            var jwt = new JwtSecurityToken(AccessTokenOptions.ISSUER,
                AccessTokenOptions.AUDIENCE,
                claims,
                expires: expires,
                signingCredentials: new SigningCredentials(AccessTokenOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256));

            var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

            return encodedJwt;
        }
        private async Task<bool> ValidateAccessTokenAsync(string accessToken)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var claimsPrincipal = tokenHandler.ValidateToken(accessToken, GetTokenValidationParameters(), out var _);

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

            var userExist = await _unitOfWork.Users.AnyAsync(new Guid(userIdClaim.Value), userEmailClaim.Value);
            if (!userExist)
            {
                return false;
            }

            return true;
        }
        public static TokenValidationParameters GetTokenValidationParameters(bool validateLifetime = false) =>
       new()
       {
           ValidateIssuer = true,
           ValidIssuer = AccessTokenOptions.ISSUER,

           ValidateAudience = true,
           ValidAudience = AccessTokenOptions.AUDIENCE,

           ClockSkew = TimeSpan.Zero,

           ValidateLifetime = validateLifetime,
           IssuerSigningKey = AccessTokenOptions.GetSymmetricSecurityKey(),
           ValidateIssuerSigningKey = true
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
