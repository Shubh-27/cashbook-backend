using System.Net;
using System.Net.Sockets;
using backend.common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Extensions
{
    public static class CorsServiceExtensions
    {
        public static IServiceCollection AddCorsPolicy(this IServiceCollection services, AppConfiguration appConfiguration, IConfiguration? configuration = null)
        {
            var allowedOrigins = appConfiguration.Cors.GetOriginsArray();
            var indexedOrigins = configuration?.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var originSet = new HashSet<string>(allowedOrigins.Concat(indexedOrigins), StringComparer.OrdinalIgnoreCase);

            services.AddCors(options =>
            {
                options.AddPolicy(Constants.CorsPolicyName, policy =>
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

                        // Allow loopback/localhost and Tailscale origins (100.64.0.0/10 or *.ts.net)
                        if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        {
                            if (uri.IsLoopback ||
                                string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                                IsTailscaleHost(uri.Host))
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

        private static bool IsTailscaleHost(string host)
        {
            if (host.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase))
                return true;

            if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ip.GetAddressBytes();
                return bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;
            }

            return false;
        }
    }
}

