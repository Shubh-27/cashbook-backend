using backend.common;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
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
        public async Task<IActionResult> Get()
        {
            var result = await _descriptionRepository.GetDescriptions();
            return Ok(result);
        }
        #endregion

        #region Get Descriptions List
        /// <summary>
        /// Retrieves a paged list of description records matching the specified search, filter, and sorting criteria.
        /// </summary>
        /// <param name="request">The search parameters used to filter, sort, and paginate descriptions.</param>
        /// <returns>An HTTP 200 response containing a paged result of description records that match the search criteria.</returns>
        [HttpPost("list")]
        [ProducesResponseType(typeof(PagedResult<VwDescriptionsList>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromBody] SearchRequestModel request)
        {
            var result = await _descriptionRepository.Search(request);
            return Ok(result);
        }
        #endregion

        #region Add Description
        /// <summary>
        /// Creates a new description based on the specified request data.
        /// </summary>
        /// <param name="request">The request model containing the details required to create a new description.</param>
        /// <returns>An IActionResult containing the created description details or BadRequest if creation fails.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(DescriptionResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Post([FromBody] DescriptionRequestModel request)
        {
            var result = await _descriptionRepository.AddDescription(request);
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
        /// <returns>An IActionResult containing the updated description details or NotFound if not found.</returns>
        [HttpPut("{descriptionSID}")]
        [ProducesResponseType(typeof(DescriptionResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Put(string descriptionSID, [FromBody] DescriptionRequestModel request)
        {
            var result = await _descriptionRepository.UpdateDescription(descriptionSID, request);
            if (result == null) return NotFound();
            return Ok(result);
        }
        #endregion

        #region Delete Description
        /// <summary>
        /// Deletes an existing description entry based on the provided identifier.
        /// </summary>
        /// <param name="descriptionSID">The unique identifier of the description to delete.</param>
        /// <returns>An IActionResult indicating success or NotFound if the description does not exist.</returns>
        [HttpDelete("{descriptionSID}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(string descriptionSID)
        {
            var result = await _descriptionRepository.DeleteDescription(descriptionSID);
            if (!result) return NotFound();
            return Ok(new { success = true });
        }
        #endregion
    }
}
