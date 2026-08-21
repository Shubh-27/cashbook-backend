using backend.common;
using backend.model.DbModels;
using static backend.common.Constants;
using backend.tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.tests.Common
{
    public class QueryExtensionsTests : IAsyncDisposable
    {
        private readonly SqliteTestDatabase _testDb;

        public QueryExtensionsTests()
        {
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: true).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        private async Task SeedTransactionsWithNullsAsync()
        {
            using var context = _testDb.CreateContext();

            var account = new Account
            {
                AccountSID = "acc-query-test",
                AccountName = "Query Test Account",
                Status = (int)StatusType.Active
            };
            await context.Accounts.AddAsync(account);
            await context.SaveChangesAsync();

            var transactions = new List<Transaction>
            {
                new Transaction
                {
                    TransactionSID = "tx-1",
                    TransactionDate = "2026-08-21T10:00:00Z",
                    AccountID = account.AccountID,
                    Debit = 100.0,
                    Credit = null,
                    Status = (int)StatusType.Active
                },
                new Transaction
                {
                    TransactionSID = "tx-2",
                    TransactionDate = "2026-08-21T11:00:00Z",
                    AccountID = account.AccountID,
                    Debit = null,
                    Credit = 200.0,
                    Status = (int)StatusType.Active
                },
                new Transaction
                {
                    TransactionSID = "tx-3",
                    TransactionDate = "2026-08-21T12:00:00Z",
                    AccountID = account.AccountID,
                    Debit = 50.0,
                    Credit = null,
                    Status = (int)StatusType.Active
                },
                new Transaction
                {
                    TransactionSID = "tx-4",
                    TransactionDate = "2026-08-21T13:00:00Z",
                    AccountID = account.AccountID,
                    Debit = null,
                    Credit = 80.0,
                    Status = (int)StatusType.Active
                }
            };

            await context.Transactions.AddRangeAsync(transactions);
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task ApplyFilters_DebitGreaterThan_HandlesNullsWithoutExceptionAndReturnsMatching()
        {
            await SeedTransactionsWithNullsAsync();

            using var context = _testDb.CreateContext();
            var filters = new List<FilterRequestModel>
            {
                new FilterRequestModel
                {
                    Key = "Debit",
                    Condition = "greater_than",
                    Value = 75.0
                }
            };

            var results = await context.Transactions
                .ApplyFilters(filters)
                .ToListAsync();

            Assert.Single(results);
            Assert.Equal("tx-1", results[0].TransactionSID);
            Assert.Equal(100.0, results[0].Debit);
        }

        [Fact]
        public async Task ApplyFilters_DebitLessThan_HandlesNullsWithoutExceptionAndReturnsMatching()
        {
            await SeedTransactionsWithNullsAsync();

            using var context = _testDb.CreateContext();
            var filters = new List<FilterRequestModel>
            {
                new FilterRequestModel
                {
                    Key = "Debit",
                    Condition = "greater_than",
                    Value = 0.0
                },
                new FilterRequestModel
                {
                    Key = "Debit",
                    Condition = "less_than",
                    Value = 75.0
                }
            };

            var results = await context.Transactions
                .ApplyFilters(filters)
                .ToListAsync();

            Assert.Single(results);
            Assert.Equal("tx-3", results[0].TransactionSID);
            Assert.Equal(50.0, results[0].Debit);
        }

        [Fact]
        public async Task ApplyFilters_CreditGreaterThan_HandlesNullsWithoutExceptionAndReturnsMatching()
        {
            await SeedTransactionsWithNullsAsync();

            using var context = _testDb.CreateContext();
            var filters = new List<FilterRequestModel>
            {
                new FilterRequestModel
                {
                    Key = "Credit",
                    Condition = "greater_than",
                    Value = 150.0
                }
            };

            var results = await context.Transactions
                .ApplyFilters(filters)
                .ToListAsync();

            Assert.Single(results);
            Assert.Equal("tx-2", results[0].TransactionSID);
            Assert.Equal(200.0, results[0].Credit);
        }

        [Fact]
        public async Task ApplyFilters_DebitBetween_HandlesNullsWithoutExceptionAndReturnsMatching()
        {
            await SeedTransactionsWithNullsAsync();

            using var context = _testDb.CreateContext();
            var filters = new List<FilterRequestModel>
            {
                new FilterRequestModel
                {
                    Key = "Debit",
                    Condition = "between",
                    From = 40.0,
                    To = 60.0
                }
            };

            var results = await context.Transactions
                .ApplyFilters(filters)
                .ToListAsync();

            Assert.Single(results);
            Assert.Equal("tx-3", results[0].TransactionSID);
            Assert.Equal(50.0, results[0].Debit);
        }

        [Fact]
        public async Task ApplyFilters_DebitEquals_HandlesNullsWithoutExceptionAndReturnsMatching()
        {
            await SeedTransactionsWithNullsAsync();

            using var context = _testDb.CreateContext();
            var filters = new List<FilterRequestModel>
            {
                new FilterRequestModel
                {
                    Key = "Debit",
                    Condition = "equals",
                    Value = 100.0
                }
            };

            var results = await context.Transactions
                .ApplyFilters(filters)
                .ToListAsync();

            Assert.Single(results);
            Assert.Equal("tx-1", results[0].TransactionSID);
        }

        [Fact]
        public void ApplyFilters_InMemoryListWithNulls_FiltersCorrectly()
        {
            var inMemoryList = new List<Transaction>
            {
                new Transaction { TransactionSID = "mem-1", Debit = 300.0, Credit = 0.0 },
                new Transaction { TransactionSID = "mem-2", Debit = null, Credit = 150.0 },
                new Transaction { TransactionSID = "mem-3", Debit = 50.0, Credit = 0.0 }
            }.AsQueryable();

            var filters = new List<FilterRequestModel>
            {
                new FilterRequestModel
                {
                    Key = "Debit",
                    Condition = "greater_than",
                    Value = 100.0
                }
            };

            var filtered = inMemoryList.ApplyFilters(filters).ToList();

            Assert.Single(filtered);
            Assert.Equal("mem-1", filtered[0].TransactionSID);
        }
    }
}
