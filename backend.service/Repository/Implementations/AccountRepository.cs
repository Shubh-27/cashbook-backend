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
        public async Task<PagedResult<VwAccountsList>> Search(SearchRequestModel request)
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
            var result = await query.ToPagedResultAsync(request.Page, request.PageSize);

            return result;
        }
        #endregion

        #region Add Account
        public async Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId = null)
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

            var added = await _unitOfWork.GetRepository<Account>().InsertAsync(newAccount);
            await _unitOfWork.SaveAsync();

            return added.Entity != null ? MapToResponse(added.Entity) : null;
        }
        #endregion

        #region Update Account
        public async Task<AccountResponseModel?> UpdateAccount(string accountSID, AccountRequestModel request, int? userId = null)
        {
            var existing = await _unitOfWork.GetRepository<Account>().SingleOrDefaultAsync(x => x.AccountSID == accountSID);
            if (existing == null) return null;

            existing.AccountName = request.AccountName;
            existing.BankName = request.BankName;
            existing.AccountNumber = !string.IsNullOrWhiteSpace(request.AccountNumber) && long.TryParse(request.AccountNumber.Trim(), out long updatedAccNum) ? updatedAccNum : null;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");
            existing.LastModifiedByUserID = userId;

            _unitOfWork.GetRepository<Account>().Update(existing);
            await _unitOfWork.SaveAsync();

            return MapToResponse(existing);
        }
        #endregion

        #region Delete Account
        public async Task<bool> DeleteAccount(string accountSID)
        {
            var accountRepo = _unitOfWork.GetRepository<Account>();
            var existing = await accountRepo.SingleOrDefaultAsync(x => x.AccountSID == accountSID);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Account>().Update(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
        #endregion

        #region Mapping Helpers
        private static AccountResponseModel MapToResponse(Account account, double? balance = null)
        {
            return new AccountResponseModel
            {
                AccountSID = account.AccountSID,
                AccountName = account.AccountName,
                AccountNumber = account.AccountNumber,
                BankName = account.BankName,
                LastModifiedDateTime = account.LastModifiedDateTime,
                LastModifiedByUserID = account.LastModifiedByUserID,
                Status = account.Status,
                Balance = balance
            };
        }
        #endregion
    }
}
