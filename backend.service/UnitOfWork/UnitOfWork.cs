using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace backend.service.UnitOfWork
{
    public interface IRepositoryFactory
    {
        IRepository<T> GetRepository<T>() where T : class;
    }

    public interface IUnitOfWork : IDisposable
    {
        IRepository<TEntity> GetRepository<TEntity>() where TEntity : class;
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
        Task<int> CommitAsyncWithTransaction(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
        void ClearContext();
        void ClearChangeTracker();
        IDbContextTransaction? DbContextTransaction { get; set; }
    }

    public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
    {
        TContext Context { get; }
    }

    public class UnitOfWork<TContext> : IRepositoryFactory, IUnitOfWork<TContext>
        where TContext : DbContext, IDisposable
    {
        private Dictionary<(Type type, string name), object>? _repositories;

        public IDbContextTransaction? DbContextTransaction { get; set; }

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
        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            DbContextTransaction ??= await Context.Database.BeginTransactionAsync(cancellationToken);
        }

        // Saves changes within the transaction WITHOUT committing.
        // Use this when you need the generated ID before continuing.
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            await Context.SaveChangesAsync(cancellationToken);
            Context.ChangeTracker.Clear();
        }

        // Commits and closes the transaction.
        // Call once after all SaveAsync() steps are done.
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (DbContextTransaction == null)
                    throw new InvalidOperationException("No active transaction. Call BeginTransactionAsync first.");

                await DbContextTransaction.CommitAsync(cancellationToken);
            }
            catch
            {
                if (DbContextTransaction != null)
                    await DbContextTransaction.RollbackAsync(CancellationToken.None);
                throw;
            }
            finally
            {
                if (DbContextTransaction != null)
                {
                    await DbContextTransaction.DisposeAsync();
                    DbContextTransaction = null;
                }
                Context.ChangeTracker.Clear();
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (DbContextTransaction != null)
            {
                try
                {
                    await DbContextTransaction.RollbackAsync(cancellationToken);
                }
                finally
                {
                    await DbContextTransaction.DisposeAsync();
                    DbContextTransaction = null;
                    Context.ChangeTracker.Clear();
                }
            }
        }

        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            var status = await Context.SaveChangesAsync(cancellationToken);
            Context.ChangeTracker.Clear();
            return status;
        }

        // ----------------------------------------------------------------
        // Commit with transaction — SQLite supports this natively
        // ----------------------------------------------------------------
        public async Task<int> CommitAsyncWithTransaction(CancellationToken cancellationToken = default)
        {
            try
            {
                await BeginTransactionAsync(cancellationToken);

                int result = await Context.SaveChangesAsync(cancellationToken);
                if (DbContextTransaction != null)
                    await DbContextTransaction.CommitAsync(cancellationToken);
                Context.ChangeTracker.Clear();
                return result;
            }
            catch (Exception)
            {
                if (DbContextTransaction != null)
                    await DbContextTransaction.RollbackAsync(CancellationToken.None);

                throw;
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

        public void ClearChangeTracker()
        {
            Context.ChangeTracker.Clear();
        }

        public void ClearContext()
        {
            Context.ChangeTracker.Clear();
            if (DbContextTransaction != null)
            {
                DbContextTransaction.Dispose();
                DbContextTransaction = null;
            }
        }

        public void Dispose()
        {
            Context?.Dispose();
        }

        internal object GetOrAddRepository(Type type, object repo)
        {
            _repositories ??= new Dictionary<(Type type, string name), object>();

            var repoName = repo.GetType().FullName ?? repo.GetType().Name;
            if (_repositories.TryGetValue((type, repoName), out var repository))
                return repository;

            _repositories.Add((type, repoName), repo);
            return repo;
        }
    }
}
