using backend.model.RequestModel;
using backend.model.ResponseModel;

namespace backend.service.Repository.Interface
{
    public interface IDescriptionRepository
    {
        Task<List<DescriptionResponseModel>> GetDescriptions();
        Task<DescriptionResponseModel?> AddDescription(DescriptionRequestModel request);
        Task<DescriptionResponseModel?> UpdateDescription(string id, DescriptionRequestModel request);
        Task<bool> DeleteDescription(string id);
    }
}
