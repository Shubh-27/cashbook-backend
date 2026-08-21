using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.ResponseModels
{
    public class TransactionResponseModel
    {
        [JsonProperty("transaction_sid")]
        [JsonPropertyName("transaction_sid")]
        public string TransactionSID { get; set; } = null!;

        [JsonProperty("transaction_date")]
        [JsonPropertyName("transaction_date")]
        public string TransactionDate { get; set; } = null!;

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

        [JsonProperty("account_id")]
        [JsonPropertyName("account_id")]
        public int? AccountID { get; set; }

        [JsonProperty("created_date_time")]
        [JsonPropertyName("created_date_time")]
        public string? CreatedDateTime { get; set; }

        [JsonProperty("created_by_user_id")]
        [JsonPropertyName("created_by_user_id")]
        public int? CreatedByUserID { get; set; }

        [JsonProperty("last_modified_date_time")]
        [JsonPropertyName("last_modified_date_time")]
        public string? LastModifiedDateTime { get; set; }

        [JsonProperty("last_modified_by_user_id")]
        [JsonPropertyName("last_modified_by_user_id")]
        public int? LastModifiedByUserID { get; set; }

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonProperty("account")]
        [JsonPropertyName("account")]
        public AccountResponseModel? Account { get; set; }

        [JsonProperty("description")]
        [JsonPropertyName("description")]
        public DescriptionResponseModel? Description { get; set; }
    }
}
