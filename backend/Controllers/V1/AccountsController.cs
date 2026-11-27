using backend.common.Models;
using backend.common;
using backend.model.TenantDB.Views;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class AccountsController : BaseController
    {
        #region Variables & Constructor
        private readonly IAccountRepository _accountRepository;

        public AccountsController(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }
        #endregion

        #region Get Accounts List
        /// <summary>
        /// Retrieves a paginated list of accounts based on the provided search, filter, and sorting criteria.
        /// </summary>
        /// <param name="request">The search parameters used to filter, sort, and paginate accounts.</param>
        /// <returns>A paged result of account records matching the search criteria.</returns>
        [HttpPost("list")]
        [ProducesResponseType(typeof(PagedResult<VwAccountsList>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromBody] SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var result = await _accountRepository.Search(request, cancellationToken);
            return Ok(result);
        }
        #endregion

        #region Add Account
        /// <summary>
        /// Creates a new account based on the specified request data.
        /// </summary>
        /// <param name="request">The account request model containing the details of the account to be added.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An IActionResult indicating the result of the operation. Returns BadRequest if the request is invalid or creation fails, and Ok with the added account details if successful.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(AccountResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post([FromBody] AccountRequestModel request, CancellationToken cancellationToken = default)
        {
            var result = await _accountRepository.AddAccount(request, null, cancellationToken);
            if (result == null) return BadRequest("Could not add account.");
            return Ok(result);
        }
        #endregion

        #region Update Account
        /// <summary>
        /// Updates the account information for the specified account identifier.
        /// </summary>
        /// <param name="accountSID">The unique identifier of the account to update. Cannot be null or empty.</param>
        /// <param name="request">The account data to update, provided in the request body. Must contain valid account information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An IActionResult indicating the outcome of the update operation. Returns Ok with the updated account if successful; NotFound if the account does not exist; BadRequest if the request data is invalid.</returns>
        [HttpPut("{accountSID}")]
        [ProducesResponseType(typeof(AccountResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Put(string accountSID, [FromBody] AccountRequestModel request, CancellationToken cancellationToken = default)
        {
            var result = await _accountRepository.UpdateAccount(accountSID, request, null, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
        }
        #endregion

        #region Delete Account
        /// <summary>
        /// Deletes the account with the specified identifier.
        /// </summary>
        /// <param name="accountSID">The unique identifier of the account to delete. Cannot be null or empty.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An IActionResult indicating the outcome of the delete operation. Returns NotFound if the account does not exist; otherwise, returns Ok with a success indicator.</returns>
        [HttpDelete("{accountSID}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string accountSID, CancellationToken cancellationToken = default)
        {
            var result = await _accountRepository.DeleteAccount(accountSID, cancellationToken);
            if (!result) return NotFound();
            return Ok(new { success = true });
        }
        #endregion
    }
}
