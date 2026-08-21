/*
 * ====================================================================================================
 * LAYER UNDER TEST: REPOSITORY LAYER (DescriptionRepository)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * DescriptionRepository is tested as an Integration Test against a temporary on-disk SQLite database.
 *
 * WHY THIS APPROACH?
 * Descriptions are shared across transactions and have unique database constraints (e.g., IX_Descriptions_DescriptionName)
 * as well as database views (vw_descriptions_list). Testing against real SQLite ensures SQL queries,
 * EF Core change tracking, and view mappings work as intended in real database environments.
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
    /// Integration tests for DescriptionRepository covering CRUD, soft-deletion safety, and active listing.
    /// </summary>
    public class DescriptionRepositoryTests : IAsyncDisposable
    {
        private readonly SqliteTestDatabase _testDb;

        public DescriptionRepositoryTests()
        {
            _testDb = SqliteTestDatabase.CreateAsync(applyMigrations: true).GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            await _testDb.DisposeAsync();
        }

        /*
         * SCENARIO PROTECTED:
         * When a user creates a new transaction category/description (e.g. "Monthly Rent"),
         * the repository must generate a DescriptionSID, set Status to Active (1), and persist it
         * to the database so subsequent transactions can link to it.
         */
        [Fact]
        public async Task AddDescription_ValidRequest_InsertsActiveDescriptionAndReturnsResponse()
        {
            // Arrange
            using var uow = _testDb.CreateUnitOfWork();
            var repository = new DescriptionRepository(uow);

            var request = new DescriptionRequestModel
            {
                DescriptionName = "Electricity & Water Utility"
            };

            // Act
            var response = await repository.AddDescription(request);

            // Assert: Returned model is populated
            Assert.NotNull(response);
            Assert.False(string.IsNullOrWhiteSpace(response.DescriptionSID));
            Assert.Equal("Electricity & Water Utility", response.DescriptionName);
            Assert.Equal(StatusType.Active, response.Status);

            // Assert: Database has the row with Active status
            using var verifyContext = _testDb.CreateContext();
            var savedInDb = await verifyContext.Descriptions.FirstOrDefaultAsync(d => d.DescriptionSID == response.DescriptionSID);
            Assert.NotNull(savedInDb);
            Assert.Equal("Electricity & Water Utility", savedInDb.DescriptionName);
            Assert.Equal((int)StatusType.Active, savedInDb.Status);
        }

        /*
         * SCENARIO PROTECTED:
         * When a user edits a description name, the change must be persisted and its LastModifiedDateTime
         * updated so history reflects the rename.
         */
        [Fact]
        public async Task UpdateDescription_ExistingActiveDescription_UpdatesNameAndReturnsResponse()
        {
            // Arrange: Seed an active description
            string descriptionSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                await seedContext.Descriptions.AddAsync(new Description
                {
                    DescriptionSID = descriptionSid,
                    DescriptionName = "Old Grocery Name",
                    Status = (int)StatusType.Active
                });
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new DescriptionRepository(uow);

            var updateRequest = new DescriptionRequestModel
            {
                DescriptionName = "Supermarket & Groceries"
            };

            // Act
            var response = await repository.UpdateDescription(descriptionSid, updateRequest);

            // Assert
            Assert.NotNull(response);
            Assert.Equal("Supermarket & Groceries", response.DescriptionName);

            // Assert: Database state reflects the update
            using var verifyContext = _testDb.CreateContext();
            var updatedDesc = await verifyContext.Descriptions.FirstAsync(d => d.DescriptionSID == descriptionSid);
            Assert.Equal("Supermarket & Groceries", updatedDesc.DescriptionName);
            Assert.NotNull(updatedDesc.LastModifiedDateTime);
        }

        /*
         * SCENARIO PROTECTED:
         * If a user tries to edit a description that was previously soft-deleted, the repository
         * must reject the update by returning null, preventing accidental resurrection of deleted records.
         */
        [Fact]
        public async Task UpdateDescription_SoftDeletedDescription_ReturnsNullAndPreventsResurrection()
        {
            // Arrange: Seed a soft-deleted description (Status = Delete = 3)
            string descriptionSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                await seedContext.Descriptions.AddAsync(new Description
                {
                    DescriptionSID = descriptionSid,
                    DescriptionName = "Deleted Category",
                    Status = (int)StatusType.Delete
                });
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new DescriptionRepository(uow);

            var updateRequest = new DescriptionRequestModel
            {
                DescriptionName = "Attempted Resurrection"
            };

            // Act: Attempt to update the deleted description
            var response = await repository.UpdateDescription(descriptionSid, updateRequest);

            // Assert: Must return null
            Assert.Null(response);
        }

        /*
         * SCENARIO PROTECTED:
         * When a description is deleted, the repository must perform a soft-delete (Status = 3).
         * This preserves historical transactions that linked to this description while removing
         * it from active dropdowns.
         */
        [Fact]
        public async Task DeleteDescription_ExistingDescription_MarksStatusAsDeleteAndReturnsTrue()
        {
            // Arrange: Seed an active description
            string descriptionSid = Guid.NewGuid().ToString();
            using (var seedContext = _testDb.CreateContext())
            {
                await seedContext.Descriptions.AddAsync(new Description
                {
                    DescriptionSID = descriptionSid,
                    DescriptionName = "Obsolete Category",
                    Status = (int)StatusType.Active
                });
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new DescriptionRepository(uow);

            // Act
            bool deleteResult = await repository.DeleteDescription(descriptionSid);

            // Assert
            Assert.True(deleteResult);

            // Assert: Database status is now Delete (3)
            using var verifyContext = _testDb.CreateContext();
            var inDb = await verifyContext.Descriptions.FirstAsync(d => d.DescriptionSID == descriptionSid);
            Assert.Equal((int)StatusType.Delete, inDb.Status);
        }

        /*
         * SCENARIO PROTECTED:
         * When populating dropdown lists in the UI for adding a transaction, the GetDescriptions
         * method must return ONLY active descriptions, filtering out any that were soft-deleted.
         */
        [Fact]
        public async Task GetDescriptions_ReturnsOnlyActiveDescriptionsAndExcludesDeleted()
        {
            // Arrange: Seed two active descriptions and one deleted description
            using (var seedContext = _testDb.CreateContext())
            {
                await seedContext.Descriptions.AddRangeAsync(
                    new Description { DescriptionSID = "desc-1", DescriptionName = "Dining Out", Status = (int)StatusType.Active },
                    new Description { DescriptionSID = "desc-2", DescriptionName = "Salary", Status = (int)StatusType.Active },
                    new Description { DescriptionSID = "desc-3", DescriptionName = "Old Inactive", Status = (int)StatusType.Delete }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new DescriptionRepository(uow);

            // Act
            var activeDescriptions = await repository.GetDescriptions();

            // Assert: Returns exactly the 2 active descriptions
            Assert.Equal(2, activeDescriptions.Count);
            Assert.Contains(activeDescriptions, d => d.DescriptionName == "Dining Out");
            Assert.Contains(activeDescriptions, d => d.DescriptionName == "Salary");
            Assert.DoesNotContain(activeDescriptions, d => d.DescriptionName == "Old Inactive");
        }

        /*
         * SCENARIO PROTECTED:
         * When searching descriptions with pagination, the repository must filter vw_descriptions_list
         * by search query, calculate the total count accurately, and return the requested page.
         */
        [Fact]
        public async Task Search_WithKeyword_FiltersDescriptionsViewCorrectly()
        {
            // Arrange: Seed descriptions
            using (var seedContext = _testDb.CreateContext())
            {
                await seedContext.Descriptions.AddRangeAsync(
                    new Description { DescriptionSID = "d-1", DescriptionName = "Amazon Web Services", Status = (int)StatusType.Active },
                    new Description { DescriptionSID = "d-2", DescriptionName = "Amazon Prime Shopping", Status = (int)StatusType.Active },
                    new Description { DescriptionSID = "d-3", DescriptionName = "Netflix Subscription", Status = (int)StatusType.Active }
                );
                await seedContext.SaveChangesAsync();
            }

            using var uow = _testDb.CreateUnitOfWork();
            var repository = new DescriptionRepository(uow);

            var searchRequest = new SearchRequestModel
            {
                Search = "Amazon",
                Page = 1,
                PageSize = 10
            };

            // Act
            var pagedResult = await repository.Search(searchRequest);

            // Assert
            Assert.Equal(2, pagedResult.TotalCount);
            Assert.Equal(2, pagedResult.Data.Count);
            Assert.All(pagedResult.Data, d => Assert.Contains("Amazon", d.DescriptionName));
        }
    }
}
