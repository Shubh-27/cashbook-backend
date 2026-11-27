using backend.common.Models;
using backend.common;
using backend.model.TenantDB;

namespace backend.service.Repository.Interface
{
    public interface IImportProfileRepository
    {
        Task<PagedResult<ImportProfile>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ImportProfile>> GetAllActiveAsync(CancellationToken cancellationToken = default);
        Task<ImportProfile?> GetActiveBySidAsync(string profileSID, CancellationToken cancellationToken = default);
        Task<ImportProfile?> GetByIdAsync(int profileId, CancellationToken cancellationToken = default);
        Task<bool> ActiveNameExistsAsync(string name, string? excludeProfileSid = null, CancellationToken cancellationToken = default);
        Task<ImportProfileVersion?> GetVersionByIdAsync(int versionId, CancellationToken cancellationToken = default);
        Task<ImportProfileVersion?> GetVersionByNumberAsync(int profileId, int versionNumber, CancellationToken cancellationToken = default);
        Task<int> GetMaxVersionNumberAsync(int profileId, CancellationToken cancellationToken = default);
        Task InsertProfileAndVersionAsync(ImportProfile profile, ImportProfileVersion version, CancellationToken cancellationToken = default);
        Task InsertVersionAndUpdateProfileAsync(ImportProfile profile, ImportProfileVersion version, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string profileSID, CancellationToken cancellationToken = default);
    }
}
