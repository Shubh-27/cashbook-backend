namespace backend.service.Services.Interfaces
{
    public interface IDatabaseService
    {
        Task<(byte[] FileBytes, string ContentType, string FileName)> ExportDatabaseAsync();
        Task ImportDatabaseAsync(Stream fileStream, string fileName);
    }
}
