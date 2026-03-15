using backend.model.RequestModel;
using backend.model.ResponseModel;

namespace backend.service.Repository.Interface
{
    public interface ITransactionRepository
    {
        Task<(List<TransactionResponseModel> Data, int TotalCount)> GetTransactions(string? accountSID, string? search, int page, int limit);
        Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request);
        Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request);
        Task<bool> DeleteTransaction(string transactionSID, string accountSID);
    }
}
