using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class AddStatementImportProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportBatchID",
                table: "Transactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportProfiles",
                columns: table => new
                {
                    ImportProfileID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportProfileSID = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    WorksheetName = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderRow = table.Column<int>(type: "INTEGER", nullable: false),
                    HeaderSignature = table.Column<string>(type: "TEXT", nullable: false),
                    ColumnMappingJson = table.Column<string>(type: "TEXT", nullable: false),
                    DateFormat = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastModifiedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfiles", x => x.ImportProfileID);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    ImportBatchID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ImportBatchSID = table.Column<string>(type: "TEXT", nullable: false),
                    ImportProfileID = table.Column<int>(type: "INTEGER", nullable: true),
                    AccountID = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    StatementMetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportedRows = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedDuplicateRows = table.Column<int>(type: "INTEGER", nullable: false),
                    InvalidRows = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UndoneDateTime = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.ImportBatchID);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Accounts_AccountID",
                        column: x => x.AccountID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportBatches_ImportProfiles_ImportProfileID",
                        column: x => x.ImportProfileID,
                        principalTable: "ImportProfiles",
                        principalColumn: "ImportProfileID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportBatchID",
                table: "Transactions",
                column: "ImportBatchID");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_AccountID_CreatedDateTime",
                table: "ImportBatches",
                columns: new[] { "AccountID", "CreatedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ImportBatchSID",
                table: "ImportBatches",
                column: "ImportBatchSID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ImportProfileID",
                table: "ImportBatches",
                column: "ImportProfileID");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfiles_ImportProfileSID",
                table: "ImportProfiles",
                column: "ImportProfileSID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfiles_Status_Name",
                table: "ImportProfiles",
                columns: new[] { "Status", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "ImportProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ImportBatchID",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ImportBatchID",
                table: "Transactions");
        }
    }
}
