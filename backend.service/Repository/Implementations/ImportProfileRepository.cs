using System.Text.Json;
using System.Text.RegularExpressions;
using backend.common;
using backend.model.DbModels;
using backend.model.RequestModels;
using backend.model.ResponseModels;
using backend.service.Repository.Interfaces;
using backend.service.Services.Interfaces;
using backend.service.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.service.Repository.Implementations
{
    public sealed class ImportProfileRepository : IImportProfileRepository
    {
        #region Variables & Constructor
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStatementGridParser _parser;
        private readonly IStatementMaskingService _masking;
        private readonly IImportProfileRuleValidator _validator;

        public ImportProfileRepository(
            IUnitOfWork unitOfWork,
            IStatementGridParser parser,
            IStatementMaskingService masking,
            IImportProfileRuleValidator validator)
        {
            _unitOfWork = unitOfWork;
            _parser = parser;
            _masking = masking;
            _validator = validator;
        }
        #endregion

        #region Search
        public async Task<PagedResult<ImportProfileResponse>> SearchAsync(SearchRequestModel request, CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.GetRepository<ImportProfile>()
                .AsQueryable(enableTracking: false)
                .Where(p => p.Status == Constants.StatusType.Active);

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                query = query.Where(x =>
                    x.Name.ToLower().Contains(search) ||
                    x.FileType.ToLower().Contains(search) ||
                    (x.WorksheetName != null && x.WorksheetName.ToLower().Contains(search)));
            }

            query = query.ApplyFilters(request.Filters);
            query = string.IsNullOrWhiteSpace(request.SortBy)
                ? query.OrderBy(x => x.Name)
                : query.ApplySorting(request.SortBy, request.SortOrder);

            var paged = await query.ToPagedResultAsync(request.Page, request.PageSize);
            return new PagedResult<ImportProfileResponse>
            {
                Data = paged.Data.Select(ToProfileResponse).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize
            };
        }
        #endregion

        #region Create Profile
        public async Task<ImportProfileCreateResult> CreateAsync(IFormFile file, ImportProfileRuleRequest rule, CancellationToken cancellationToken = default)
        {
            var parsed = await _parser.ParseWithMetadataAsync(file, cancellationToken);
            var grids = parsed.Grids;
            var grid = grids.FirstOrDefault(item => string.Equals(item.Name, rule.WorksheetName, StringComparison.OrdinalIgnoreCase)) ?? grids.First();
            var ambiguous = _parser.HasAmbiguousDayMonthDates(grid.Rows.SelectMany(row => row.Cells));
            var problems = _validator.Validate(rule, grid, ambiguous).ToList();

            var profileRepo = _unitOfWork.GetRepository<ImportProfile>();
            if (!string.IsNullOrWhiteSpace(rule.Name) &&
                await profileRepo.AsQueryable(enableTracking: false)
                    .AnyAsync(profile => profile.Status == Constants.StatusType.Active && profile.Name == rule.Name.Trim(), cancellationToken))
            {
                problems.Add(new("name_duplicate", "An import profile with this name already exists."));
            }

            if (problems.Count > 0) return new() { Problems = problems };

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var header = grid.Rows.Single(row => row.RowNumber == rule.HeaderRow);
                var footer = grid.Rows.FirstOrDefault(row => row.RowNumber == rule.EndRow + 1);
                // Compute from raw cells, then mask before persistence. Future import matching must mask incoming cells identically before comparing.
                var headerSignature = Signature(header.Cells);
                var footerMarker = footer == null ? null : _masking.MaskCell(string.Join(" ", footer.Cells));
                var footerSignature = footer == null ? null : Signature(footer.Cells);
                var sample = new StatementProfileSample
                {
                    TopRows = grid.Rows.Take(70).Select(Masked).ToList(),
                    BottomRows = grid.Rows.TakeLast(30).Where(row => row.RowNumber > 70).Select(Masked).ToList(),
                    TransactionEndRow = rule.EndRow,
                    FooterMarker = footerMarker
                };
                var sampleJson = JsonSerializer.Serialize(sample);
                var mappingJson = JsonSerializer.Serialize(rule);

                var profile = new ImportProfile
                {
                    ImportProfileSID = Guid.NewGuid().ToString(),
                    Name = rule.Name!.Trim(),
                    FileType = parsed.FileType,
                    WorksheetName = grid.Name,
                    HeaderRow = rule.HeaderRow,
                    HeaderSignature = headerSignature,
                    ColumnMappingJson = mappingJson,
                    DateFormat = rule.DateFormat,
                    SampleJson = sampleJson,
                    Status = Constants.StatusType.Active
                };
                await profileRepo.InsertAsync(profile, cancellationToken);
                await _unitOfWork.SaveAsync();

                var versionRepo = _unitOfWork.GetRepository<ImportProfileVersion>();
                var version = new ImportProfileVersion
                {
                    ProfileID = profile.ImportProfileID,
                    VersionNumber = 1,
                    FileType = profile.FileType.ToUpperInvariant(),
                    WorksheetName = grid.Name,
                    HeaderSignature = headerSignature,
                    HeaderRowIndexHint = rule.HeaderRow,
                    HeaderCellCount = header.Cells.Count,
                    DateColumnIndex = rule.Date,
                    DescriptionCol = rule.Description,
                    DebitCol = rule.Debit,
                    CreditCol = rule.Credit,
                    AmountCol = rule.Amount,
                    BalanceCol = rule.Balance,
                    NotesCol = rule.Notes,
                    DateFormat = rule.DateFormat,
                    FooterMarker = footerMarker,
                    FooterSignature = footerSignature,
                    SecondaryTableMarker = rule.SecondaryTableMarkerRow?.ToString(),
                    SampleJson = sampleJson,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                };
                await versionRepo.InsertAsync(version, cancellationToken);
                await _unitOfWork.SaveAsync();

                profile.CurrentVersionID = version.ImportProfileVersionID;
                profileRepo.Update(profile);
                await _unitOfWork.SaveAsync();

                await _unitOfWork.CommitTransactionAsync();
                return new() { ImportProfileSID = profile.ImportProfileSID, VersionNumber = 1 };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }
        #endregion

        #region Update Profile
        public async Task<ImportProfileCreateResult> UpdateAsync(string profileSID, IFormFile? file, ImportProfileRuleRequest rule, CancellationToken cancellationToken = default)
        {
            var profileRepo = _unitOfWork.GetRepository<ImportProfile>();
            var profile = await profileRepo.AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(item => item.ImportProfileSID == profileSID && item.Status == Constants.StatusType.Active, cancellationToken);
            if (profile == null) return new() { Problems = [new("profile_not_found", "Import profile was not found.")] };

            var current = await _unitOfWork.GetRepository<ImportProfileVersion>().AsQueryable(enableTracking: false)
                .SingleAsync(item => item.ImportProfileVersionID == profile.CurrentVersionID, cancellationToken);

            if (!string.IsNullOrWhiteSpace(rule.Name) &&
                await profileRepo.AsQueryable(enableTracking: false)
                    .AnyAsync(item => item.ImportProfileSID != profileSID && item.Status == Constants.StatusType.Active && item.Name == rule.Name.Trim(), cancellationToken))
            {
                return new() { Problems = [new("name_duplicate", "An import profile with this name already exists.")] };
            }

            if (file == null) return await UpdateWithoutFileAsync(profile, current, rule, cancellationToken);
            return await UpdateWithFileAsync(profile, rule, file, cancellationToken);
        }

        private async Task<ImportProfileCreateResult> UpdateWithoutFileAsync(ImportProfile profile, ImportProfileVersion current, ImportProfileRuleRequest rule, CancellationToken token)
        {
            var sample = JsonSerializer.Deserialize<StatementProfileSample>(current.SampleJson) ?? new();
            if (!string.IsNullOrWhiteSpace(rule.WorksheetName) && !string.Equals(rule.WorksheetName, current.WorksheetName, StringComparison.OrdinalIgnoreCase)
                || rule.HeaderRow != current.HeaderRowIndexHint
                || rule.EndRow != (sample.TransactionEndRow ?? current.HeaderRowIndexHint))
            {
                return new() { Problems = [new("source_layout_locked", "Worksheet, header row, and end row require a new file.")] };
            }

            var problems = _validator.ValidateCoreRules(rule, current.HeaderCellCount);
            if (problems.Count > 0) return new() { Problems = problems.ToList() };

            return await PersistVersionAsync(profile, current.FileType, current.WorksheetName, current.HeaderSignature, current.HeaderRowIndexHint, current.HeaderCellCount, current.FooterMarker, current.FooterSignature, current.SampleJson, rule, token);
        }

        private async Task<ImportProfileCreateResult> UpdateWithFileAsync(ImportProfile profile, ImportProfileRuleRequest rule, IFormFile file, CancellationToken token)
        {
            var parsed = await _parser.ParseWithMetadataAsync(file, token);
            var grids = parsed.Grids;
            var grid = grids.FirstOrDefault(item => string.Equals(item.Name, rule.WorksheetName, StringComparison.OrdinalIgnoreCase)) ?? grids.First();
            var problems = _validator.Validate(rule, grid, _parser.HasAmbiguousDayMonthDates(grid.Rows.SelectMany(row => row.Cells))).ToList();
            if (problems.Count > 0) return new() { Problems = problems };

            var header = grid.Rows.Single(row => row.RowNumber == rule.HeaderRow);
            var footer = grid.Rows.FirstOrDefault(row => row.RowNumber == rule.EndRow + 1);
            // Future import matching must mask incoming cells identically before comparing persisted signatures.
            var signature = Signature(header.Cells);
            var footerSignature = footer == null ? null : Signature(footer.Cells);
            var sample = new StatementProfileSample
            {
                TopRows = grid.Rows.Take(70).Select(Masked).ToList(),
                BottomRows = grid.Rows.TakeLast(30).Where(row => row.RowNumber > 70).Select(Masked).ToList(),
                TransactionEndRow = rule.EndRow,
                FooterMarker = footer == null ? null : _masking.MaskCell(string.Join(" ", footer.Cells))
            };
            return await PersistVersionAsync(profile, parsed.FileType, grid.Name, signature, rule.HeaderRow, header.Cells.Count, sample.FooterMarker, footerSignature, JsonSerializer.Serialize(sample), rule, token);
        }

        private async Task<ImportProfileCreateResult> PersistVersionAsync(
            ImportProfile profile,
            string fileType,
            string? worksheet,
            string signature,
            int headerRow,
            int headerCount,
            string? footerMarker,
            string? footerSignature,
            string sampleJson,
            ImportProfileRuleRequest rule,
            CancellationToken token)
        {
            var profileId = profile.ImportProfileID;
            var profileRepo = _unitOfWork.GetRepository<ImportProfile>();
            var versionRepo = _unitOfWork.GetRepository<ImportProfileVersion>();

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var latest = await versionRepo.AsQueryable(enableTracking: false)
                        .Where(item => item.ProfileID == profile.ImportProfileID)
                        .MaxAsync(item => (int?)item.VersionNumber, token) ?? 0;

                    var version = new ImportProfileVersion
                    {
                        ProfileID = profile.ImportProfileID,
                        VersionNumber = latest + 1,
                        FileType = fileType.ToUpperInvariant(),
                        WorksheetName = worksheet,
                        HeaderSignature = signature,
                        HeaderRowIndexHint = headerRow,
                        HeaderCellCount = headerCount,
                        DateColumnIndex = rule.Date,
                        DescriptionCol = rule.Description,
                        DebitCol = rule.Debit,
                        CreditCol = rule.Credit,
                        AmountCol = rule.Amount,
                        BalanceCol = rule.Balance,
                        NotesCol = rule.Notes,
                        DateFormat = rule.DateFormat,
                        FooterMarker = footerMarker,
                        FooterSignature = footerSignature,
                        SecondaryTableMarker = rule.SecondaryTableMarkerRow?.ToString(),
                        SampleJson = sampleJson,
                        CreatedAt = DateTime.UtcNow.ToString("O")
                    };
                    await versionRepo.InsertAsync(version, token);
                    await _unitOfWork.SaveAsync();

                    profile.Name = rule.Name!.Trim();
                    profile.FileType = fileType;
                    profile.WorksheetName = worksheet;
                    profile.HeaderRow = headerRow;
                    profile.HeaderSignature = signature;
                    profile.ColumnMappingJson = JsonSerializer.Serialize(rule);
                    profile.DateFormat = rule.DateFormat;
                    profile.SampleJson = sampleJson;
                    profile.CurrentVersionID = version.ImportProfileVersionID;
                    profile.LastModifiedDateTime = DateTime.UtcNow.ToString("O");

                    profileRepo.Update(profile);
                    await _unitOfWork.SaveAsync();
                    await _unitOfWork.CommitTransactionAsync();

                    return new() { ImportProfileSID = profile.ImportProfileSID, VersionNumber = version.VersionNumber };
                }
                catch (DbUpdateException exception) when (attempt == 0 && IsVersionNumberConflict(exception))
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    profile = await profileRepo.AsQueryable(enableTracking: false)
                        .SingleAsync(item => item.ImportProfileID == profileId, token);
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }

            throw new InvalidOperationException("Profile version could not be assigned.");
        }
        #endregion

        #region Read & Delete Profiles
        public async Task<ImportProfileVersionResponse?> GetAsync(string profileSID, int? versionNumber, CancellationToken cancellationToken = default)
        {
            var profile = await _unitOfWork.GetRepository<ImportProfile>().AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(item => item.ImportProfileSID == profileSID && item.Status == Constants.StatusType.Active, cancellationToken);
            if (profile == null) return null;

            var versionQuery = _unitOfWork.GetRepository<ImportProfileVersion>().AsQueryable(enableTracking: false);
            var version = versionNumber.HasValue
                ? await versionQuery.SingleOrDefaultAsync(item => item.ProfileID == profile.ImportProfileID && item.VersionNumber == versionNumber, cancellationToken)
                : await versionQuery.SingleOrDefaultAsync(item => item.ImportProfileVersionID == profile.CurrentVersionID, cancellationToken);
            if (version == null) return null;

            var sample = JsonSerializer.Deserialize<StatementProfileSample>(version.SampleJson) ?? new();
            return new()
            {
                ImportProfileSID = profile.ImportProfileSID,
                Name = profile.Name,
                VersionNumber = version.VersionNumber,
                FileType = version.FileType,
                WorksheetName = version.WorksheetName,
                HeaderRow = version.HeaderRowIndexHint,
                EndRow = sample.TransactionEndRow ?? version.HeaderRowIndexHint,
                DateFormat = version.DateFormat,
                Date = version.DateColumnIndex,
                Description = version.DescriptionCol,
                Debit = version.DebitCol,
                Credit = version.CreditCol,
                Amount = version.AmountCol,
                Balance = version.BalanceCol,
                Notes = version.NotesCol,
                SecondaryTableMarkerRow = int.TryParse(version.SecondaryTableMarker, out var marker) ? marker : null,
                Sample = sample
            };
        }

        public async Task<IReadOnlyList<ImportProfileResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var profiles = await _unitOfWork.GetRepository<ImportProfile>().AsQueryable(enableTracking: false)
                .Where(p => p.Status == Constants.StatusType.Active)
                .OrderBy(p => p.Name)
                .ToListAsync(cancellationToken);

            return profiles.Select(ToProfileResponse).ToList();
        }

        public async Task<bool> DeleteAsync(string profileSID, CancellationToken cancellationToken = default)
        {
            var repo = _unitOfWork.GetRepository<ImportProfile>();
            var profile = await repo.SingleOrDefaultAsync(p => p.ImportProfileSID == profileSID && p.Status == Constants.StatusType.Active);
            if (profile == null) return false;

            profile.Status = Constants.StatusType.Delete;
            profile.LastModifiedDateTime = DateTime.UtcNow.ToString("O");
            repo.Update(profile);
            await _unitOfWork.SaveAsync();
            return true;
        }
        #endregion

        #region Helpers
        private static bool IsVersionNumberConflict(DbUpdateException exception) =>
            exception.InnerException is SqliteException { SqliteErrorCode: 19 };

        private StatementProfileSampleRow Masked(StatementGridRow row) => new() { RowNumber = row.RowNumber, Values = _masking.MaskRow(row.Cells) };

        private string Signature(IEnumerable<string> raw) => string.Join('|', _masking.MaskRow(raw).Select(Normalize));

        private static string Normalize(string value) => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");

        private static ImportProfileResponse ToProfileResponse(ImportProfile profile)
        {
            var sample = string.IsNullOrWhiteSpace(profile.SampleJson) ? null : JsonSerializer.Deserialize<StatementProfileSample>(profile.SampleJson, JsonOptions);
            var headerValues = sample?.TopRows.FirstOrDefault(row => row.RowNumber == profile.HeaderRow)?.Values;
            var headers = headerValues == null
                ? profile.HeaderSignature.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
                : MakeUniqueHeaders(headerValues);
            var mapping = DeserializeMapping(profile.ColumnMappingJson, headerValues ?? profile.HeaderSignature.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList());

            return new ImportProfileResponse
            {
                ImportProfileSID = profile.ImportProfileSID,
                Name = profile.Name,
                FileType = profile.FileType,
                WorksheetName = profile.WorksheetName,
                HeaderRow = profile.HeaderRow,
                TransactionEndRow = sample?.TransactionEndRow,
                Headers = headers,
                Mapping = mapping,
                DateFormat = profile.DateFormat,
                Sample = sample
            };
        }

        internal static StatementColumnMapping DeserializeMapping(string json, IReadOnlyList<string>? headers = null)
        {
            try { return JsonSerializer.Deserialize<StatementColumnMapping>(json, JsonOptions) ?? new(); }
            catch (JsonException)
            {
                var rule = JsonSerializer.Deserialize<ImportProfileRuleRequest>(json, JsonOptions) ?? new();
                var headerList = headers ?? [];
                string? HeaderAt(int? index) => index.HasValue && index.Value >= 0 && index.Value < headerList.Count ? headerList[index.Value] : null;
                return new()
                {
                    Date = HeaderAt(rule.Date),
                    Description = HeaderAt(rule.Description),
                    Debit = HeaderAt(rule.Debit),
                    Credit = HeaderAt(rule.Credit),
                    Amount = HeaderAt(rule.Amount),
                    Balance = HeaderAt(rule.Balance),
                    Notes = HeaderAt(rule.Notes)
                };
            }
        }

        internal static List<string> MakeUniqueHeaders(IReadOnlyList<string> row)
        {
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            return row.Select((cell, i) =>
            {
                var baseName = string.IsNullOrWhiteSpace(cell) ? $"Column {i + 1}" : cell.Trim();
                used.TryGetValue(baseName, out var count);
                used[baseName] = count + 1;
                return count == 0 ? baseName : $"{baseName} ({count + 1})";
            }).ToList();
        }
        #endregion
    }
}
