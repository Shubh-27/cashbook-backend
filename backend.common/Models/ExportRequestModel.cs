using System.Text.Json.Serialization;

namespace backend.common.Models
{
    public class ExportRequestModel : SearchRequestModel
    {
        [JsonPropertyName("export_type")]
        public string ExportType { get; set; } = "excel"; // "excel" or "csv"

        [JsonPropertyName("separate_sheets")]
        public bool SeparateSheets { get; set; } = false;
        
        [JsonPropertyName("excel_name")]
        public string ExcelName { get; set; } = String.Empty;
        
        [JsonPropertyName("account_sid")]
        public string? AccountSID { get; set; }
        
        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }
    }
}
