using backend.common.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.model.TenantDB;
using backend.model.TenantDB.Views;
using backend.service.Helpers;
using backend.service.Repository.Interface;
using backend.service.Services.Interfaces;
using static backend.common.Constants;

namespace backend.service.Services.Implementations
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;

        public TransactionService(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            return _transactionRepository.Search(request, cancellationToken);
        }

        public async Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request, CancellationToken cancellationToken = default)
        {
            var account = await _transactionRepository.GetActiveAccountBySidAsync(request.AccountSID, cancellationToken);
            if (account == null) return null;

            await _transactionRepository.BeginTransactionAsync(cancellationToken);
            try
            {
                var description = await GetOrCreateDescriptionAsync(request.DescriptionSID, request.DescriptionName, cancellationToken);

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

                var added = await _transactionRepository.InsertTransactionAsync(transaction, cancellationToken);
                await _transactionRepository.SaveAsync(cancellationToken);
                await _transactionRepository.CommitTransactionAsync(cancellationToken);

                return MapToResponse(added, description, account);
            }
            catch
            {
                await _transactionRepository.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request, CancellationToken cancellationToken = default)
        {
            var existing = await _transactionRepository.GetActiveTransactionBySidAsync(transactionSID, cancellationToken);
            if (existing == null) return null;

            await _transactionRepository.BeginTransactionAsync(cancellationToken);
            try
            {
                Account? account = null;
                if (!string.IsNullOrEmpty(request.AccountSID))
                {
                    account = await _transactionRepository.GetActiveAccountBySidAsync(request.AccountSID, cancellationToken);
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

                var description = await GetOrCreateDescriptionAsync(request.DescriptionSID, request.DescriptionName, cancellationToken);
                if (description != null)
                {
                    existing.DescriptionID = description.DescriptionID;
                }

                _transactionRepository.UpdateTransaction(existing);
                await _transactionRepository.SaveAsync(cancellationToken);
                await _transactionRepository.CommitTransactionAsync(cancellationToken);

                return MapToResponse(existing, description, account);
            }
            catch
            {
                await _transactionRepository.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task<bool> DeleteTransaction(string transactionSID, string accountSID, CancellationToken cancellationToken = default)
        {
            var account = await _transactionRepository.GetActiveAccountBySidAsync(accountSID, cancellationToken);
            if (account == null) return false;

            var existing = await _transactionRepository.GetActiveTransactionBySidAndAccountIdAsync(transactionSID, account.AccountID, cancellationToken);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _transactionRepository.UpdateTransaction(existing);
            await _transactionRepository.SaveAsync(cancellationToken);
            return true;
        }

        private async Task<Description?> GetOrCreateDescriptionAsync(string? descriptionSID, string? descriptionName, CancellationToken cancellationToken)
        {
            Description? description = null;

            if (!string.IsNullOrEmpty(descriptionSID))
            {
                description = await _transactionRepository.GetActiveDescriptionBySidAsync(descriptionSID, cancellationToken);
            }

            if (description == null && !string.IsNullOrWhiteSpace(descriptionName))
            {
                var trimmedName = descriptionName.Trim();
                description = await _transactionRepository.GetActiveDescriptionByNameAsync(trimmedName, cancellationToken);
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
                        description = await _transactionRepository.InsertDescriptionAsync(newDesc, cancellationToken);
                        await _transactionRepository.SaveAsync(cancellationToken);
                    }
                    catch (Exception)
                    {
                        // Handle race condition: another concurrent request created the description with unique index
                        _transactionRepository.ClearChangeTracker();
                        description = await _transactionRepository.GetActiveDescriptionByNameAsync(trimmedName, cancellationToken);
                        if (description == null)
                        {
                            throw;
                        }
                    }
                }
            }

            return description;
        }

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
                Description = description != null ? description.ToResponseModel() : null,
                Account = account != null ? account.ToResponseModel() : null
            };
        }
    }
}
