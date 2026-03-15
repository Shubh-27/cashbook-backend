using System.Text.Json.Serialization;

namespace backend.model.ResponseModel
{
    public class AccountResponseModel
    {
        [JsonPropertyName("account_sid")]
        public string AccountSid { get; set; } = null!;

        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }

        [JsonPropertyName("account_number")]
        public int? AccountNumber { get; set; }

        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }

        [JsonPropertyName("last_modified_date_time")]
        public string? LastModifiedDateTime { get; set; }
        
        [JsonPropertyName("last_modified_by_user_id")]
        public int? LastModifiedByUserID { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("balance")]
        public double? Balance { get; set; }
    }
}
