using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class FilterTransactionsByStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_transactions_list;");
            migrationBuilder.Sql(@"
                CREATE VIEW vw_transactions_list AS
                SELECT 
                    t.TransactionSID,
                    t.TransactionDate,
                    t.Debit,
                    t.Credit,
                    t.Balance,
                    t.Notes,
                    t.Status,
                    a.AccountSID,
                    a.AccountName,
                    a.AccountNumber,
                    a.BankName,
                    d.DescriptionSID,
                    d.DescriptionName
                FROM Transactions t
                INNER JOIN Accounts a ON t.AccountID = a.AccountID AND a.Status = 1
                INNER JOIN Descriptions d ON t.DescriptionID = d.DescriptionID AND d.Status = 1
                WHERE t.Status = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_transactions_list;");
            migrationBuilder.Sql(@"
                CREATE VIEW vw_transactions_list AS
                SELECT 
                    t.TransactionSID,
                    t.TransactionDate,
                    t.Debit,
                    t.Credit,
                    t.Balance,
                    t.Notes,
                    t.Status,
                    a.AccountSID,
                    a.AccountName,
                    a.AccountNumber,
                    a.BankName,
                    d.DescriptionSID,
                    d.DescriptionName
                FROM Transactions t
                INNER JOIN Accounts a ON t.AccountID = a.AccountID AND a.Status = 1
                INNER JOIN Descriptions d ON t.DescriptionID = d.DescriptionID AND d.Status = 1;
            ");
        }
    }
}
