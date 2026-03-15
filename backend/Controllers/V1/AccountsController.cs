using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class AccountsController : BaseController
    {
        private readonly IAccountRepository _accountRepository;
        public AccountsController(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        [HttpGet]
        public async Task<List<AccountResponseModel>> Get()
        {
            var accounts = await _accountRepository.GetAccounts();
            return accounts;
        }

        [HttpGet("balances")]
        public async Task<List<AccountResponseModel>> GetBalances()
        {
            return await _accountRepository.GetAccountBalances();
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] backend.model.RequestModel.AccountRequestModel request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _accountRepository.AddAccount(request);
            if (result == null) return BadRequest("Could not add account.");
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody] backend.model.RequestModel.AccountRequestModel request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _accountRepository.UpdateAccount(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _accountRepository.DeleteAccount(id);
            if (!result) return NotFound();
            return Ok(new { success = true });
        }
    }
}
