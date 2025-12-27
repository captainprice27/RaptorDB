using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Models
{
    /// <summary>
    /// Represents a single row of data inside a table.
    /// Maps column name → value.
    /// In the future this will serialize into binary for .data files.
    /// </summary>
    internal class RowData
    {
        /// <summary>
        /// The internal key-value store for the row.
        /// Key = ColumnName, Value = FieldValue.
        /// </summary>
        public Dictionary<string, string> Fields { get; }

        public RowData()
        {
            Fields = new Dictionary<string, string>();
        }

        /// <summary>
        /// Sets a value for a column.
        /// </summary>
        public void Set(string column, string value)
        {
            Fields[column] = value;
        }

        /// <summary>
        /// Retrieves a value by column name (returns null if missing).
        /// </summary>
        public string? Get(string column)
        {
            return Fields.ContainsKey(column) ? Fields[column] : null;
        }
    }
}
