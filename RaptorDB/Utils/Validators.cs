using System;
using System.Globalization;
using RaptorDB.RaptorDB.Models;

namespace RaptorDB.RaptorDB.Utils
{
    /// <summary>
    /// Validates and converts raw string input into safe internal storage values.
    /// Updated for .NET 10:
    ///  - Culture-invariant numeric parsing throughout (NumberStyles + InvariantCulture)
    ///  - BOOL type fully implemented (was stubbed in v1.1)
    ///  - DATETIME parsing extended to ISO 8601 formats supported by .NET 10
    ///  - Read-only spans used where applicable to avoid string allocations
    /// </summary>
    internal static class Validators
    {
        // ---------------------------------------------------------------
        // TYPE SUPPORT CHECK
        // ---------------------------------------------------------------

        public static bool IsSupportedType(DataType type) => type switch
        {
            DataType.INT      => true,
            DataType.LONG     => true,
            DataType.FLOAT    => true,
            DataType.STR      => true,
            DataType.DATE     => true,
            DataType.DATETIME => true,
            DataType.BOOL     => true,  // ← fully implemented in .NET 10 build
            _                 => false
        };

        // ---------------------------------------------------------------
        // VALUE VALIDATION
        // ---------------------------------------------------------------

        public static bool ValidateValue(string value, DataType expected)
        {
            if (value is null) return false;

            return expected switch
            {
                DataType.INT  =>
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),

                DataType.LONG =>
                    long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),

                DataType.FLOAT =>
                    // InvariantCulture: '.' is always the decimal separator regardless of OS locale.
                    float.TryParse(value, NumberStyles.Float | NumberStyles.AllowLeadingSign,
                                   CultureInfo.InvariantCulture, out _),

                DataType.STR  => true,  // any string is valid

                DataType.BOOL =>
                    // Accept 'true'/'false' (case-insensitive) and '1'/'0'
                    bool.TryParse(value, out _) ||
                    value == "0" || value == "1",

                DataType.DATE =>
                    DateTime.TryParseExact(value,
                        ["yyyy-MM-dd", "dd/MM/yyyy", "MM-dd-yyyy"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _),

                DataType.DATETIME => ValidateDateTimeFormat(value),

                _ => false
            };
        }

        // ---------------------------------------------------------------
        // CONVERT TO INTERNAL STORAGE FORMAT
        // ---------------------------------------------------------------

        public static string ConvertToInternal(string value, DataType expected)
        {
            return expected switch
            {
                DataType.INT or DataType.LONG =>
                    value.Trim(),

                DataType.FLOAT =>
                    // Normalise to InvariantCulture string so locale differences don't corrupt data.
                    float.TryParse(value.Trim(), NumberStyles.Float | NumberStyles.AllowLeadingSign,
                                   CultureInfo.InvariantCulture, out float f)
                        ? f.ToString(CultureInfo.InvariantCulture)
                        : throw new Exception($"Invalid FLOAT value: {value}"),

                DataType.STR =>
                    value.Trim('"').Trim('\''),   // strip surrounding quotes

                DataType.BOOL =>
                    // Normalise to canonical 'true'/'false'
                    (bool.TryParse(value, out bool b)
                        ? b
                        : value == "1").ToString().ToLower(),

                DataType.DATE =>
                    DateTime.TryParseExact(value,
                        ["yyyy-MM-dd", "dd/MM/yyyy", "MM-dd-yyyy"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime d)
                            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                            : throw new Exception($"Invalid DATE format: {value}"),

                DataType.DATETIME =>
                    ParseDateTimeInternal(value),

                _ => throw new Exception($"Unsupported datatype conversion: {expected}")
            };
        }

        // ---------------------------------------------------------------
        // DATETIME HELPERS
        // ---------------------------------------------------------------

        /// <summary>
        /// Accepts a wide set of DATETIME formats, including ISO 8601 with T-separator
        /// which is commonly generated by .NET 10 system APIs.
        /// </summary>
        private static bool ValidateDateTimeFormat(string value)
        {
            return DateTime.TryParseExact(
                value,
                [
                    "yyyy-MM-dd HH:mm:ss.ff",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-ddTHH:mm:ss",          // ISO 8601 (.NET 10 default)
                    "yyyy-MM-ddTHH:mm:ss.ff",       // ISO 8601 with hundredths
                    "dd/MM/yyyy HH:mm:ss.ff",
                    "MM-dd-yyyy HH:mm:ss.ff"
                ],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            );
        }

        private static string ParseDateTimeInternal(string value)
        {
            if (DateTime.TryParseExact(value,
                [
                    "yyyy-MM-dd HH:mm:ss.ff",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-ddTHH:mm:ss",
                    "yyyy-MM-ddTHH:mm:ss.ff",
                    "dd/MM/yyyy HH:mm:ss.ff",
                    "MM-dd-yyyy HH:mm:ss.ff"
                ],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime dt))
            {
                return dt.ToString("yyyy-MM-dd HH:mm:ss.ff", CultureInfo.InvariantCulture);
            }
            throw new Exception($"Invalid DATETIME format: {value}");
        }
    }
}
