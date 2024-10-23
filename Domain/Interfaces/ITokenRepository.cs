using Domain.DataTypes;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces
{
    public interface ITokenRepository : IGenericRepository<Token>
    {
        Task<Token?> GetTokenByUserAndUserAgentAsync(Guid userId, UserAgentData userAgentData);
        Task<User?> GetUserByRefreshTokenAndUserAgentAsync(TokensData tokens, UserAgentData userAgentData);
        Task AddTokenAsync(Guid userId, string refreshToken, string OS, string Browser);
        Task<IEnumerable<Token>> GetExpiredTokensAsync();
    }
}
