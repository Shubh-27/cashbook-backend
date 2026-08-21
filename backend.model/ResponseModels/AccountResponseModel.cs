using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.ResponseModels
{
    public class AccountResponseModel
    {
        [JsonProperty("account_sid")]
        [JsonPropertyName("account_sid")]
        public string AccountSID { get; set; } = null!;

        [JsonProperty("account_name")]
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }

        [JsonProperty("account_number")]
        [JsonPropertyName("account_number")]
        public long? AccountNumber { get; set; }

        [JsonProperty("bank_name")]
        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }

        [JsonProperty("last_modified_date_time")]
        [JsonPropertyName("last_modified_date_time")]
        public string? LastModifiedDateTime { get; set; }
        
        [JsonProperty("last_modified_by_user_id")]
        [JsonPropertyName("last_modified_by_user_id")]
        public int? LastModifiedByUserID { get; set; }

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonProperty("balance")]
        [JsonPropertyName("balance")]
        public double? Balance { get; set; }
    }
}
