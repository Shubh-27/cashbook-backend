using backend.common.Models;
using backend.common;
using backend.model.TenantDB.Views;
using backend.model.RequestModel;
using backend.model.ResponseModel;

namespace backend.service.Repository.Interface
{
    public interface IAccountRepository
    {
        Task<PagedResult<VwAccountsList>> Search(SearchRequestModel request, CancellationToken cancellationToken);
        Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId, CancellationToken cancellationToken);
        Task<AccountResponseModel?> UpdateAccount(string accountSID, AccountRequestModel request, int? userId, CancellationToken cancellationToken);
        Task<bool> DeleteAccount(string accountSID, CancellationToken cancellationToken);
    }
}
