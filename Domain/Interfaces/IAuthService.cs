using Domain.DataTypes;
using Domain.DTO;
using Domain.Entities;

namespace Domain.Interfaces
{
    public interface IAuthService
    {
        Task<string> GenerateJwtToken(User user);
        Task<bool> ValidateTwoFactorRegisterCodeAsync(User user, string code);
        Task<bool> ValidateTwoFactorLoginCodeAsync(User user, string code);
        Task<string> RefreshToken(string refreshToken);
        Task<string> GenerateRefreshToken(User user, int timeExpires);
        Task<UserData> LoginAsync(AuthDto request, UserAgentData userAgentData);
        Task<UserData> RegistrationAsync(AuthDto request, UserAgentData userAgentData);
        Task<string> LogoutAsync(string refreshToken);
    }
}
