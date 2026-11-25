using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.RequestModels
{
    public sealed class ImportProfileRuleRequest
    {
        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string? Name { get; set; }

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
    }
}
