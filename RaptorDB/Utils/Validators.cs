using System;
using System.Globalization;
using RaptorDB.RaptorDB.Models; // <<--- match your actual namespace

namespace RaptorDB.RaptorDB.Utils
{
    internal static class Validators
    {
        // ------------------------------------------------------------
        // Main validation entry point used by ExecutionEngine
        // ------------------------------------------------------------
        public static bool ValidateValue(string value, RaptorDB.Models.DataType expected)
        {
            switch (expected)
            {
                case RaptorDB.Models.DataType.INT:
                    return int.TryParse(value, out _);

                case RaptorDB.Models.DataType.LONG:
                    return long.TryParse(value, out _);

                case RaptorDB.Models.DataType.FLOAT:
                    return float.TryParse(value, out _);

                case RaptorDB.Models.DataType.STR:
                    return true; // always valid, stored raw

                case RaptorDB.Models.DataType.DATE:
                    return DateTime.TryParse(value, out _);

                case RaptorDB.Models.DataType.DATETIME:
                    return ValidateDateTimeWithMilliseconds(value);

                default:
                    return false;
            }
        }

        // ------------------------------------------------------------
        // Convert values to internal DB safe storage format
        // ------------------------------------------------------------
        public static string ConvertToInternal(string value, RaptorDB.Models.DataType expected)
        {
            switch (expected)
            {
                case RaptorDB.Models.DataType.INT:
                case RaptorDB.Models.DataType.LONG:
                case RaptorDB.Models.DataType.FLOAT:
                    return value.Trim();

                case RaptorDB.Models.DataType.STR:
                    return value.Trim('"').Trim('\''); // remove quotes

                case RaptorDB.Models.DataType.DATE:
                    if (DateTime.TryParse(value, out var d))
                        return d.ToString("yyyy-MM-dd");
                    throw new Exception($"Invalid DATE format: {value}");

                case RaptorDB.Models.DataType.DATETIME:
                    if (DateTime.TryParse(value, out var dt))
                        return dt.ToString("yyyy-MM-dd HH:mm:ss.ff");
                    throw new Exception($"Invalid DATETIME format: {value}");

                default:
                    throw new Exception($"Unsupported datatype conversion: {expected}");
            }
        }

        // ------------------------------------------------------------
        // Validation with precise .ff millisecond accuracy
        // ------------------------------------------------------------
        private static bool ValidateDateTimeWithMilliseconds(string value)
        {
            return DateTime.TryParseExact(
                value,
                new[] {
                    "yyyy-MM-dd HH:mm:ss.ff",
                    "dd/MM/yyyy HH:mm:ss.ff",
                    "MM-dd-yyyy HH:mm:ss.ff"
                },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            );
        }

        // Validators.cs
        public static bool IsSupportedType(DataType type)
        {
            return type switch
            {
                DataType.INT => true,
                DataType.LONG => true,
                DataType.FLOAT => true,
                DataType.STR => true,
                DataType.DATE => true,
                DataType.DATETIME => true,
                _ => false
            };
        }


    }
}

