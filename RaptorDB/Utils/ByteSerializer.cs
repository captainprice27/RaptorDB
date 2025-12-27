using System;
using System.Text;

namespace RaptorDB.RaptorDB.Utils
{
    /// <summary>
    /// Converts data between string and byte-array formats.
    /// This will evolve into a core component when fixed-length binary
    /// records and page-based storage are implemented.
    /// </summary>
    internal static class ByteSerializer
    {
        private static readonly Encoding _encoding = Encoding.UTF8;

        /// <summary>
        /// Convert string to bytes for writing to .data binary files.
        /// </summary>
        public static byte[] ToBytes(string value)
        {
            return _encoding.GetBytes(value);
        }

        /// <summary>
        /// Convert bytes back into a string from storage.
        /// </summary>
        public static string FromBytes(byte[] data)
        {
            return _encoding.GetString(data);
        }

        /// <summary>
        /// Serialize a dictionary (row) as a compact string.
        /// Example: col1=10|col2=John|col3=22
        /// </summary>
        public static string SerializeRow(Dictionary<string, string> row)
        {
            return string.Join("|", row);
        }

        /// <summary>
        /// Deserialize a stored row string back into a dictionary.
        /// </summary>
        public static Dictionary<string, string> DeserializeRow(string serialized)
        {
            var row = new Dictionary<string, string>();
            var fields = serialized.Split('|');

            foreach (var field in fields)
            {
                var parts = field.Split('=');
                if (parts.Length == 2)
                    row[parts[0]] = parts[1];
            }

            return row;
        }
    }
}
