using System.Text.Json.Serialization;

namespace backend.model.ResponseModel
{
    public class TransactionResponseModel
    {
        [JsonPropertyName("transaction_sid")]
        public string TransactionSid { get; set; } = null!;
        
        [JsonPropertyName("transaction_date")]
        public string TransactionDate { get; set; } = null!;
        
        [JsonPropertyName("debit")]
        public double? Debit { get; set; }
        
        [JsonPropertyName("credit")]
        public double? Credit { get; set; }
        
        [JsonPropertyName("balance")]
        public double? Balance { get; set; }
        
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
        
        // Include related models for easy frontend consumption
        [JsonPropertyName("description")]
        public DescriptionResponseModel? Description { get; set; }
        
        [JsonPropertyName("account")]
        public AccountResponseModel? Account { get; set; }
    }
}
