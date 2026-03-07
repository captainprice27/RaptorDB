using System;
using System.Collections.Generic;
using System.Text;

namespace RaptorDB.RaptorDB.Utils
{
    /// <summary>
    /// Handles Base64 serialization of row data to prevent delimiter injection.
    /// Updated for .NET 10: uses Span-based encoding/decoding and the cleaner
    /// string.Split(char, int) overload instead of the old char-array form.
    /// </summary>
    internal static class ByteSerializer
    {
        // Shared UTF-8 encoder instance — avoids repeated allocations.
        private static readonly Encoding _encoding = Encoding.UTF8;

        // ---------------------------------------------------------------
        // ENCODING HELPERS
        // ---------------------------------------------------------------

        /// <summary>
        /// Encodes a string to Base64 to prevent '|' or '=' inside data
        /// values from corrupting the pipe-delimited row format.
        /// Uses the Span&lt;byte&gt; path (.NET 10) for zero-copy encoding.
        /// </summary>
        private static string SafeEncode(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Span-based: avoids intermediate byte[] heap allocation for small strings.
            int maxBytes = _encoding.GetMaxByteCount(input.Length);
            Span<byte> buffer = maxBytes <= 512
                ? stackalloc byte[maxBytes]
                : new byte[maxBytes];

            int written = _encoding.GetBytes(input, buffer);
            return Convert.ToBase64String(buffer[..written]);
        }

        /// <summary>
        /// Decodes a Base64 string back to its original text.
        /// Falls back to returning the raw value if it was stored un-encoded
        /// (backward-compatibility guard).
        /// </summary>
        private static string SafeDecode(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            try
            {
                // TryFromBase64String (.NET 5+) avoids an exception on bad input.
                int maxLen = (input.Length * 3) / 4 + 4;
                Span<byte> decoded = maxLen <= 512
                    ? stackalloc byte[maxLen]
                    : new byte[maxLen];

                if (Convert.TryFromBase64String(input, decoded, out int bytesLen))
                    return _encoding.GetString(decoded[..bytesLen]);

                return input; // not Base64 — return as-is (backward compat)
            }
            catch
            {
                return input;
            }
        }

        // ---------------------------------------------------------------
        // ROW SERIALIZATION
        // ---------------------------------------------------------------

        /// <summary>
        /// Serialises a row dictionary to a pipe-delimited storage string.
        /// Format: col1=Base64Value|col2=Base64Value|...
        /// </summary>
        public static string SerializeRow(Dictionary<string, string> row)
        {
            // Use a pre-sized StringBuilder to reduce reallocations.
            var sb = new StringBuilder(row.Count * 32);
            bool first = true;
            foreach (var (key, value) in row)
            {
                if (!first) sb.Append('|');
                sb.Append(key).Append('=').Append(SafeEncode(value));
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Deserialises a stored row string back into a column→value dictionary.
        /// Uses string.Split(char, int) (.NET 5+) instead of the old char-array overload.
        /// </summary>
        public static Dictionary<string, string> DeserializeRow(string serialized)
        {
            var row = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(serialized)) return row;

            // Split on '|' to get individual col=Base64 tokens.
            var fields = serialized.Split('|');
            foreach (var field in fields)
            {
                // Split only on the FIRST '=' — Base64 padding (==) must not break parsing.
                var parts = field.Split('=', 2); // .NET 5+ overload — no char[] allocation
                if (parts.Length == 2)
                    row[parts[0]] = SafeDecode(parts[1]);
            }

            return row;
        }

        // ---------------------------------------------------------------
        // BINARY HELPERS (used by future binary storage modules)
        // ---------------------------------------------------------------
        public static byte[] ToBytes(string value)   => _encoding.GetBytes(value);
        public static string FromBytes(byte[] data)  => _encoding.GetString(data);
    }
}