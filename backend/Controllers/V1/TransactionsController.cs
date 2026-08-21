using backend.common;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class TransactionsController : BaseController
    {
        #region Variables & Constructor
        private readonly ITransactionRepository _transactionRepository;
        private readonly IExportService _exportService;

        public TransactionsController(ITransactionRepository transactionRepository, IExportService exportService)
        {
            _transactionRepository = transactionRepository;
            _exportService = exportService;
        }
        #endregion

        #region Export Transactions
        /// <summary>
        /// Exports transactions to Excel or ZIP based on the specified filters, grouping, and preferences.
        /// </summary>
        /// <param name="request">The search and export parameters used to filter and format the transaction data.</param>
        /// <returns>An HTTP 200 response containing the generated file to download.</returns>
        [HttpPost("export")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Export([FromBody] ExportRequestModel request)
        {
            var (fileBytes, contentType, fileName) = await _exportService.ExportTransactionsAsync(request);
            return File(fileBytes, contentType, fileName);
        }
        #endregion

        #region Get Transactions List
        /// <summary>
        /// Retrieves a paged list of transactions matching the specified search, filter, and sorting criteria.
        /// </summary>
        /// <param name="request">The search parameters used to filter, sort, and paginate transactions.</param>
        /// <returns>An HTTP 200 response containing a paged result of transactions matching the search criteria.</returns>
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
        /// <param name="request">The transaction details to be added.</param>
        /// <returns>An IActionResult containing the result of the transaction creation, or BadRequest if the transaction could not be created.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(TransactionResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post([FromBody] TransactionRequestModel request)
        {
            var result = await _transactionRepository.AddTransaction(request);
            if (result == null) return BadRequest("Could not add transaction.");
            return Ok(result);
        }
        #endregion

        #region Update Transaction
        /// <summary>
        /// Updates an existing transaction with the specified details.
        /// </summary>
        /// <param name="transactionSID">The unique identifier of the transaction to update. Cannot be null or empty.</param>
        /// <param name="request">The transaction details to apply to the update operation.</param>
        /// <returns>An IActionResult representing the updated transaction or NotFound if not found.</returns>
        [HttpPut("{transactionSID}")]
        [ProducesResponseType(typeof(TransactionResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Put(string transactionSID, [FromBody] TransactionRequestModel request)
        {
            var result = await _transactionRepository.UpdateTransaction(transactionSID, request);
            if (result == null) return NotFound();
            return Ok(result);
        }
        #endregion

        #region Delete Transaction
        /// <summary>
        /// Deletes the specified transaction for the given account.
        /// </summary>
        /// <param name="transactionSID">The unique identifier of the transaction to delete. Cannot be null or empty.</param>
        /// <param name="accountSID">The unique identifier of the account associated with the transaction. Cannot be null or empty.</param>
        /// <returns>An IActionResult indicating success or NotFound if the transaction does not exist.</returns>
        [HttpDelete("{transactionSID}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string transactionSID, [FromQuery] string accountSID)
        {
            var result = await _transactionRepository.DeleteTransaction(transactionSID, accountSID);
            if (!result) return NotFound();
            return Ok(new { success = true });
        }
        #endregion
    }
}
