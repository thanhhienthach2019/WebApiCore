using Domain.DTO;
using Domain.Entities;
using Domain.Interfaces;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;

        private static Dictionary<string, User> _pendingUsers = new Dictionary<string, User>();
        public UserController(IUnitOfWork unitOfWork, IAuthService authService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _emailService = emailService;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto, [FromHeader(Name = "device-fingerprint")] string deviceFingerprint)
        {
            if (await _unitOfWork.Users.GetByUsernameAsync(registerDto.Username) != null)
                return BadRequest("Username is already taken");
            var emailUser = await _unitOfWork.Users.FindAsync(u => u.Email == registerDto.Email);
            var email = emailUser.FirstOrDefault();
            if (email != null)
                return BadRequest("Email is already in use");

            // Hash the password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = hashedPassword,
                TwoFactorCodeRegister = new Random().Next(100000, 999999).ToString(),
                TwoFactorRegisterExpiryTime = DateTime.Now.AddMinutes(5),
                DeviceFingerprint = "testdevice"
            };

            // Store user temporarily
            _pendingUsers[registerDto.Username] = user;

            // Send two-factor code to the user's email
            await _emailService.SendEmailAsync(user.Email, "Your Two-Factor Authentication Code Register Account",
                $"Your verification code is: {user.TwoFactorCodeRegister}");

            return Ok(new { Message = "Two-factor code register account has been sent. Please verify your email." });
        }
        [HttpPost("verify-2fa-register")]
        public async Task<IActionResult> VerifyTwoFactorRegister([FromBody] VerifyTwoFactorDto verifyDto)
        {
            if (!_pendingUsers.ContainsKey(verifyDto.Username))
                return Unauthorized("User not found or registration has expired");

            var user = _pendingUsers[verifyDto.Username];

            if (!await _authService.ValidateTwoFactorRegisterCodeAsync(user, verifyDto.TwoFactorCode))
                return Unauthorized("Invalid or expired 2FA code");

            // Save the user to the database upon successful 2FA verification
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            // Remove the user from the pending list
            _pendingUsers.Remove(verifyDto.Username);

            return Ok(new { Message = "Registration successful. You can now log in." });
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto, [FromHeader(Name = "device-fingerprint")] string deviceFingerprint)
        {
            int expiresDay = 7;
            int loginExpiryTime = 1;
            var user = await _unitOfWork.Users.GetByUsernameAsync(loginDto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials");
            if (string.IsNullOrEmpty(user.DeviceFingerprint) || user.DeviceFingerprint != deviceFingerprint)
            {
                user.TwoFactorCodeLogin = new Random().Next(100000, 999999).ToString();
                user.TwoFactorLoginExpiryTime = DateTime.Now.AddMinutes(loginExpiryTime);
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.CompleteAsync();

                //await _emailService.SendEmailAsync(user.Email, "Your Two-Factor Authentication Code Login",
                //$"Your verification code is: {user.TwoFactorCodeLogin}");

                //return Ok(new { Message = "Two-factor code login has been sent. Please verify your email." });
                return Ok(new { Message = "Mã xác thực đã được gửi đến email của bạn.", requiresTwoFactor = true, expiryTime = loginExpiryTime });
            }
                // Generate JWT token
            var token = await _authService.GenerateJwtToken(user);
            var refreshToken = await _authService.GenerateRefreshToken(user, expiresDay);

            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(expiresDay)
            });

            return Ok(new { Token = token, RefreshToken = refreshToken });
        }
        [HttpPost("verify-2fa-login")]
        public async Task<IActionResult> VerifyTwoFactorLogin([FromBody] VerifyTwoFactorDto verifyDto, [FromHeader(Name = "device-fingerprint")] string deviceFingerprint)
        {
            int timeExpiresDays = 7;
            var user = await _unitOfWork.Users.GetByUsernameAsync(verifyDto.Username);
            if (user == null)
                return Unauthorized("User not found");

            if (!await _authService.ValidateTwoFactorLoginCodeAsync(user, verifyDto.TwoFactorCode))
                return Unauthorized("Invalid or expired 2FA code");
            
            // Generate JWT token
            var token = await _authService.GenerateJwtToken(user);
            var refreshToken = await _authService.GenerateRefreshToken(user, timeExpiresDays);
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,  // Prevent access via JavaScript
                Secure = true,    // Ensure the cookie is only sent over HTTPS
                SameSite = (SameSiteMode)SameSite.Strict, // Protect against CSRF
                Expires = DateTime.UtcNow.AddDays(timeExpiresDays) // Cookie expiry time
            });
            //Update deviceFingerprint to database
            user.DeviceFingerprint = deviceFingerprint;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CompleteAsync();

            return Ok(new { Token = token, RefreshToken = refreshToken });
        }
        [HttpPost("refreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto request)
        {
            var newAccessToken = await _authService.RefreshToken(request.RefreshToken);

            if (newAccessToken == null)
            {
                return Unauthorized();  
            }

            return Ok(new { accessToken = newAccessToken });  
        }
    }
}
