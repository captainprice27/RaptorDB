//using System;
//using System.Text;

//namespace RaptorDB.RaptorDB.Utils
//{
//    /// <summary>
//    /// Converts data between string and byte-array formats.
//    /// This will evolve into a core component when fixed-length binary
//    /// records and page-based storage are implemented.
//    /// </summary>
//    internal static class ByteSerializer
//    {
//        private static readonly Encoding _encoding = Encoding.UTF8;

//        /// <summary>
//        /// Convert string to bytes for writing to .data binary files.
//        /// </summary>
//        public static byte[] ToBytes(string value)
//        {
//            return _encoding.GetBytes(value);
//        }

//        /// <summary>
//        /// Convert bytes back into a string from storage.
//        /// </summary>
//        public static string FromBytes(byte[] data)
//        {
//            return _encoding.GetString(data);
//        }

//        /// <summary>
//        /// Serialize a dictionary (row) as a compact string.
//        /// Example: col1=10|col2=John|col3=22
//        /// </summary>
//        public static string SerializeRow(Dictionary<string, string> row)
//        {
//            return string.Join("|", row);
//        }

//        /// <summary>
//        /// Deserialize a stored row string back into a dictionary.
//        /// </summary>
//        public static Dictionary<string, string> DeserializeRow(string serialized)
//        {
//            var row = new Dictionary<string, string>();
//            var fields = serialized.Split('|');

//            foreach (var field in fields)
//            {
//                var parts = field.Split('=');
//                if (parts.Length == 2)
//                    row[parts[0]] = parts[1];
//            }

//            return row;
//        }
//    }
//}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RaptorDB.RaptorDB.Utils
{
    internal static class ByteSerializer
    {
        private static readonly Encoding _encoding = Encoding.UTF8;

        /// <summary>
        /// Encodes a string to Base64 to prevent delimiters like '|' or '=' 
        /// from breaking the file format.
        /// </summary>
        private static string SafeEncode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return Convert.ToBase64String(_encoding.GetBytes(input));
        }

        /// <summary>
        /// Decodes a Base64 string back to the original text.
        /// </summary>
        private static string SafeDecode(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            try
            {
                return _encoding.GetString(Convert.FromBase64String(input));
            }
            catch
            {
                return input; // Fallback if data wasn't encoded (backward compatibility)
            }
        }

        /// <summary>
        /// Serialize a dictionary (row) into a safe storage string.
        /// Format: col1=Base64Value|col2=Base64Value
        /// </summary>
        public static string SerializeRow(Dictionary<string, string> row)
        {
            // FIX: Uses .Select to properly format Key=Value strings before joining
            return string.Join("|", row.Select(kv => $"{kv.Key}={SafeEncode(kv.Value)}"));
        }

        /// <summary>
        /// Deserialize a stored row string back into a dictionary.
        /// </summary>
        public static Dictionary<string, string> DeserializeRow(string serialized)
        {
            var row = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(serialized)) return row;

            var fields = serialized.Split('|');

            foreach (var field in fields)
            {
                // FIX: Split only on the FIRST '=' found. 
                // This ensures Base64 padding (==) at the end doesn't break the logic.
                var parts = field.Split(new[] { '=' }, 2);

                if (parts.Length == 2)
                {
                    row[parts[0]] = SafeDecode(parts[1]);
                }
            }

            return row;
        }

        // --- Binary Helpers (Unchanged) ---
        public static byte[] ToBytes(string value) => _encoding.GetBytes(value);
        public static string FromBytes(byte[] data) => _encoding.GetString(data);
    }
}