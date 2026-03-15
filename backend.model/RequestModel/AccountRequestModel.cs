using System.Text.Json.Serialization;

namespace backend.model.RequestModel
{
    public class AccountRequestModel
    {
        [JsonPropertyName("account_name")]
        public string AccountName { get; set; } = null!;

        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }

        [JsonPropertyName("account_number")]
        public string? AccountNumber { get; set; }
    }
}
