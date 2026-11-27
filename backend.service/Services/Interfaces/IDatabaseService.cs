namespace backend.service.Services.Interfaces
{
    public interface IDatabaseService
    {
        Task<(byte[] FileBytes, string ContentType, string FileName)> ExportDatabaseAsync(CancellationToken cancellationToken = default);
        Task ImportDatabaseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    }
}
