using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.Models;
using RaptorDB.RaptorDB.Utils;

namespace RaptorDB.RaptorDB.Storage
{
    internal class SchemaManager
    {
        private readonly DBEngine _engine;
        private string BasePath => _engine.GetActiveDbPath();

        // Normalize identifiers: remove semicolons, trim, lowercase
        private string Normalize(string name) =>
            name?.Trim().TrimEnd(';').ToLower();

        public SchemaManager(DBEngine engine)
        {
            _engine = engine;
        }

        // --------------------------------------------------------------------
        // CREATE TABLE
        // --------------------------------------------------------------------
        public string CreateCustomTable(string table, List<ColumnDefinition> columns)
        {
            table = Normalize(table);
            if (table is null or "") throw new Exception("Invalid table name.");

            string schemaFile = Path.Combine(BasePath, $"{table}.schema");

            if (File.Exists(schemaFile))
                return $"ERROR: Table '{table}' already exists.";

            // Validate datatypes + primary key rules
            foreach (var col in columns)
            {
                col.Name = Normalize(col.Name);

                if (!Validators.IsSupportedType(col.Type))
                    throw new Exception(string.Format(Errors.UnsupportedColumnType, col.Type));

                if (col.IsPrimaryKey && !col.IsTypeAllowedAsPrimaryKey())
                    throw new Exception(string.Format(Errors.InvalidPrimaryKeyType, col.Type));
            }

            if (columns.Count(c => c.IsPrimaryKey) != 1)
                throw new Exception(Errors.MissingPrimaryKey);

            Directory.CreateDirectory(BasePath);

            using var writer = new StreamWriter(schemaFile, false);
            foreach (var col in columns)
                writer.WriteLine($"{col.Name}:{col.Type}:{(col.IsPrimaryKey ? "PK" : "")}");

            return $"[OK] Table '{table}' created successfully.";
        }

        // --------------------------------------------------------------------
        // LOAD SCHEMA
        // --------------------------------------------------------------------
        public TableSchema Load(string table)
        {
            table = Normalize(table);
            string schemaFile = Path.Combine(BasePath, $"{table}.schema");

            if (!File.Exists(schemaFile))
                throw new Exception($"ERROR: Table '{table}' does not exist.");

            var cols = new List<ColumnDefinition>();

            foreach (var line in File.ReadAllLines(schemaFile))
            {
                var p = line.Split(':');
                if (p.Length < 2)
                    throw new Exception("SCHEMA PARSE ERROR: Invalid schema format.");

                cols.Add(new ColumnDefinition
                {
                    Name = Normalize(p[0]),
                    Type = Enum.Parse<DataType>(p[1], true),
                    IsPrimaryKey = p.Length > 2 && p[2] == "PK"
                });
            }

            return new TableSchema(table, cols);
        }

        // --------------------------------------------------------------------
        // DROP TABLE (Schema only; IndexManager + RecordManager handle the rest)
        // --------------------------------------------------------------------
        public void DeleteSchema(string table)
        {
            table = Normalize(table);
            string schema = Path.Combine(BasePath, $"{table}.schema");

            if (File.Exists(schema))
                File.Delete(schema);
        }
    }
}
