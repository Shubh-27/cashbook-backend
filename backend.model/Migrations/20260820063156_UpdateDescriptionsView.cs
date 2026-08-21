using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDescriptionsView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_descriptions_list;");
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
    }
}
