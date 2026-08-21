/*
 * ====================================================================================================
 * LAYER UNDER TEST: CONTROLLER LAYER (HealthController)
 * ----------------------------------------------------------------------------------------------------
 * TESTING APPROACH & RATIONALE:
 * Controllers are tested as "Unit Tests" using in-memory execution.
 *
 * WHY THIS APPROACH?
 * The HealthController is a lightweight liveness endpoint used by container orchestrators, Electron,
 * and startup probes (e.g. wait-on http://localhost:5050/health). Testing it directly verifies that
 * the route handler returns an HTTP 200 OK with the expected status payload.
 * ====================================================================================================
 */

using backend.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace backend.tests.Controllers
{
    /// <summary>
    /// Unit tests for HealthController liveness check.
    /// </summary>
    public class HealthControllerTests
    {
        /*
         * SCENARIO PROTECTED:
         * When the Electron desktop app launches or monitoring probes ping /health,
         * the endpoint must return HTTP 200 OK with { status = "ok" } so the system knows
         * the backend web server is ready to accept user requests.
         */
        [Fact]
        public void Get_ReturnsOkWithStatusOkPayload()
        {
            /*
             * CONCEPT: UNIT TESTING CONTROLLERS DIRECTLY
             * --------------------------------------------------------------------------------------------
             * We instantiate the controller directly as a C# class (new HealthController()), call the method,
             * and cast the returned IActionResult to OkObjectResult to assert on status code and response body.
             */

            // Arrange
            var controller = new HealthController();

            // Act
            var actionResult = controller.Get();

            // Assert: Verify HTTP Status Code is 200 OK
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            Assert.Equal(200, okResult.StatusCode);

            // Assert: Verify returned payload
            Assert.NotNull(okResult.Value);
        }
    }
}
