using backend.model.TenantDB;
using backend.model.RequestModel;
using backend.model.ResponseModel;

namespace backend.service.Helpers
{
    public static class ResponseMappingExtensions
    {
        public static AccountResponseModel ToResponseModel(this Account account, double? balance = null)
        {
            return new AccountResponseModel
            {
                AccountSID = account.AccountSID,
                AccountName = account.AccountName,
                AccountNumber = account.AccountNumber,
                BankName = account.BankName,
                LastModifiedDateTime = account.LastModifiedDateTime,
                LastModifiedByUserID = account.LastModifiedByUserID,
                Status = account.Status,
                Balance = balance
            };
        }

        public static DescriptionResponseModel ToResponseModel(this Description description)
        {
            return new DescriptionResponseModel
            {
                DescriptionSID = description.DescriptionSID,
                DescriptionName = description.DescriptionName,
                LastModifiedDateTime = description.LastModifiedDateTime,
                Status = description.Status
            };
        }

        public static StatementProfileSample BuildProfileSample(
            IReadOnlyList<StatementProfileSampleRow> topRows,
            IReadOnlyList<StatementProfileSampleRow> bottomRows,
            int transactionEndRow,
            string? footerMarker)
        {
            return new StatementProfileSample
            {
                TopRows = topRows.ToList(),
                BottomRows = bottomRows.ToList(),
                TransactionEndRow = transactionEndRow,
                FooterMarker = footerMarker
            };
        }
    }
}
