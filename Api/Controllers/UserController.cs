using Domain.DTO;
using Domain.Entities;
using Domain.Interfaces;
using Flurl.Http;
using Microsoft.AspNetCore.Authorization;
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
        private readonly ILogger<UserController> _logger;

        private static Dictionary<string, User> _pendingUsers = new Dictionary<string, User>();
        public UserController(IUnitOfWork unitOfWork, IAuthService authService, IEmailService emailService, ILogger<UserController> logger)
        {
            _unitOfWork = unitOfWork;
            _authService = authService;
            _emailService = emailService;
            _logger = logger;
        }
        [Authorize]
        [HttpGet(nameof(GetUsers))]
        public async Task<ActionResult<List<UserDto>>> GetUsers()
        {
            try
            {
                _logger.LogInformation("Start GetUsers");
                var users = await _unitOfWork.Users.GetAllAsync();
                if (!users.Any())
                {
                    _logger.LogWarning("Users are not present in the database!");
                }

                return this.Ok(users);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed GetUsers:{e}");
                return this.BadRequest($"Error retrieving the list of all users:{e.Message}");
            }
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
            if (!_pendingUsers.ContainsKey(verifyDto.Email))
                return Unauthorized("User not found or registration has expired");

            var user = _pendingUsers[verifyDto.Email];

            if (!await _authService.ValidateTwoFactorRegisterCodeAsync(user, verifyDto.TwoFactorCode))
                return Unauthorized("Invalid or expired 2FA code");

            // Save the user to the database upon successful 2FA verification
            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CompleteAsync();

            // Remove the user from the pending list
            _pendingUsers.Remove(verifyDto.Email);

            return Ok(new { Message = "Registration successful. You can now log in." });
        }        
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            Response.Cookies.Delete("jwtToken");
            Response.Cookies.Delete("refreshToken");
            return Ok(new { Message = "Đăng xuất thành công." });
        }                
    }
}
