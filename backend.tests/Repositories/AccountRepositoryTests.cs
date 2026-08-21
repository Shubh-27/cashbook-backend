/*
 * ====================================================================================================
 * LAYER UNDER TEST: REPOSITORY LAYER (AccountRepository)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * Repositories are tested as "Integration Tests" against a real, temporary on-disk SQLite database
 * rather than using fake in-memory mocks (like Mock<AppDbContext>).
 *
 * WHY THIS APPROACH?
 * 1. Repositories write LINQ queries that EF Core translates into SQLite SQL commands, table constraints,
 *    indexes, and database views (e.g. vw_accounts_list). A mocked database cannot test whether the
 *    generated SQL is valid or whether SQLite triggers, defaults, or foreign keys behave correctly.
 * 2. By creating a throwaway SQLite file for each test fixture ("Test Isolation"), we guarantee tests
 *    never pollute each other's data and never touch production or development databases.
 * ====================================================================================================
 */

using backend.common;
using backend.model.DbModels;
using backend.model.RequestModels;
using backend.service.Repository.Implementations;
using backend.tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static backend.common.Constants;

namespace backend.tests.Repositories
{
    /// <summary>
    /// Integration tests for AccountRepository covering CRUD operations, soft deletion, and view searching.
    /// </summary>
    public class AccountRepositoryTests : IAsyncDisposable
    {
        /*
         * CONCEPT: TEST FIXTURE & TEST ISOLATION
         * ------------------------------------------------------------------------------------------------
         * A "Fixture" is the baseline environment needed for tests to run reliably.
         * "Test Isolation" means each test class runs against its own fresh SQLite database file created on
         * the fly and deleted when tests complete (via IAsyncDisposable). This guarantees that leftover rows
         * from one test never cause another test to fail randomly.
         */
        private readonly SqliteTestDatabase _testDb;

        public AccountRepositoryTests()
        {
            // Spins up a clean, migrated SQLite database file in the OS temp directory
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: true).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            // Cleans up connection pools and deletes the temporary SQLite file
            await _testDb.DisposeAsync();
        }

        /*
         * SCENARIO PROTECTED:
         * When a user creates a new bank account (e.g., "Checking Account" at "Chase Bank"),
         * the system must generate a unique AccountSID, save all details, set its status to Active (1),
         * and return a response model so the frontend can immediately show the new account.
         */
        [Fact]
        public async Task AddAccount_ValidRequest_InsertsActiveAccountAndReturnsResponse()
        {
            /*
             * CONCEPT: ARRANGE - ACT - ASSERT (AAA Pattern)
             * --------------------------------------------------------------------------------------------
             * The gold standard for structuring automated tests into three distinct phases:
             * 1. Arrange: Set up preconditions, test data, and dependencies.
             * 2. Act: Execute the single method or action being tested.
             * 3. Assert: Verify the outcome (return values, database state, side effects).
             */

            // Arrange: Prepare a repository instance and a request model
            using var uow = _testDb.CreateUnitOfWork();
            var repository = new AccountRepository(uow);

            var request = new AccountRequestModel
            {
                AccountName = "Primary Checking",
                BankName = "JPMorgan Chase",
                AccountNumber = "1234567890"
            };

            // Act: Call the repository method to add the account
            var response = await repository.AddAccount(request, userId: 10);

            // Assert: Verify the returned response object
            Assert.NotNull(response);
            Assert.False(string.IsNullOrWhiteSpace(response.AccountSID));
            Assert.Equal("Primary Checking", response.AccountName);
            Assert.Equal("JPMorgan Chase", response.BankName);
            Assert.Equal(1234567890L, response.AccountNumber);
            Assert.Equal(StatusType.Active, response.Status);

            // Assert: Verify the row was actually persisted to the database
            using var verifyContext = _testDb.CreateContext();
            var accountInDb = await verifyContext.Accounts.FirstOrDefaultAsync(a => a.AccountSID == response.AccountSID);
            Assert.NotNull(accountInDb);
            Assert.Equal("Primary Checking", accountInDb.AccountName);
            Assert.Equal((int)StatusType.Active, accountInDb.Status);
            Assert.Equal(10, accountInDb.CreatedByUserID);
        }

        /*
         * SCENARIO PROTECTED:
         * When a user renames an account or changes their account number, the updates must be saved
         * to the database and the LastModifiedDateTime must be updated so audit tracking reflects the change.
         */
        [Fact]
        public async Task UpdateAccount_ExistingAccount_UpdatesFieldsAndReturnsUpdatedResponse()
        {
            // Arrange: Seed an existing account
            string accountSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account
                {
                    AccountSID = accountSid,
                    AccountName = "Old Account Name",
                    BankName = "Old Bank",
                    AccountNumber = 11112222L,
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new AccountRepository(uow);

            var updateRequest = new AccountRequestModel
            {
                AccountName = "Updated Savings Name",
                BankName = "Wells Fargo",
                AccountNumber = "99998888"
            };

            // Act: Update the existing account
            var response = await repository.UpdateAccount(accountSid, updateRequest, userId: 42);

            // Assert: Response contains updated information
            Assert.NotNull(response);
            Assert.Equal("Updated Savings Name", response.AccountName);
            Assert.Equal("Wells Fargo", response.BankName);
            Assert.Equal(99998888L, response.AccountNumber);

            // Assert: Direct database check confirms the modifications were written
            using var verifyContext = _testDb.CreateContext();
            var updatedInDb = await verifyContext.Accounts.FirstAsync(a => a.AccountSID == accountSid);
            Assert.Equal("Updated Savings Name", updatedInDb.AccountName);
            Assert.Equal("Wells Fargo", updatedInDb.BankName);
            Assert.Equal(42, updatedInDb.LastModifiedByUserID);
        }

        /*
         * SCENARIO PROTECTED:
         * If an API client attempts to update an account SID that does not exist (or was deleted),
         * the repository must safely return null instead of throwing an unhandled NullReferenceException.
         */
        [Fact]
        public async Task UpdateAccount_NonExistentAccount_ReturnsNull()
        {
            // Arrange
            using var uow = _testDb.CreateUnitOfWork();
            var repository = new AccountRepository(uow);

            var updateRequest = new AccountRequestModel
            {
                AccountName = "Non-existent Account"
            };

            // Act
            var result = await repository.UpdateAccount("non-existent-sid", updateRequest);

            // Assert: Gracefully returns null
            Assert.Null(result);
        }

        /*
         * SCENARIO PROTECTED:
         * Financial systems avoid hard-deleting account rows to preserve transaction history and referential
         * integrity. When an account is deleted, the repository must perform a "Soft Delete" by changing
         * Status to Delete (3), ensuring the record remains for history but is excluded from active views.
         */
        [Fact]
        public async Task DeleteAccount_ExistingAccount_MarksStatusAsDeleteAndReturnsTrue()
        {
            /*
             * CONCEPT: SOFT DELETION
             * --------------------------------------------------------------------------------------------
             * Instead of issuing a SQL "DELETE FROM Accounts", we update the record's Status column to 3 (Delete).
             * This test ensures our repository executes this business requirement correctly.
             */

            // Arrange: Seed an active account
            string accountSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account
                {
                    AccountSID = accountSid,
                    AccountName = "Account To Delete",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new AccountRepository(uow);

            // Act
            bool deleteResult = await repository.DeleteAccount(accountSid);

            // Assert: Returned true indicating success
            Assert.True(deleteResult);

            // Assert: In the database, the row still exists but its status is now Delete (3)
            using var verifyContext = _testDb.CreateContext();
            var deletedAccount = await verifyContext.Accounts.FirstOrDefaultAsync(a => a.AccountSID == accountSid);
            Assert.NotNull(deletedAccount);
            Assert.Equal((int)StatusType.Delete, deletedAccount.Status);
        }

        /*
         * SCENARIO PROTECTED:
         * If a user tries to delete an account ID that does not exist in the database,
         * the method must return false so the controller can return a 404 Not Found error.
         */
        [Fact]
        public async Task DeleteAccount_NonExistentAccount_ReturnsFalse()
        {
            // Arrange
            using var uow = _testDb.CreateUnitOfWork();
            var repository = new AccountRepository(uow);

            // Act
            bool result = await repository.DeleteAccount("unknown-sid-999");

            // Assert
            Assert.False(result);
        }

        /*
         * SCENARIO PROTECTED:
         * When the user types into the search bar on the Accounts page, the Search method
         * must query the SQL view (vw_accounts_list) and filter by account name or bank name,
         * returning only matching records with accurate pagination metadata.
         */
        [Fact]
        public async Task Search_WithSearchKeyword_ReturnsMatchingAccountsFromView()
        {
            // Arrange: Seed multiple accounts
            using (var seedContext = _testDb.CreateContext())
            {
                await seedContext.Accounts.AddRangeAsync(
                    new Account { AccountSID = "acc-1", AccountName = "Citibank Checking", BankName = "Citigroup", Status = (int)StatusType.Active },
                    new Account { AccountSID = "acc-2", AccountName = "Chase Savings", BankName = "JPMorgan", Status = (int)StatusType.Active },
                    new Account { AccountSID = "acc-3", AccountName = "Credit Card", BankName = "Citigroup", Status = (int)StatusType.Active }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new AccountRepository(uow);

            var searchRequest = new SearchRequestModel
            {
                Search = "citi", // Should match "Citibank Checking" and "Citigroup"
                Page = 1,
                PageSize = 10
            };

            // Act: Perform search
            var searchResult = await repository.Search(searchRequest);

            // Assert: Found matching accounts in the view
            Assert.NotNull(searchResult);
            Assert.Equal(2, searchResult.TotalCount);
            Assert.Equal(2, searchResult.Data.Count);
            Assert.All(searchResult.Data, a => 
                Assert.True(a.AccountName!.Contains("Citi", StringComparison.OrdinalIgnoreCase) || 
                            a.BankName!.Contains("Citi", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
