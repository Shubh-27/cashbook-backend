using backend.common.Models;
using backend.common;
using backend.model.TenantDB.Views;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class DescriptionsController : BaseController
    {
        #region Variables & Constructor
        private readonly IDescriptionRepository _descriptionRepository;

        public DescriptionsController(IDescriptionRepository descriptionRepository)
        {
            _descriptionRepository = descriptionRepository;
        }
        #endregion

        #region Get Descriptions
        /// <summary>
        /// Retrieves a list of all active descriptions from the system.
        /// </summary>
        /// <returns>A list of active description records.</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<DescriptionResponseModel>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
        {
            var result = await _descriptionRepository.GetDescriptions(cancellationToken);
            return Ok(result);
        }
        #endregion

        #region Get Descriptions List
        /// <summary>
        /// Retrieves a paged list of description records matching the specified search, filter, and sorting criteria.
        /// </summary>
        /// <param name="request">The search parameters used to filter, sort, and paginate descriptions.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An HTTP 200 response containing a paged result of description records that match the search criteria.</returns>
        [HttpPost("list")]
        [ProducesResponseType(typeof(PagedResult<VwDescriptionsList>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromBody] SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var result = await _descriptionRepository.Search(request, cancellationToken);
            return Ok(result);
        }
        #endregion

        #region Add Description
        /// <summary>
        /// Creates a new description based on the specified request data.
        /// </summary>
        /// <param name="request">The request model containing the details required to create a new description.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An IActionResult containing the created description details or BadRequest if creation fails.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(DescriptionResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post([FromBody] DescriptionRequestModel request, CancellationToken cancellationToken = default)
        {
            var result = await _descriptionRepository.AddDescription(request, cancellationToken);
            if (result == null) return BadRequest("Could not add description.");
            return Ok(result);
        }
        #endregion

        #region Update Description
        /// <summary>
        /// Updates an existing description entry based on the provided request data.
        /// </summary>
        /// <param name="descriptionSID">The unique identifier of the description to update.</param>
        /// <param name="request">The request model containing the updated details for the description.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An IActionResult containing the updated description details or NotFound if not found.</returns>
        [HttpPut("{descriptionSID}")]
        [ProducesResponseType(typeof(DescriptionResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Put(string descriptionSID, [FromBody] DescriptionRequestModel request, CancellationToken cancellationToken = default)
        {
            var result = await _descriptionRepository.UpdateDescription(descriptionSID, request, cancellationToken);
            if (result == null) return NotFound();
            return Ok(result);
        }
        #endregion

        #region Delete Description
        /// <summary>
        /// Deletes an existing description entry based on the provided identifier.
        /// </summary>
        /// <param name="descriptionSID">The unique identifier of the description to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An IActionResult indicating success or NotFound if the description does not exist.</returns>
        [HttpDelete("{descriptionSID}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string descriptionSID, CancellationToken cancellationToken = default)
        {
            var result = await _descriptionRepository.DeleteDescription(descriptionSID, cancellationToken);
            if (!result) return NotFound();
            return Ok(new { success = true });
        }
        #endregion
    }
}
