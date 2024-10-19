using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlTypes;

namespace DataAccess.EFCore.Repositories
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        public UserRepository(ApplicationContext context) : base(context)
        {

        }
        public async Task<User> GetByUsernameAsync(string username)
        {
            try
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            }
            catch (SqlNullValueException ex)
            {
                throw new Exception("Null value encountered in the database.", ex);
            }
        }
    }
}
