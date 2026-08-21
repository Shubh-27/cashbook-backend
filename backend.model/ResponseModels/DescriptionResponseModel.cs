using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.ResponseModels
{
    public class DescriptionResponseModel
    {
        [JsonProperty("description_sid")]
        [JsonPropertyName("description_sid")]
        public string DescriptionSID { get; set; } = null!;

        [JsonProperty("description_name")]
        [JsonPropertyName("description_name")]
        public string? DescriptionName { get; set; }

        [JsonProperty("last_modified_date_time")]
        [JsonPropertyName("last_modified_date_time")]
        public string? LastModifiedDateTime { get; set; }

        [JsonProperty("status")]
        [JsonPropertyName("status")]
        public int Status { get; set; }
    }
}
