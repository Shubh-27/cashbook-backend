using System.Text.Json.Serialization;

namespace backend.common.Models
{
    public class SearchRequestModel
    {
        [JsonPropertyName("search")]
        public string? Search { get; set; } = string.Empty;

        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        [JsonPropertyName("page_size")]
        public int PageSize { get; set; } = 50;

        [JsonPropertyName("sort_by")]
        public string? SortBy { get; set; } = string.Empty;

        [JsonPropertyName("sort_order")]
        public string? SortOrder { get; set; } = string.Empty;

        [JsonPropertyName("filters")]
        public List<FilterRequestModel> Filters { get; set; } = [];
    }

    public class FilterRequestModel
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("condition")]
        public string? Condition { get; set; }

        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonPropertyName("from")]
        public object? From { get; set; }

        [JsonPropertyName("to")]
        public object? To { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class DateRequestModel
    {
        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("end_date")]
        public DateTime EndDate { get; set; }
    }
}
