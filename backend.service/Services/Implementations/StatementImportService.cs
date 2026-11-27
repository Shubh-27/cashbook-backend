using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using backend.common;
using backend.model.TenantDB;
using backend.model.RequestModel;
using backend.model.ResponseModel;
using backend.service.Helpers;
using backend.service.Repository.Implementations;
using backend.service.Repository.Interface;
using backend.service.Services.Interfaces;
using backend.service.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace backend.service.Services.Implementations
{
    public partial class StatementImportService : IStatementImportService
    {
        #region Variables & Constructors
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private const int MaxRows = 25_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStatementMaskingService _maskingService;
    private readonly IStatementGridParser _parser;
    private readonly IImportProfileService _profileService;

    public StatementImportService(
        IUnitOfWork unitOfWork,
        IStatementMaskingService maskingService,
        IStatementGridParser parser,
        IImportProfileService profileService)
    {
        _unitOfWork = unitOfWork;
        _maskingService = maskingService;
        _parser = parser;
        _profileService = profileService;
    }

    public StatementImportService(IUnitOfWork unitOfWork, IStatementMaskingService maskingService, IStatementGridParser parser)
        : this(unitOfWork, maskingService, parser, new ImportProfileService(new ImportProfileRepository(unitOfWork), unitOfWork, parser, maskingService, new ImportProfileRuleValidator()))
    {
    }

    public StatementImportService(IUnitOfWork unitOfWork, IStatementMaskingService maskingService)
        : this(unitOfWork, maskingService, new StatementGridParser())
    {
    }

    public StatementImportService(IUnitOfWork unitOfWork)
        : this(unitOfWork, new StatementMaskingService(), new StatementGridParser())
    {
    }
    #endregion

    #region Analyze
    /// <summary>
    /// Parses and analyzes a bank statement file for duplicate detection and column mapping.
    /// </summary>
    public async Task<StatementImportAnalysis> AnalyzeAsync(
        IFormFile file,
        string? sheetName,
        int? headerRow,
        int? transactionEndRow,
        string? profileSID,
        string? dateFormat,
        string? mappingJson,
        string? accountSID,
        CancellationToken cancellationToken = default)
    {
        StatementParseResult parsed;
        try { parsed = await _parser.ParseWithMetadataAsync(file, cancellationToken); }
        catch (StatementParsingException exception) { throw new HttpStatusCodeException(400, exception.Message); }
        var fileType = parsed.FileType;
        var grids = parsed.Grids;

        var profileRepo = _unitOfWork.GetRepository<ImportProfile>();
        var versionRepo = _unitOfWork.GetRepository<ImportProfileVersion>();
        var profile = !string.IsNullOrWhiteSpace(profileSID)
            ? await profileRepo.AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(p => p.ImportProfileSID == profileSID && p.Status == Constants.StatusType.Active, cancellationToken)
            : null;
        var selectedGrid = grids.FirstOrDefault(g => string.Equals(g.Name, sheetName ?? profile?.WorksheetName, StringComparison.OrdinalIgnoreCase)) ?? grids[0];
        var selected = new SourceSheet(
            selectedGrid.Name,
            selectedGrid.Rows.Select(row => row.Cells).ToList(),
            selectedGrid.TotalRows,
            selectedGrid.Rows.TakeLast(30).Select(row => row.Cells).ToList());
        var candidates = FindHeaderCandidates(selected.Rows);
        profile ??= await SuggestProfileAsync(fileType, selected, candidates, cancellationToken);
        var profileVersion = profile?.CurrentVersionID is int currentVersionId
            ? await versionRepo.AsQueryable(enableTracking: false)
                .SingleOrDefaultAsync(version => version.ImportProfileVersionID == currentVersionId, cancellationToken)
            : null;

        var selectedHeader = headerRow ?? FindProfileHeaderRow(profile, selected.Rows) ?? profile?.HeaderRow ?? candidates.FirstOrDefault()?.RowNumber ?? 1;
        if (selectedHeader < 1 || selectedHeader > selected.Rows.Count) throw new HttpStatusCodeException(400, "The selected header row is outside the statement.");
        var headers = ImportProfileService.MakeUniqueHeaders(selected.Rows[selectedHeader - 1]);
        StatementColumnMapping mapping;
        if (!string.IsNullOrWhiteSpace(mappingJson))
        {
            try { mapping = ImportProfileService.DeserializeMapping(mappingJson, headers); }
            catch (JsonException) { throw new HttpStatusCodeException(400, "Invalid column mapping JSON."); }
        }
        else
        {
            mapping = profileVersion != null
                ? MappingFromIndexes(headers, profileVersion)
                : profile == null ? SuggestMapping(headers) : ImportProfileService.DeserializeMapping(profile.ColumnMappingJson, headers);
        }
        var effectiveDateFormat = dateFormat ?? profileVersion?.DateFormat ?? profile?.DateFormat;
        var transactionEnd = ResolveTransactionEndRow(selected.Rows, selectedHeader, candidates, profile, transactionEndRow, profileVersion?.FooterMarker);
        var result = new StatementImportAnalysis
        {
            FileName = file.FileName,
            FileType = fileType,
            FileHash = parsed.FileHash,
            Sheets = grids.Select(g => g.Name).ToList(),
            SelectedSheet = selected.Name,
            HeaderRow = selectedHeader,
            TransactionEndRow = transactionEnd,
            HeaderCandidates = candidates,
            Headers = headers,
            Mapping = mapping,
            DateFormat = effectiveDateFormat,
            Metadata = ExtractMetadata(selected.Rows, selectedHeader),
            SuggestedProfileSID = profile?.ImportProfileSID,
            Sample = CreateProfileSample(selected, transactionEnd)
        };

        var indexes = headers.Select((h, i) => (h, i)).ToDictionary(x => x.h, x => x.i, StringComparer.OrdinalIgnoreCase);
        for (var index = selectedHeader; index < Math.Min(transactionEnd, selected.Rows.Count); index++)
        {
            var source = selected.Rows[index];
            if (source.All(string.IsNullOrWhiteSpace)) continue;
            if (result.Rows.Count >= MaxRows) throw new HttpStatusCodeException(400, "Statements may contain at most 25,000 transaction rows.");
            result.Rows.Add(ParseRow(source, indexes, mapping, effectiveDateFormat, index + 1));
        }

        if (!string.IsNullOrWhiteSpace(accountSID)) await MarkDuplicatesAsync(result.Rows, accountSID, cancellationToken);

        return result;
    }
    #endregion

    #region Commit
    /// <summary>
    /// Commits validated statement rows as transactions within a tracked import batch.
    /// </summary>
    public async Task<StatementImportCommitResult> CommitAsync(CommitStatementImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SaveProfile != null) throw new HttpStatusCodeException(400, "Create reusable profiles from Import Profiles using a sample statement file.");
        var account = await _unitOfWork.GetRepository<Account>().AsQueryable(enableTracking: false)
            .SingleOrDefaultAsync(a => a.AccountSID == request.AccountSID && a.Status == Constants.StatusType.Active, cancellationToken)
            ?? throw new HttpStatusCodeException(400, "Select an active CashBook account.");
        var fileHash = NormalizeFileHash(request.FileHash);
        var batchRepo = _unitOfWork.GetRepository<ImportBatch>();
        if (fileHash != null && await batchRepo.AsQueryable(enableTracking: false).AnyAsync(batch => batch.AccountID == account.AccountID && batch.FileHash == fileHash && batch.UndoneDateTime == null, cancellationToken))
            throw new HttpStatusCodeException(400, "This statement file has already been imported for the selected account.");
        if (request.Rows.Count == 0 || request.Rows.Count > MaxRows) throw new HttpStatusCodeException(400, "There are no valid statement rows to import.");

        var selected = request.Rows.Where(r => r.Include).ToList();
        if (selected.Count == 0) throw new HttpStatusCodeException(400, "Select at least one valid row to import.");
        var validationFailures = selected.Where(r => ValidateRow(r).Count > 0).ToList();
        if (validationFailures.Count > 0) throw new HttpStatusCodeException(400, "Fix or exclude invalid rows before importing.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            ImportProfile? profile = null;
            ImportProfileVersion? profileVersion = null;
            if (!string.IsNullOrWhiteSpace(request.ProfileSID))
                profile = await _unitOfWork.GetRepository<ImportProfile>().AsQueryable(enableTracking: false)
                    .SingleOrDefaultAsync(p => p.ImportProfileSID == request.ProfileSID && p.Status == Constants.StatusType.Active, cancellationToken);
            if (profile?.CurrentVersionID is int currentVersionId)
                profileVersion = await _unitOfWork.GetRepository<ImportProfileVersion>().AsQueryable(enableTracking: false)
                    .SingleOrDefaultAsync(version => version.ImportProfileVersionID == currentVersionId, cancellationToken);

            var importTime = DateTime.UtcNow.ToString("O");
            var batch = new ImportBatch
            {
                ImportBatchSID = Guid.NewGuid().ToString(),
                ImportProfileID = profile?.ImportProfileID,
                ProfileVersionID = profileVersion?.ImportProfileVersionID,
                AccountID = account.AccountID,
                SourceFileName = Path.GetFileName(request.FileName),
                FileType = request.FileType.ToLowerInvariant(),
                FileHash = fileHash,
                // Metadata comes from statement headings. Mask it again here so a caller
                // cannot bypass the analysis response and persist unmasked identifiers.
                StatementMetadataJson = JsonSerializer.Serialize(MaskMetadata(request.Metadata), JsonOptions),
                TotalRows = request.Rows.Count,
                InvalidRows = request.Rows.Count(r => ValidateRow(r).Count > 0),
                Status = Constants.StatusType.Active
            };
            await batchRepo.InsertAsync(batch, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            var transactionRepo = _unitOfWork.GetRepository<Transaction>();

            var descriptionRepo = _unitOfWork.GetRepository<Description>();
            var targetNames = selected
                .Select(r => r.Description?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!.ToLower())
                .Distinct()
                .ToList();
            var descriptions = targetNames.Count == 0
                ? new List<Description>()
                : await descriptionRepo.AsQueryable(enableTracking: false)
                    .Where(d => d.Status == Constants.StatusType.Active && targetNames.Contains(d.DescriptionName.ToLower()))
                    .ToListAsync(cancellationToken);
            var descriptionLookup = descriptions.ToDictionary(d => d.DescriptionName, StringComparer.OrdinalIgnoreCase);

            var newDescriptions = new List<Description>();
            var rowsToInsert = new List<(StatementImportRow Row, Description Description)>();
            var skipped = request.Rows.Count(r => !r.Include && string.Equals(r.Status, "exact_duplicate", StringComparison.OrdinalIgnoreCase));

            foreach (var row in selected)
            {
                var (description, isNew) = GetOrCreateDescription(row.Description!, descriptionLookup);
                if (isNew) newDescriptions.Add(description);
                rowsToInsert.Add((row, description));
            }

            if (newDescriptions.Count > 0)
            {
                await descriptionRepo.InsertRangeAsync(newDescriptions, cancellationToken);
                await _unitOfWork.SaveAsync(cancellationToken);
            }

            var transactions = rowsToInsert.Select(item => new Transaction
            {
                TransactionSID = Guid.NewGuid().ToString(),
                TransactionDate = item.Row.TransactionDate!,
                AccountID = account.AccountID,
                ImportBatchID = batch.ImportBatchID,
                RowHash = RowHash(item.Row),
                DescriptionID = item.Description.DescriptionID,
                Debit = item.Row.Debit ?? 0,
                Credit = item.Row.Credit ?? 0,
                Balance = item.Row.Balance ?? 0,
                Notes = item.Row.Notes,
                CreatedDateTime = importTime,
                LastModifiedDateTime = importTime,
                Status = Constants.StatusType.Active
            }).ToList();

            if (transactions.Count > 0)
                await transactionRepo.InsertRangeAsync(transactions, cancellationToken);

            batch.ImportedRows = transactions.Count;
            batch.SkippedDuplicateRows = skipped;
            batchRepo.Update(batch);
            await _unitOfWork.SaveAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return new StatementImportCommitResult { ImportBatchSID = batch.ImportBatchSID, ImportedRows = transactions.Count, SkippedDuplicateRows = skipped, InvalidRows = batch.InvalidRows };
        }
        catch { await _unitOfWork.RollbackTransactionAsync(); throw; }
    }
    #endregion

    #region Profiles & History
    /// <summary>
    /// Lists all active import profiles.
    /// </summary>
    public Task<IReadOnlyList<ImportProfileResponse>> GetProfilesAsync(CancellationToken cancellationToken = default)
        => _profileService.GetAllAsync(cancellationToken);

    /// <summary>
    /// Retrieves recent statement import batches.
    /// </summary>
    public async Task<IReadOnlyList<ImportBatchResponse>> GetHistoryAsync(CancellationToken cancellationToken = default) => await _unitOfWork.GetRepository<ImportBatch>()
        .AsQueryable(enableTracking: false)
        .Include(b => b.Account).Include(b => b.ImportProfile).OrderByDescending(b => b.CreatedDateTime).Take(100)
        .Select(b => new ImportBatchResponse { ImportBatchSID = b.ImportBatchSID, SourceFileName = b.SourceFileName, ProfileName = b.ImportProfile != null ? b.ImportProfile.Name : null, AccountName = b.Account.AccountName, TotalRows = b.TotalRows, ImportedRows = b.ImportedRows, SkippedDuplicateRows = b.SkippedDuplicateRows, InvalidRows = b.InvalidRows, CreatedDateTime = b.CreatedDateTime, UndoneDateTime = b.UndoneDateTime }).ToListAsync(cancellationToken);

    /// <summary>
    /// Soft-deletes unmodified transactions from an import batch and marks it undone.
    /// </summary>
    public async Task<bool> UndoAsync(string batchSID, CancellationToken cancellationToken = default)
    {
        var batchRepo = _unitOfWork.GetRepository<ImportBatch>();
        var batch = await batchRepo.SingleOrDefaultAsync(
            b => b.ImportBatchSID == batchSID && b.Status == Constants.StatusType.Active && b.UndoneDateTime == null,
            cancellationToken: cancellationToken);
        if (batch == null) return false;

        var transactionRepo = _unitOfWork.GetRepository<Transaction>();
        var transactions = await transactionRepo.AsQueryable()
            .Where(t => t.ImportBatchID == batch.ImportBatchID && t.Status == Constants.StatusType.Active && t.CreatedDateTime == t.LastModifiedDateTime)
            .ToListAsync(cancellationToken);
        foreach (var transaction in transactions)
        {
            transaction.Status = Constants.StatusType.Delete;
            transactionRepo.Update(transaction);
        }
        batch.UndoneDateTime = DateTime.UtcNow.ToString("O");
        batchRepo.Update(batch);
        await _unitOfWork.SaveAsync(cancellationToken);
        return true;
    }
    #endregion

    #region Parsing & Duplicate Helpers
    private async Task<ImportProfile?> SuggestProfileAsync(string extension, SourceSheet sheet, List<StatementHeaderCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var profileCandidates = await _unitOfWork.GetRepository<ImportProfile>().AsQueryable(enableTracking: false)
            .Where(p => p.Status == Constants.StatusType.Active && p.FileType == extension.TrimStart('.')).ToListAsync(cancellationToken);
        var headers = candidates.FirstOrDefault()?.Values ?? sheet.Rows.FirstOrDefault() ?? [];
        // Signatures are persisted only after masking, so imported headers must be masked before matching them.
        var normalized = _maskingService.MaskRow(headers).Select(Normalize).Where(x => x.Length > 0).ToHashSet();
        return profileCandidates.Select(p => new { Profile = p, Score = p.HeaderSignature.Split('|').Count(normalized.Contains) }).Where(x => x.Score >= 2).OrderByDescending(x => x.Score).Select(x => x.Profile).FirstOrDefault();
    }

    private int? FindProfileHeaderRow(ImportProfile? profile, List<List<string>> rows)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.HeaderSignature)) return null;
        var expected = profile.HeaderSignature.Split('|', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        return rows.Take(50).Select((row, index) => new { Row = index + 1, Score = _maskingService.MaskRow(row).Select(Normalize).Count(expected.Contains) })
            .Where(x => x.Score >= 2).OrderByDescending(x => x.Score).Select(x => (int?)x.Row).FirstOrDefault();
    }

    private static int ResolveTransactionEndRow(List<List<string>> rows, int headerRow, List<StatementHeaderCandidate> candidates, ImportProfile? profile, int? requestedEndRow, string? versionFooterMarker = null)
    {
        if (requestedEndRow.HasValue && requestedEndRow.Value >= headerRow) return Math.Min(requestedEndRow.Value, rows.Count);

        var marker = versionFooterMarker ?? DeserializeSample(profile?.SampleJson)?.FooterMarker;
        if (!string.IsNullOrWhiteSpace(marker))
        {
            var normalizedMarker = Normalize(marker);
            var markerRow = rows.Select((row, index) => new { RowNumber = index + 1, Row = row })
                .Where(row => row.RowNumber > headerRow && row.Row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                .FirstOrDefault(row => Normalize(string.Join(" ", row.Row)).Contains(normalizedMarker, StringComparison.Ordinal));
            if (markerRow != null) return markerRow.RowNumber - 1;
        }

        var nextHeader = candidates.Where(candidate => candidate.RowNumber > headerRow && candidate.Score >= 2)
            .OrderBy(candidate => candidate.RowNumber).FirstOrDefault();
        var fallbackEnd = nextHeader == null ? rows.Count : nextHeader.RowNumber - 1;
        var footerStart = rows.Select((row, index) => new { RowNumber = index + 1, Values = row })
            .FirstOrDefault(row => row.RowNumber > headerRow && row.RowNumber <= fallbackEnd
                && row.Values.Count(value => !string.IsNullOrWhiteSpace(value)) == 1
                && string.Join(" ", row.Values).Trim().Length >= 20);
        return footerStart == null ? fallbackEnd : footerStart.RowNumber - 1;
    }

    private static (Description Description, bool IsNew) GetOrCreateDescription(string name, Dictionary<string, Description> lookup)
    {
        var trimmed = name.Trim();
        if (lookup.TryGetValue(trimmed, out var existing)) return (existing, false);
        var description = new Description { DescriptionSID = Guid.NewGuid().ToString(), DescriptionName = trimmed, Status = Constants.StatusType.Active };
        lookup[description.DescriptionName] = description;
        return (description, true);
    }

    private async Task MarkDuplicatesAsync(List<StatementImportRow> rows, string accountSID, CancellationToken cancellationToken = default)
    {
        var validRows = rows.Where(r => r.Issues.Count == 0).ToList();
        if (validRows.Count == 0) return;

        var account = await _unitOfWork.GetRepository<Account>().AsQueryable(enableTracking: false)
            .SingleOrDefaultAsync(a => a.AccountSID == accountSID && a.Status == Constants.StatusType.Active, cancellationToken);
        if (account == null) return;

        var parsedDates = validRows
            .Select(r => DateTime.TryParse(r.TransactionDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt.Date : (DateTime?)null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        var query = _unitOfWork.GetRepository<Transaction>().AsQueryable(enableTracking: false)
            .Where(t => t.AccountID == account.AccountID && t.Status == Constants.StatusType.Active);

        if (parsedDates.Count > 0)
        {
            // Pad by +/-1 day so timezone offsets in stored ISO strings are still matched.
            var minBound = parsedDates.Min().AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var maxBound = parsedDates.Max().AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            query = query.Where(t => string.Compare(t.TransactionDate, minBound) >= 0 && string.Compare(t.TransactionDate, maxBound) < 0);
        }

        var existing = await query
            .Select(t => new { t.TransactionDate, DescriptionName = t.Description != null ? t.Description.DescriptionName : null, t.Debit, t.Credit })
            .ToListAsync(cancellationToken);
        var exact = existing.Select(t => Fingerprint(t.TransactionDate, t.DescriptionName, t.Debit, t.Credit)).ToHashSet(StringComparer.Ordinal);
        var possible = existing.Select(t => PossibleFingerprint(t.TransactionDate, t.Debit, t.Credit)).ToHashSet(StringComparer.Ordinal);
        foreach (var row in validRows)
        {
            if (exact.Contains(Fingerprint(row))) { row.Status = "exact_duplicate"; row.Include = false; }
            else if (possible.Contains(PossibleFingerprint(row))) row.Status = "possible_duplicate";
        }
    }

    private static StatementColumnMapping MappingFromIndexes(IReadOnlyList<string> headers, ImportProfileVersion version)
    {
        string? HeaderAt(int? index) => index.HasValue && index.Value >= 0 && index.Value < headers.Count ? headers[index.Value] : null;
        return new()
        {
            Date = HeaderAt(version.DateColumnIndex),
            Description = HeaderAt(version.DescriptionCol),
            Debit = HeaderAt(version.DebitCol),
            Credit = HeaderAt(version.CreditCol),
            Amount = HeaderAt(version.AmountCol),
            Balance = HeaderAt(version.BalanceCol),
            Notes = HeaderAt(version.NotesCol)
        };
    }

    private static StatementProfileSample? DeserializeSample(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<StatementProfileSample>(json, JsonOptions);

    private static string? NormalizeFileHash(string? fileHash)
    {
        if (string.IsNullOrWhiteSpace(fileHash)) return null;
        var normalized = fileHash.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new HttpStatusCodeException(400, "The statement file hash is invalid.");
        return normalized;
    }

    private StatementProfileSample CreateProfileSample(SourceSheet sheet, int transactionEndRow)
    {
        var topRows = sheet.Rows.Take(Constants.StatementSampleTopRows).Select((values, index) => new StatementProfileSampleRow { RowNumber = index + 1, Values = _maskingService.MaskRow(values) }).ToList();
        var bottomStart = sheet.TotalRows - sheet.BottomRows.Count + 1;
        var bottomRows = sheet.BottomRows.Select((values, index) => new StatementProfileSampleRow { RowNumber = bottomStart + index, Values = _maskingService.MaskRow(values) })
            .Where(row => row.RowNumber > Constants.StatementSampleTopRows).ToList();
        var footerMarker = sheet.Rows.Skip(transactionEndRow).Select(row => Normalize(string.Join(" ", _maskingService.MaskRow(row)))).FirstOrDefault(text => text.Length >= 8);
        return ResponseMappingExtensions.BuildProfileSample(
            topRows,
            bottomRows,
            transactionEndRow,
            footerMarker?[..Math.Min(32, footerMarker.Length)]);
    }

    private static List<StatementHeaderCandidate> FindHeaderCandidates(List<List<string>> rows) => rows.Take(50).Select((row, index) => new StatementHeaderCandidate { RowNumber = index + 1, Score = row.Sum(HeaderScore), Values = row }).OrderByDescending(c => c.Score).ThenBy(c => c.RowNumber).ToList();
    private static int HeaderScore(string value) { var text = Normalize(value); if (text.Contains("date")) return 2; if (text.Contains("narration") || text.Contains("description") || text.Contains("particular")) return 2; if (text.Contains("debit") || text.Contains("withdraw") || text == "dr" || text.Contains("credit") || text.Contains("deposit") || text == "cr" || text.Contains("amount") || text.Contains("balance")) return 1; return 0; }
    private static StatementColumnMapping SuggestMapping(List<string> headers) => new() { Date = FirstHeader(headers, "date"), Description = FirstHeader(headers, "narration", "description", "particular", "detail"), Debit = FirstHeader(headers, "debit", "withdraw", "dr"), Credit = FirstHeader(headers, "credit", "deposit", "cr"), Amount = FirstHeader(headers, "amount"), Balance = FirstHeader(headers, "balance") };
    private static string? FirstHeader(IEnumerable<string> headers, params string[] aliases) => headers.FirstOrDefault(h => aliases.Any(a => Normalize(h).Contains(a)));
    private Dictionary<string, string> ExtractMetadata(List<List<string>> rows, int headerRow)
    {
        var data = new Dictionary<string, string>();
        foreach (var row in rows.Take(Math.Max(0, headerRow - 1)))
        {
            var text = string.Join(" ", row.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            if (text.Length == 0) continue;
            var pair = text.Split(':', 2);
            if (pair.Length == 2 && pair[0].Length <= 40)
                data[_maskingService.MaskCell(pair[0].Trim())] = _maskingService.MaskCell(pair[1].Trim());
            else if (!data.ContainsKey("Statement title"))
                data["Statement title"] = _maskingService.MaskCell(text);
        }
        return data;
    }

    private Dictionary<string, string> MaskMetadata(IReadOnlyDictionary<string, string>? metadata) => (metadata ?? new Dictionary<string, string>())
        .ToDictionary(pair => _maskingService.MaskCell(pair.Key), pair => _maskingService.MaskCell(pair.Value));
    private static StatementImportRow ParseRow(List<string> values, Dictionary<string, int> indexes, StatementColumnMapping mapping, string? dateFormat, int rowNumber)
    {
        string? Get(string? header) => header != null && indexes.TryGetValue(header, out var i) && i < values.Count ? values[i].Trim() : null;
        var row = new StatementImportRow { RowNumber = rowNumber, Description = Get(mapping.Description), Notes = Get(mapping.Notes), Debit = ParseAmount(Get(mapping.Debit)), Credit = ParseAmount(Get(mapping.Credit)), Balance = ParseAmount(Get(mapping.Balance)) };
        var amount = ParseAmount(Get(mapping.Amount)); if (amount.HasValue) { if (amount.Value < 0) row.Debit = Math.Abs(amount.Value); else row.Credit = amount.Value; }
        var dateText = Get(mapping.Date); if (TryParseDate(dateText, dateFormat, out var date)) row.TransactionDate = date.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture); else row.Issues.Add(string.IsNullOrWhiteSpace(dateText) ? "Transaction date is required." : "Choose the correct date format or fix this date.");
        row.Issues.AddRange(ValidateRow(row)); row.Issues = row.Issues.Distinct().ToList(); row.Status = row.Issues.Count > 0 ? "invalid" : "new"; row.Include = row.Issues.Count == 0; return row;
    }
    private static List<string> ValidateRow(StatementImportRow row) { var issues = new List<string>(); if (string.IsNullOrWhiteSpace(row.TransactionDate)) issues.Add("Transaction date is required."); else if (!DateTime.TryParseExact(row.TransactionDate, "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) issues.Add("Transaction date must be a valid ISO date."); if (string.IsNullOrWhiteSpace(row.Description)) issues.Add("Description is required."); else if (row.Description.Trim().Length > 100) issues.Add("Description cannot exceed 100 characters."); if (row.Notes?.Length > 500) issues.Add("Notes cannot exceed 500 characters."); if ((row.Debit.HasValue && !double.IsFinite(row.Debit.Value)) || (row.Credit.HasValue && !double.IsFinite(row.Credit.Value))) issues.Add("Amounts must be finite numbers."); if ((row.Debit ?? 0) <= 0 && (row.Credit ?? 0) <= 0) issues.Add("A debit or credit amount is required."); if ((row.Debit ?? 0) > 0 && (row.Credit ?? 0) > 0) issues.Add("A row cannot contain both debit and credit."); return issues; }
    private static bool TryParseDate(string? value, string? format, out DateTime date) { if (!string.IsNullOrWhiteSpace(format) && DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true; return DateTime.TryParseExact(value, ["yyyy-MM-dd", "yyyy/MM/dd", "dd-MMM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date); }
    private static double? ParseAmount(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var cleaned = AmountCleanupRegex().Replace(value, ""); if (cleaned.StartsWith('(') && cleaned.EndsWith(')')) cleaned = "-" + cleaned[1..^1]; return double.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ? amount : null; }
    private static string Fingerprint(StatementImportRow row) => Fingerprint(row.TransactionDate, row.Description, row.Debit, row.Credit);
    private static string Fingerprint(string? date, string? description, double? debit, double? credit) => $"{CanonicalDate(date)}|{Normalize(description)}|{debit ?? 0:0.00}|{credit ?? 0:0.00}";
    private static string RowHash(StatementImportRow row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{CanonicalDate(row.TransactionDate)}|{Normalize(row.Description)}|{row.Debit ?? 0:0.00}|{row.Credit ?? 0:0.00}|{row.Balance ?? 0:0.00}"))).ToLowerInvariant();
    private static string PossibleFingerprint(string? date, double? debit, double? credit) => $"{CanonicalDate(date)}|{debit ?? 0:0.00}|{credit ?? 0:0.00}";
    private static string PossibleFingerprint(StatementImportRow r) => PossibleFingerprint(r.TransactionDate, r.Debit, r.Credit);
    private static string CanonicalDate(string? date) => DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : date ?? "";
    private static string Normalize(string? value) => AlphanumericRegex().Replace((value ?? "").Trim().ToLowerInvariant(), "");

    [GeneratedRegex("[^0-9.\\-()]")]
    private static partial Regex AmountCleanupRegex();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex AlphanumericRegex();
    #endregion

    private sealed record SourceSheet(string Name, List<List<string>> Rows, int TotalRows, List<List<string>> BottomRows);
    }
}
