namespace backend.common
{
    public static class AppConfiguration
    {
        #region Configuration properties
        public static string ValidIssuer { get; private set; } = string.Empty;
        public static string ValidAudience { get; private set; } = string.Empty;
        public static string IssuerSigningKeyBytes { get; private set; } = string.Empty;
        public static string WebHost { get; private set; } = string.Empty;
        public static string ApiHost { get; private set; } = string.Empty;
        public static int ExpiryMins { get; private set; } = 600;
        public static int SuperExpiryMins { get; private set; } = 600;
        public static int InternalExpiryMins { get; private set; } = 600;
        public static string SaltKey { get; private set; } = string.Empty;
        #endregion

        #region App Configuration
        static AppConfiguration()
        {
            var configuration = ConfigHelper.GetConfig();

            ValidIssuer = configuration["ValidIssuer"] ?? string.Empty;
            ValidAudience = configuration["ValidAudience"] ?? string.Empty;
            IssuerSigningKeyBytes = configuration["IssuerSigningKeyBytes"] ?? string.Empty;
            WebHost = configuration["WebHost"] ?? string.Empty;
            ApiHost = configuration["ApiHost"] ?? string.Empty;
            ExpiryMins = int.TryParse(configuration["ExpiryMins"], out var expiryMins) ? expiryMins : 600;
            SuperExpiryMins = int.TryParse(configuration["SuperExpiryMins"], out var superExpiryMins) ? superExpiryMins : 600;
            InternalExpiryMins = int.TryParse(configuration["InternalExpiryMins"], out var internalExpiryMins) ? internalExpiryMins : 600;
            SaltKey = configuration["saltKey"] ?? string.Empty;
        }
        #endregion
    }
}
