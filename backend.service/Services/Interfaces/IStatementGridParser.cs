using backend.model.RequestModel;
using backend.model.ResponseModel;
using Microsoft.AspNetCore.Http;

namespace backend.service.Services.Interfaces;

public sealed record StatementParseResult(string FileType, string FileHash, IReadOnlyList<StatementWorksheetGrid> Grids);

public interface IStatementGridParser
{
    Task<string> DetectFileTypeAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<StatementParseResult> ParseWithMetadataAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementWorksheetGrid>> ParseAsync(IFormFile file, CancellationToken cancellationToken = default);
    HeaderMatchResult MatchHeader(IReadOnlyList<string> savedHeaderCells, IReadOnlyList<string> candidateHeaderCells);
    bool HasAmbiguousDayMonthDates(IEnumerable<string> values);
}
