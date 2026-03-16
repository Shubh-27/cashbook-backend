using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.common.Models;
using backend.model.Models.Views;

namespace backend.service.Repository.Interface
{
    public interface ITransactionRepository
    {
        Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request);
        Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request);
        Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request);
        Task<bool> DeleteTransaction(string transactionSID, string accountSID);
    }
}
