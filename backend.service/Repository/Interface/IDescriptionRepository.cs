using backend.common.Models;
using backend.common;
using backend.model.TenantDB.Views;
using backend.model.RequestModel;
using backend.model.ResponseModel;

namespace backend.service.Repository.Interface
{
    public interface IDescriptionRepository
    {
        Task<PagedResult<VwDescriptionsList>> Search(SearchRequestModel request, CancellationToken cancellationToken);
        Task<List<DescriptionResponseModel>> GetDescriptions(CancellationToken cancellationToken);
        Task<DescriptionResponseModel?> AddDescription(DescriptionRequestModel request, CancellationToken cancellationToken);
        Task<DescriptionResponseModel?> UpdateDescription(string descriptionSID, DescriptionRequestModel request, CancellationToken cancellationToken);
        Task<bool> DeleteDescription(string descriptionSID, CancellationToken cancellationToken);
    }
}
