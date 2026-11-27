using backend.common.Models;
using backend.model.TenantDB;
using backend.model.TenantDB.Views;

namespace backend.service.Repository.Interface
{
    public interface ITransactionRepository
    {
        Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request, CancellationToken cancellationToken);
        Task<Account?> GetActiveAccountBySidAsync(string accountSID, CancellationToken cancellationToken);
        Task<Transaction?> GetActiveTransactionBySidAsync(string transactionSID, CancellationToken cancellationToken);
        Task<Transaction?> GetActiveTransactionBySidAndAccountIdAsync(string transactionSID, int accountId, CancellationToken cancellationToken);
        Task<Description?> GetActiveDescriptionBySidAsync(string descriptionSID, CancellationToken cancellationToken);
        Task<Description?> GetActiveDescriptionByNameAsync(string descriptionName, CancellationToken cancellationToken);
        Task<Description> InsertDescriptionAsync(Description description, CancellationToken cancellationToken);
        Task<Transaction> InsertTransactionAsync(Transaction transaction, CancellationToken cancellationToken);
        void UpdateTransaction(Transaction transaction);
        Task BeginTransactionAsync(CancellationToken cancellationToken);
        Task CommitTransactionAsync(CancellationToken cancellationToken);
        Task RollbackTransactionAsync(CancellationToken cancellationToken);
        Task SaveAsync(CancellationToken cancellationToken);
        void ClearChangeTracker();
    }
}
