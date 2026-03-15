using System.Text.Json.Serialization;

namespace backend.model.RequestModel
{
    public class DescriptionRequestModel
    {
        [JsonPropertyName("description_name")]
        public string DescriptionName { get; set; } = null!;
    }
}
