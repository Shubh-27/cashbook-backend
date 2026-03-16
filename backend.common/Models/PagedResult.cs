using System.Text.Json.Serialization;

namespace backend.common.Models
{
    public class PagedResult<T>
    {
        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = [];

        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("page_size")]
        public int PageSize { get; set; }
    }
}
