using backend.common;
using backend.model.DbModels.Views;
using backend.model.RequestModels;
using backend.model.ResponseModels;

namespace backend.service.Repository.Interfaces
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
