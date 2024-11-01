using Domain.DataTypes;
using Domain.DTO;

namespace Domain.Interfaces
{
    public interface ITokensService
    {
        Task SaveRefreshTokenAsync(Guid userId, string refreshToken, UserAgentData userAgentData, string DeviceFingerprint);
        TokensData GenerateTokens(UserDto user);
        Task<string> RemoveRefreshTokenAsync(string refreshToken);
        Task RemoveExpiredTokensAsync();
        Task<UserData?> RefreshAsync(TokensData tokens, UserAgentData userAgentData, string deviceFingerprint);
    }
}
