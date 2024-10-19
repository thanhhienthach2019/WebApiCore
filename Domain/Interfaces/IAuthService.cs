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
    }
}
