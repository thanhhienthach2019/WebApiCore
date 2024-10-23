using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data.SqlTypes;

namespace DataAccess.EFCore.Repositories
{
    public class UserRepository : GenericRepository<User>, IUserRepository
    {
        private readonly ApplicationContext _context;
        public UserRepository(ApplicationContext context) : base(context)
        {
            _context = context;
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
        public async Task<bool> AnyAsync(Guid userId, string email)
        {
            // Kiểm tra xem có người dùng nào thỏa mãn điều kiện không
            return await _context.Users
                .AnyAsync(u => u.Id.Equals(userId) && u.Email.Equals(email));
        }
    }
}
