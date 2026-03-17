using backend.common.Models;

namespace backend.service.Service.Interface
{
    public interface IExportService
    {
        Task<(byte[] FileContents, string ContentType, string FileName)> ExportTransactionsAsync(ExportRequestModel request);
    }
}
