using Domain.Entities;

namespace Domain.Interfaces
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        IEnumerable<Product> GetProductsByCategoryId(int categoryId);
        IEnumerable<Product> GetTopSellingProducts(int count);
    }
}
