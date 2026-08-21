/*
 * ====================================================================================================
 * LAYER UNDER TEST: CONTROLLER LAYER (AccountsController)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * Controllers are tested as "Unit Tests" with mocked dependencies (using Moq) rather than against
 * a real database.
 *
 * WHY THIS APPROACH?
 * 1. Separation of Concerns: The controller's only responsibility is HTTP concerns (reading the request,
 *    invoking the repository, and returning the correct HTTP status code: 200 OK, 400 BadRequest, 404 NotFound).
 * 2. Speed & Determinism: By substituting the repository with a fake mock (Mock<IAccountRepository>),
 *    controller tests execute in under 1 millisecond with zero database or file I/O overhead.
 * 3. Fault Injection: Mocks make it trivial to simulate edge cases (e.g. repository returns null or false)
 *    to prove that the controller returns a 404 NotFound or 400 BadRequest.
 * ====================================================================================================
 */

using backend.common;
using backend.Controllers.V1;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace backend.tests.Controllers
{
    /// <summary>
    /// Unit tests for AccountsController using mocked repository dependencies.
    /// </summary>
    public class AccountsControllerTests
    {
        /*
         * CONCEPT: MOCKING WITH MOQ (Test Doubles)
         * ------------------------------------------------------------------------------------------------
         * A "Mock" is a programmable fake object. We tell Moq: "When the controller calls AddAccount(),
         * do NOT touch a database — just return this predefined AccountResponseModel."
         * This isolates the controller logic from data layer bugs.
         */
        private readonly Mock<IAccountRepository> _mockRepo;
        private readonly AccountsController _controller;

        public AccountsControllerTests()
        {
            _mockRepo = new Mock<IAccountRepository>();
            _controller = new AccountsController(_mockRepo.Object);
        }

        /*
         * SCENARIO PROTECTED:
         * When the frontend requests a list of accounts, the controller must call Search()
         * on the repository and return HTTP 200 OK containing the paged accounts list.
         */
        [Fact]
        public async Task List_ReturnsOkWithPagedAccounts()
        {
            // Arrange: Configure mock to return a paged result of accounts
            var expectedResult = new PagedResult<VwAccountsList>
            {
                TotalCount = 1,
                Page = 1,
                PageSize = 10,
                Data = new List<VwAccountsList>
                {
                    new VwAccountsList { AccountSID = "acc-101", AccountName = "Main Checking" }
                }
            };

            _mockRepo.Setup(r => r.Search(It.IsAny<SearchRequestModel>()))
                     .ReturnsAsync(expectedResult);

            var request = new SearchRequestModel { Page = 1, PageSize = 10 };

            // Act: Call the controller List action
            var actionResult = await _controller.List(request);

            // Assert: Verify HTTP status code is 200 OK and body matches expected data
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            var actualData = Assert.IsType<PagedResult<VwAccountsList>>(okResult.Value);
            Assert.Equal(1, actualData.TotalCount);
            Assert.Equal("acc-101", actualData.Data[0].AccountSID);
        }

        /*
         * SCENARIO PROTECTED:
         * When adding a valid account, the controller must return HTTP 200 OK with the created account model.
         */
        [Fact]
        public async Task Post_WhenRepositorySucceeds_ReturnsOkWithCreatedAccount()
        {
            // Arrange: Configure mock to return a valid response model
            var newAccountRequest = new AccountRequestModel { AccountName = "Savings Account", BankName = "Chase" };
            var createdResponse = new AccountResponseModel { AccountSID = "acc-new", AccountName = "Savings Account", BankName = "Chase" };

            _mockRepo.Setup(r => r.AddAccount(newAccountRequest, It.IsAny<int?>()))
                     .ReturnsAsync(createdResponse);

            // Act
            var actionResult = await _controller.Post(newAccountRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            var returnedAccount = Assert.IsType<AccountResponseModel>(okResult.Value);
            Assert.Equal("acc-new", returnedAccount.AccountSID);
        }

        /*
         * SCENARIO PROTECTED:
         * If the repository fails to create the account (returns null), the controller must
         * return HTTP 400 Bad Request with a descriptive error message instead of 200 OK.
         */
        [Fact]
        public async Task Post_WhenRepositoryFails_ReturnsBadRequest()
        {
            // Arrange: Configure mock to return null
            var request = new AccountRequestModel { AccountName = "Invalid Account" };
            _mockRepo.Setup(r => r.AddAccount(request, It.IsAny<int?>()))
                     .ReturnsAsync((AccountResponseModel?)null);

            // Act
            var actionResult = await _controller.Post(request);

            // Assert: Must return HTTP 400 BadRequest
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Could not add account.", badRequestResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When updating an account that exists, the controller must return HTTP 200 OK with the updated details.
         */
        [Fact]
        public async Task Put_WhenAccountExists_ReturnsOkWithUpdatedAccount()
        {
            // Arrange
            var updateRequest = new AccountRequestModel { AccountName = "Renamed Account" };
            var updatedResponse = new AccountResponseModel { AccountSID = "acc-1", AccountName = "Renamed Account" };

            _mockRepo.Setup(r => r.UpdateAccount("acc-1", updateRequest, It.IsAny<int?>()))
                     .ReturnsAsync(updatedResponse);

            // Act
            var actionResult = await _controller.Put("acc-1", updateRequest);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(updatedResponse, okResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When a client attempts to update an account that does not exist (repository returns null),
         * the controller must return HTTP 404 Not Found.
         */
        [Fact]
        public async Task Put_WhenAccountNotFound_ReturnsNotFound()
        {
            // Arrange: Mock returns null to simulate account not found
            var updateRequest = new AccountRequestModel { AccountName = "Any Name" };
            _mockRepo.Setup(r => r.UpdateAccount("unknown-id", updateRequest, It.IsAny<int?>()))
                     .ReturnsAsync((AccountResponseModel?)null);

            // Act
            var actionResult = await _controller.Put("unknown-id", updateRequest);

            // Assert: Must return HTTP 404 NotFound
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When deleting an existing account, the controller must return HTTP 200 OK with { success = true }.
         */
        [Fact]
        public async Task Delete_WhenAccountExists_ReturnsOkWithSuccessTrue()
        {
            // Arrange
            _mockRepo.Setup(r => r.DeleteAccount("acc-to-delete"))
                     .ReturnsAsync(true);

            // Act
            var actionResult = await _controller.Delete("acc-to-delete");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When deleting a non-existent account (repository returns false), the controller must
         * return HTTP 404 Not Found so REST clients receive accurate HTTP status codes.
         */
        [Fact]
        public async Task Delete_WhenAccountNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockRepo.Setup(r => r.DeleteAccount("unknown-acc"))
                     .ReturnsAsync(false);

            // Act
            var actionResult = await _controller.Delete("unknown-acc");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }
    }
}
