using backend.common;
using backend.model.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;

namespace backend.service.Repository.Implementation
{
    public class AccountRepository : IAccountRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        public AccountRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<AccountResponseModel>> GetAccounts()
        {
            var accounts = await _unitOfWork.GetRepository<Accounts>().GetAllAsync();
            return [.. accounts.Select(x => CommonHelper.UpdateModel(x, new AccountResponseModel()))];
        }

        public async Task<List<AccountResponseModel>> GetAccountBalances()
        {
            var accounts = await _unitOfWork.GetRepository<Accounts>().GetAllAsync();
            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var allTxs = await txRepo.GetAllAsync();

            var result = new List<AccountResponseModel>();
            foreach (var acc in accounts)
            {
                var model = CommonHelper.UpdateModel(acc, new AccountResponseModel());
                
                // Find latest transaction for this account
                var latestTx = allTxs
                    .Where(t => t.AccountID == acc.AccountID)
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.TransactionID)
                    .FirstOrDefault();

                if (latestTx != null)
                {
                    model.Balance = latestTx.Balance;
                }
                else
                {
                    model.Balance = 0;
                }
                
                result.Add(model);
            }

            return result;
        }

        public async Task<double> GetTotalBalance()
        {
            var accounts = await _unitOfWork.GetRepository<Accounts>().GetAllAsync();
            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var allTxs = await txRepo.GetAllAsync();

            double total = 0;
            foreach (var acc in accounts)
            {
                var latestTx = allTxs
                    .Where(t => t.AccountID == acc.AccountID)
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.TransactionID)
                    .FirstOrDefault();
                
                if (latestTx != null)
                {
                    total += latestTx.Balance ?? 0;
                }
            }
            return total;
        }

        public async Task<AccountResponseModel?> AddAccount(AccountRequestModel request, int? userId = null)
        {
            var newAccount = new Accounts
            {
                AccountSID = Guid.NewGuid().ToString(),
                AccountName = request.AccountName,
                BankName = request.BankName,
                AccountNumber = !string.IsNullOrEmpty(request.AccountNumber) ? int.Parse(request.AccountNumber) : null,
                CreatedDateTime = DateTime.UtcNow.ToString("O"),
                CreatedByUserID = userId,
                LastModifiedDateTime = DateTime.UtcNow.ToString("O"),
                LastModifiedByUserID = userId,
                Status = 1
            };

            var added = await _unitOfWork.GetRepository<Accounts>().InsertAsync(newAccount);
            await _unitOfWork.SaveAsync();

            return added.Entity != null ? CommonHelper.UpdateModel(added.Entity, new AccountResponseModel()) : null;
        }

        public async Task<AccountResponseModel?> UpdateAccount(string accountId, AccountRequestModel request, int? userId = null)
        {
            var existing = await _unitOfWork.GetRepository<Accounts>().SingleOrDefaultAsync(x => x.AccountSID == accountId);
            if (existing == null) return null;

            existing.AccountName = request.AccountName;
            existing.BankName = request.BankName;
            existing.AccountNumber = !string.IsNullOrEmpty(request.AccountNumber) ? int.Parse(request.AccountNumber) : null;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");
            existing.LastModifiedByUserID = userId;

            _unitOfWork.GetRepository<Accounts>().Update(existing);
            await _unitOfWork.SaveAsync();

            return CommonHelper.UpdateModel(existing, new AccountResponseModel());
        }

        public async Task<bool> DeleteAccount(string accountId)
        {
            var accountRepo = _unitOfWork.GetRepository<Accounts>();
            var existing = await accountRepo.SingleOrDefaultAsync(x => x.AccountSID == accountId);
            if (existing == null) return false;

            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var txs = await txRepo.GetAllAsync(t => t.AccountID == existing.AccountID);
            
            if (txs.Any())
            {
                txRepo.Delete(txs);
                await _unitOfWork.SaveAsync();
            }

            accountRepo.Delete(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}
