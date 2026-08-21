/*
 * ====================================================================================================
 * LAYER UNDER TEST: CONTROLLER LAYER (TransactionsController)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * Unit Testing with Moq (Mock<ITransactionRepository> and Mock<IExportService>).
 *
 * WHY THIS APPROACH?
 * TransactionsController handles complex HTTP responses including JSON responses and file downloads (FileContentResult).
 * By mocking both the repository and the export service, we can verify that the controller correctly routes
 * export requests to File results and CRUD operations to standard REST status codes (200, 400, 404).
 * ====================================================================================================
 */

using System.Text;
using backend.common;
using backend.Controllers.V1;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace backend.tests.Controllers
{
    /// <summary>
    /// Unit tests for TransactionsController testing file exports and transaction CRUD endpoints.
    /// </summary>
    public class TransactionsControllerTests
    {
        private readonly Mock<ITransactionRepository> _mockTxRepo;
        private readonly Mock<IExportService> _mockExportService;
        private readonly TransactionsController _controller;

        public TransactionsControllerTests()
        {
            _mockTxRepo = new Mock<ITransactionRepository>();
            _mockExportService = new Mock<IExportService>();
            _controller = new TransactionsController(_mockTxRepo.Object, _mockExportService.Object);
        }

        /*
         * SCENARIO PROTECTED:
         * When a user requests an export (POST /api/transactions/export), the controller must call
         * ExportTransactionsAsync on the export service and return a FileContentResult with binary payload,
         * correct MIME type, and download filename.
         */
        [Fact]
        public async Task Export_ReturnsFileResultWithExcelOrZipPayload()
        {
            /*
             * CONCEPT: TESTING FILE DOWNLOAD ENDPOINTS (FileContentResult)
             * --------------------------------------------------------------------------------------------
             * In ASP.NET Core, file download actions return File(bytes, contentType, fileName) which creates
             * a FileContentResult. We cast IActionResult to FileContentResult to assert on ContentType,
             * FileDownloadName, and FileContents.
             */

            // Arrange: Configure export service mock
            var fakeFileBytes = Encoding.UTF8.GetBytes("FAKE EXCEL CONTENTS");
            const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            const string fileName = "Transactions_2026.xlsx";

            _mockExportService.Setup(s => s.ExportTransactionsAsync(It.IsAny<ExportRequestModel>()))
                              .ReturnsAsync((fakeFileBytes, contentType, fileName));

            var request = new ExportRequestModel { ExcelName = "Transactions_2026" };

            // Act
            var actionResult = await _controller.Export(request);

            // Assert: Verify FileContentResult properties
            var fileResult = Assert.IsType<FileContentResult>(actionResult);
            Assert.Equal(contentType, fileResult.ContentType);
            Assert.Equal(fileName, fileResult.FileDownloadName);
            Assert.Equal(fakeFileBytes, fileResult.FileContents);
        }

        /*
         * SCENARIO PROTECTED:
         * When retrieving the transaction list for the feed (POST /api/transactions/list),
         * the controller must call Search() and return HTTP 200 OK with the paged result.
         */
        [Fact]
        public async Task List_ReturnsOkWithPagedTransactions()
        {
            // Arrange
            var pagedResult = new PagedResult<VwTransactionsList>
            {
                TotalCount = 1,
                Page = 1,
                PageSize = 10,
                Data = new List<VwTransactionsList>
                {
                    new VwTransactionsList { TransactionSID = "tx-100", Debit = 50.0 }
                }
            };

            _mockTxRepo.Setup(r => r.Search(It.IsAny<SearchRequestModel>()))
                       .ReturnsAsync(pagedResult);

            // Act
            var actionResult = await _controller.List(new SearchRequestModel { Page = 1, PageSize = 10 });

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            var actualData = Assert.IsType<PagedResult<VwTransactionsList>>(okResult.Value);
            Assert.Equal(1, actualData.TotalCount);
        }

        /*
         * SCENARIO PROTECTED:
         * When adding a transaction, the controller must return HTTP 200 OK with the created transaction.
         */
        [Fact]
        public async Task Post_WhenTransactionCreated_ReturnsOkWithCreatedTransaction()
        {
            // Arrange
            var request = new TransactionRequestModel { AccountSID = "acc-1", Debit = 100.0, TransactionDate = "2026-08-21T10:00:00Z" };
            var response = new TransactionResponseModel { TransactionSID = "tx-created", Debit = 100.0 };

            _mockTxRepo.Setup(r => r.AddTransaction(request))
                       .ReturnsAsync(response);

            // Act
            var actionResult = await _controller.Post(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(response, okResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * If transaction creation fails (e.g. account does not exist), the controller must return HTTP 400 BadRequest.
         */
        [Fact]
        public async Task Post_WhenCreationFails_ReturnsBadRequest()
        {
            // Arrange
            var request = new TransactionRequestModel { AccountSID = "non-existent" };
            _mockTxRepo.Setup(r => r.AddTransaction(request))
                       .ReturnsAsync((TransactionResponseModel?)null);

            // Act
            var actionResult = await _controller.Post(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Could not add transaction.", badRequestResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When updating an existing transaction, the controller must return HTTP 200 OK with updated details.
         */
        [Fact]
        public async Task Put_WhenTransactionUpdated_ReturnsOkWithUpdatedTransaction()
        {
            // Arrange
            var request = new TransactionRequestModel { Debit = 150.0, TransactionDate = "2026-08-21T12:00:00Z" };
            var response = new TransactionResponseModel { TransactionSID = "tx-up", Debit = 150.0 };

            _mockTxRepo.Setup(r => r.UpdateTransaction("tx-up", request))
                       .ReturnsAsync(response);

            // Act
            var actionResult = await _controller.Put("tx-up", request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(response, okResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When updating a transaction that does not exist, the controller must return HTTP 404 NotFound.
         */
        [Fact]
        public async Task Put_WhenTransactionNotFound_ReturnsNotFound()
        {
            // Arrange
            var request = new TransactionRequestModel { Debit = 20.0 };
            _mockTxRepo.Setup(r => r.UpdateTransaction("unknown-tx", request))
                       .ReturnsAsync((TransactionResponseModel?)null);

            // Act
            var actionResult = await _controller.Put("unknown-tx", request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When deleting a transaction matching accountSID, the controller must return HTTP 200 OK.
         */
        [Fact]
        public async Task Delete_WhenTransactionDeleted_ReturnsOkWithSuccessTrue()
        {
            // Arrange
            _mockTxRepo.Setup(r => r.DeleteTransaction("tx-del", "acc-owner"))
                       .ReturnsAsync(true);

            // Act
            var actionResult = await _controller.Delete("tx-del", "acc-owner");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When deleting a transaction that does not exist or account mismatch, the controller must return HTTP 404 NotFound.
         */
        [Fact]
        public async Task Delete_WhenTransactionNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockTxRepo.Setup(r => r.DeleteTransaction("unknown-tx", "wrong-acc"))
                       .ReturnsAsync(false);

            // Act
            var actionResult = await _controller.Delete("unknown-tx", "wrong-acc");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }
    }
}
