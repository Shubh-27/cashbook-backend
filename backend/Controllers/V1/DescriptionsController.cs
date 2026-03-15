using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using Microsoft.AspNetCore.Mvc;
using backend.model.RequestModel;

namespace backend.Controllers.V1
{
    [Route("api/[controller]")]
    public class DescriptionsController : BaseController
    {
        private readonly IDescriptionRepository _descriptionRepository;
        public DescriptionsController(IDescriptionRepository descriptionRepository)
        {
            _descriptionRepository = descriptionRepository;
        }

        [HttpGet]
        public async Task<List<DescriptionResponseModel>> Get()
        {
            return await _descriptionRepository.GetDescriptions();
        }

        [HttpPost]
        public async Task<DescriptionResponseModel?> Post([FromBody] DescriptionRequestModel request)
        {
            return await _descriptionRepository.AddDescription(request);
        }

        [HttpPut("{id}")]
        public async Task<DescriptionResponseModel?> Put(string id, [FromBody] DescriptionRequestModel request)
        {
            return await _descriptionRepository.UpdateDescription(id, request);
        }

        [HttpDelete("{id}")]
        public async Task<bool> Delete(string id)
        {
            return await _descriptionRepository.DeleteDescription(id);
        }
    }
}
