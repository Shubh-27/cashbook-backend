using backend.model.ResponseModel;
using backend.model.RequestModel;
using backend.common.Models;
using backend.model.Models.Views;

namespace backend.service.Repository.Interface
{
    public interface IAccountRepository
    {
        Task<PagedResult<VwAccountsList>> Search(SearchRequestModel request);
        Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId = null);
        Task<AccountResponseModel?> UpdateAccount(string accountSID, AccountRequestModel request, int? userId = null);
        Task<bool> DeleteAccount(string accountSID);
    }
}
