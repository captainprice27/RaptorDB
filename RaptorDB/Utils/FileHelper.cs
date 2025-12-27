using System;
using System.IO;

namespace RaptorDB.RaptorDB.Utils
{
    /// <summary>
    /// Utility class for common file and directory operations.
    /// Ensures the storage structure exists before read/write.
    /// Used by SchemaManager, RecordManager, IndexManager, WALManager.
    /// </summary>
    internal static class FileHelper
    {
        /// <summary>
        /// Ensures the directory for a file exists, creates it if missing.
        /// </summary>
        public static void EnsureDirectory(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Checks if a file exists and throws if not.
        /// Useful for schema enforcement.
        /// </summary>
        public static void EnsureFileExists(string path, string errorMessage)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(errorMessage, path);
        }

        /// <summary>
        /// Safe delete: remove if exists, ignore if not.
        /// </summary>
        public static void SafeDelete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        /// <summary>
        /// Returns true if the file exists, reduces repeated checks.
        /// </summary>
        public static bool Exists(string filePath)
        {
            return File.Exists(filePath);
        }
    }
}
