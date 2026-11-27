using System.Text.Json.Serialization;
using backend.common.Models;
using Newtonsoft.Json;

namespace backend.model.RequestModel
{
    public class ExportRequestModel : SearchRequestModel
    {
        [JsonProperty("export_type")]
        [JsonPropertyName("export_type")]
        public string? ExportType { get; set; }

        [JsonProperty("excel_name")]
        [JsonPropertyName("excel_name")]
        public string? ExcelName { get; set; }

        [JsonProperty("separate_sheets")]
        [JsonPropertyName("separate_sheets")]
        public bool SeparateSheets { get; set; } = true;

        [JsonProperty("merge_accounts")]
        [JsonPropertyName("merge_accounts")]
        public bool MergeAccounts { get; set; } = false;

        [JsonProperty("merge_descriptions")]
        [JsonPropertyName("merge_descriptions")]
        public bool MergeDescriptions { get; set; } = false;
    }
}
