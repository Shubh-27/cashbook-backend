using backend.model.ResponseModel;
using backend.model.RequestModel;

namespace backend.service.Repository.Interface
{
    public interface IAccountRepository
    {
        Task<List<AccountResponseModel>> GetAccounts();
        Task<List<AccountResponseModel>> GetAccountBalances();
        Task<double> GetTotalBalance();
        Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId = null);
        Task<AccountResponseModel?> UpdateAccount(string accountId, AccountRequestModel request, int? userId = null);
        Task<bool> DeleteAccount(string accountId);
    }
}
