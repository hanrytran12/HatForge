using System.Linq.Expressions;
using HatForge.Domain.Interfaces;
using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HatForge.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    private readonly DbSet<T> _set;

    public Repository(AppDbContext context)
    {
        Context = context;
        _set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

    public async Task<T?> GetByIdAsync(int id, string[] includes)
    {
        var query = includes.Aggregate(_set.AsQueryable(), (q, inc) => q.Include(inc));
        return await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
    }

    public async Task<IReadOnlyList<T>> ListAllAsync() => await _set.ToListAsync();

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, string[]? includes = null)
    {
        var query = _set.AsQueryable();
        if (includes != null)
            query = includes.Aggregate(query, (q, inc) => q.Include(inc));
        return await query.Where(predicate).ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, string[]? includes = null)
    {
        var query = _set.AsQueryable();
        if (includes != null)
            query = includes.Aggregate(query, (q, inc) => q.Include(inc));
        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task AddAsync(T entity) => await _set.AddAsync(entity);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
