using backend.common;
using backend.model.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;

namespace backend.service.Repository.Implementation
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        public TransactionRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<(List<TransactionResponseModel> Data, int TotalCount)> GetTransactions(string? accountSID, string? descriptionSID, string? search, int page, int limit)
        {
            var repo = _unitOfWork.GetRepository<Transactions>();
            var accRepo = _unitOfWork.GetRepository<Accounts>();
            var descRepo = _unitOfWork.GetRepository<Descriptions>();

            Accounts? account = null;
            if (!string.IsNullOrEmpty(accountSID))
            {
                account = await accRepo.SingleOrDefaultAsync(x => x.AccountSID == accountSID);
                if (account == null) return (new List<TransactionResponseModel>(), 0);
            }

            Descriptions? description = null;
            if (!string.IsNullOrEmpty(descriptionSID))
            {
                description = await descRepo.SingleOrDefaultAsync(x => x.DescriptionSID == descriptionSID);
                if (description == null) return (new List<TransactionResponseModel>(), 0);
            }

            var query = (await repo.GetAllAsync()).AsQueryable();

            if (account != null)
            {
                query = query.Where(t => t.AccountID == account.AccountID);
            }

            if (description != null)
            {
                query = query.Where(t => t.DescriptionID == description.DescriptionID);
            }

            if (!string.IsNullOrEmpty(search))
            {
                // Note: Assuming search matches Notes or related DescriptionName
                search = search.ToLower();
                var matchingDescIds = (await descRepo.GetAllAsync())
                    .Where(d => d.DescriptionName.ToLower().Contains(search))
                    .Select(d => d.DescriptionID)
                    .ToList();

                query = query.Where(t => 
                    (t.Notes != null && t.Notes.ToLower().Contains(search)) || 
                    (t.DescriptionID.HasValue && matchingDescIds.Contains(t.DescriptionID.Value))
                );
            }

            // Ordering by date descending
            query = query.OrderByDescending(t => t.TransactionDate);

            int total = query.Count();
            
            int p = page > 0 ? page : 1;
            int l = limit > 0 ? limit : 50;
            var list = query.Skip((p - 1) * l).Take(l).ToList();

            var result = new List<TransactionResponseModel>();
            foreach (var t in list)
            {
                var model = CommonHelper.UpdateModel(t, new TransactionResponseModel());
                
                if (t.DescriptionID.HasValue)
                {
                    var desc = await descRepo.SingleOrDefaultAsync(d => d.DescriptionID == t.DescriptionID.Value);
                    if (desc != null) model.Description = CommonHelper.UpdateModel(desc, new DescriptionResponseModel());
                }

                if (t.AccountID.HasValue)
                {
                    var acc = await accRepo.SingleOrDefaultAsync(a => a.AccountID == t.AccountID.Value);
                    if (acc != null) model.Account = CommonHelper.UpdateModel(acc, new AccountResponseModel());
                }

                result.Add(model);
            }

            return (result, total);
        }

        public async Task<TransactionResponseModel?> AddTransaction(TransactionRequestModel request)
        {
            var accRepo = _unitOfWork.GetRepository<Accounts>();
            var descRepo = _unitOfWork.GetRepository<Descriptions>();
            var txRepo = _unitOfWork.GetRepository<Transactions>();

            var account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == request.AccountSid);
            if (account == null) return null;

            Descriptions? description = null;

            // Resolve description by SID or Name
            if (!string.IsNullOrEmpty(request.DescriptionSid))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionSID == request.DescriptionSid);
            }
            
            // If we have a name but no valid SID, create the description on the fly
            if (description == null && !string.IsNullOrEmpty(request.DescriptionName))
            {
                var existingDesc = await descRepo.SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == request.DescriptionName.ToLower());
                if (existingDesc != null)
                {
                    description = existingDesc;
                }
                else
                {
                    description = new Descriptions
                    {
                        DescriptionSID = Guid.NewGuid().ToString(),
                        DescriptionName = request.DescriptionName,
                        Status = 1
                    };
                    var descEntry = await descRepo.InsertAsync(description);
                    await _unitOfWork.SaveAsync(); // save immediately to get ID
                    description = descEntry.Entity;
                }
            }

            var transaction = new Transactions
            {
                TransactionSID = Guid.NewGuid().ToString(),
                TransactionDate = request.TransactionDate,
                AccountID = account.AccountID,
                DescriptionID = description?.DescriptionID,
                Debit = request.Debit ?? 0,
                Credit = request.Credit ?? 0,
                Notes = request.Notes,
                Status = 1
            };

            // Calculate balance (Mock logic for now, in real scenario you calculate sum of all previous transactions)
            var allTransactions = await txRepo.GetAllAsync();
            var prevTx = allTransactions
                .Where(t => t.AccountID == account.AccountID && string.Compare(t.TransactionDate, request.TransactionDate) <= 0)
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionID)
                .FirstOrDefault();

            double prevBalance = prevTx?.Balance ?? 0;
            transaction.Balance = prevBalance - (transaction.Debit ?? 0) + (transaction.Credit ?? 0);

            var added = await txRepo.InsertAsync(transaction);
            await _unitOfWork.SaveAsync();
            
            // Recalculate subsequent balances if needed (omitted for brevity, assume simple append for now)

            var result = CommonHelper.UpdateModel(added.Entity, new TransactionResponseModel());
            if (description != null) result.Description = CommonHelper.UpdateModel(description, new DescriptionResponseModel());
            return result;
        }

        public async Task<TransactionResponseModel?> UpdateTransaction(string transactionSID, TransactionRequestModel request)
        {
            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var descRepo = _unitOfWork.GetRepository<Descriptions>();
            
            var existing = await txRepo.SingleOrDefaultAsync(t => t.TransactionSID == transactionSID);
            if (existing == null) return null;

            existing.TransactionDate = request.TransactionDate;
            existing.Debit = request.Debit ?? 0;
            existing.Credit = request.Credit ?? 0;
            existing.Notes = request.Notes;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

            // Handle description update
            Descriptions? description = null;
            if (!string.IsNullOrEmpty(request.DescriptionSid))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionSID == request.DescriptionSid);
            }
            if (description == null && !string.IsNullOrEmpty(request.DescriptionName))
            {
                description = await descRepo.SingleOrDefaultAsync(d => d.DescriptionName.ToLower() == request.DescriptionName.ToLower());
                if (description == null)
                {
                     description = new Descriptions
                    {
                        DescriptionSID = Guid.NewGuid().ToString(),
                        DescriptionName = request.DescriptionName,
                        Status = 1
                    };
                    var descEntry = await descRepo.InsertAsync(description);
                    await _unitOfWork.SaveAsync();
                    description = descEntry.Entity;
                }
            }

            if (description != null)
            {
                existing.DescriptionID = description.DescriptionID;
            }

            txRepo.Update(existing);
            await _unitOfWork.SaveAsync();

            var result = CommonHelper.UpdateModel(existing, new TransactionResponseModel());
            if (description != null) result.Description = CommonHelper.UpdateModel(description, new DescriptionResponseModel());
            return result;
        }

        public async Task<bool> DeleteTransaction(string transactionSID, string accountSID)
        {
            var txRepo = _unitOfWork.GetRepository<Transactions>();
            var accRepo = _unitOfWork.GetRepository<Accounts>();

            var account = await accRepo.SingleOrDefaultAsync(a => a.AccountSID == accountSID);
            if (account == null) return false;

            var existing = await txRepo.SingleOrDefaultAsync(t => t.TransactionSID == transactionSID && t.AccountID == account.AccountID);
            if (existing == null) return false;

            txRepo.Delete(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }
}
