using Domain.DataTypes;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.EFCore.Repositories
{
    public class TokenRepository : GenericRepository<Token>, ITokenRepository
    {
        private readonly ApplicationContext _context;
        public TokenRepository(ApplicationContext context) : base(context)
        {
            _context = context;
        }
        public async Task<Token?> GetTokenByUserAndUserAgentAsync(Guid userId, UserAgentData userAgentData, string DeviceFingerprint)
        {
            // Truy vấn để lấy token dựa vào userId và thông tin UserAgent
            return await _context.Tokens
                                 .Include(t => t.UserAgent) // Đảm bảo rằng UserAgent đã được include
                                 .FirstOrDefaultAsync(t => t.UserID.Equals(userId)
                                                           && t.UserAgent.OS.Equals(userAgentData.OS)
                                                           && t.UserAgent.DeviceFingerprint.Equals(DeviceFingerprint)
                                                           && t.UserAgent.Browser.Equals(userAgentData.Browser));
        }
        public async Task<User?> GetUserByRefreshTokenAndUserAgentAsync(TokensData tokens, UserAgentData userAgentData, string DeviceFingerprint)
        {
            var user = await _context.Tokens
                .Include(t => t.UserAgent)
                                 .Where(t => t.RefreshToken.Equals(tokens.RefreshJwt)
                                             && t.UserAgent.OS.Equals(userAgentData.OS)
                                             && t.UserAgent.DeviceFingerprint.Equals(DeviceFingerprint)
                                             && t.UserAgent.Browser.Equals(userAgentData.Browser))
                                 .Select(t => t.User)
                                 .FirstOrDefaultAsync(); ;

            return user;
        }
        public async Task AddTokenAsync(Guid userId, string refreshToken, string OS, string Browser, string DeviceFingerprint)
        {
            var newToken = new Token
            {
                UserID = userId,
                RefreshToken = refreshToken,                
            };

            await _context.Tokens.AddAsync(newToken);
            await _context.UserAgents.AddAsync(new UserAgent
            {
                Id = newToken.Id, // Dùng Id của token đã thêm
                OS = OS,
                Browser = Browser,
                DeviceFingerprint = DeviceFingerprint
            });
        }
        public async Task<IEnumerable<Token>> GetExpiredTokensAsync()
        {
            return await _context.Tokens
                .Where(t => DateTime.UtcNow >= t.Expired)
                .ToListAsync();
        }
    }
}
