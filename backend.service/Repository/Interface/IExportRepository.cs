using backend.common.Models;

namespace backend.service.Repository.Interface
{
    public interface IExportRepository
    {
        Task<(byte[] FileContents, string ContentType, string FileName)> ExportTransactionsAsync(ExportRequestModel request);
    }
}
