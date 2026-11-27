using backend.common.Models;
using backend.common;
using backend.model.Extensions;
using backend.model.TenantDB;
using backend.model.TenantDB.Views;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Helpers;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using static backend.common.Constants;

namespace backend.service.Repository.Implementations
{
    public class AccountRepository : IAccountRepository
    {
        #region Variables & Constructor
        private readonly IUnitOfWork _unitOfWork;

        public AccountRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Search
        public async Task<PagedResult<VwAccountsList>> Search(SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.GetRepository<VwAccountsList>().AsQueryable(enableTracking: false);

            // Generic Search
            if (!string.IsNullOrEmpty(request.Search))
            {
                var search = request.Search.ToLower();
                query = query.Where(x => 
                    (x.AccountName != null && x.AccountName.ToLower().Contains(search)) ||
                    (x.BankName != null && x.BankName.ToLower().Contains(search))
                );
            }

            // Generic Filtering
            query = query.ApplyFilters(request.Filters);

            // Generic Sorting
            query = query.ApplySorting(request.SortBy, request.SortOrder);

            // Generic Pagination
            var result = await query.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

            return result;
        }
        #endregion

        #region Add Account
        public async Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId = null, CancellationToken cancellationToken = default)
        {
            var newAccount = new Account
            {
                AccountSID = Guid.NewGuid().ToString(),
                AccountName = request.AccountName,
                BankName = request.BankName,
                AccountNumber = !string.IsNullOrWhiteSpace(request.AccountNumber) && long.TryParse(request.AccountNumber.Trim(), out long accNum) ? accNum : null,
                CreatedDateTime = DateTime.UtcNow.ToString("O"),
                CreatedByUserID = userId,
                LastModifiedDateTime = DateTime.UtcNow.ToString("O"),
                LastModifiedByUserID = userId,
                Status = StatusType.Active
            };

            var added = await _unitOfWork.GetRepository<Account>().InsertAsync(newAccount, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            return added.Entity != null ? added.Entity.ToResponseModel() : null;
        }
        #endregion

        #region Update Account
        public async Task<AccountResponseModel?> UpdateAccount(string accountSID, AccountRequestModel request, int? userId = null, CancellationToken cancellationToken = default)
        {
            var existing = await _unitOfWork.GetRepository<Account>().SingleOrDefaultAsync(x => x.AccountSID == accountSID, cancellationToken: cancellationToken);
            if (existing == null) return null;

            existing.AccountName = request.AccountName;
            existing.BankName = request.BankName;
            existing.AccountNumber = !string.IsNullOrWhiteSpace(request.AccountNumber) && long.TryParse(request.AccountNumber.Trim(), out long updatedAccNum) ? updatedAccNum : null;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");
            existing.LastModifiedByUserID = userId;

            _unitOfWork.GetRepository<Account>().Update(existing);
            await _unitOfWork.SaveAsync(cancellationToken);

            return existing.ToResponseModel();
        }
        #endregion

        #region Delete Account
        public async Task<bool> DeleteAccount(string accountSID, CancellationToken cancellationToken = default)
        {
            var accountRepo = _unitOfWork.GetRepository<Account>();
            var existing = await accountRepo.SingleOrDefaultAsync(x => x.AccountSID == accountSID, cancellationToken: cancellationToken);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Account>().Update(existing);
            await _unitOfWork.SaveAsync(cancellationToken);
            return true;
        }
        #endregion
    }
}
