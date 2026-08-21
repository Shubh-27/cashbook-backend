using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace backend.common
{
    public class SearchRequestModel
    {
        [JsonProperty("page")]
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        [JsonProperty("page_size")]
        [JsonPropertyName("page_size")]
        public int PageSize { get; set; } = 50;

        [JsonProperty("search")]
        [JsonPropertyName("search")]
        public string? Search { get; set; }

        [JsonProperty("sort_by")]
        [JsonPropertyName("sort_by")]
        public string? SortBy { get; set; }

        [JsonProperty("sort_order")]
        [JsonPropertyName("sort_order")]
        public string? SortOrder { get; set; }

        [JsonProperty("filters")]
        [JsonPropertyName("filters")]
        public List<FilterRequestModel>? Filters { get; set; }
    }

    public class FilterRequestModel
    {
        [JsonProperty("key")]
        [JsonPropertyName("key")]
        public string Key { get; set; } = null!;

        [JsonProperty("condition")]
        [JsonPropertyName("condition")]
        public string? Condition { get; set; }

        [JsonProperty("value")]
        [JsonPropertyName("value")]
        public object? Value { get; set; }

        [JsonProperty("from")]
        [JsonPropertyName("from")]
        public object? From { get; set; }

        [JsonProperty("to")]
        [JsonPropertyName("to")]
        public object? To { get; set; }

        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string? Type { get; set; }
    }

    public class PagedResult<T>
    {
        [JsonProperty("data")]
        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();

        [JsonProperty("total_count")]
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonProperty("page")]
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonProperty("page_size")]
        [JsonPropertyName("page_size")]
        public int PageSize { get; set; }
    }

    public class ExportRequestModel : SearchRequestModel
    {
        [JsonProperty("export_type")]
        [JsonPropertyName("export_type")]
        public string? ExportType { get; set; }

        [JsonProperty("excel_name")]
        [JsonPropertyName("excel_name")]
        public string? ExcelName { get; set; }

        [JsonProperty("separate_sheets")]
        [JsonPropertyName("separate_sheets")]
        public bool SeparateSheets { get; set; } = true;

        [JsonProperty("merge_accounts")]
        [JsonPropertyName("merge_accounts")]
        public bool MergeAccounts { get; set; } = false;

        [JsonProperty("merge_descriptions")]
        [JsonPropertyName("merge_descriptions")]
        public bool MergeDescriptions { get; set; } = false;
    }
}
