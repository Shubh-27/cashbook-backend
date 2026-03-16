using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Mvc;
using backend.model.RequestModel;
using backend.common.Models;
using backend.model.Models.Views;

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

        #region Get All Accounts
        /// <summary>
        /// Retrieves a paginated list of accounts based on the provided search criteria.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("list")]
        [ProducesResponseType(typeof(PagedResult<VwAccountsList>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromBody] SearchRequestModel request)
        {
            var result = await _accountRepository.Search(request);
            return Ok(result);
        }
        #endregion

        #region Get Account By ID
        /// <summary>
        /// Retrieves the details of a specific account by its unique identifier.
        /// </summary>
        /// <param name="request">The account request model containing the details of the account to be added.</param>
        /// <returns>An IActionResult indicating the result of the operation. Returns BadRequest if the request is invalid or the account could not be added, and Ok with the added account details if successful.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(AccountResponseModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> Post([FromBody] AccountRequestModel request)
        {
            var result = await _accountRepository.AddAccount(request);
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
        /// <returns>An IActionResult indicating the outcome of the update operation. Returns Ok with the updated account if
        /// successful; NotFound if the account does not exist; BadRequest if the request data is invalid.</returns>
        [HttpPut("{accountSID}")]
        public async Task<IActionResult> Put(string accountSID, [FromBody] AccountRequestModel request)
        {
            var result = await _accountRepository.UpdateAccount(accountSID, request);
            if (result == null) return NotFound();
            return Ok(result);
        }
        #endregion

        #region Delete Account
        /// <summary>
        /// Deletes the account with the specified identifier.
        /// </summary>
        /// <param name="accountSID">The unique identifier of the account to delete. Cannot be null or empty.</param>
        /// <returns>An IActionResult indicating the outcome of the delete operation. Returns NotFound if the account does not
        /// exist; otherwise, returns Ok with a success indicator.</returns>
        [HttpDelete("{accountSID}")]
        public async Task<IActionResult> Delete(string accountSID)
        {
            var result = await _accountRepository.DeleteAccount(accountSID);
            if (!result) return NotFound();
            return Ok(new { success = true });
        }
        #endregion
    }
}
