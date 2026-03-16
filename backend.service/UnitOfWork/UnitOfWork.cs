using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.service.UnitOfWork
{
    public interface IRepositoryFactory
    {
        IRepository<T> GetRepository<T>() where T : class;
    }

    public interface IUnitOfWork : IDisposable
    {
        IRepository<TEntity> GetRepository<TEntity>() where TEntity : class;
        Task<int> CommitAsync();
        Task<int> CommitAsyncWithTransaction();
        Task BeginTransactionAsync();
        Task SaveAsync();
        Task CommitTransactionAsync();
        void ClearContext();
        IDbContextTransaction DbContextTransaction { get; set; }
    }

    public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
    {
        TContext Context { get; }
    }

    public class UnitOfWork<TContext> : IRepositoryFactory, IUnitOfWork<TContext>
        where TContext : DbContext, IDisposable
    {
        private Dictionary<(Type type, string name), object> _repositories;

        public IDbContextTransaction DbContextTransaction { get; set; }

        public TContext Context { get; }

        public UnitOfWork(TContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IRepository<TEntity> GetRepository<TEntity>() where TEntity : class
        {
            return (IRepository<TEntity>)GetOrAddRepository(
                typeof(TEntity),
                new Repository<TEntity>(Context));
        }

        // ----------------------------------------------------------------
        // Simple commit — no transaction
        // ----------------------------------------------------------------
        public async Task BeginTransactionAsync()
        {
            DbContextTransaction ??= await Context.Database.BeginTransactionAsync();
        }

        // Saves changes within the transaction WITHOUT committing.
        // Use this when you need the generated ID before continuing.
        public async Task SaveAsync()
        {
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
        }

        // Commits and closes the transaction.
        // Call once after all SaveAsync() steps are done.
        public async Task CommitTransactionAsync()
        {
            try
            {
                if (DbContextTransaction == null)
                    throw new InvalidOperationException("No active transaction. Call BeginTransactionAsync first.");

                await DbContextTransaction.CommitAsync();
            }
            catch
            {
                await DbContextTransaction.RollbackAsync();
                throw;
            }
            finally
            {
                await DbContextTransaction.DisposeAsync();
                DbContextTransaction = null;
                Context.ChangeTracker.Clear();
            }
        }

        public async Task<int> CommitAsync()
        {
            var status = await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return status;
        }

        // ----------------------------------------------------------------
        // Commit with transaction — SQLite supports this natively
        // ----------------------------------------------------------------
        public async Task<int> CommitAsyncWithTransaction()
        {
            try
            {
                await BeginTransactionAsync();

                int result = await Context.SaveChangesAsync();
                await DbContextTransaction.CommitAsync();
                Context.ChangeTracker.Clear();
                return result;
            }
            catch (Exception)
            {
                if (DbContextTransaction != null)
                    await DbContextTransaction.RollbackAsync();

                throw;
                //throw new HttpStatusCodeException(
                //    StatusCodes.Status400BadRequest,
                //    "Error in query: " + ex.Message);
            }
            finally
            {
                if (DbContextTransaction != null)
                {
                    await DbContextTransaction.DisposeAsync();
                    DbContextTransaction = null;
                }
            }
        }

        public void ClearContext()
        {
            Context.ChangeTracker.Clear();
            if (DbContextTransaction != null)
                DbContextTransaction = null;
        }

        public void Dispose()
        {
            Context?.Dispose();
        }

        internal object GetOrAddRepository(Type type, object repo)
        {
            _repositories ??= new Dictionary<(Type type, string Name), object>();

            if (_repositories.TryGetValue((type, repo.GetType().FullName), out var repository))
                return repository;

            _repositories.Add((type, repo.GetType().FullName), repo);
            return repo;
        }
    }
}
