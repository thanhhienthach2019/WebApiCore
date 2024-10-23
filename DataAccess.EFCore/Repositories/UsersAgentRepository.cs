using Domain.Entities;
using Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.EFCore.Repositories
{
    public class UsersAgentRepository : GenericRepository<UserAgent>, IUsersAgentRepository
    {
        private readonly ApplicationContext _context;

        public UsersAgentRepository(ApplicationContext context) : base(context)
        {
            _context = context;
        }
    }
}
