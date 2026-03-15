using backend.common;
using backend.model.Models;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Repository.Interface;
using backend.service.UnitOfWork;

namespace backend.service.Repository.Implementation
{
    public class DescriptionRepository : IDescriptionRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        public DescriptionRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<DescriptionResponseModel>> GetDescriptions()
        {
            var descriptions = await _unitOfWork.GetRepository<Descriptions>().GetAllAsync();
            return [.. descriptions.OrderBy(d => d.DescriptionName).Select(x => CommonHelper.UpdateModel(x, new DescriptionResponseModel()))];
        }

        public async Task<DescriptionResponseModel?> AddDescription(DescriptionRequestModel request)
        {
            var newDescription = new Descriptions
            {
                DescriptionSID = Guid.NewGuid().ToString(),
                DescriptionName = request.DescriptionName,
                Status = 1
            };

            var added = await _unitOfWork.GetRepository<Descriptions>().InsertAsync(newDescription);
            await _unitOfWork.SaveAsync();

            return added.Entity != null ? CommonHelper.UpdateModel(added.Entity, new DescriptionResponseModel()) : null;
        }

        public async Task<DescriptionResponseModel?> UpdateDescription(string id, DescriptionRequestModel request)
        {
            var existing = await _unitOfWork.GetRepository<Descriptions>().SingleOrDefaultAsync(x => x.DescriptionSID == id);
            if (existing == null) return null;

            existing.DescriptionName = request.DescriptionName;
            existing.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

            _unitOfWork.GetRepository<Descriptions>().Update(existing);
            await _unitOfWork.SaveAsync();

            return CommonHelper.UpdateModel(existing, new DescriptionResponseModel());
        }

        public async Task<bool> DeleteDescription(string id)
        {
            var existing = await _unitOfWork.GetRepository<Descriptions>().SingleOrDefaultAsync(x => x.DescriptionSID == id);
            if (existing == null) return false;

            _unitOfWork.GetRepository<Descriptions>().Delete(existing);
            await _unitOfWork.SaveAsync();

            return true;
        }
    }
}
