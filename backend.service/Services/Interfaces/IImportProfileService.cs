using backend.common.Models;
using backend.common;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Interfaces
{
    public interface IImportProfileService
    {
        Task<PagedResult<ImportProfileResponse>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken = default);
        Task<ImportProfileCreateResult> CreateAsync(IFormFile file, ImportProfileRuleRequest rule, CancellationToken cancellationToken = default);
        Task<ImportProfileCreateResult> UpdateAsync(string profileSID, IFormFile? file, ImportProfileRuleRequest rule, CancellationToken cancellationToken = default);
        Task<ImportProfileVersionResponse?> GetAsync(string profileSID, int? versionNumber, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ImportProfileResponse>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string profileSID, CancellationToken cancellationToken = default);
    }
}
