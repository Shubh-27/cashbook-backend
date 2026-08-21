/*
 * ====================================================================================================
 * LAYER UNDER TEST: REPOSITORY LAYER (TransactionRepository)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * TransactionRepository is tested as an Integration Test against a temporary on-disk SQLite database.
 *
 * WHY THIS APPROACH?
 * Transactions are the core financial engine of the application. They coordinate multiple database tables
 * (Accounts, Descriptions, Transactions) inside UnitOfWork database transactions, use foreign key links,
 * evaluate complex status filtering, and query SQL views (vw_transactions_list). Real SQLite tests verify
 * concurrency lock retries, rollback semantics on partial failure, and relational integrity.
 * ====================================================================================================
 */

using backend.common;
using backend.model.DbModels;
using backend.model.RequestModels;
using backend.service.Repository.Implementations;
using backend.service.UnitOfWork;
using backend.tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static backend.common.Constants;

namespace backend.tests.Repositories
{
    /// <summary>
    /// Integration tests for TransactionRepository covering concurrency, rollbacks, CRUD, and view searching.
    /// </summary>
    public class TransactionRepositoryTests : IAsyncDisposable
    {
        private readonly SqliteTestDatabase _testDb;

        public TransactionRepositoryTests()
        {
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: true).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        #region Concurrency & Rollback Regression Tests
        /*
         * SCENARIO PROTECTED:
         * If two users (or two browser tabs) save a transaction at the exact same moment with the same new
         * description (e.g., "Shared Grocery Store"), the database unique constraint and repository retry logic
         * must handle the race condition cleanly: creating only ONE description row and linking both transactions
         * to it, without throwing a 500 duplicate key error.
         */
        [Fact]
        public async Task AddTransaction_ConcurrentRequestsWithSameNewDescription_CreatesSingleDescriptionRowAndBothReferenceIt()
        {
            // Arrange: Seed an active account
            var accountSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account
                {
                    AccountSID = accountSid,
                    AccountName = "Concurrency Test Account",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();
            }

            const string newDescriptionName = "Shared Grocery Store";

            var req1 = new TransactionRequestModel
            {
                AccountSID = accountSid,
                DescriptionName = newDescriptionName,
                Debit = 25.50,
                TransactionDate = "2026-08-21T10:00:00Z"
            };

            var req2 = new TransactionRequestModel
            {
                AccountSID = accountSid,
                DescriptionName = newDescriptionName,
                Credit = 40.00,
                TransactionDate = "2026-08-21T11:00:00Z"
            };

            // Act: Fire two concurrent AddTransaction calls with separate UnitOfWork instances
            var task1 = Task.Run(async () =>
            {
                using var uow = _testDb.CreateUnitOfWork();
                var repo = new TransactionRepository(uow);
                return await repo.AddTransaction(req1);
            });

            var task2 = Task.Run(async () =>
            {
                using var uow = _testDb.CreateUnitOfWork();
                var repo = new TransactionRepository(uow);
                return await repo.AddTransaction(req2);
            });

            var results = await Task.WhenAll(task1, task2);

            // Assert: Both calls succeeded and returned valid response models
            Assert.NotNull(results[0]);
            Assert.NotNull(results[1]);

            using var verifyContext = _testDb.CreateContext();

            // Assert: Only ONE description row was created for the name
            var matchingDescriptions = await verifyContext.Descriptions
                .Where(d => d.DescriptionName == newDescriptionName)
                .ToListAsync();

            Assert.Single(matchingDescriptions);
            var singleDesc = matchingDescriptions[0];

            // Assert: Both transactions reference that exact same DescriptionID
            var transactions = await verifyContext.Transactions.ToListAsync();
            Assert.Equal(2, transactions.Count);
            Assert.All(transactions, t => Assert.Equal(singleDesc.DescriptionID, t.DescriptionID));
        }

        /*
         * SCENARIO PROTECTED:
         * If creating a transaction fails halfway through (e.g. after inserting a description but before
         * completing the transaction record), the database transaction must roll back completely so that
         * no orphaned, unused description is left cluttering the user's category list.
         */
        [Fact]
        public async Task AddTransaction_WhenTransactionInsertFails_RollsBackCreatedDescription()
        {
            // Arrange: Seed an active account
            var accountSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account
                {
                    AccountSID = accountSid,
                    AccountName = "Rollback Test Account",
                    Status = (int)StatusType.Active
                };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();
            }

            const string candidateDescription = "Orphaned Description Candidate";

            // We force the transaction insert step to fail AFTER description creation by passing null! for TransactionDate.
            // In the SQLite schema, Transactions.TransactionDate has a NOT NULL constraint, causing DbUpdateException on SaveAsync.
            var invalidRequest = new TransactionRequestModel
            {
                AccountSID = accountSid,
                DescriptionName = candidateDescription,
                TransactionDate = null!, // Triggers SQLite NOT NULL constraint violation during transaction save
                Debit = 150.00
            };

            using var uow = _testDb.CreateUnitOfWork();
            var repo = new TransactionRepository(uow);

            // Act & Assert: AddTransaction must throw an exception
            await Assert.ThrowsAnyAsync<Exception>(() => repo.AddTransaction(invalidRequest));

            // Assert: The created description must have been rolled back, leaving NO orphaned description row
            using var verifyContext = _testDb.CreateContext();
            var orphanedDesc = await verifyContext.Descriptions
                .FirstOrDefaultAsync(d => d.DescriptionName == candidateDescription);

            Assert.Null(orphanedDesc);

            // Assert: No transaction row was created
            var transactionCount = await verifyContext.Transactions.CountAsync();
            Assert.Equal(0, transactionCount);
        }
        #endregion

        #region Standard CRUD & Query Tests
        /*
         * SCENARIO PROTECTED:
         * When adding a transaction linked to an existing Description by SID, the repository
         * must link the existing Description foreign key rather than creating a duplicate description.
         */
        [Fact]
        public async Task AddTransaction_WithExistingDescription_LinksExistingCategoryAndPersists()
        {
            // Arrange: Seed account and description
            string accountSid = Guid.NewGuid().ToString();
            string descriptionSid = Guid.NewGuid().ToString();

            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account { AccountSID = accountSid, AccountName = "Checking", Status = (int)StatusType.Active };
                var description = new Description { DescriptionSID = descriptionSid, DescriptionName = "Office Supplies", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.Descriptions.AddAsync(description);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new TransactionRepository(uow);

            var request = new TransactionRequestModel
            {
                AccountSID = accountSid,
                DescriptionSID = descriptionSid,
                Debit = 45.00,
                TransactionDate = "2026-08-21T14:00:00Z",
                Notes = "Bought pens and paper"
            };

            // Act
            var response = await repository.AddTransaction(request);

            // Assert: Response contains mapped fields and nested description/account
            Assert.NotNull(response);
            Assert.Equal(45.00, response.Debit);
            Assert.Equal("Bought pens and paper", response.Notes);
            Assert.NotNull(response.Description);
            Assert.Equal("Office Supplies", response.Description.DescriptionName);
            Assert.NotNull(response.Account);
            Assert.Equal("Checking", response.Account.AccountName);

            // Assert: Verify row in database
            using var verifyContext = _testDb.CreateContext();
            var inDb = await verifyContext.Transactions.FirstOrDefaultAsync(t => t.TransactionSID == response.TransactionSID);
            Assert.NotNull(inDb);
            Assert.Equal(45.00, inDb.Debit);
        }

        /*
         * SCENARIO PROTECTED:
         * If a request specifies an AccountSID that does not exist in the database,
         * AddTransaction must reject the operation by returning null before starting database work.
         */
        [Fact]
        public async Task AddTransaction_WhenAccountNotFound_ReturnsNull()
        {
            // Arrange
            using var uow = _testDb.CreateUnitOfWork();
            var repository = new TransactionRepository(uow);

            var request = new TransactionRequestModel
            {
                AccountSID = "non-existent-account-sid",
                Debit = 100.0,
                TransactionDate = "2026-08-21T12:00:00Z"
            };

            // Act
            var response = await repository.AddTransaction(request);

            // Assert
            Assert.Null(response);
        }

        /*
         * SCENARIO PROTECTED:
         * When a user edits a transaction (e.g. adjusts the amount or notes), the update
         * must be committed and LastModifiedDateTime must be updated.
         */
        [Fact]
        public async Task UpdateTransaction_ExistingTransaction_UpdatesValuesAndReturnsResponse()
        {
            // Arrange: Seed account and transaction
            string accountSid = Guid.NewGuid().ToString();
            string transactionSid = Guid.NewGuid().ToString();

            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account { AccountSID = accountSid, AccountName = "Main Account", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();

                var tx = new Transaction
                {
                    TransactionSID = transactionSid,
                    AccountID = account.AccountID,
                    TransactionDate = "2026-08-21T10:00:00Z",
                    Debit = 50.00,
                    Notes = "Initial Note",
                    Status = (int)StatusType.Active
                };
                await seedContext.Transactions.AddAsync(tx);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new TransactionRepository(uow);

            var updateRequest = new TransactionRequestModel
            {
                AccountSID = accountSid,
                Debit = 75.50,
                TransactionDate = "2026-08-21T15:00:00Z",
                Notes = "Corrected Amount"
            };

            // Act
            var response = await repository.UpdateTransaction(transactionSid, updateRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(75.50, response.Debit);
            Assert.Equal("Corrected Amount", response.Notes);

            // Assert: Database state reflects changes
            using var verifyContext = _testDb.CreateContext();
            var updatedInDb = await verifyContext.Transactions.FirstAsync(t => t.TransactionSID == transactionSid);
            Assert.Equal(75.50, updatedInDb.Debit);
            Assert.Equal("Corrected Amount", updatedInDb.Notes);
        }

        /*
         * SCENARIO PROTECTED:
         * When a user deletes a transaction, the operation must only succeed if the specified AccountSID
         * actually owns the transaction. This prevents accidental cross-account deletion or data tampering.
         */
        [Fact]
        public async Task DeleteTransaction_ExistingTransactionWithMatchingAccount_MarksStatusAsDeleteAndReturnsTrue()
        {
            // Arrange: Seed account and transaction
            string accountSid = Guid.NewGuid().ToString();
            string transactionSid = Guid.NewGuid().ToString();

            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account { AccountSID = accountSid, AccountName = "Checking Account", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddAsync(account);
                await seedContext.SaveChangesAsync();

                var tx = new Transaction
                {
                    TransactionSID = transactionSid,
                    AccountID = account.AccountID,
                    TransactionDate = "2026-08-21T10:00:00Z",
                    Debit = 10.00,
                    Status = (int)StatusType.Active
                };
                await seedContext.Transactions.AddAsync(tx);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new TransactionRepository(uow);

            // Act: Delete transaction matching accountSID
            bool result = await repository.DeleteTransaction(transactionSid, accountSid);

            // Assert
            Assert.True(result);

            // Assert: Status in database is now Delete (3)
            using var verifyContext = _testDb.CreateContext();
            var inDb = await verifyContext.Transactions.FirstAsync(t => t.TransactionSID == transactionSid);
            Assert.Equal((int)StatusType.Delete, inDb.Status);
        }

        /*
         * SCENARIO PROTECTED:
         * If someone attempts to delete a transaction while passing the wrong AccountSID,
         * the repository must refuse the deletion and leave the transaction Active.
         */
        [Fact]
        public async Task DeleteTransaction_WrongAccountSID_ReturnsFalseAndLeavesTransactionActive()
        {
            // Arrange: Seed account and transaction
            string accountSid = Guid.NewGuid().ToString();
            string wrongAccountSid = Guid.NewGuid().ToString();
            string transactionSid = Guid.NewGuid().ToString();

            using (var seedContext = _testDb.CreateContext())
            {
                var account = new Account { AccountSID = accountSid, AccountName = "Real Owner", Status = (int)StatusType.Active };
                var wrongAccount = new Account { AccountSID = wrongAccountSid, AccountName = "Wrong Account", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddRangeAsync(account, wrongAccount);
                await seedContext.SaveChangesAsync();

                var tx = new Transaction
                {
                    TransactionSID = transactionSid,
                    AccountID = account.AccountID,
                    TransactionDate = "2026-08-21T10:00:00Z",
                    Debit = 20.00,
                    Status = (int)StatusType.Active
                };
                await seedContext.Transactions.AddAsync(tx);
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new TransactionRepository(uow);

            // Act: Attempt delete with wrong account SID
            bool result = await repository.DeleteTransaction(transactionSid, wrongAccountSid);

            // Assert: Must return false
            Assert.False(result);

            // Assert: Transaction remains Active (1) in database
            using var verifyContext = _testDb.CreateContext();
            var inDb = await verifyContext.Transactions.FirstAsync(t => t.TransactionSID == transactionSid);
            Assert.Equal((int)StatusType.Active, inDb.Status);
        }

        /*
         * SCENARIO PROTECTED:
         * When searching transactions in the transaction feed, the query must search across
         * notes, account names, and description names in the SQL view (vw_transactions_list).
         */
        [Fact]
        public async Task Search_WithKeyword_FiltersTransactionsViewCorrectly()
        {
            // Arrange: Seed accounts, descriptions, and transactions
            using (var seedContext = _testDb.CreateContext())
            {
                var acc = new Account { AccountSID = "acc-search", AccountName = "Business Checking", Status = (int)StatusType.Active };
                var desc1 = new Description { DescriptionSID = "desc-search-1", DescriptionName = "Client Lunch", Status = (int)StatusType.Active };
                var desc2 = new Description { DescriptionSID = "desc-search-2", DescriptionName = "Flight Ticket", Status = (int)StatusType.Active };
                await seedContext.Accounts.AddAsync(acc);
                await seedContext.Descriptions.AddRangeAsync(desc1, desc2);
                await seedContext.SaveChangesAsync();

                await seedContext.Transactions.AddRangeAsync(
                    new Transaction
                    {
                        TransactionSID = "tx-s-1",
                        AccountID = acc.AccountID,
                        DescriptionID = desc1.DescriptionID,
                        TransactionDate = "2026-08-21T10:00:00Z",
                        Debit = 80.0,
                        Notes = "Meeting with Sarah",
                        Status = (int)StatusType.Active
                    },
                    new Transaction
                    {
                        TransactionSID = "tx-s-2",
                        AccountID = acc.AccountID,
                        DescriptionID = desc2.DescriptionID,
                        TransactionDate = "2026-08-21T11:00:00Z",
                        Debit = 450.0,
                        Notes = "Conference travel",
                        Status = (int)StatusType.Active
                    }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new TransactionRepository(uow);

            var searchRequest = new SearchRequestModel
            {
                Search = "Sarah",
                Page = 1,
                PageSize = 10
            };

            // Act
            var searchResult = await repository.Search(searchRequest);

            // Assert: Found the transaction with "Sarah" in notes
            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.TotalCount);
            Assert.Equal("tx-s-1", searchResult.Data[0].TransactionSID);
        }
        #endregion
    }
}
