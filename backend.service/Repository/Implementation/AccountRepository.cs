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
            var newAccount = new Accounts
            {
                AccountSID = Guid.NewGuid().ToString(),
                AccountName = request.AccountName,
                BankName = request.BankName,
                AccountNumber = !string.IsNullOrEmpty(request.AccountNumber) ? int.Parse(request.AccountNumber) : null,
                CreatedDateTime = DateTime.UtcNow.ToString("O"),
                CreatedByUserID = userId,
                LastModifiedDateTime = DateTime.UtcNow.ToString("O"),
                LastModifiedByUserID = userId,
                Status = StatusType.Active
            };

            var added = await _unitOfWork.GetRepository<Accounts>().InsertAsync(newAccount);
            await _unitOfWork.SaveAsync();

            return added.Entity != null ? CommonHelper.UpdateModel(added.Entity, new AccountResponseModel()) : null;
        }
        #endregion

        #region Update Account
        public async Task<AccountResponseModel?> UpdateAccount(string accountSID, AccountRequestModel request, int? userId = null)
        {
            var existing = await _unitOfWork.GetRepository<Accounts>().SingleOrDefaultAsync(x => x.AccountSID == accountSID);
            if (existing == null) return null;

            existing.AccountName = request.AccountName;
            existing.BankName = request.BankName;
            existing.AccountNumber = !string.IsNullOrEmpty(request.AccountNumber) ? int.Parse(request.AccountNumber) : null;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");
            existing.LastModifiedByUserID = userId;

            _unitOfWork.GetRepository<Accounts>().Update(existing);
            await _unitOfWork.SaveAsync();

            return CommonHelper.UpdateModel(existing, new AccountResponseModel());
        }
        #endregion

        #region Delete Account
        public async Task<bool> DeleteAccount(string accountSID)
        {
            var accountRepo = _unitOfWork.GetRepository<Accounts>();
            var existing = await accountRepo.SingleOrDefaultAsync(x => x.AccountSID == accountSID);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Accounts>().Update(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
        #endregion
    }
}
