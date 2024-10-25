using DataAccess.EFCore;
using DataAccess.EFCore.Repositories;
using DataAccess.EFCore.Repositories.Service;
using Domain.DataTypes;
using Domain.DTO;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly TokensService _tokensService;
        private readonly ILogger<AuthController> _logger;
        private readonly string refreshTokenKey;
        private static Dictionary<string, User> _pendingUsers = new Dictionary<string, User>();
        public AuthController(IUnitOfWork unitOfWork, IAuthService authService, ILogger<AuthController> logger, IConfiguration configuration, TokensService tokensService)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _logger = logger;
            _tokensService = tokensService;
            var refreshTokenSection = configuration.GetSection("CookiesNames").GetChildren()
                                            .FirstOrDefault(c => c.Key.Equals("RefreshToken"));
            if (refreshTokenSection != null)
            {
                refreshTokenKey = refreshTokenSection.Value;

                // Kiểm tra giá trị
                if (string.IsNullOrEmpty(refreshTokenKey))
                {
                    _logger.LogWarning("RefreshToken key found but its value is null or empty.");
                }
            }
            else
            {
                refreshTokenKey = string.Empty; // Hoặc một giá trị mặc định nào đó
                _logger.LogWarning("RefreshToken key not found in configuration.");
            }
        }
        [AllowAnonymous]
        [HttpPost(nameof(Login))]
        public async Task<ActionResult<UserData>> Login([FromBody] AuthDto request)
        {
            try
            {
                int loginExpiryTime = 5;
                var userAgentData = Utilities.GetUserAgentData(this.Request.Headers["User-Agent"]);

                _logger.LogInformation("Start login");
                var userData = await _authService.LoginAsync(request, userAgentData);

                if (string.IsNullOrEmpty(userData.DeviceFingerprint) || userData.DeviceFingerprint != request.Fingerprint)
                {
                    var user = new User
                    {
                        TwoFactorCodeLogin = new Random().Next(100000, 999999).ToString(),
                        TwoFactorLoginExpiryTime = DateTime.Now.AddMinutes(loginExpiryTime)
                    };                                        
                    await _unitOfWork.Users.UpdateAsync(user);
                    await _unitOfWork.CompleteAsync();
                    return this.Ok(new { userDataSend = userData, requiresTwoFactor = true, expiryTime = loginExpiryTime });
                }

                _logger.LogInformation("Add refresh token cookie");
                this.AddRefreshTokenCookie(new Token(), userData);

                return this.Ok(new { userDataSend = userData});
            }
            catch (Exception e)
            {
                return this.BadRequest($"Error:{e.Message}");
            }
        }
        [AllowAnonymous]
        [HttpPost("verify-2fa-login")]
        public async Task<ActionResult<UserData>> VerifyTwoFactorLogin([FromBody] VerifyTwoFactorDto request, [FromHeader(Name = "device-fingerprint")] string deviceFingerprint)
        {
            try
            {                
                var userAgentData = Utilities.GetUserAgentData(this.Request.Headers["User-Agent"]);
                if (userAgentData == null)
                    return Unauthorized("User not found");

                _logger.LogInformation("Start VerifyTwoFactorLogin");
                var twoFactor = await _authService.ValidateTwoFactorLoginCodeAsync(request, userAgentData);
                                              
                _logger.LogInformation("Add refresh token cookie");
                this.AddRefreshTokenCookie(new Token(), twoFactor);
                var user = new User
                {
                    DeviceFingerprint = deviceFingerprint,
                };                
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.CompleteAsync();

                return Ok(new { StatusLogin = true });
            }
            catch (Exception e)
            {
                return this.BadRequest($"Error:{e.Message}");
            }
        }
        private void AddRefreshTokenCookie(Token refreshToken, UserData userData)
        {
            var cookieOptions = new CookieOptions
            {
                Expires = refreshToken.Expired,
                MaxAge = TimeSpan.FromMinutes(refreshToken.LifeTime)
            };

            this.Response.Cookies.Append(this.refreshTokenKey, userData.TokensData.RefreshJwt, cookieOptions);
        }
        [AllowAnonymous]
        [HttpPost(nameof(Registration))]
        public async Task<ActionResult<UserData>> Registration([FromBody] AuthDto request)
        {
            try
            {
                var userAgentData = Utilities.GetUserAgentData(this.Request.Headers["User-Agent"]);

                _logger.LogInformation("Start registration");
                var userData = await _authService.RegistrationAsync(request, userAgentData);

                _logger.LogInformation("Add refresh token cookie");
                this.AddRefreshTokenCookie(new Token(), userData);

                return this.Ok(userData);
            }
            catch (Exception e)
            {
                return this.BadRequest($"Error:{e.Message}");
            }
        }
        [AllowAnonymous]
        [HttpPut(nameof(Refresh))]
        public async Task<ActionResult<UserData>> Refresh([FromBody] string accessToken)
        {
            try
            {
                var cookieIsExist = this.Request.Cookies.TryGetValue(this.refreshTokenKey, out var refreshTokenValue);
                if (!cookieIsExist)
                {
                    _logger.LogError("Refresh token cookie is not found!");
                    throw new Exception("Куки рефреш токена отсутствуют!");
                }

                var userAgentData = Utilities.GetUserAgentData(this.Request.Headers["User-Agent"]);

                var tokens = new TokensData { AccessJwt = accessToken, RefreshJwt = refreshTokenValue! };

                _logger.LogInformation("Start refresh");
                var userData = await _tokensService.RefreshAsync(tokens, userAgentData);

                _logger.LogInformation("Save refresh token");
                await _tokensService.SaveRefreshTokenAsync(userData!.UserDto.Id, userData.TokensData.RefreshJwt, userAgentData);

                _logger.LogInformation("Add refresh token cookie");
                this.AddRefreshTokenCookie(new Token(), userData);

                return this.Ok(userData);
            }
            catch
            {
                return this.Unauthorized("Unable to authorize");
            }
        }
        [Authorize]
        [HttpDelete(nameof(Logout))]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var cookieExist = this.Request.Cookies.TryGetValue(this.refreshTokenKey, out var refreshTokenValue);
                if (!cookieExist)
                {
                    _logger.LogError("Refresh token cookie is not found!");
                    throw new Exception("Refresh token cookie is not found!");
                }

                _logger.LogInformation("Start logout");
                await _authService.LogoutAsync(refreshTokenValue!);

                _logger.LogInformation("Delete Refresh token cookie");
                this.Response.Cookies.Delete(this.refreshTokenKey);

                return this.Ok();
            }
            catch
            {
                return this.BadRequest("Error logging out of the account!");
            }
        }
    }
}
