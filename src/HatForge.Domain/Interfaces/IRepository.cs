using System.Linq.Expressions;

namespace HatForge.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetByIdAsync(int id, string[] includes);
    Task<IReadOnlyList<T>> ListAllAsync();
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, string[]? includes = null);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, string[]? includes = null);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}
