using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.common.Models;
using backend.model.Models.Views;

namespace backend.service.Repository.Interface
{
    public interface IDescriptionRepository
    {
        Task<PagedResult<VwDescriptionsList>> Search(SearchRequestModel request);
        Task<List<DescriptionResponseModel>> GetDescriptions();
        Task<DescriptionResponseModel?> AddDescription(DescriptionRequestModel request);
        Task<DescriptionResponseModel?> UpdateDescription(string descriptionSID, DescriptionRequestModel request);
        Task<bool> DeleteDescription(string descriptionSID);
    }
}
