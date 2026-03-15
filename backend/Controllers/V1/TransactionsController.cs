using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Mvc;
using backend.model.RequestModel;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class TransactionsController : BaseController
    {
        private readonly ITransactionRepository _transactionRepository;
        public TransactionsController(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? accountId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int limit = 50)
        {
            var result = await _transactionRepository.GetTransactions(accountId, search, page, limit);
            return Ok(new { data = result.Data, total = result.TotalCount });
        }

        [HttpPost]
        public async Task<TransactionResponseModel?> Post([FromBody] TransactionRequestModel request)
        {
            return await _transactionRepository.AddTransaction(request);
        }

        [HttpPut("{id}")]
        public async Task<TransactionResponseModel?> Put(string id, [FromBody] TransactionRequestModel request)
        {
            return await _transactionRepository.UpdateTransaction(id, request);
        }

        [HttpDelete("{id}")]
        public async Task<bool> Delete(string id, [FromQuery] string accountSid)
        {
            return await _transactionRepository.DeleteTransaction(id, accountSid);
        }
    }
}
