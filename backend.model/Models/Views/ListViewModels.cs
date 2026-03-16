using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.model.Models.Views
{
    public class VwTransactionsList
    {
        [JsonPropertyName("transaction_sid")]
        public string? TransactionSID { get; set; }
        [JsonPropertyName("transaction_date")]
        public string? TransactionDate { get; set; }
        [JsonPropertyName("debit")]
        public double? Debit { get; set; }
        [JsonPropertyName("credit")]
        public double? Credit { get; set; }
        [JsonPropertyName("balance")]
        public double? Balance { get; set; }
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("account_sid")]
        public string? AccountSID { get; set; }
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }
        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }
        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }
    }

    public class VwAccountsList
    {
        [JsonPropertyName("account_sid")]
        public string? AccountSID { get; set; }
        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }
        [JsonPropertyName("account_number")]
        public int? AccountNumber { get; set; }
        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("transaction_count")]
        public int TransactionCount { get; set; }
    }

    public class VwDescriptionsList
    {
        [JsonPropertyName("description_sid")]
        public string? DescriptionSID { get; set; }
        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("usage_count")]
        public int UsageCount { get; set; }
    }
}
