using Microsoft.EntityFrameworkCore;
using NsiTransfer.DAL.Db.Context;
using NsiTransfer.DAL.Interfaces.Db;
using System.Linq.Expressions;

namespace NsiTransfer.DAL.Repositories.Db;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _dbSet.FindAsync(new object[] { id }, cancellationToken);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;

        if (include != null) query = include(query);
        if (predicate != null) query = query.Where(predicate);
        if (orderBy != null) query = orderBy(query);

        return await query.ToListAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<TResult>> GetAllAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet.AsNoTracking();

        if (predicate is not null)
            query = query.Where(predicate);

        if (orderBy is not null)
            query = orderBy(query);

        if (take.HasValue)
            query = query.Take(take.Value);

        return await query.Select(selector).ToListAsync(cancellationToken);
    }


    public async Task<TResult?> GetAsync<TResult>(
        Expression<Func<T, TResult>> selector, 
        SingleOrFirst singleOrFirst, 
        Expression<Func<T, bool>>? predicate = null, 
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;

        if (predicate != null) query = query.Where(predicate);
        if (orderBy != null) query = orderBy(query);

        IQueryable<TResult> projectedQuery = query.Select(selector);

        if (singleOrFirst == SingleOrFirst.Single)
        {
            return await projectedQuery.SingleOrDefaultAsync(cancellationToken);
        }
        else if (singleOrFirst == SingleOrFirst.First)
        {
            return await projectedQuery.FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            throw new NotImplementedException($"Недопустимое значение параметра {nameof(singleOrFirst)}");
        }
    }

    public virtual async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;

        if (include != null) query = include(query);
        if (predicate != null) query = query.Where(predicate);
        if (orderBy != null) query = orderBy(query);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<T> FirstAsync(
        Expression<Func<T, bool>>? predicate = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _dbSet;

        if (include != null) query = include(query);
        if (predicate != null) query = query.Where(predicate);
        if (orderBy != null) query = orderBy(query);

        var result = await query.FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            var entityName = typeof(T).Name;
            var conditionInfo = predicate != null ? " по заданному условию" : "";
            throw new InvalidOperationException(
                $"Не удалось найти сущность типа '{entityName}'{conditionInfo}. " +
                $"Убедитесь, что данные существуют в базе данных.");
        }

        return result;
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        => await _dbSet.Where(predicate).ToListAsync(cancellationToken);

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        => await _dbSet.AddRangeAsync(entities, cancellationToken);

    public virtual void Update(T entity) => _dbSet.Update(entity);
    public virtual void UpdateRange(IEnumerable<T> entities) => _dbSet.UpdateRange(entities);
    public virtual void Delete(T entity) => _dbSet.Remove(entity);
    public virtual void DeleteRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => predicate == null ? await _dbSet.AnyAsync(cancellationToken) : await _dbSet.AnyAsync(predicate, cancellationToken);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => predicate == null ? await _dbSet.CountAsync(cancellationToken) : await _dbSet.CountAsync(predicate, cancellationToken);

    public virtual IQueryable<T> Queryable() => _dbSet;
}
