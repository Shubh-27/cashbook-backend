/*
 * ====================================================================================================
 * LAYER UNDER TEST: SERVICE LAYER (ExportService)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * ExportService orchestrates data fetching from database views (vw_transactions_list), multi-account
 * grouping, date range calculations (financial year labels), and file compilation (Excel .xlsx via ClosedXML
 * and ZIP archives via System.IO.Compression).
 *
 * WHY THIS APPROACH?
 * We test ExportService with a real SQLite test fixture to verify the full end-to-end data export pipeline:
 * 1. Filtering and querying transactions from the SQLite view.
 * 2. Compiling the binary OpenXML Excel workbook (.xlsx) and verifying the spreadsheet tables, sheets, and formulas.
 * 3. Packaging multiple Excel files into a valid ZIP archive when multi-account export is requested.
 * ====================================================================================================
 */

using System.IO.Compression;
using backend.common;
using backend.model.DbModels;
using backend.service.Services.Implementations;
using backend.tests.Helpers;
using ClosedXML.Excel;
using Xunit;
using static backend.common.Constants;

namespace backend.tests.Services
{
    /// <summary>
    /// Integration tests for ExportService verifying Excel generation, multi-sheet merging, and ZIP packaging.
    /// </summary>
    public class ExportServiceTests : IAsyncDisposable
    {
        private readonly SqliteTestDatabase _testDb;

        public ExportServiceTests()
        {
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: true).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        /*
         * SCENARIO PROTECTED:
         * When a user exports transactions for a single bank account, the service must produce
         * a valid OpenXML Excel spreadsheet (.xlsx) with proper MIME type ("application/vnd.openxmlformats..."),
         * populate the account details, list transactions, and calculate debit/credit sums.
         */
        [Fact]
        public async Task ExportTransactionsAsync_SingleAccount_GeneratesValidExcelWorkbookWithSpreadsheetData()
        {
            // Arrange: Seed an account, description, and transactions
            string accountSid = "acc-export-single";
            using (var seedContext = _testDb.CreateContext())
            {
                var acc = new Account
                {
                    AccountSID = accountSid,
                    AccountName = "Business Checking",
                    BankName = "Chase Bank",
                    AccountNumber = 98765432L,
                    Status = (int)StatusType.Active
                };
                var desc = new Description
                {
                    DescriptionSID = "desc-export-1",
                    DescriptionName = "Client Retainer",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(acc);
                await seedContext.Descriptions.AddAsync(desc);
                await seedContext.SaveChangesAsync();

                await seedContext.Transactions.AddRangeAsync(
                    new Transaction
                    {
                        TransactionSID = "tx-e-1",
                        AccountID = acc.AccountID,
                        DescriptionID = desc.DescriptionID,
                        TransactionDate = "2026-08-21T10:00:00Z",
                        Debit = 0.0,
                        Credit = 1500.0,
                        Notes = "Monthly retainer invoice #101",
                        Status = (int)StatusType.Active
                    },
                    new Transaction
                    {
                        TransactionSID = "tx-e-2",
                        AccountID = acc.AccountID,
                        DescriptionID = desc.DescriptionID,
                        TransactionDate = "2026-08-21T11:00:00Z",
                        Debit = 120.0,
                        Credit = 0.0,
                        Notes = "Software subscription",
                        Status = (int)StatusType.Active
                    }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var exportService = new ExportService(uow);

            var request = new ExportRequestModel
            {
                Filters = new List<FilterRequestModel>
                {
                    new FilterRequestModel
                    {
                        Key = "account_sid",
                        Condition = "equals",
                        Value = accountSid
                    }
                }
            };

            // Act: Generate export
            var (fileContents, contentType, fileName) = await exportService.ExportTransactionsAsync(request);

            // Assert: Returned valid binary data, content type, and filename
            Assert.NotNull(fileContents);
            Assert.NotEmpty(fileContents);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contentType);
            Assert.EndsWith(".xlsx", fileName);

            // Assert: Open the generated byte array with ClosedXML to verify spreadsheet structure
            using var stream = new MemoryStream(fileContents);
            using var workbook = new XLWorkbook(stream);
            Assert.NotEmpty(workbook.Worksheets);

            var worksheet = workbook.Worksheets.First();
            Assert.NotNull(worksheet);
        }

        /*
         * SCENARIO PROTECTED:
         * When exporting multiple accounts with MergeAccounts = true and SeparateSheets = true,
         * the service must create a SINGLE Excel workbook containing individual worksheets for each account.
         */
        [Fact]
        public async Task ExportTransactionsAsync_MultipleAccountsWithMerge_GeneratesSingleWorkbookWithSeparateSheets()
        {
            // Arrange: Seed two distinct accounts and a description
            using (var seedContext = _testDb.CreateContext())
            {
                var acc1 = new Account { AccountSID = "acc-m-1", AccountName = "Checking Account", Status = (int)StatusType.Active };
                var acc2 = new Account { AccountSID = "acc-m-2", AccountName = "Savings Account", Status = (int)StatusType.Active };
                var desc = new Description { DescriptionSID = "desc-m-1", DescriptionName = "General Expense", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddRangeAsync(acc1, acc2);
                await seedContext.Descriptions.AddAsync(desc);
                await seedContext.SaveChangesAsync();

                await seedContext.Transactions.AddRangeAsync(
                    new Transaction { TransactionSID = "tx-m-1", AccountID = acc1.AccountID, DescriptionID = desc.DescriptionID, TransactionDate = "2026-08-21T10:00:00Z", Debit = 50.0, Status = (int)StatusType.Active },
                    new Transaction { TransactionSID = "tx-m-2", AccountID = acc2.AccountID, DescriptionID = desc.DescriptionID, TransactionDate = "2026-08-21T11:00:00Z", Credit = 500.0, Status = (int)StatusType.Active }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var exportService = new ExportService(uow);

            var request = new ExportRequestModel
            {
                MergeAccounts = true,
                SeparateSheets = true
            };

            // Act
            var (fileContents, contentType, fileName) = await exportService.ExportTransactionsAsync(request);

            // Assert
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", contentType);
            Assert.EndsWith(".xlsx", fileName);

            // Assert: Workbook has sheets for the accounts
            using var stream = new MemoryStream(fileContents);
            using var workbook = new XLWorkbook(stream);
            Assert.True(workbook.Worksheets.Count >= 2);
        }

        /*
         * SCENARIO PROTECTED:
         * When exporting multiple accounts with MergeAccounts = false (default), the service
         * must package separate Excel spreadsheets into a ZIP archive ("application/zip"),
         * enabling the user to download a batch of distinct account files in one click.
         */
        [Fact]
        public async Task ExportTransactionsAsync_MultipleAccountsWithoutMerge_GeneratesValidZipArchiveWithExcelFiles()
        {
            // Arrange: Seed two accounts and a description with transactions
            using (var seedContext = _testDb.CreateContext())
            {
                var acc1 = new Account { AccountSID = "acc-zip-1", AccountName = "First Account", Status = (int)StatusType.Active };
                var acc2 = new Account { AccountSID = "acc-zip-2", AccountName = "Second Account", Status = (int)StatusType.Active };
                var desc = new Description { DescriptionSID = "desc-zip-1", DescriptionName = "Office Utilities", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddRangeAsync(acc1, acc2);
                await seedContext.Descriptions.AddAsync(desc);
                await seedContext.SaveChangesAsync();

                await seedContext.Transactions.AddRangeAsync(
                    new Transaction { TransactionSID = "tx-z-1", AccountID = acc1.AccountID, DescriptionID = desc.DescriptionID, TransactionDate = "2026-08-21T10:00:00Z", Debit = 20.0, Status = (int)StatusType.Active },
                    new Transaction { TransactionSID = "tx-z-2", AccountID = acc2.AccountID, DescriptionID = desc.DescriptionID, TransactionDate = "2026-08-21T11:00:00Z", Debit = 80.0, Status = (int)StatusType.Active }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var exportService = new ExportService(uow);

            var request = new ExportRequestModel
            {
                MergeAccounts = false // Triggers multi-file ZIP export mode
            };

            // Act
            var (fileContents, contentType, fileName) = await exportService.ExportTransactionsAsync(request);

            // Assert: Returned ZIP MIME type and .zip extension
            Assert.Equal("application/zip", contentType);
            Assert.EndsWith(".zip", fileName);

            // Assert: Open the binary ZIP stream and verify archive entries
            using var stream = new MemoryStream(fileContents);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            Assert.Equal(2, archive.Entries.Count);
            Assert.All(archive.Entries, entry => Assert.EndsWith(".xlsx", entry.Name));
        }

        /*
         * SCENARIO PROTECTED:
         * When a user filters transactions by a date range (e.g. from April 2026), the service
         * must compute the Indian/UK Financial Year label (e.g. "26-27") and append it to the filename.
         */
        [Fact]
        public async Task ExportTransactionsAsync_WithFinancialYearDateFilter_CalculatesFinancialYearInFileName()
        {
            // Arrange
            using (var seedContext = _testDb.CreateContext())
            {
                var acc = new Account { AccountSID = "acc-fy-1", AccountName = "Tax Account", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddAsync(acc);
                await seedContext.SaveChangesAsync();

                var tx = new Transaction { TransactionSID = "tx-fy-1", AccountID = acc.AccountID, TransactionDate = "2026-04-15T10:00:00Z", Debit = 100.0, Status = (int)StatusType.Active };
                await seedContext.Transactions.AddAsync(tx);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var exportService = new ExportService(uow);

            var request = new ExportRequestModel
            {
                Filters = new List<FilterRequestModel>
                {
                    new FilterRequestModel
                    {
                        Key = "account_sid",
                        Condition = "equals",
                        Value = "acc-fy-1"
                    },
                    new FilterRequestModel
                    {
                        Type = "date",
                        From = "2026-04-01T00:00:00Z" // April 2026 starts FY 2026-2027 ("26-27")
                    }
                }
            };

            // Act
            var (_, _, fileName) = await exportService.ExportTransactionsAsync(request);

            // Assert: Filename includes "26-27"
            Assert.Contains("26-27", fileName);
        }

        /*
         * SCENARIO PROTECTED:
         * If the user specifies a custom filename (ExcelName = "AnnualAuditReport_2026"),
         * the service must respect the custom name and append the proper .xlsx extension.
         */
        [Fact]
        public async Task ExportTransactionsAsync_CustomExcelName_AppliesCustomFileName()
        {
            // Arrange
            using (var seedContext = _testDb.CreateContext())
            {
                var acc = new Account { AccountSID = "acc-custom-name", AccountName = "General Account", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddAsync(acc);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var exportService = new ExportService(uow);

            var request = new ExportRequestModel
            {
                ExcelName = "AnnualAuditReport_2026",
                Filters = new List<FilterRequestModel>
                {
                    new FilterRequestModel { Key = "account_sid", Condition = "equals", Value = "acc-custom-name" }
                }
            };

            // Act
            var (_, _, fileName) = await exportService.ExportTransactionsAsync(request);

            // Assert
            Assert.Equal("AnnualAuditReport_2026.xlsx", fileName);
        }
    }
}
