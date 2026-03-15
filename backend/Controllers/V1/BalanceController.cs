using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class BalanceController : BaseController
    {
        private readonly IAccountRepository _accountRepository;
        public BalanceController(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository;
        }

        [HttpGet("total")]
        public async Task<IActionResult> GetTotalBalance()
        {
            double total = await _accountRepository.GetTotalBalance();
            return Ok(total);
        }
    }
}
