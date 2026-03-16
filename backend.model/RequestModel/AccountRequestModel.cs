using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.model.RequestModel
{
    public class AccountRequestModel
    {
        [Required(ErrorMessage = "Account name is required.")]
        [StringLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
        [JsonPropertyName("account_name")]
        public string AccountName { get; set; } = null!;

        [StringLength(100, ErrorMessage = "Bank name cannot exceed 100 characters.")]
        [JsonPropertyName("bank_name")]
        public string? BankName { get; set; }

        [RegularExpression(@"^[0-9]{4,20}$", ErrorMessage = "Account number must be 4–20 digits.")]
        [JsonPropertyName("account_number")]
        public string? AccountNumber { get; set; }
    }
}
