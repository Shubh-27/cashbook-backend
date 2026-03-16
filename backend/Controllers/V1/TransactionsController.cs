using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Mvc;
using backend.model.RequestModel;
using backend.common.Models;
using backend.model.Models.Views;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class TransactionsController : BaseController
    {
        #region Variables & Constructor
        private readonly ITransactionRepository _transactionRepository;
        public TransactionsController(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }
        #endregion

        #region Get Transactions List
        /// <summary>
        /// Retrieves a paged list of transactions matching the specified search criteria.
        /// </summary>
        /// <remarks>Use this method to obtain transaction records based on filters such as date range,
        /// status, or other criteria defined in the search request. The result is paged to support large
        /// datasets.</remarks>
        /// <param name="request">The search parameters used to filter and page the transactions. Cannot be null.</param>
        /// <returns>An HTTP 200 response containing a paged result of transactions that match the search criteria.</returns>
        [HttpPost("list")]
        [ProducesResponseType(typeof(PagedResult<VwTransactionsList>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromBody] SearchRequestModel request)
        {
            var result = await _transactionRepository.Search(request);
            return Ok(result);
        }
        #endregion

        #region Add Transaction
        /// <summary>
        /// Creates a new transaction based on the specified request data.
        /// </summary>
        /// <param name="request">The transaction details to be added. Cannot be null. The request must contain all required fields for a
        /// valid transaction.</param>
        /// <returns>A model containing the result of the transaction creation, or null if the transaction could not be created.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(TransactionResponseModel), StatusCodes.Status200OK)]
        public async Task<TransactionResponseModel?> Post([FromBody] TransactionRequestModel request)
        {
            return await _transactionRepository.AddTransaction(request);
        }
        #endregion

        #region Update Transaction
        /// <summary>
        /// Updates an existing transaction with the specified details.
        /// </summary>
        /// <param name="transactionSid">The unique identifier of the transaction to update. Cannot be null or empty.</param>
        /// <param name="request">The transaction details to apply to the update operation. Must contain valid transaction data.</param>
        /// <returns>A model representing the updated transaction if the update succeeds; otherwise, null if the transaction is
        /// not found.</returns>
        [HttpPut("{transactionSID}")]
        [ProducesResponseType(typeof(TransactionResponseModel), StatusCodes.Status200OK)]
        public async Task<TransactionResponseModel?> Put(string transactionSID, [FromBody] TransactionRequestModel request)
        {
            return await _transactionRepository.UpdateTransaction(transactionSID, request);
        }
        #endregion

        #region Delete Transaction
        /// <summary>
        /// Deletes the specified transaction for the given account.
        /// </summary>
        /// <param name="transactionSid">The unique identifier of the transaction to delete. Cannot be null or empty.</param>
        /// <param name="accountSID">The unique identifier of the account associated with the transaction. Cannot be null or empty.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the
        /// transaction was successfully deleted; otherwise, <see langword="false"/>.</returns>
        [HttpDelete("{transactionSID}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<bool> Delete(string transactionSID, [FromQuery] string accountSID)
        {
            return await _transactionRepository.DeleteTransaction(transactionSID, accountSID);
        }
        #endregion
    }
}
