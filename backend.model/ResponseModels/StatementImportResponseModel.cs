using System.Text.Json.Serialization;
using backend.model.RequestModels;
using Newtonsoft.Json;

namespace backend.model.ResponseModels
{
    public class StatementImportAnalysis
    {
        [JsonProperty("file_name")]
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = null!;

        [JsonProperty("file_type")]
        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = null!;

        [JsonProperty("file_hash")]
        [JsonPropertyName("file_hash")]
        public string FileHash { get; set; } = null!;

        [JsonProperty("sheets")]
        [JsonPropertyName("sheets")]
        public List<string> Sheets { get; set; } = [];

        [JsonProperty("selected_sheet")]
        [JsonPropertyName("selected_sheet")]
        public string SelectedSheet { get; set; } = null!;

        [JsonProperty("header_row")]
        [JsonPropertyName("header_row")]
        public int HeaderRow { get; set; }

        [JsonProperty("transaction_end_row")]
        [JsonPropertyName("transaction_end_row")]
        public int? TransactionEndRow { get; set; }

        [JsonProperty("header_candidates")]
        [JsonPropertyName("header_candidates")]
        public List<StatementHeaderCandidate> HeaderCandidates { get; set; } = [];

        [JsonProperty("headers")]
        [JsonPropertyName("headers")]
        public List<string> Headers { get; set; } = [];

        [JsonProperty("mapping")]
        [JsonPropertyName("mapping")]
        public StatementColumnMapping Mapping { get; set; } = new();

        [JsonProperty("date_format")]
        [JsonPropertyName("date_format")]
        public string? DateFormat { get; set; }

        [JsonProperty("rows")]
        [JsonPropertyName("rows")]
        public List<StatementImportRow> Rows { get; set; } = [];

        [JsonProperty("metadata")]
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new();

        [JsonProperty("suggested_profile_sid")]
        [JsonPropertyName("suggested_profile_sid")]
        public string? SuggestedProfileSID { get; set; }

        [JsonProperty("sample")]
        [JsonPropertyName("sample")]
        public StatementProfileSample Sample { get; set; } = new();
    }

    public class StatementImportCommitResult
    {
        [JsonProperty("import_batch_sid")]
        [JsonPropertyName("import_batch_sid")]
        public string ImportBatchSID { get; set; } = null!;

        [JsonProperty("imported_rows")]
        [JsonPropertyName("imported_rows")]
        public int ImportedRows { get; set; }

        [JsonProperty("skipped_duplicate_rows")]
        [JsonPropertyName("skipped_duplicate_rows")]
        public int SkippedDuplicateRows { get; set; }

        [JsonProperty("invalid_rows")]
        [JsonPropertyName("invalid_rows")]
        public int InvalidRows { get; set; }
    }

    public class ImportBatchResponse
    {
        [JsonProperty("import_batch_sid")]
        [JsonPropertyName("import_batch_sid")]
        public string ImportBatchSID { get; set; } = null!;

        [JsonProperty("source_file_name")]
        [JsonPropertyName("source_file_name")]
        public string SourceFileName { get; set; } = null!;

        [JsonProperty("profile_name")]
        [JsonPropertyName("profile_name")]
        public string? ProfileName { get; set; }

        [JsonProperty("account_name")]
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }

        [JsonProperty("total_rows")]
        [JsonPropertyName("total_rows")]
        public int TotalRows { get; set; }

        [JsonProperty("imported_rows")]
        [JsonPropertyName("imported_rows")]
        public int ImportedRows { get; set; }

        [JsonProperty("skipped_duplicate_rows")]
        [JsonPropertyName("skipped_duplicate_rows")]
        public int SkippedDuplicateRows { get; set; }

        [JsonProperty("invalid_rows")]
        [JsonPropertyName("invalid_rows")]
        public int InvalidRows { get; set; }

        [JsonProperty("created_date_time")]
        [JsonPropertyName("created_date_time")]
        public string? CreatedDateTime { get; set; }

        [JsonProperty("undone_date_time")]
        [JsonPropertyName("undone_date_time")]
        public string? UndoneDateTime { get; set; }
    }
}
