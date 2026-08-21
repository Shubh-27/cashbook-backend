using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_AccountID\" ON \"Transactions\" (\"AccountID\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_DescriptionID\" ON \"Transactions\" (\"DescriptionID\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_TransactionDate\" ON \"Transactions\" (\"TransactionDate\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_Status\" ON \"Transactions\" (\"Status\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_Status_TransactionDate\" ON \"Transactions\" (\"Status\", \"TransactionDate\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_AccountID_Status\" ON \"Transactions\" (\"AccountID\", \"Status\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Transactions_DescriptionID_Status\" ON \"Transactions\" (\"DescriptionID\", \"Status\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Transactions_DescriptionID_Status\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Transactions_AccountID_Status\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Transactions_Status_TransactionDate\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Transactions_Status\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Transactions_TransactionDate\";");
        }
    }
}
