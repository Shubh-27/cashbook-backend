using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class SnakeCaseViewAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_accounts_list;");
            migrationBuilder.Sql(@"
                CREATE VIEW vw_accounts_list AS
                WITH TransactionCounts AS (
                    SELECT 
                        AccountID,
                        COUNT(1) AS transaction_count
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
                    COALESCE(tc.transaction_count, 0) AS transaction_count
                FROM Accounts a
                LEFT JOIN TransactionCounts tc 
                    ON tc.AccountID = a.AccountID
                WHERE a.Status = 1;
            ");

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_descriptions_list;");
            migrationBuilder.Sql(@"
                CREATE VIEW vw_descriptions_list AS
                WITH TransactionCounts AS (
                    SELECT 
                        t.DescriptionID,
                        COUNT(1) AS transaction_count
                    FROM Transactions t
                    LEFT JOIN Accounts a
                        ON t.AccountID = a.AccountID
                    WHERE t.Status = 1
                        AND a.Status = 1
                    GROUP BY t.DescriptionID
                )
                SELECT 
                    d.DescriptionSID,
                    d.DescriptionName,
                    d.Status,
                    COALESCE(tc.transaction_count, 0) AS usage_count
                FROM Descriptions d
                LEFT JOIN TransactionCounts tc 
                    ON tc.DescriptionID = d.DescriptionID
                WHERE d.Status = 1;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_accounts_list;");
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

            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_descriptions_list;");
            migrationBuilder.Sql(@"
                CREATE VIEW vw_descriptions_list AS
                WITH TransactionCounts AS (
                    SELECT 
                        t.DescriptionID,
                        COUNT(1) AS TransactionCount
                    FROM Transactions t
                    LEFT JOIN Accounts a
                        ON t.AccountID = a.AccountID
                    WHERE t.Status = 1
                        AND a.Status = 1
                    GROUP BY t.DescriptionID
                )
                SELECT 
                    d.DescriptionSID,
                    d.DescriptionName,
                    d.Status,
                    COALESCE(tc.TransactionCount, 0) AS UsageCount
                FROM Descriptions d
                LEFT JOIN TransactionCounts tc 
                    ON tc.DescriptionID = d.DescriptionID
                WHERE d.Status = 1;
            ");
        }
    }
}
