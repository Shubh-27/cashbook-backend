using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountSID = table.Column<string>(type: "TEXT", nullable: true),
                    AccountName = table.Column<string>(type: "TEXT", nullable: true),
                    AccountNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    BankName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    LastModifiedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastModifiedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountID);
                });

            migrationBuilder.CreateTable(
                name: "Descriptions",
                columns: table => new
                {
                    DescriptionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DescriptionSID = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    LastModifiedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastModifiedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Descriptions", x => x.DescriptionID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserSID = table.Column<string>(type: "TEXT", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    MiddleName = table.Column<string>(type: "TEXT", nullable: true),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastModifiedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    TransactionID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionSID = table.Column<string>(type: "TEXT", nullable: true),
                    TransactionDate = table.Column<string>(type: "TEXT", nullable: false),
                    DescriptionID = table.Column<int>(type: "INTEGER", nullable: true),
                    Debit = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    Credit = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    Balance = table.Column<double>(type: "REAL", nullable: true, defaultValue: 0.0),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    AccountID = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    LastModifiedDateTime = table.Column<string>(type: "TEXT", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastModifiedByUserID = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.TransactionID);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountID",
                        column: x => x.AccountID,
                        principalTable: "Accounts",
                        principalColumn: "AccountID");
                    table.ForeignKey(
                        name: "FK_Transactions_Descriptions_DescriptionID",
                        column: x => x.DescriptionID,
                        principalTable: "Descriptions",
                        principalColumn: "DescriptionID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountID",
                table: "Accounts",
                column: "AccountID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountSID",
                table: "Accounts",
                column: "AccountSID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Descriptions_DescriptionID",
                table: "Descriptions",
                column: "DescriptionID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Descriptions_DescriptionSID",
                table: "Descriptions",
                column: "DescriptionSID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountID",
                table: "Transactions",
                column: "AccountID");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_DescriptionID",
                table: "Transactions",
                column: "DescriptionID");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionID",
                table: "Transactions",
                column: "TransactionID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionSID",
                table: "Transactions",
                column: "TransactionSID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserID",
                table: "Users",
                column: "UserID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserSID",
                table: "Users",
                column: "UserSID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Descriptions");
        }
    }
}
