using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.model.RequestModels
{
    public class DescriptionRequestModel
    {
        [JsonProperty("description_name")]
        [JsonPropertyName("description_name")]
        public string DescriptionName { get; set; } = null!;
    }
}
