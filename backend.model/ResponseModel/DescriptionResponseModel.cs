using System.Text.Json.Serialization;

namespace backend.model.ResponseModel
{
    public class DescriptionResponseModel
    {
        [JsonPropertyName("description_sid")]
        public string DescriptionSID { get; set; } = null!;

        [JsonPropertyName("description_name")]
        public string DescriptionName { get; set; } = null!;

        [JsonPropertyName("created_date_time")]
        public string? CreatedDateTime { get; set; }

        [JsonPropertyName("created_by_user_id")]
        public int? CreatedByUserID { get; set; }

        [JsonPropertyName("last_modified_date_time")]
        public string? LastModifiedDateTime { get; set; }

        [JsonPropertyName("last_modified_by_user_id")]
        public int? LastModifiedByUserID { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }
    }
}
