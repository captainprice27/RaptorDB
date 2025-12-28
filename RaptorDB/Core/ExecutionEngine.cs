using System;
using System.Collections.Generic;
using System.Linq;
using RaptorDB.RaptorDB.Parser.AST;
using RaptorDB.RaptorDB.Storage;
using RaptorDB.RaptorDB.Models;
using RaptorDB.RaptorDB.Utils;

namespace RaptorDB.RaptorDB.Core
{
    internal class ExecutionEngine
    {
        private readonly DBEngine _db;
        private readonly SchemaManager _schema;
        private readonly RecordManager _records;
        private readonly IndexManager _index;
        private readonly WALManager _wal;

        private string Normalize(string name) => name?.Trim().TrimEnd(';').ToLower();

        public ExecutionEngine(DBEngine engine)
        {
            _db = engine;
            _schema = new SchemaManager(_db);
            _records = new RecordManager(_db);
            _index = new IndexManager(_db);
            _wal = new WALManager(_db);
        }

        public string Execute(AstNode node)
        {
            return node switch
            {
                ListTablesNode => ExecuteListTables(),
                CurrentDatabaseNode => $"Active Database: {_db.GetActiveDatabaseName()}",
                CreateTableNode ct => CreateTable(ct),
                InsertNode ins => ExecuteInsert(ins),
                SelectNode sel => ExecuteSelect(sel),
                DeleteNode del => ExecuteDelete(del),
                UpdateNode upd => ExecuteUpdate(upd),
                DropTableNode dt => DropTable(Normalize(dt.TableName)),
                DropDatabaseNode dd => DropDatabase(Normalize(dd.DatabaseName)),
                UseDatabaseNode ud => _db.SwitchDatabase(Normalize(ud.DatabaseName)),
                CreateDatabaseNode cd => _db.CreateDatabase(Normalize(cd.Name)),
                _ => "Unknown or unsupported command."
            };
        }

        // --- MULTI-CONDITION EVALUATOR ---
        private bool MatchesAllConditions(Dictionary<string, string> row, List<Condition> conditions, TableSchema schema)
        {
            if (conditions == null || conditions.Count == 0) return true;

            foreach (var cond in conditions)
            {
                string colName = Normalize(cond.Column);
                if (!row.ContainsKey(colName)) return false;

                var colDef = schema.Columns.FirstOrDefault(c => c.Name == colName);
                if (colDef == null) throw new Exception($"Column '{cond.Column}' not found.");

                if (!EvaluateSingle(row[colName], cond.Value, cond.Operator, colDef.Type))
                    return false; // AND logic: one fail = all fail
            }
            return true;
        }

        private bool EvaluateSingle(string recordValue, string queryValue, string op, DataType type)
        {
            try
            {
                if (recordValue == null) return false;
                switch (type)
                {
                    case DataType.INT:
                        if (!int.TryParse(recordValue, out int i1) || !int.TryParse(queryValue, out int i2)) return false;
                        return Compare(i1, i2, op);
                    case DataType.LONG:
                        if (!long.TryParse(recordValue, out long l1) || !long.TryParse(queryValue, out long l2)) return false;
                        return Compare(l1, l2, op);
                    case DataType.FLOAT:
                        if (!double.TryParse(recordValue, out double d1) || !double.TryParse(queryValue, out double d2)) return false;
                        return Compare(d1, d2, op);
                    case DataType.DATE:
                    case DataType.DATETIME:
                        if (!DateTime.TryParse(recordValue, out DateTime dt1) || !DateTime.TryParse(queryValue, out DateTime dt2)) return false;
                        return Compare(dt1, dt2, op);
                    case DataType.STR:
                    default:
                        int cmp = string.Compare(recordValue, queryValue, StringComparison.OrdinalIgnoreCase);
                        return Compare(cmp, 0, op);
                }
            }
            catch { return false; }
        }

        private bool Compare<T>(T v1, T v2, string op) where T : IComparable<T>
        {
            int cmp = v1.CompareTo(v2);
            return op switch { ">" => cmp > 0, "<" => cmp < 0, ">=" => cmp >= 0, "<=" => cmp <= 0, "=" => cmp == 0, "!=" => cmp != 0, _ => false };
        }

        // ---------------- SELECT ----------------
        private string ExecuteSelect(SelectNode node)
        {
            string table = Normalize(node.TableName);
            var rows = _records.ReadAll(table);

            if (node.Conditions.Count > 0)
            {
                var schema = _schema.Load(table);
                rows = rows.Where(r => MatchesAllConditions(r, node.Conditions, schema)).ToList();
            }
            return Format(rows);
        }

        // ---------------- DELETE ----------------
        private string ExecuteDelete(DeleteNode node)
        {
            string table = Normalize(node.TableName);
            var rows = _records.ReadAll(table);
            var schema = _schema.Load(table);

            int removed = rows.RemoveAll(r => MatchesAllConditions(r, node.Conditions, schema));

            _records.RewriteTable(table, rows);
            _wal.Log("DELETE", table, $"Removed {removed} rows");
            return $"[OK] {removed} row(s) deleted.";
        }

        // ---------------- UPDATE ----------------
        private string ExecuteUpdate(UpdateNode node)
        {
            string table = Normalize(node.TableName);
            var schema = _schema.Load(table);
            var setCol = schema.Columns.First(c => c.Name == Normalize(node.SetColumn));

            if (!Validators.ValidateValue(node.SetValue, setCol.Type))
                throw new Exception($"Invalid value for {setCol.Name} : {node.SetValue}");

            var rows = _records.ReadAll(table);
            int updated = 0;

            foreach (var r in rows)
            {
                if (MatchesAllConditions(r, node.WhereConditions, schema))
                {
                    r[Normalize(node.SetColumn)] = Validators.ConvertToInternal(node.SetValue, setCol.Type);
                    updated++;
                }
            }

            _records.RewriteTable(table, rows);
            _wal.Log("UPDATE", table, $"{node.SetColumn}={node.SetValue}");
            return $"[OK] {updated} row(s) updated.";
        }

        // --- BOILERPLATE HELPERS (INSERT, CREATE, ETC) ---
        // (Keep the existing implementation from your current file for these methods)
        private string CreateTable(CreateTableNode node)
        {
            string table = Normalize(node.TableName);
            var cols = node.Columns.Select(c => new ColumnDefinition
            {
                Name = Normalize(c.Name),
                Type = Enum.Parse<DataType>(c.Type, true),
                IsPrimaryKey = c.IsPK
            }).ToList();
            return _schema.CreateCustomTable(table, cols);
        }

        private string ExecuteInsert(InsertNode node)
        {
            string table = Normalize(node.TableName);
            var schema = _schema.Load(table);
            var row = schema.MapAndValidateInsert(node.Values, node.Columns);
            var pkCol = schema.GetPrimaryKeyColumn();
            var pk = schema.GetPrimaryKeyValue(row);
            if (_index.Lookup(table, pk, pkCol.Type) != -1)
                throw new Exception($"Duplicate primary key '{pk}'.");
            long offset = _records.InsertRecord(table, row);
            _index.AddIndexEntry(table, pk, offset, pkCol.Type);
            _wal.Log("INSERT", table, $"{pk}");
            return $"[OK] Inserted record with PK={pk}.";
        }

        private string DropTable(string table)
        {
            table = Normalize(table);
            _wal.LogDropTable(table);
            _schema.DeleteSchema(table);
            _records.DeleteTableData(table);
            _index.DropTableIndexes(table);
            return $"[OK] Table '{table}' dropped.";
        }

        private string DropDatabase(string db)
        {
            db = Normalize(db);
            if (_db.GetActiveDatabaseName() == db) return "ERROR: Cannot drop active DB. Switch first.";
            string dbPath = _db.GetDbPath(db);
            _index.DropDatabaseIndexes(dbPath);
            _db.DropDatabase(db);
            return $"[OK] Database '{db}' dropped.";
        }

        private string ExecuteListTables()
        {
            string dbPath = _db.GetActiveDbPath();
            if (!System.IO.Directory.Exists(dbPath)) return "ERROR: Active database directory missing.";
            string[] schemaFiles = System.IO.Directory.GetFiles(dbPath, "*.schema");
            if (schemaFiles.Length == 0) return "No tables found in current database.";
            var tableNames = schemaFiles.Select(f => System.IO.Path.GetFileNameWithoutExtension(f)).ToList();
            return "Tables in '" + _db.GetActiveDatabaseName() + "':\n" + string.Join("\n", tableNames.Select(t => $" 📄 {t}"));
        }

        private string Format(List<Dictionary<string, string>> rows)
        {
            if (rows.Count == 0) return "(no results)";
            return string.Join("\n", rows.Select(r =>
                "{ " + string.Join(", ", r.Select(k => k.Key + ":" + k.Value)) + " }"));
        }
    }
}