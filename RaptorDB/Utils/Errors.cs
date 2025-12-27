namespace RaptorDB.RaptorDB.Utils
{
    internal static class Errors
    {
        // ---------------- GENERAL ----------------
        public const string InvalidType =
            "TYPE ERROR: Column '{0}' expects '{1}' but received '{2}'.";

        public const string UnsupportedColumnType =
            "SCHEMA ERROR: '{0}' is not a supported data type.";

        public const string InvalidPrimaryKeyType =
            "PRIMARY KEY ERROR: PK type '{0}' is not allowed. Allowed: INT, LONG, STR, DATE, DATETIME.";

        public const string UnknownColumn =
            "SCHEMA ERROR: Column '{0}' does not exist in table definition.";

        // ---------------- DATE / DATETIME ----------------
        public const string DateFormatError =
            "DATE FORMAT ERROR: Expected YYYY-MM-DD or valid slash format.";

        public const string DateTimeFormatError =
            "DATETIME FORMAT ERROR: Expected 'YYYY-MM-DD HH:MM:SS.ff' or slash format 'YYYY/MM/DD HH:MM:SS.ff'.";

        // ---------------- VALUE CHECKS ----------------
        public const string BoolError =
            "BOOLEAN ERROR: Expected 'true' or 'false'.";

        public const string FloatPrecisionError =
            "FLOAT ERROR: Floating values cannot be used as primary keys.";

        // ---------------- PK CONSTRAINT ----------------
        public const string DuplicatePrimaryKey =
            "PRIMARY KEY ERROR: Value '{0}' already exists.";

        public const string MissingPrimaryKey =
            "SCHEMA ERROR: Table must define exactly ONE primary key.";
    }
}
