using backend.model.ResponseModel;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Interfaces;

public interface IImportProfilePreviewService
{
    Task<ImportProfilePreviewResponse> PreviewAsync(IFormFile file, string? worksheetName, CancellationToken cancellationToken = default);
}
