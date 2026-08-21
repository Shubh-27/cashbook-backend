using backend.common;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Extensions
{
    public static class CorsServiceExtensions
    {
        public static IServiceCollection AddCorsPolicy(this IServiceCollection services, AppConfiguration appConfiguration)
        {
            var allowedOrigins = appConfiguration.Cors.GetOriginsArray();
            var originSet = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        // Requests without Origin header (e.g. non-browser HTTP, desktop client, curl, Electron file:// fetches) are permitted
                        if (string.IsNullOrEmpty(origin))
                            return true;

                        // Electron local file requests send Origin: "null" or "file://"
                        if (string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase) ||
                            origin.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(origin, "file:", StringComparison.OrdinalIgnoreCase))
                            return true;

                        // Direct match against configured origins
                        if (originSet.Contains(origin))
                            return true;

                        // Allow loopback/localhost origins (localhost or 127.0.0.1)
                        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        {
                            if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }

                        return false;
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Content-Disposition");
                });
            });

            return services;
        }
    }
}

