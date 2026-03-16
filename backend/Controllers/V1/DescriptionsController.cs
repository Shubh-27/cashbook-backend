using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Mvc;
using backend.model.RequestModel;
using backend.common.Models;
using backend.model.Models.Views;

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
        /// Retrieves a list of all active descriptions from the system. This endpoint is designed to return only descriptions that are currently marked as active.
        /// </summary>
        /// <returns></returns>
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
        /// Retrieves a paged list of description records matching the specified search criteria.
        /// </summary>
        /// <remarks>Use this method to obtain descriptions in a paged format based on filters provided in
        /// the request. The result includes paging information such as total count and page size.</remarks>
        /// <param name="request">The search parameters used to filter and page the descriptions. Cannot be null.</param>
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
        /// <param name="request">The request model containing the details required to create a new description. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a response model with the
        /// created description details, or null if creation fails.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(backend.model.ResponseModel.DescriptionResponseModel), StatusCodes.Status200OK)]
        public async Task<backend.model.ResponseModel.DescriptionResponseModel?> Post([FromBody] DescriptionRequestModel request)
        {
            return await _descriptionRepository.AddDescription(request);
        }
        #endregion

        #region Update Description
        /// <summary>
        /// Updates an existing description entry based on the provided request data.
        /// </summary>
        /// <param name="descriptionSID">The ID of the description to update.</param>
        /// <param name="request">The request model containing the updated details for the description. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a response model with the
        /// updated description details, or null if the update fails.</returns>
        [HttpPut("{descriptionSID}")]
        [ProducesResponseType(typeof(backend.model.ResponseModel.DescriptionResponseModel), StatusCodes.Status200OK)]
        public async Task<backend.model.ResponseModel.DescriptionResponseModel?> Put(string descriptionSID, [FromBody] DescriptionRequestModel request)
        {
            return await _descriptionRepository.UpdateDescription(descriptionSID, request);
        }
        #endregion

        #region Delete Description
        /// <summary>
        /// Deletes an existing description entry based on the provided ID.
        /// </summary>
        /// <param name="descriptionSID">The ID of the description to delete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating
        /// whether the deletion was successful.</returns>
        [HttpDelete("{descriptionSID}")]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        public async Task<bool> Delete(string descriptionSID)
        {
            return await _descriptionRepository.DeleteDescription(descriptionSID);
        }
        #endregion
    }
}
