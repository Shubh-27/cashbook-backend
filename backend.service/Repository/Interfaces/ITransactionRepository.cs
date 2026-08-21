using backend.common;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;

namespace backend.service.Repository.Interfaces
{
    public interface ITransactionRepository
    {
        Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request);
        Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request);
        Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request);
        Task<bool> DeleteTransaction(string transactionSID, string accountSID);
    }
}
