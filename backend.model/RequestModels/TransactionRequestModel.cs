using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.RequestModels
{
    public class TransactionRequestModel
    {
        [JsonProperty("transaction_date")]
        [JsonPropertyName("transaction_date")]
        public string TransactionDate { get; set; } = null!;

        [JsonProperty("account_sid")]
        [JsonPropertyName("account_sid")]
        public string AccountSID { get; set; } = null!;

        [JsonProperty("description_sid")]
        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }

        [JsonProperty("description_name")]
        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }

        [JsonProperty("debit")]
        [JsonPropertyName("debit")]
        public double? Debit { get; set; }

        [JsonProperty("credit")]
        [JsonPropertyName("credit")]
        public double? Credit { get; set; }

        [JsonProperty("notes")]
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
