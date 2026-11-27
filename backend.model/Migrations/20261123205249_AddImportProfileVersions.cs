using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class AddImportProfileVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RowHash",
                table: "Transactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentVersionID",
                table: "ImportProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "ImportBatches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileVersionID",
                table: "ImportBatches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportProfileVersions",
                columns: table => new
                {
                    ImportProfileVersionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProfileID = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    WorksheetName = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderSignature = table.Column<string>(type: "TEXT", nullable: false),
                    HeaderRowIndexHint = table.Column<int>(type: "INTEGER", nullable: false),
                    HeaderCellCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DateColumnIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    DateFormat = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionCol = table.Column<int>(type: "INTEGER", nullable: true),
                    DebitCol = table.Column<int>(type: "INTEGER", nullable: true),
                    CreditCol = table.Column<int>(type: "INTEGER", nullable: true),
                    AmountCol = table.Column<int>(type: "INTEGER", nullable: true),
                    BalanceCol = table.Column<int>(type: "INTEGER", nullable: true),
                    NotesCol = table.Column<int>(type: "INTEGER", nullable: true),
                    FooterMarker = table.Column<string>(type: "TEXT", nullable: true),
                    FooterSignature = table.Column<string>(type: "TEXT", nullable: true),
                    SecondaryTableMarker = table.Column<string>(type: "TEXT", nullable: true),
                    SampleJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileVersions", x => x.ImportProfileVersionID);
                    table.ForeignKey(
                        name: "FK_ImportProfileVersions_ImportProfiles_ProfileID",
                        column: x => x.ProfileID,
                        principalTable: "ImportProfiles",
                        principalColumn: "ImportProfileID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountID_RowHash",
                table: "Transactions",
                columns: new[] { "AccountID", "RowHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileVersions_ProfileID_VersionNumber",
                table: "ImportProfileVersions",
                columns: new[] { "ProfileID", "VersionNumber" },
                unique: true);


            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_ImportBatches_AccountID_FileHash_Active"
                ON "ImportBatches" ("AccountID", "FileHash")
                WHERE "UndoneDateTime" IS NULL AND "FileHash" IS NOT NULL;

                CREATE TRIGGER "TR_ImportProfileVersions_NoUpdate"
                BEFORE UPDATE ON "ImportProfileVersions"
                BEGIN
                    SELECT RAISE(ABORT, 'Import profile versions are immutable.');
                END;

                CREATE TRIGGER "TR_ImportProfileVersions_NoDelete"
                BEFORE DELETE ON "ImportProfileVersions"
                BEGIN
                    SELECT RAISE(ABORT, 'Import profile versions are immutable.');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("SQLite rollback is intentionally blocked because dropping the added columns requires rebuilding existing tables. Restore a verified pre-migration database backup instead.");
        }
    }
}
