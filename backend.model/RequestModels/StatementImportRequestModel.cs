using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.RequestModels
{
    public class StatementColumnMapping
    {
        [JsonProperty("date")]
        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonProperty("description")]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("debit")]
        [JsonPropertyName("debit")]
        public string? Debit { get; set; }

        [JsonProperty("credit")]
        [JsonPropertyName("credit")]
        public string? Credit { get; set; }

        [JsonProperty("amount")]
        [JsonPropertyName("amount")]
        public string? Amount { get; set; }

        [JsonProperty("balance")]
        [JsonPropertyName("balance")]
        public string? Balance { get; set; }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public class StatementImportRow
    {
        [JsonProperty("row_number")]
        [JsonPropertyName("row_number")]
        public int RowNumber { get; set; }

        [JsonProperty("transaction_date")]
        [JsonPropertyName("transaction_date")]
        public string? TransactionDate { get; set; }

        [JsonProperty("description")]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonProperty("debit")]
        [JsonPropertyName("debit")]
        public double? Debit { get; set; }

        [JsonProperty("credit")]
        [JsonPropertyName("credit")]
        public double? Credit { get; set; }

        [JsonProperty("balance")]
        [JsonPropertyName("balance")]
        public double? Balance { get; set; }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonProperty("include")]
        [JsonPropertyName("include")]
        public bool Include { get; set; } = true;

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public string Status { get; set; } = "new";

        [JsonProperty("issues")]
        [JsonPropertyName("issues")]
        public List<string> Issues { get; set; } = [];
    }

    public class StatementHeaderCandidate
    {
        [JsonProperty("row_number")]
        [JsonPropertyName("row_number")]
        public int RowNumber { get; set; }

        [JsonProperty("score")]
        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonProperty("values")]
        [JsonPropertyName("values")]
        public List<string> Values { get; set; } = [];
    }

    public class StatementProfileSampleRow
    {
        [JsonProperty("row_number")]
        [JsonPropertyName("row_number")]
        public int RowNumber { get; set; }

        [JsonProperty("values")]
        [JsonPropertyName("values")]
        public List<string> Values { get; set; } = [];
    }

    public class StatementProfileSample
    {
        [JsonProperty("top_rows")]
        [JsonPropertyName("top_rows")]
        public List<StatementProfileSampleRow> TopRows { get; set; } = [];

        [JsonProperty("bottom_rows")]
        [JsonPropertyName("bottom_rows")]
        public List<StatementProfileSampleRow> BottomRows { get; set; } = [];

        [JsonProperty("transaction_end_row")]
        [JsonPropertyName("transaction_end_row")]
        public int? TransactionEndRow { get; set; }

        [JsonProperty("footer_marker")]
        [JsonPropertyName("footer_marker")]
        public string? FooterMarker { get; set; }
    }

    public class SaveImportProfileRequest
    {
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("file_type")]
        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = null!;

        [JsonProperty("worksheet_name")]
        [JsonPropertyName("worksheet_name")]
        public string? WorksheetName { get; set; }

        [JsonProperty("header_row")]
        [JsonPropertyName("header_row")]
        public int HeaderRow { get; set; }

        [JsonProperty("transaction_end_row")]
        [JsonPropertyName("transaction_end_row")]
        public int? TransactionEndRow { get; set; }

        [JsonProperty("headers")]
        [JsonPropertyName("headers")]
        public List<string> Headers { get; set; } = [];

        [JsonProperty("mapping")]
        [JsonPropertyName("mapping")]
        public StatementColumnMapping Mapping { get; set; } = new();

        [JsonProperty("date_format")]
        [JsonPropertyName("date_format")]
        public string? DateFormat { get; set; }

        [JsonProperty("sample")]
        [JsonPropertyName("sample")]
        public StatementProfileSample? Sample { get; set; }
    }

    public class CommitStatementImportRequest
    {
        [JsonProperty("account_sid")]
        [JsonPropertyName("account_sid")]
        public string AccountSID { get; set; } = null!;

        [JsonProperty("file_name")]
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = null!;

        [JsonProperty("file_type")]
        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = null!;

        [JsonProperty("file_hash")]
        [JsonPropertyName("file_hash")]
        public string? FileHash { get; set; }

        [JsonProperty("profile_sid")]
        [JsonPropertyName("profile_sid")]
        public string? ProfileSID { get; set; }

        [JsonProperty("save_profile")]
        [JsonPropertyName("save_profile")]
        public SaveImportProfileRequest? SaveProfile { get; set; }

        [JsonProperty("metadata")]
        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }

        [JsonProperty("rows")]
        [JsonPropertyName("rows")]
        public List<StatementImportRow> Rows { get; set; } = [];
    }

    public sealed class StatementGridRow
    {
        [JsonProperty("row_number")]
        [JsonPropertyName("row_number")]
        public int RowNumber { get; set; }

        [JsonProperty("cells")]
        [JsonPropertyName("cells")]
        public List<string> Cells { get; set; } = [];
    }

    public sealed class StatementWorksheetGrid
    {
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("total_rows")]
        [JsonPropertyName("total_rows")]
        public int TotalRows { get; set; }

        [JsonProperty("total_columns")]
        [JsonPropertyName("total_columns")]
        public int TotalColumns { get; set; }

        [JsonProperty("rows")]
        [JsonPropertyName("rows")]
        public List<StatementGridRow> Rows { get; set; } = [];
    }
}
