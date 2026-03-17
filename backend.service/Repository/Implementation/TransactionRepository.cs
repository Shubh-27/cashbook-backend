using backend.common;
using backend.model.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using static backend.common.Constants;
using backend.common.Models;
using backend.common.Extensions;
using backend.model.Models.Views;
using backend.model.Data;

namespace backend.service.Repository.Implementation
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
            var accRepo = _unitOfWork.GetRepository<Accounts>();
            var descRepo = _unitOfWork.GetRepository<Descriptions>();
            var txRepo = _unitOfWork.GetRepository<Transactions>();

            var account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == request.AccountSID && a.Status == StatusType.Active);
            if (account == null) return null;

            Descriptions? description = null;

            // Resolve description by SID or Name
            if (!string.IsNullOrEmpty(request.DescriptionSID))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionSID == request.DescriptionSID && d.Status == StatusType.Active);
            }

            // If we have a name but no valid SID, create the description on the fly
            if (description == null && !string.IsNullOrEmpty(request.DescriptionName))
            {
                var existingDesc = await descRepo.SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == request.DescriptionName.ToLower() && d.Status == StatusType.Active);
                if (existingDesc != null)
                {
                    description = existingDesc;
                }
                else
                {
                    description = new Descriptions
                    {
                        DescriptionSID = Guid.NewGuid().ToString(),
                        DescriptionName = request.DescriptionName,
                        Status = StatusType.Active
                    };
                    var descEntry = await descRepo.InsertAsync(description);
                    await _unitOfWork.SaveAsync(); // save immediately to get ID
                    description = descEntry.Entity;
                }
            }

            var transaction = new Transactions
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

            var result = CommonHelper.UpdateModel(added.Entity, new TransactionResponseModel());
            if (description != null) result.Description = CommonHelper.UpdateModel(description, new DescriptionResponseModel());
            return result;
        }
        #endregion

        #region Update Transaction
        public async Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request)
        {
            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var descRepo = _unitOfWork.GetRepository<Descriptions>();

            var existing = await txRepo.SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.Status == StatusType.Active);
            if (existing == null) return null;

            existing.TransactionDate = request.TransactionDate;
            existing.Debit = request.Debit ?? 0;
            existing.Credit = request.Credit ?? 0;
            existing.Notes = request.Notes;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

            // Handle description update
            Descriptions? description = null;
            if (!string.IsNullOrEmpty(request.DescriptionSID))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionSID == request.DescriptionSID && d.Status == StatusType.Active);
            }
            if (description == null && !string.IsNullOrEmpty(request.DescriptionName))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == request.DescriptionName.ToLower() && d.Status == StatusType.Active);
                if (description == null)
                {
                    description = new Descriptions
                    {
                        DescriptionSID = Guid.NewGuid().ToString(),
                        DescriptionName = request.DescriptionName,
                        Status = StatusType.Active
                    };
                    var descEntry = await descRepo.InsertAsync(description);
                    await _unitOfWork.SaveAsync();
                    description = descEntry.Entity;
                }
            }

            if (description != null)
            {
                existing.DescriptionID = description.DescriptionID;
            }

            txRepo.Update(existing);
            await _unitOfWork.SaveAsync();

            var result = CommonHelper.UpdateModel(existing, new TransactionResponseModel());
            if (description != null) result.Description = CommonHelper.UpdateModel(description, new DescriptionResponseModel());
            return result;
        }
        #endregion

        #region Delete Transaction
        public async Task<bool> DeleteTransaction(string transactionSID, string accountSID)
        {
            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var accRepo = _unitOfWork.GetRepository<Accounts>();

            var account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == accountSID && a.Status == StatusType.Active);
            if (account == null) return false;

            var existing = await txRepo.SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.AccountID == account.AccountID && t.Status == StatusType.Active);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Transactions>().Update(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
        #endregion
    }
}
