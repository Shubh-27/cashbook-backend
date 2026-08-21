/*
 * ====================================================================================================
 * LAYER UNDER TEST: CONTROLLER LAYER (DescriptionsController)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * Unit Testing with Moq (Mock<IDescriptionRepository>).
 *
 * WHY THIS APPROACH?
 * We verify that DescriptionsController correctly translates HTTP requests to repository calls and returns
 * appropriate HTTP status codes (200 OK, 400 BadRequest on failure, 404 NotFound on missing records)
 * in pure in-memory execution.
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
    /// Unit tests for DescriptionsController testing HTTP endpoints and status codes.
    /// </summary>
    public class DescriptionsControllerTests
    {
        private readonly Mock<IDescriptionRepository> _mockRepo;
        private readonly DescriptionsController _controller;

        public DescriptionsControllerTests()
        {
            _mockRepo = new Mock<IDescriptionRepository>();
            _controller = new DescriptionsController(_mockRepo.Object);
        }

        /*
         * SCENARIO PROTECTED:
         * When the UI requests active descriptions for a dropdown list (GET /api/descriptions),
         * the controller must return HTTP 200 OK containing the list of active descriptions.
         */
        [Fact]
        public async Task Get_ReturnsOkWithListOfActiveDescriptions()
        {
            // Arrange: Configure mock to return a list of active descriptions
            var sampleList = new List<DescriptionResponseModel>
            {
                new DescriptionResponseModel { DescriptionSID = "desc-1", DescriptionName = "Groceries" },
                new DescriptionResponseModel { DescriptionSID = "desc-2", DescriptionName = "Utilities" }
            };

            _mockRepo.Setup(r => r.GetDescriptions())
                     .ReturnsAsync(sampleList);

            // Act
            var actionResult = await _controller.Get();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            var returnedList = Assert.IsType<List<DescriptionResponseModel>>(okResult.Value);
            Assert.Equal(2, returnedList.Count);
        }

        /*
         * SCENARIO PROTECTED:
         * When searching descriptions with pagination (POST /api/descriptions/list),
         * the controller must return HTTP 200 OK containing the paged view data.
         */
        [Fact]
        public async Task List_ReturnsOkWithPagedDescriptions()
        {
            // Arrange
            var expectedPagedResult = new PagedResult<VwDescriptionsList>
            {
                TotalCount = 1,
                Page = 1,
                PageSize = 10,
                Data = new List<VwDescriptionsList>
                {
                    new VwDescriptionsList { DescriptionSID = "desc-p-1", DescriptionName = "Coffee & Dining" }
                }
            };

            _mockRepo.Setup(r => r.Search(It.IsAny<SearchRequestModel>()))
                     .ReturnsAsync(expectedPagedResult);

            // Act
            var actionResult = await _controller.List(new SearchRequestModel { Page = 1, PageSize = 10 });

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            var pagedData = Assert.IsType<PagedResult<VwDescriptionsList>>(okResult.Value);
            Assert.Equal(1, pagedData.TotalCount);
        }

        /*
         * SCENARIO PROTECTED:
         * When adding a new description, the controller must return HTTP 200 OK with the created model.
         */
        [Fact]
        public async Task Post_WhenRepositorySucceeds_ReturnsOkWithCreatedDescription()
        {
            // Arrange
            var request = new DescriptionRequestModel { DescriptionName = "Health Insurance" };
            var response = new DescriptionResponseModel { DescriptionSID = "desc-hi", DescriptionName = "Health Insurance" };

            _mockRepo.Setup(r => r.AddDescription(request))
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
         * If description creation fails (repository returns null), the controller must return HTTP 400 BadRequest.
         */
        [Fact]
        public async Task Post_WhenRepositoryFails_ReturnsBadRequest()
        {
            // Arrange
            var request = new DescriptionRequestModel { DescriptionName = "Invalid Desc" };
            _mockRepo.Setup(r => r.AddDescription(request))
                     .ReturnsAsync((DescriptionResponseModel?)null);

            // Act
            var actionResult = await _controller.Post(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Equal(400, badRequestResult.StatusCode);
            Assert.Equal("Could not add description.", badRequestResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When updating an existing description, the controller must return HTTP 200 OK with updated model.
         */
        [Fact]
        public async Task Put_WhenDescriptionExists_ReturnsOkWithUpdatedDescription()
        {
            // Arrange
            var request = new DescriptionRequestModel { DescriptionName = "Updated Category" };
            var response = new DescriptionResponseModel { DescriptionSID = "desc-up", DescriptionName = "Updated Category" };

            _mockRepo.Setup(r => r.UpdateDescription("desc-up", request))
                     .ReturnsAsync(response);

            // Act
            var actionResult = await _controller.Put("desc-up", request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
            Assert.Equal(response, okResult.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When updating a non-existent description, the controller must return HTTP 404 NotFound.
         */
        [Fact]
        public async Task Put_WhenDescriptionNotFound_ReturnsNotFound()
        {
            // Arrange
            var request = new DescriptionRequestModel { DescriptionName = "Unknown" };
            _mockRepo.Setup(r => r.UpdateDescription("unknown-desc", request))
                     .ReturnsAsync((DescriptionResponseModel?)null);

            // Act
            var actionResult = await _controller.Put("unknown-desc", request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When deleting an existing description, the controller must return HTTP 200 OK with success confirmation.
         */
        [Fact]
        public async Task Delete_WhenDescriptionExists_ReturnsOkWithSuccessTrue()
        {
            // Arrange
            _mockRepo.Setup(r => r.DeleteDescription("desc-del"))
                     .ReturnsAsync(true);

            // Act
            var actionResult = await _controller.Delete("desc-del");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);
        }

        /*
         * SCENARIO PROTECTED:
         * When deleting a non-existent description, the controller must return HTTP 404 NotFound.
         */
        [Fact]
        public async Task Delete_WhenDescriptionNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockRepo.Setup(r => r.DeleteDescription("unknown-desc"))
                     .ReturnsAsync(false);

            // Act
            var actionResult = await _controller.Delete("unknown-desc");

            // Assert
            var notFoundResult = Assert.IsType<NotFoundResult>(actionResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }
    }
}
