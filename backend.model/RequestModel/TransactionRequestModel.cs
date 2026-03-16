using System.Text.Json.Serialization;

namespace backend.model.RequestModel
{
    public class TransactionRequestModel
    {
        [JsonPropertyName("account_sid")]
        public string AccountSID { get; set; } = null!;

        [JsonPropertyName("transaction_date")]
        public string TransactionDate { get; set; } = null!;

        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }

        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }

        [JsonPropertyName("debit")]
        public double? Debit { get; set; }

        [JsonPropertyName("credit")]
        public double? Credit { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }
}
