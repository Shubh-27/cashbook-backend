using backend.common.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.model.TenantDB.Views;

namespace backend.service.Services.Interfaces
{
    public interface ITransactionService
    {
        Task<PagedResult<VwTransactionsList>> Search(SearchRequestModel request, CancellationToken cancellationToken);
        Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request, CancellationToken cancellationToken);
        Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request, CancellationToken cancellationToken);
        Task<bool> DeleteTransaction(string transactionSID, string accountSID, CancellationToken cancellationToken);
    }
}
