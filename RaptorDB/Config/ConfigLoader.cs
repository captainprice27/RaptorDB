using System;
using System.Configuration;

namespace RaptorDB.RaptorDB.Config
{
    /// <summary>
    /// Loads configuration values from engine.config / storage.config
    /// Requires NuGet: System.Configuration.ConfigurationManager
    /// </summary>
    internal static class ConfigLoader
    {
        /// <summary>
        /// Reads a configuration setting using section:key convention.
        /// </summary>
        public static string Get(string section, string key)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string fullKey = $"{section}:{key}";

            var setting = config.AppSettings.Settings[fullKey];

            return setting?.Value ?? string.Empty;
        }
    }
}
