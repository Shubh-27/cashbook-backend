using backend.common;
using backend.model.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;
using static backend.common.Constants;
using backend.common.Models;
using backend.common.Extensions;
using backend.model.Models.Views;
using backend.model.Data;

namespace backend.service.Repository.Implementation
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
        public async Task<PagedResult<VwDescriptionsList>> Search(SearchRequestModel request)
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
            var result = await query.ToPagedResultAsync(request.Page, request.PageSize);

            return result;
        }

        public async Task<List<DescriptionResponseModel>> GetDescriptions()
        {
            var data = await _unitOfWork.GetRepository<Descriptions>()
                .GetAllAsync(x => x.Status == StatusType.Active);
            
            return data.Select(x => CommonHelper.UpdateModel(x, new DescriptionResponseModel())).ToList();
        }
        #endregion

        #region Add Description
        public async Task<DescriptionResponseModel?> AddDescription(DescriptionRequestModel request)
        {
            var newDescription = new Descriptions
            {
                DescriptionSID = Guid.NewGuid().ToString(),
                DescriptionName = request.DescriptionName,
                Status = StatusType.Active
            };

            var added = await _unitOfWork.GetRepository<Descriptions>().InsertAsync(newDescription);
            await _unitOfWork.SaveAsync();

            return added.Entity != null ? CommonHelper.UpdateModel(added.Entity, new DescriptionResponseModel()) : null;
        }
        #endregion

        #region Update Description
        public async Task<DescriptionResponseModel?> UpdateDescription(string descriptionSID, DescriptionRequestModel request)
        {
            var existing = await _unitOfWork.GetRepository<Descriptions>().SingleOrDefaultAsync(x => x.DescriptionSID == descriptionSID && x.Status == StatusType.Active);
            if (existing == null) return null;

            existing.DescriptionName = request.DescriptionName;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

            _unitOfWork.GetRepository<Descriptions>().Update(existing);
            await _unitOfWork.SaveAsync();

            return CommonHelper.UpdateModel(existing, new DescriptionResponseModel());
        }
        #endregion

        #region Delete Description
        public async Task<bool> DeleteDescription(string descriptionSID)
        {
            var existing = await _unitOfWork.GetRepository<Descriptions>().SingleOrDefaultAsync(x => x.DescriptionSID == descriptionSID && x.Status == StatusType.Active);
            if (existing == null) return false;

            existing.Status = StatusType.Delete;

            _unitOfWork.GetRepository<Descriptions>().Update(existing);
            await _unitOfWork.SaveAsync();
            return true;
        }
        #endregion
    }
}
