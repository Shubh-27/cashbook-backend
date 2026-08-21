using backend.common;
using backend.model.DbModels;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
using backend.service.UnitOfWork;
using static backend.common.Constants;

namespace backend.service.Repository.Implementations
{
    public class TransactionRepository : ITransactionRepository
    {
        #region Variables & Constructor
        private readonly IUnitOfWork _unitOfWork;

        public TransactionRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Search
        public async Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request)
        {
            var query = _unitOfWork.GetRepository<VwTransactionsList>().AsQueryable(enableTracking: false);

            // Generic Search
            if (!string.IsNullOrEmpty(request.Search))
            {
                var search = request.Search.ToLower();
                query = query.Where(x => 
                    (x.Notes != null && x.Notes.ToLower().Contains(search)) ||
                    (x.AccountName != null && x.AccountName.ToLower().Contains(search)) ||
                    (x.DescriptionName != null && x.DescriptionName.ToLower().Contains(search))
                );
            }

            // Generic Filtering
            query = query.ApplyFilters(request.Filters);

            // Generic Sorting
            query = query.ApplySorting(request.SortBy, request.SortOrder);

            // Generic Pagination
            var result = await query.ToPagedResultAsync(request.Page, request.PageSize);

            return result;
        }
        #endregion

        #region Add Transaction
        public async Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request)
        {
            var accRepo = _unitOfWork.GetRepository<Account>();
            var txRepo = _unitOfWork.GetRepository<Transaction>();

            var account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == request.AccountSID && a.Status == StatusType.Active);
            if (account == null) return null;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var description = await GetOrCreateDescriptionAsync(request.DescriptionSID, request.DescriptionName);

                var transaction = new Transaction
                {
                    TransactionSID = Guid.NewGuid().ToString(),
                    TransactionDate = request.TransactionDate,
                    AccountID = account.AccountID,
                    DescriptionID = description?.DescriptionID,
                    Debit = request.Debit ?? 0,
                    Credit = request.Credit ?? 0,
                    Notes = request.Notes,
                    Balance = 0,
                    Status = StatusType.Active,
                };

                var added = await txRepo.InsertAsync(transaction);
                await _unitOfWork.SaveAsync();

                await _unitOfWork.CommitTransactionAsync();

                return added.Entity != null ? MapToResponse(added.Entity, description, account) : null;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
        #endregion

        #region Update Transaction
        public async Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request)
        {
            var txRepo = _unitOfWork.GetRepository<Transaction>();
            var accRepo = _unitOfWork.GetRepository<Account>();

            var existing = await txRepo.SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.Status == StatusType.Active);
            if (existing == null) return null;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Handle account update
                Account? account = null;
                if (!string.IsNullOrEmpty(request.AccountSID))
                {
                    account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == request.AccountSID && a.Status == StatusType.Active);
                    if (account != null)
                    {
                        existing.AccountID = account.AccountID;
                    }
                }

                existing.TransactionDate = request.TransactionDate;
                existing.Debit = request.Debit ?? 0;
                existing.Credit = request.Credit ?? 0;
                existing.Notes = request.Notes;
                existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

                // Handle description update
                var description = await GetOrCreateDescriptionAsync(request.DescriptionSID, request.DescriptionName);
                if (description != null)
                {
                    existing.DescriptionID = description.DescriptionID;
                }

                txRepo.Update(existing);
                await _unitOfWork.SaveAsync();

                await _unitOfWork.CommitTransactionAsync();

                return MapToResponse(existing, description, account);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
        #endregion

        #region Description Helper
        private async Task<Description?> GetOrCreateDescriptionAsync(string? descriptionSID, string? descriptionName)
        {
            var descRepo = _unitOfWork.GetRepository<Description>();
            Description? description = null;

            if (!string.IsNullOrEmpty(descriptionSID))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionSID == descriptionSID && d.Status == StatusType.Active);
            }

            if (description == null && !string.IsNullOrWhiteSpace(descriptionName))
            {
                var trimmedName = descriptionName.Trim();
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == trimmedName.ToLower() && d.Status == StatusType.Active);
                if (description == null)
                {
                    try
                    {
                        var newDesc = new Description
                        {
                            DescriptionSID = Guid.NewGuid().ToString(),
                            DescriptionName = trimmedName,
                            Status = StatusType.Active
                        };
                        var descEntry = await descRepo.InsertAsync(newDesc);
                        await _unitOfWork.SaveAsync();
                        description = descEntry.Entity;
                    }
                    catch (Exception)
                    {
                        // Handle race condition: another concurrent request created the description with unique index
                        _unitOfWork.ClearChangeTracker();
                        description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == trimmedName.ToLower() && d.Status == StatusType.Active);
                        if (description == null)
                        {
                            throw;
                        }
                    }
                }
            }

            return description;
        }
        #endregion

        #region Delete Transaction
        public async Task<bool> DeleteTransaction(string transactionSID, string accountSID)
        {
            var txRepo = _unitOfWork.GetRepository<Transaction>();
            var accRepo = _unitOfWork.GetRepository<Account>();

            var account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == accountSID && a.Status == StatusType.Active);
            if (account == null) return false;

            var existing = await txRepo.SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.AccountID == account.AccountID && t.Status == StatusType.Active);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Transaction>().Update(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
        #endregion

        #region Mapping Helpers
        private static TransactionResponseModel MapToResponse(Transaction transaction, Description? description = null, Account? account = null)
        {
            return new TransactionResponseModel
            {
                TransactionSID = transaction.TransactionSID,
                TransactionDate = transaction.TransactionDate,
                AccountID = transaction.AccountID,
                Debit = transaction.Debit,
                Credit = transaction.Credit,
                Balance = transaction.Balance,
                Notes = transaction.Notes,
                CreatedDateTime = transaction.CreatedDateTime,
                CreatedByUserID = transaction.CreatedByUserID,
                LastModifiedDateTime = transaction.LastModifiedDateTime,
                LastModifiedByUserID = transaction.LastModifiedByUserID,
                Status = transaction.Status,
                Description = description != null ? MapToDescriptionResponse(description) : null,
                Account = account != null ? MapToAccountResponse(account) : null
            };
        }

        private static DescriptionResponseModel MapToDescriptionResponse(Description description)
        {
            return new DescriptionResponseModel
            {
                DescriptionSID = description.DescriptionSID,
                DescriptionName = description.DescriptionName,
                LastModifiedDateTime = description.LastModifiedDateTime,
                Status = description.Status
            };
        }

        private static AccountResponseModel MapToAccountResponse(Account account)
        {
            return new AccountResponseModel
            {
                AccountSID = account.AccountSID,
                AccountName = account.AccountName,
                AccountNumber = account.AccountNumber,
                BankName = account.BankName,
                LastModifiedDateTime = account.LastModifiedDateTime,
                LastModifiedByUserID = account.LastModifiedByUserID,
                Status = account.Status
            };
        }
        #endregion
    }
}
