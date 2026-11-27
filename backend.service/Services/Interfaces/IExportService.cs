using backend.model.RequestModel;

namespace backend.service.Services.Interfaces
{
    public interface IExportService
    {
        Task<(byte[] FileContents, string ContentType, string FileName)> ExportTransactionsAsync(ExportRequestModel request, CancellationToken cancellationToken = default);
    }
}
