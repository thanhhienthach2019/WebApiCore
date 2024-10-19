using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DataAccess.EFCore.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(ApplicationContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        // Method to get an entity by its ID
        public async Task<T> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        // Method to get all entities
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        // Method to find entities matching a given expression
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> expression)
        {
            return await _dbSet.Where(expression).ToListAsync();
        }

        // Method to add a new entity
        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        // Method to update an existing entity
        public async Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
        }

        // Method to add a range of entities
        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        // Method to remove an entity
        public async Task RemoveAsync(T entity)
        {
            _dbSet.Remove(entity);
        }

        // Method to remove a range of entities
        public async Task RemoveRangeAsync(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        // Method to delete an entity by its ID
        public async Task DeleteAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);  // Find the entity by ID
            if (entity != null)
            {
                _dbSet.Remove(entity);  // Remove the entity if found
                await _context.SaveChangesAsync();  // Save changes to the database
            }
        }
    }
}