namespace RaptorDB.RaptorDB.Models
{
    internal enum DataType
    {
        INT,
        LONG,
        STR,
        BOOL,
        FLOAT,
        DATE,
        DATETIME
    }

    internal class ColumnDefinition
    {
        public string Name { get; set; }
        public DataType Type { get; set; }
        public bool IsPrimaryKey { get; set; }

        public bool IsTypeAllowedAsPrimaryKey()
        {
            return Type == DataType.INT ||
                   Type == DataType.LONG ||
                   Type == DataType.STR ||
                   Type == DataType.DATE ||
                   Type == DataType.DATETIME;
        }
    }
}
