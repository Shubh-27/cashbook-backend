using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class AddListViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    d.DescriptionSID,
                    d.DescriptionName
                FROM Transactions t
                INNER JOIN Accounts a ON t.AccountID = a.AccountID AND a.Status = 1
                INNER JOIN Descriptions d ON t.DescriptionID = d.DescriptionID AND d.Status = 1;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW vw_accounts_list AS
                WITH TransactionCounts AS (
                    SELECT 
                        AccountID,
                        COUNT(1) AS TransactionCount
                    FROM Transactions
                    WHERE Status = 1
                    GROUP BY AccountID
                )
                SELECT 
                    a.AccountSID,
                    a.AccountName,
                    a.AccountNumber,
                    a.BankName,
                    a.Status,
                    COALESCE(tc.TransactionCount, 0) AS TransactionCount
                FROM Accounts a
                LEFT JOIN TransactionCounts tc 
                    ON tc.AccountID = a.AccountID
                WHERE a.Status = 1;
            ");

            migrationBuilder.Sql(@"
                CREATE VIEW vw_descriptions_list AS
                SELECT 
                    d.DescriptionSID,
                    d.DescriptionName,
                    d.Status,
                    (SELECT COUNT(*) FROM Transactions t WHERE t.DescriptionID = d.DescriptionID) as UsageCount
                FROM Descriptions d
                WHERE d.Status = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_transactions_list;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_accounts_list;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_descriptions_list;");
        }
    }
}
