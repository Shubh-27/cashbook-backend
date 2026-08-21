using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [Route("[controller]")]
    public class HealthController : BaseController
    {
        #region Health Check
        /// <summary>
        /// Checks the health status of the API service.
        /// </summary>
        /// <returns>An HTTP 200 response with the service health status.</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Get()
        {
            return Ok(new { status = "ok" });
        }
        #endregion
    }
}
