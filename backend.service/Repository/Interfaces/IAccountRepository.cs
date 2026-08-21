using backend.common;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;

namespace backend.service.Repository.Interfaces
{
    public interface IAccountRepository
    {
        Task<PagedResult<VwAccountsList>> Search(SearchRequestModel request);
        Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId = null);
        Task<AccountResponseModel?> UpdateAccount(string accountSID, AccountRequestModel request, int? userId = null);
        Task<bool> DeleteAccount(string accountSID);
    }
}
