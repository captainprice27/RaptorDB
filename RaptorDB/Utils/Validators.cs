////using RaptorDB.RaptorDB.Models;
////using System;
////using System.ComponentModel.DataAnnotations;
////using System.Globalization;

////namespace RaptorDB.RaptorDB.Utils
////{
////    internal static class Validators
////    {
////        // Allowed DB types
////        public static bool IsSupportedType(RaptorDB.RaptorDB.Models.DataType type) =>
////            type == RaptorDB.RaptorDB.Models.DataType.INT || type == RaptorDB.RaptorDB.Models.DataType.LONG ||
////            type == RaptorDB.RaptorDB.Models.DataType.STR || type == RaptorDB.RaptorDB.Models.DataType.FLOAT ||
////            type == RaptorDB.RaptorDB.Models.DataType.BOOL || type == DataType.DATE ||
////            type == DataType.DATETIME;

////        // Validate primitive types before conversion
////        public static bool ValidateValue(string value, DataType type)
////        {
////            return type switch
////            {
////                DataType.INT => int.TryParse(value, out _),
////                DataType.LONG => long.TryParse(value, out _),
////                DataType.FLOAT => float.TryParse(value, out _),
////                DataType.BOOL => value == "true" || value == "false",
////                DataType.STR => true,
////                DataType.DATE => TryParseDateToEpoch(value, out _),
////                DataType.DATETIME => TryParseDateTimeToEpoch(value, out _),
////                _ => false
////            };
////        }

////        // Normalize DATE to numeric epoch days
////        public static bool TryParseDateToEpoch(string input, out long epochDays)
////        {
////            input = input.Replace('/', '-');
////            bool ok = DateTime.TryParseExact(
////                input,
////                "yyyy-MM-dd",
////                CultureInfo.InvariantCulture,
////                DateTimeStyles.None,
////                out var dt);

////            epochDays = ok ? dt.Ticks / TimeSpan.TicksPerDay : -1;
////            return ok;
////        }

////        // Normalize DATETIME to epoch ticks
////        public static bool TryParseDateTimeToEpoch(string input, out long epochTicks)
////        {
////            input = input.Replace('/', '-').Replace('T', ' ');
////            bool ok = DateTime.TryParseExact(
////                input,
////                "yyyy-MM-dd HH:mm:ss.ff",
////                CultureInfo.InvariantCulture,
////                DateTimeStyles.None,
////                out var dt);

////            epochTicks = ok ? dt.Ticks : -1;
////            return ok;
////        }

////        // Convert a validated value into internal storage form
////        public static string ConvertToInternal(string value, DataType type)
////        {
////            return type switch
////            {
////                DataType.DATE =>
////                    TryParseDateToEpoch(value, out var dayEpoch)
////                        ? dayEpoch.ToString()
////                        : throw new Exception(Errors.DateFormatError),

////                DataType.DATETIME =>
////                    TryParseDateTimeToEpoch(value, out var timeEpoch)
////                        ? timeEpoch.ToString()
////                        : throw new Exception(Errors.DateTimeFormatError),

////                _ => value // primitive types remain raw
////            };
////        }
////    }
////}
//using System;
//using System.Globalization;
//using RaptorDB.RaptorDB.Models;
////using RaptorDB.Models;

//namespace RaptorDB.RaptorDB.Utils
//{
//    internal static class Validators
//    {
//        // ------------------------------------------
//        // Validate a value matches the expected type
//        // ------------------------------------------
//        public static bool ValidateDataType(string value, RaptorDB.Models.DataType expected)
//        {
//            return expected switch
//            {
//                RaptorDB.Models.DataType.INT => int.TryParse(value, out _),
//                RaptorDB.Models.DataType.LONG => long.TryParse(value, out _),
//                RaptorDB.Models.DataType.FLOAT => float.TryParse(value, out _),
//                RaptorDB.Models.DataType.STR => true, // text always valid
//                RaptorDB.Models.DataType.DATE => DateTime.TryParse(value, out _),
//                RaptorDB.Models.DataType.DATETIME => ValidateDateTimeWithMilliseconds(value),
//                _ => false
//            };
//        }

//        // ------------------------------------------------------------
//        // Normalize and convert values (internal DB writing standard)
//        // ------------------------------------------------------------
//        public static string NormalizeValue(string value, RaptorDB.Models.DataType expected)
//        {
//            switch (expected)
//            {
//                case RaptorDB.Models.DataType.INT:
//                case RaptorDB.Models.DataType.LONG:
//                case RaptorDB.Models.DataType.FLOAT:
//                    return value.Trim();

//                case RaptorDB.Models.DataType.STR:
//                    return value.Trim('"').Trim('\'');

//                case RaptorDB.Models.DataType.DATE:
//                    if (DateTime.TryParse(value, out var d))
//                        return d.ToString("yyyy-MM-dd");
//                    throw new Exception($"Invalid DATE value: {value}");

//                case RaptorDB.Models.DataType.DATETIME:
//                    if (DateTime.TryParse(value, out var dt))
//                        return dt.ToString("yyyy-MM-dd HH:mm:ss.ff"); // fixed 2-dec ms format
//                    throw new Exception($"Invalid DATETIME value: {value}");

//                default:
//                    throw new Exception($"Unsupported datatype normalization: {expected}");
//            }
//        }

//        // ------------------------------------------------------------
//        // Special validation for DATETIME with precise formatting
//        // ------------------------------------------------------------
//        private static bool ValidateDateTimeWithMilliseconds(string value)
//        {
//            // Accepts 2025-12-26 17:23:44.67
//            return DateTime.TryParseExact(
//                value,
//                new[] {
//                    "yyyy-MM-dd HH:mm:ss.ff",
//                    "dd/MM/yyyy HH:mm:ss.ff",
//                    "MM-dd-yyyy HH:mm:ss.ff"
//                },
//                CultureInfo.InvariantCulture,
//                DateTimeStyles.None,
//                out _
//            );
//        }
//    }
//}
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

