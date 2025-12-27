using System;
using System.Collections.Generic;
using System.Linq;
using RaptorDB.RaptorDB.Utils;

namespace RaptorDB.RaptorDB.Models
{
    internal class TableSchema
    {
        public string TableName { get; set; }
        public List<ColumnDefinition> Columns { get; set; } = new();

        public TableSchema() { }

        public TableSchema(string name, List<ColumnDefinition> columns)
        {
            TableName = name;
            Columns = columns;

            // enforce exactly one primary key
            var pkCount = Columns.Count(c => c.IsPrimaryKey);

            if (pkCount == 0)
                throw new Exception(Errors.MissingPrimaryKey);

            if (pkCount > 1)
                throw new Exception("SCHEMA ERROR: Only one PRIMARY KEY allowed.");
        }

        // Validate + convert values before write
        public Dictionary<string, string> MapAndValidateInsert(
            List<string> rawValues, List<string>? names = null)
        {
            var mapped = new Dictionary<string, string>();

            if (names != null && names.Count > 0)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    var col = Columns.FirstOrDefault(c => c.Name == names[i])
                        ?? throw new Exception(string.Format(Errors.UnknownColumn, names[i]));

                    string val = rawValues[i];

                    if (!Validators.ValidateValue(val, col.Type))
                        throw new Exception(string.Format(Errors.InvalidType, col.Name, col.Type, val));

                    mapped[col.Name] = Validators.ConvertToInternal(val, col.Type);
                }
            }
            else
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    var col = Columns[i];
                    string val = rawValues[i];

                    if (!Validators.ValidateValue(val, col.Type))
                        throw new Exception(string.Format(Errors.InvalidType, col.Name, col.Type, val));

                    mapped[col.Name] = Validators.ConvertToInternal(val, col.Type);
                }
            }

            return mapped;
        }

        public string GetPrimaryKeyValue(Dictionary<string, string> row)
        {
            var pk = Columns.First(c => c.IsPrimaryKey);
            return row[pk.Name];
        }

        public ColumnDefinition GetPrimaryKeyColumn()
        {
            return Columns.First(c => c.IsPrimaryKey);
        }
    }
}
