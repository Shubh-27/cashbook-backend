namespace backend.common
{
    public class AppConfiguration
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public CorsSettings Cors { get; set; } = new();
        public bool AutoMigrate { get; set; } = true;
    }

    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; } = string.Empty;
    }

    public class CorsSettings
    {
        public string AllowedOrigins { get; set; } = "http://localhost:5173,http://localhost:5050,http://127.0.0.1:5173,http://127.0.0.1:5050,null";

        public string[] GetOriginsArray()
        {
            if (string.IsNullOrWhiteSpace(AllowedOrigins))
                return Array.Empty<string>();
            return AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
