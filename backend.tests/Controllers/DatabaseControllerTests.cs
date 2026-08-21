/*
 * ====================================================================================================
 * LAYER UNDER TEST: CONTROLLER LAYER (DatabaseController)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * Unit Testing with Moq (Mock<IDatabaseService> and Mock<IFormFile>).
 *
 * WHY THIS APPROACH?
 * The DatabaseController is the entry point for disaster-recovery file uploads (IFormFile) and database
 * backups. We test HTTP validation (e.g. rejecting null/empty uploads before reading streams) and proper
 * FileContentResult streaming for database downloads.
 * ====================================================================================================
 */

using System.Text;
using backend.Controllers.V1;
using backend.service.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace backend.tests.Controllers
{
    /// <summary>
    /// Unit tests for DatabaseController verifying database backup and restore endpoints.
    /// </summary>
    public class DatabaseControllerTests
    {
        private readonly Mock<IDatabaseService> _mockDatabaseService;
        private readonly DatabaseController _controller;

        public DatabaseControllerTests()
        {
            _mockDatabaseService = new Mock<IDatabaseService>();
            _controller = new DatabaseController(_mockDatabaseService.Object);
        }

        /*
         * SCENARIO PROTECTED:
         * When the user clicks "Download Database Backup" (GET /api/database/export),
         * the controller must return a FileContentResult with the .db binary stream.
         */
        [Fact]
        public async Task ExportDatabase_ReturnsFileResultWithDatabaseBackup()
        {
            // Arrange
            var dummyDbBytes = Encoding.UTF8.GetBytes("SQLite format 3");
            const string contentType = "application/octet-stream";
            const string fileName = "bank_20260821_120000.db";

            _mockDatabaseService.Setup(s => s.ExportDatabaseAsync())
                                .ReturnsAsync((dummyDbBytes, contentType, fileName));

            // Act
            var actionResult = await _controller.ExportDatabase();

            // Assert: Verify FileContentResult download metadata
            var fileResult = Assert.IsType<FileContentResult>(actionResult);
            Assert.Equal(contentType, fileResult.ContentType);
            Assert.Equal(fileName, fileResult.FileDownloadName);
            Assert.Equal(dummyDbBytes, fileResult.FileContents);
        }

        /*
         * SCENARIO PROTECTED:
         * If the user submits the restore form without selecting a file (null or 0-length IFormFile),
         * the controller must return HTTP 400 BadRequest with "No file uploaded." before executing any service logic.
         */
        [Fact]
        public async Task ImportDatabase_WhenFileNullOrEmpty_ReturnsBadRequest()
        {
            // Arrange: Null file
            var nullActionResult = await _controller.ImportDatabase(file: null!);
            var badRequestNull = Assert.IsType<BadRequestObjectResult>(nullActionResult);
            Assert.Equal(400, badRequestNull.StatusCode);
            Assert.Equal("No file uploaded.", badRequestNull.Value);

            // Arrange: 0-length mock file
            var mockEmptyFile = new Mock<IFormFile>();
            mockEmptyFile.Setup(f => f.Length).Returns(0);

            // Act
            var emptyActionResult = await _controller.ImportDatabase(mockEmptyFile.Object);

            // Assert
            var badRequestEmpty = Assert.IsType<BadRequestObjectResult>(emptyActionResult);
            Assert.Equal(400, badRequestEmpty.StatusCode);
            Assert.Equal("No file uploaded.", badRequestEmpty.Value);
        }

        /*
         * SCENARIO PROTECTED:
         * When a valid file is uploaded, the controller must open the stream, pass it to the database service
         * for validation and migration, and return HTTP 200 OK with a success message.
         */
        [Fact]
        public async Task ImportDatabase_WhenValidFileUploaded_CallsServiceAndReturnsOk()
        {
            /*
             * CONCEPT: MOCKING IFormFile
             * --------------------------------------------------------------------------------------------
             * ASP.NET Core represents uploaded files as IFormFile. We mock OpenReadStream(), FileName,
             * and Length to simulate a real HTTP multipart/form-data file upload without creating network traffic.
             */

            // Arrange
            var fileBytes = Encoding.UTF8.GetBytes("SQLite format 3 binary payload");
            using var fileStream = new MemoryStream(fileBytes);

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(fileBytes.Length);
            mockFile.Setup(f => f.FileName).Returns("my_backup.db");
            mockFile.Setup(f => f.OpenReadStream()).Returns(fileStream);

            _mockDatabaseService.Setup(s => s.ImportDatabaseAsync(It.IsAny<Stream>(), "my_backup.db"))
                                .Returns(Task.CompletedTask);

            // Act
            var actionResult = await _controller.ImportDatabase(mockFile.Object);

            // Assert: Returned HTTP 200 OK
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            // Assert: Verify that the service was invoked exactly once with the uploaded filename
            _mockDatabaseService.Verify(s => s.ImportDatabaseAsync(It.IsAny<Stream>(), "my_backup.db"), Times.Once);
        }
    }
}
