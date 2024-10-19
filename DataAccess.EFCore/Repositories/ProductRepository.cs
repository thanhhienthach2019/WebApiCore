using Domain.DTO;
using Domain.Entities;
using Domain.Interfaces;

namespace DataAccess.EFCore.Repositories
{
    public class ProductRepository : GenericRepository<Product>, IProductRepository
    {
        private readonly ApplicationContext _context;

        public ProductRepository(ApplicationContext context) : base(context)
        {
            _context = context;
        }

        // Method to get products along with their categories
        public IEnumerable<ProductWithCategoryDto> GetProductsWithCategory()
        {
            var result = from p in _context.Products
                         join c in _context.Categories on p.CategoryId equals c.Id
                         select new ProductWithCategoryDto
                         {
                             ProductId = p.Id,
                             ProductName = p.Name,
                             Price = p.Price,
                             Description = p.Description,
                             CategoryName = c.Name
                         };

            return result.ToList();
        }

        // Method to get products by category ID
        public IEnumerable<Product> GetProductsByCategoryId(int categoryId)
        {
            return _context.Products.Where(p => p.CategoryId == categoryId).ToList();
        }

        // Method to get the top-selling products based on the 'Sales' property
        public IEnumerable<Product> GetTopSellingProducts(int count)
        {
            // Assuming there is a 'Sales' property in Product to count the number of items sold
            return _context.Products.OrderByDescending(p => p.Sales).Take(count).ToList();
        }
    }
}