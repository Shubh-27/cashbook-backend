using backend.common.Models;
using backend.model.Extensions;
using backend.model.TenantDB;
using backend.model.TenantDB.Views;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using static backend.common.Constants;

namespace backend.service.Repository.Implementations
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        public TransactionRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.GetRepository<VwTransactionsList>().AsQueryable(enableTracking: false);

            if (!string.IsNullOrEmpty(request.Search))
            {
                var search = request.Search.ToLower();
                query = query.Where(x =>
                    (x.Notes != null && x.Notes.ToLower().Contains(search)) ||
                    (x.AccountName != null && x.AccountName.ToLower().Contains(search)) ||
                    (x.DescriptionName != null && x.DescriptionName.ToLower().Contains(search))
                );
            }

            query = query.ApplyFilters(request.Filters);
            query = query.ApplySorting(request.SortBy, request.SortOrder);

            return await query.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
        }

        public Task<Account?> GetActiveAccountBySidAsync(string accountSID, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<Account>()
                .SingleOrDefaultAsync(a => a.AccountSID == accountSID && a.Status == StatusType.Active, cancellationToken: cancellationToken);
        }

        public Task<Transaction?> GetActiveTransactionBySidAsync(string transactionSID, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<Transaction>()
                .SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.Status == StatusType.Active, cancellationToken: cancellationToken);
        }

        public Task<Transaction?> GetActiveTransactionBySidAndAccountIdAsync(string transactionSID, int accountId, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<Transaction>()
                .SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.AccountID == accountId && t.Status == StatusType.Active, cancellationToken: cancellationToken);
        }

        public Task<Description?> GetActiveDescriptionBySidAsync(string descriptionSID, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<Description>()
                .SingleOrDefaultAsync(d => d.DescriptionSID == descriptionSID && d.Status == StatusType.Active, cancellationToken: cancellationToken);
        }

        public Task<Description?> GetActiveDescriptionByNameAsync(string descriptionName, CancellationToken cancellationToken = default)
        {
            var lowerName = descriptionName.ToLower();
            return _unitOfWork.GetRepository<Description>()
                .SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == lowerName && d.Status == StatusType.Active, cancellationToken: cancellationToken);
        }

        public async Task<Description> InsertDescriptionAsync(Description description, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.GetRepository<Description>().InsertAsync(description, cancellationToken);
            return entry.Entity;
        }

        public async Task<Transaction> InsertTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            var entry = await _unitOfWork.GetRepository<Transaction>().InsertAsync(transaction, cancellationToken);
            return entry.Entity;
        }

        public void UpdateTransaction(Transaction transaction)
        {
            _unitOfWork.GetRepository<Transaction>().Update(transaction);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return _unitOfWork.BeginTransactionAsync(cancellationToken);
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            return _unitOfWork.CommitTransactionAsync(cancellationToken);
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            return _unitOfWork.RollbackTransactionAsync(cancellationToken);
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return _unitOfWork.SaveAsync(cancellationToken);
        }

        public void ClearChangeTracker()
        {
            _unitOfWork.ClearChangeTracker();
        }
    }
}
