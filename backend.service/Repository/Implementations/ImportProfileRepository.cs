using backend.common.Models;
using backend.common;
using backend.model.Extensions;
using backend.model.TenantDB;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace backend.service.Repository.Implementations
{
    public sealed class ImportProfileRepository : IImportProfileRepository
    {
        #region Variables & Constructor
        private readonly IUnitOfWork _unitOfWork;

        public ImportProfileRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Queries
        public async Task<PagedResult<ImportProfile>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.GetRepository<ImportProfile>()
                .AsQueryable(enableTracking: false)
                .Where(p => p.Status == Constants.StatusType.Active);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                query = query.Where(x =>
                    x.Name.ToLower().Contains(search) ||
                    x.FileType.ToLower().Contains(search) ||
                    (x.WorksheetName != null && x.WorksheetName.ToLower().Contains(search)));
            }

            query = query.ApplyFilters(request.Filters);
            query = string.IsNullOrWhiteSpace(request.SortBy)
                ? query.OrderBy(x => x.Name)
                : query.ApplySorting(request.SortBy, request.SortOrder);

            return await query.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
        }

        public async Task<IReadOnlyList<ImportProfile>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.GetRepository<ImportProfile>()
                .AsQueryable(enableTracking: false)
                .Where(p => p.Status == Constants.StatusType.Active)
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<ImportProfile?> GetActiveBySidAsync(string profileSID, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<ImportProfile>()
                .AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(item => item.ImportProfileSID == profileSID && item.Status == Constants.StatusType.Active, cancellationToken);
        }

        public Task<ImportProfile?> GetByIdAsync(int profileId, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<ImportProfile>()
                .AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(item => item.ImportProfileID == profileId, cancellationToken);
        }

        public Task<bool> ActiveNameExistsAsync(string name, string? excludeProfileSid = null, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.GetRepository<ImportProfile>()
                .AsQueryable(enableTracking: false)
                .Where(p => p.Status == Constants.StatusType.Active && p.Name == name);

            if (!string.IsNullOrWhiteSpace(excludeProfileSid))
                query = query.Where(p => p.ImportProfileSID != excludeProfileSid);

            return query.AnyAsync(cancellationToken);
        }

        public Task<ImportProfileVersion?> GetVersionByIdAsync(int versionId, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<ImportProfileVersion>()
                .AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(item => item.ImportProfileVersionID == versionId, cancellationToken);
        }

        public Task<ImportProfileVersion?> GetVersionByNumberAsync(int profileId, int versionNumber, CancellationToken cancellationToken = default)
        {
            return _unitOfWork.GetRepository<ImportProfileVersion>()
                .AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(item => item.ProfileID == profileId && item.VersionNumber == versionNumber, cancellationToken);
        }

        public async Task<int> GetMaxVersionNumberAsync(int profileId, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.GetRepository<ImportProfileVersion>()
                .AsQueryable(enableTracking: false)
                .Where(item => item.ProfileID == profileId)
                .MaxAsync(item => (int?)item.VersionNumber, cancellationToken) ?? 0;
        }
        #endregion

        #region Persistence
        public async Task InsertProfileAndVersionAsync(ImportProfile profile, ImportProfileVersion version, CancellationToken cancellationToken = default)
        {
            var profileRepo = _unitOfWork.GetRepository<ImportProfile>();
            var versionRepo = _unitOfWork.GetRepository<ImportProfileVersion>();

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await profileRepo.InsertAsync(profile, cancellationToken);
                await _unitOfWork.SaveAsync(cancellationToken);

                version.ProfileID = profile.ImportProfileID;
                await versionRepo.InsertAsync(version, cancellationToken);
                await _unitOfWork.SaveAsync(cancellationToken);

                profile.CurrentVersionID = version.ImportProfileVersionID;
                profileRepo.Update(profile);
                await _unitOfWork.SaveAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task InsertVersionAndUpdateProfileAsync(ImportProfile profile, ImportProfileVersion version, CancellationToken cancellationToken = default)
        {
            var profileRepo = _unitOfWork.GetRepository<ImportProfile>();
            var versionRepo = _unitOfWork.GetRepository<ImportProfileVersion>();

            await versionRepo.InsertAsync(version, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            profile.CurrentVersionID = version.ImportProfileVersionID;
            profileRepo.Update(profile);
            await _unitOfWork.SaveAsync(cancellationToken);
        }

        public async Task<bool> DeleteAsync(string profileSID, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<ImportProfile>();
            var profile = await repo.SingleOrDefaultAsync(
                p => p.ImportProfileSID == profileSID && p.Status == Constants.StatusType.Active,
                cancellationToken: cancellationToken);
            if (profile == null) return false;

            profile.Status = Constants.StatusType.Delete;
            profile.LastModifiedDateTime = DateTime.UtcNow.ToString("O");
            repo.Update(profile);
            await _unitOfWork.SaveAsync(cancellationToken);
            return true;
        }
        #endregion
    }
}
