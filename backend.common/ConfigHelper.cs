using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace backend.common
{
    public static class ConfigHelper
    {
        #region Variables
        private const string DefaultConfigFileName = "appsettings.json";
        private const string LocalConfigFileName = "local.settings.json";
        #endregion

        #region Get Config
        /// <summary>
        /// Gets the configuration from either 'local.settings.json' or 'appsettings.json'.
        /// </summary>
        /// <returns>The configuration.</returns>
        public static IConfiguration GetConfig()
        {
            var configFileName = File.Exists(Path.Combine(Directory.GetCurrentDirectory(), LocalConfigFileName))
                ? LocalConfigFileName
                : DefaultConfigFileName;

            return BuildConfiguration(configFileName);
        }
        #endregion

        #region Get AppConfig
        /// <summary>
        /// Gets the configuration from 'appsettings.json'.
        /// </summary>
        /// <returns>The configuration.</returns>
        public static IConfiguration GetAppConfig()
        {
            return BuildConfiguration(DefaultConfigFileName);
        }
        #endregion

        #region Internal
        /// <summary>
        /// Builds the configuration from the specified JSON file and environment variables.
        /// </summary>
        /// <param name="configFileName">The configuration file name.</param>
        /// <returns>The built configuration.</returns>
        private static IConfiguration BuildConfiguration(string configFileName)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configFileName, optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            return builder.Build();
        }
        #endregion
    }
}
