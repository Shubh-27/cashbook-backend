using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.RequestModels
{
    public class AccountRequestModel
    {
        [JsonProperty("account_name")]
        [JsonPropertyName("account_name")]
        public string AccountName { get; set; } = null!;

        [JsonProperty("bank_name")]
        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }

        [JsonProperty("account_number")]
        [JsonPropertyName("account_number")]
        public string? AccountNumber { get; set; }
    }
}
