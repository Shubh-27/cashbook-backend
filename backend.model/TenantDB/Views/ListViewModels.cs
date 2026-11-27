using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.TenantDB.Views
{
    public class VwTransactionsList
    {
        [JsonProperty("transaction_sid")]
        [JsonPropertyName("transaction_sid")]
        public string? TransactionSID { get; set; }
        [JsonProperty("transaction_date")]
        [JsonPropertyName("transaction_date")]
        public string? TransactionDate { get; set; }
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
        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonProperty("account_sid")]
        [JsonPropertyName("account_sid")]
        public string? AccountSID { get; set; }
        [JsonProperty("account_name")]
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }
        [JsonProperty("description_sid")]
        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }
        [JsonProperty("description_name")]
        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }
        [JsonProperty("account_number")]
        [JsonPropertyName("account_number")]
        public long? AccountNumber { get; set; }
        [JsonProperty("bank_name")]
        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }
    }

    public class VwAccountsList
    {
        [JsonProperty("account_sid")]
        [JsonPropertyName("account_sid")]
        public string? AccountSID { get; set; }
        [JsonProperty("account_name")]
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }
        [JsonProperty("account_number")]
        [JsonPropertyName("account_number")]
        public long? AccountNumber { get; set; }
        [JsonProperty("bank_name")]
        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }
        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonProperty("transaction_count")]
        [JsonPropertyName("transaction_count")]
        public int TransactionCount { get; set; }
    }

    public class VwDescriptionsList
    {
        [JsonProperty("description_sid")]
        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }
        [JsonProperty("description_name")]
        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }
        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonProperty("usage_count")]
        [JsonPropertyName("usage_count")]
        public int UsageCount { get; set; }
    }
}
