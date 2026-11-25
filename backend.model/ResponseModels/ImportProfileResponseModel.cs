using System.Text.Json.Serialization;
using backend.model.RequestModels;
using Newtonsoft.Json;

namespace backend.model.ResponseModels
{
    public sealed record ImportProfileRuleProblem(
        [property: JsonProperty("code"), JsonPropertyName("code")] string Code,
        [property: JsonProperty("message"), JsonPropertyName("message")] string Message);

    public sealed class ImportProfileCreateResult
    {
        [JsonProperty("import_profile_sid")]
        [JsonPropertyName("import_profile_sid")]
        public string? ImportProfileSID { get; set; }

        [JsonProperty("version_number")]
        [JsonPropertyName("version_number")]
        public int? VersionNumber { get; set; }

        [JsonProperty("problems")]
        [JsonPropertyName("problems")]
        public List<ImportProfileRuleProblem> Problems { get; set; } = [];

        [JsonProperty("success")]
        [JsonPropertyName("success")]
        public bool Success => Problems.Count == 0;
    }

    public sealed class ImportProfileVersionResponse
    {
        [JsonProperty("import_profile_sid")]
        [JsonPropertyName("import_profile_sid")]
        public string ImportProfileSID { get; set; } = null!;

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonProperty("version_number")]
        [JsonPropertyName("version_number")]
        public int VersionNumber { get; set; }

        [JsonProperty("file_type")]
        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = null!;

        [JsonProperty("worksheet_name")]
        [JsonPropertyName("worksheet_name")]
        public string? WorksheetName { get; set; }

        [JsonProperty("header_row")]
        [JsonPropertyName("header_row")]
        public int HeaderRow { get; set; }

        [JsonProperty("end_row")]
        [JsonPropertyName("end_row")]
        public int EndRow { get; set; }

        [JsonProperty("date_format")]
        [JsonPropertyName("date_format")]
        public string? DateFormat { get; set; }

        [JsonProperty("date")]
        [JsonPropertyName("date")]
        public int? Date { get; set; }

        [JsonProperty("description")]
        [JsonPropertyName("description")]
        public int? Description { get; set; }

        [JsonProperty("debit")]
        [JsonPropertyName("debit")]
        public int? Debit { get; set; }

        [JsonProperty("credit")]
        [JsonPropertyName("credit")]
        public int? Credit { get; set; }

        [JsonProperty("amount")]
        [JsonPropertyName("amount")]
        public int? Amount { get; set; }

        [JsonProperty("balance")]
        [JsonPropertyName("balance")]
        public int? Balance { get; set; }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public int? Notes { get; set; }

        [JsonProperty("secondary_table_marker_row")]
        [JsonPropertyName("secondary_table_marker_row")]
        public int? SecondaryTableMarkerRow { get; set; }

        [JsonProperty("sample")]
        [JsonPropertyName("sample")]
        public StatementProfileSample Sample { get; set; } = new();
    }

    public class ImportProfileResponse : SaveImportProfileRequest
    {
        [JsonProperty("import_profile_sid")]
        [JsonPropertyName("import_profile_sid")]
        public string ImportProfileSID { get; set; } = null!;
    }

    public sealed class ImportProfilePreviewResponse
    {
        /// <summary>All cells are server-masked formatted display text; Excel date cells use ISO yyyy-MM-dd text.</summary>
        [JsonProperty("cell_value_format")]
        [JsonPropertyName("cell_value_format")]
        public string CellValueFormat { get; set; } = "formatted_text";

        [JsonProperty("file_type")]
        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = null!;

        [JsonProperty("worksheets")]
        [JsonPropertyName("worksheets")]
        public List<string> Worksheets { get; set; } = [];

        [JsonProperty("selected_worksheet")]
        [JsonPropertyName("selected_worksheet")]
        public string SelectedWorksheet { get; set; } = null!;

        [JsonProperty("first_rows")]
        [JsonPropertyName("first_rows")]
        public List<StatementGridRow> FirstRows { get; set; } = [];

        [JsonProperty("last_rows")]
        [JsonPropertyName("last_rows")]
        public List<StatementGridRow> LastRows { get; set; } = [];

        [JsonProperty("divider_after_row")]
        [JsonPropertyName("divider_after_row")]
        public int? DividerAfterRow { get; set; }

        [JsonProperty("total_rows")]
        [JsonPropertyName("total_rows")]
        public int TotalRows { get; set; }

        [JsonProperty("detected_header_row")]
        [JsonPropertyName("detected_header_row")]
        public int? DetectedHeaderRow { get; set; }

        [JsonProperty("footer_candidate_rows")]
        [JsonPropertyName("footer_candidate_rows")]
        public List<int> FooterCandidateRows { get; set; } = [];

        [JsonProperty("secondary_table_candidate_rows")]
        [JsonPropertyName("secondary_table_candidate_rows")]
        public List<int> SecondaryTableCandidateRows { get; set; } = [];

        [JsonProperty("has_ambiguous_date_format")]
        [JsonPropertyName("has_ambiguous_date_format")]
        public bool HasAmbiguousDateFormat { get; set; }
    }

    public sealed class HeaderMatchResult
    {
        [JsonProperty("confidence")]
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonProperty("is_match")]
        [JsonPropertyName("is_match")]
        public bool IsMatch { get; set; }
    }
}
