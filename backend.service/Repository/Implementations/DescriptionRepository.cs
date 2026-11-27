using backend.common.Models;
using backend.common;
using backend.model.Extensions;
using backend.model.TenantDB;
using backend.model.TenantDB.Views;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Helpers;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using static backend.common.Constants;

namespace backend.service.Repository.Implementations
{
    public class DescriptionRepository : IDescriptionRepository
    {
        #region Variables & Constructor
        private readonly IUnitOfWork _unitOfWork;

        public DescriptionRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        #region Search
        public async Task<PagedResult<VwDescriptionsList>> Search(SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.GetRepository<VwDescriptionsList>().AsQueryable(enableTracking: false);
            
            // Generic Search
            if (!string.IsNullOrEmpty(request.Search))
            {
                var search = request.Search.ToLower();
                query = query.Where(x => x.DescriptionName != null && x.DescriptionName.ToLower().Contains(search));
            }

            // Generic Filtering
            query = query.ApplyFilters(request.Filters);

            // Generic Sorting
            query = query.ApplySorting(request.SortBy, request.SortOrder);

            // Generic Pagination
            var result = await query.ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);

            return result;
        }

        public async Task<List<DescriptionResponseModel>> GetDescriptions(CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.GetRepository<Description>()
                .AsQueryable(enableTracking: false)
                .Where(x => x.Status == StatusType.Active)
                .Select(description => new DescriptionResponseModel
                {
                    DescriptionSID = description.DescriptionSID,
                    DescriptionName = description.DescriptionName,
                    LastModifiedDateTime = description.LastModifiedDateTime,
                    Status = description.Status
                })
                .ToListAsync(cancellationToken);
        }
        #endregion

        #region Add Description
        public async Task<DescriptionResponseModel?> AddDescription(DescriptionRequestModel request, CancellationToken cancellationToken = default)
        {
            var newDescription = new Description
            {
                DescriptionSID = Guid.NewGuid().ToString(),
                DescriptionName = request.DescriptionName,
                Status = StatusType.Active
            };

            var added = await _unitOfWork.GetRepository<Description>().InsertAsync(newDescription, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            return added.Entity != null ? added.Entity.ToResponseModel() : null;
        }
        #endregion

        #region Update Description
        public async Task<DescriptionResponseModel?> UpdateDescription(string descriptionSID, DescriptionRequestModel request, CancellationToken cancellationToken = default)
        {
            var existing = await _unitOfWork.GetRepository<Description>().SingleOrDefaultAsync(x => x.DescriptionSID == descriptionSID && x.Status == StatusType.Active, cancellationToken: cancellationToken);
            if (existing == null) return null;

            existing.DescriptionName = request.DescriptionName;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

            _unitOfWork.GetRepository<Description>().Update(existing);
            await _unitOfWork.SaveAsync(cancellationToken);

            return existing.ToResponseModel();
        }
        #endregion

        #region Delete Description
        public async Task<bool> DeleteDescription(string descriptionSID, CancellationToken cancellationToken = default)
        {
            var existing = await _unitOfWork.GetRepository<Description>().SingleOrDefaultAsync(x => x.DescriptionSID == descriptionSID && x.Status == StatusType.Active, cancellationToken: cancellationToken);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Description>().Update(existing);
            await _unitOfWork.SaveAsync(cancellationToken);
            return true;
        }
        #endregion
    }
}
