
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using backend.common.Models;
using backend.common.Extensions;

namespace backend.service.UnitOfWork
{

    public interface IRepository<T> where T : class
    {
        Task<T> SingleOrDefaultAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false);

        Task<IList<T>> GetAllAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false,
            CancellationToken cancellationToken = default);

        Task<int> GetCountAsync(
            Expression<Func<T, bool>> predicate = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false);

        Task<PagedResult<T>> GetPagedResultAsync(
            SearchRequestModel request,
            bool enableTracking = true,
            bool ignoreQueryFilters = false,
            CancellationToken cancellationToken = default);

        IQueryable<T> AsQueryable(bool enableTracking = true);

        ValueTask<EntityEntry<T>> InsertAsync(T entity,
            CancellationToken cancellationToken = default);

        Task InsertAsync(params T[] entities);

        Task InsertAsync(IEnumerable<T> entities,
            CancellationToken cancellationToken = default);

        Task InsertRangeAsync(IEnumerable<T> entities);

        EntityEntry<T> Update(T entity);
        void Update(T[] entities);
        void Update(IEnumerable<T> entities);

        void Delete(T entity);
        void Delete(params T[] entities);
        void Delete(IEnumerable<T> entities);
    }

    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbSet<T> _dbSet;

        public Repository(DbContext dbContext)
        {
            _dbSet = dbContext.Set<T>();
        }

        #region Get

        public virtual async Task<T> SingleOrDefaultAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false)
        {
            IQueryable<T> query = _dbSet;

            if (!enableTracking) query = query.AsNoTracking();
            if (include != null) query = include(query);
            if (predicate != null) query = query.Where(predicate);
            if (ignoreQueryFilters) query = query.IgnoreQueryFilters();
            if (orderBy != null) return await orderBy(query).FirstOrDefaultAsync();

            return await query.FirstOrDefaultAsync();
        }

        public virtual async Task<IList<T>> GetAllAsync(
            Expression<Func<T, bool>> predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null,
            Func<IQueryable<T>, IIncludableQueryable<T, object>> include = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false,
            CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = _dbSet;

            if (!enableTracking) query = query.AsNoTracking();
            if (include != null) query = include(query);
            if (predicate != null) query = query.Where(predicate);
            if (ignoreQueryFilters) query = query.IgnoreQueryFilters();

            if (orderBy != null) return await orderBy(query).ToListAsync(cancellationToken);

            return await query.ToListAsync(cancellationToken);
        }

        public virtual async Task<PagedResult<T>> GetPagedResultAsync(
            SearchRequestModel request,
            bool enableTracking = true,
            bool ignoreQueryFilters = false,
            CancellationToken cancellationToken = default)
        {
            IQueryable<T> query = _dbSet;

            if (!enableTracking) query = query.AsNoTracking();
            if (ignoreQueryFilters) query = query.IgnoreQueryFilters();

            // Generic Search (can be refined if T has specific properties, but here we use QueryExtensions)
            query = backend.common.Extensions.QueryExtensions.ApplyFilters(query, request.Filters);
            query = backend.common.Extensions.QueryExtensions.ApplySorting(query, request.SortBy, request.SortOrder);

            return await backend.common.Extensions.QueryExtensions.ToPagedResultAsync(query, request.Page, request.PageSize);
        }

        public virtual IQueryable<T> AsQueryable(bool enableTracking = true)
        {
            IQueryable<T> query = _dbSet;
            if (!enableTracking) query = query.AsNoTracking();
            return query;
        }

        public virtual async Task<int> GetCountAsync(
            Expression<Func<T, bool>> predicate = null,
            bool enableTracking = true,
            bool ignoreQueryFilters = false)
        {
            IQueryable<T> query = _dbSet;

            if (!enableTracking) query = query.AsNoTracking();
            if (predicate != null) query = query.Where(predicate);
            if (ignoreQueryFilters) query = query.IgnoreQueryFilters();

            return await query.CountAsync();
        }

        #endregion

        #region Insert

        public virtual ValueTask<EntityEntry<T>> InsertAsync(T entity,
            CancellationToken cancellationToken = default)
        {
            return _dbSet.AddAsync(entity, cancellationToken);
        }

        public virtual Task InsertAsync(params T[] entities)
        {
            return _dbSet.AddRangeAsync(entities);
        }

        public virtual Task InsertAsync(IEnumerable<T> entities,
            CancellationToken cancellationToken = default)
        {
            return _dbSet.AddRangeAsync(entities, cancellationToken);
        }

        public virtual Task InsertRangeAsync(IEnumerable<T> entities)
        {
            return _dbSet.AddRangeAsync(entities);
        }

        #endregion

        #region Update

        public virtual EntityEntry<T> Update(T entity)
        {
            return _dbSet.Update(entity);
        }

        public virtual void Update(T[] entities)
        {
            _dbSet.UpdateRange(entities);
        }

        public virtual void Update(IEnumerable<T> entities)
        {
            _dbSet.UpdateRange(entities);
        }

        #endregion

        #region Delete

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        public void Delete(params T[] entities)
        {
            _dbSet.RemoveRange(entities);
        }

        public void Delete(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }

        #endregion
    }
}
