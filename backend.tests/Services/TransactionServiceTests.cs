using backend.model.RequestModel;
using backend.model.TenantDB;
using backend.service.Repository.Interface;
using backend.service.Services.Implementations;
using Moq;
using Xunit;
using static backend.common.Constants;

namespace backend.tests.Services
{
    public class TransactionServiceTests
    {
        [Fact]
        public async Task AddTransaction_WhenInsertDescriptionThrowsConcurrencyException_ClearsTrackerAndReusesExistingDescription()
        {
            var mockRepo = new Mock<ITransactionRepository>();
            var account = new Account { AccountID = 1, AccountSID = "acc-1", AccountName = "Checking", Status = StatusType.Active };
            var existingDesc = new Description { DescriptionID = 42, DescriptionSID = "desc-42", DescriptionName = "Coffee", Status = StatusType.Active };

            mockRepo.Setup(r => r.GetActiveAccountBySidAsync("acc-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            mockRepo.SetupSequence(r => r.GetActiveDescriptionByNameAsync("Coffee", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Description?)null)
                .ReturnsAsync(existingDesc);
            mockRepo.Setup(r => r.InsertDescriptionAsync(It.IsAny<Description>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unique constraint violation"));
            mockRepo.Setup(r => r.InsertTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Transaction t, CancellationToken _) => t);

            var service = new TransactionService(mockRepo.Object);
            var request = new TransactionRequestModel
            {
                AccountSID = "acc-1",
                DescriptionName = "  Coffee  ",
                Debit = 5.50,
                TransactionDate = "2026-08-21T10:00:00Z"
            };

            var result = await service.AddTransaction(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.Description);
            Assert.Equal("desc-42", result.Description.DescriptionSID);
            mockRepo.Verify(r => r.ClearChangeTracker(), Times.Once);
            mockRepo.Verify(r => r.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddTransaction_WithDescriptionSid_UsesExistingDescriptionWithoutCreatingNewOne()
        {
            var mockRepo = new Mock<ITransactionRepository>();
            var account = new Account { AccountID = 1, AccountSID = "acc-1", AccountName = "Checking", Status = StatusType.Active };
            var existingDesc = new Description { DescriptionID = 7, DescriptionSID = "desc-7", DescriptionName = "Rent", Status = StatusType.Active };

            mockRepo.Setup(r => r.GetActiveAccountBySidAsync("acc-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            mockRepo.Setup(r => r.GetActiveDescriptionBySidAsync("desc-7", It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingDesc);
            mockRepo.Setup(r => r.InsertTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Transaction t, CancellationToken _) => t);

            var service = new TransactionService(mockRepo.Object);
            var request = new TransactionRequestModel
            {
                AccountSID = "acc-1",
                DescriptionSID = "desc-7",
                Debit = 1200.00,
                TransactionDate = "2026-08-21T10:00:00Z"
            };

            var result = await service.AddTransaction(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result.Description);
            Assert.Equal("Rent", result.Description.DescriptionName);
            mockRepo.Verify(r => r.InsertDescriptionAsync(It.IsAny<Description>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
