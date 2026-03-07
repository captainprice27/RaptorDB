using System;
using System.Configuration;

namespace RaptorDB.RaptorDB.Config
{
    /// <summary>
    /// Loads configuration values from engine.config / storage.config using
    /// System.Configuration.ConfigurationManager (NuGet v10.0.0).
    ///
    /// .NET 10 note: System.Configuration.ConfigurationManager is a separate NuGet
    /// package on all modern .NET versions. The API surface is identical but the
    /// package is now compiled against .NET 10 runtime features.
    ///
    /// Usage: ConfigLoader.Get("engine", "WelcomeBanner")
    ///        → looks for key "engine:WelcomeBanner" in App.config / engine.config
    /// </summary>
    internal static class ConfigLoader
    {
        /// <summary>
        /// Reads a setting using the "section:key" naming convention.
        /// Returns <see cref="string.Empty"/> if the key is absent.
        /// </summary>
        public static string Get(string section, string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section);   // .NET 7+ guard
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            string fullKey = $"{section}:{key}";
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            return config.AppSettings.Settings[fullKey]?.Value ?? string.Empty;
        }

        /// <summary>
        /// Typed overload: reads a bool setting, returning <paramref name="fallback"/>
        /// if the key is missing or cannot be parsed.
        /// </summary>
        public static bool GetBool(string section, string key, bool fallback = false)
        {
            string raw = Get(section, key);
            return bool.TryParse(raw, out bool result) ? result : fallback;
        }

        /// <summary>
        /// Typed overload: reads an int setting, returning <paramref name="fallback"/>
        /// if the key is missing or cannot be parsed.
        /// </summary>
        public static int GetInt(string section, string key, int fallback = 0)
        {
            string raw = Get(section, key);
            return int.TryParse(raw, out int result) ? result : fallback;
        }
    }
}
