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

        private string Normalize(string name) =>
            name?.Trim().TrimEnd(';').ToLower();

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

        // ---------------- CREATE TABLE ------------------------
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

        // ---------------- INSERT ------------------------
        private string ExecuteInsert(InsertNode node)
        {
            string table = Normalize(node.TableName);
            var schema = _schema.Load(table);

            var row = schema.MapAndValidateInsert(node.Values, node.Columns);
            var pkCol = schema.GetPrimaryKeyColumn();
            var pk = schema.GetPrimaryKeyValue(row);

            long exists = _index.Lookup(table, pk, pkCol.Type);
            if (exists != -1)
                throw new Exception($"Duplicate primary key '{pk}'.");

            long offset = _records.InsertRecord(table, row);
            _index.AddIndexEntry(table, pk, offset, pkCol.Type);

            _wal.Log("INSERT", table, $"{pk}");

            return $"[OK] Inserted record with PK={pk}.";
        }

        // ---------------- SELECT ------------------------
        private string ExecuteSelect(SelectNode node)
        {
            string table = Normalize(node.TableName);
            var rows = _records.ReadAll(table);

            if (!string.IsNullOrEmpty(node.Column) && !string.IsNullOrEmpty(node.Value))
            {
                rows = rows.Where(r =>
                    r.ContainsKey(Normalize(node.Column)) &&
                    r[Normalize(node.Column)].Equals(node.Value, StringComparison.OrdinalIgnoreCase))
                .ToList();
            }

            return Format(rows);
        }

        // ---------------- DELETE ------------------------
        private string ExecuteDelete(DeleteNode node)
        {
            string table = Normalize(node.TableName);
            var rows = _records.ReadAll(table);

            int removed = rows.RemoveAll(r =>
                r.ContainsKey(Normalize(node.Column)) &&
                r[Normalize(node.Column)] == node.Value);

            _records.RewriteTable(table, rows);
            _wal.Log("DELETE", table, $"{node.Column}={node.Value}");

            return $"[OK] {removed} row(s) deleted.";
        }

        // ---------------- UPDATE ------------------------
        private string ExecuteUpdate(UpdateNode node)
        {
            string table = Normalize(node.TableName);
            var schema = _schema.Load(table);

            var col = schema.Columns.First(c => c.Name == Normalize(node.SetColumn));
            if (!Validators.ValidateValue(node.SetValue, col.Type))
                throw new Exception($"Invalid value for {col.Name} : {node.SetValue}");

            var rows = _records.ReadAll(table);
            int updated = 0;

            foreach (var r in rows)
            {
                if (r.ContainsKey(Normalize(node.WhereColumn)) &&
                    r[Normalize(node.WhereColumn)] == node.WhereValue)
                {
                    r[Normalize(node.SetColumn)] =
                        Validators.ConvertToInternal(node.SetValue, col.Type);
                    updated++;
                }
            }

            _records.RewriteTable(table, rows);
            _wal.Log("UPDATE", table, $"{node.SetColumn}={node.SetValue}");

            return $"[OK] {updated} row(s) updated.";
        }

        // ---------------- DROP TABLE ------------------------
        private string DropTable(string table)
        {
            table = Normalize(table);
            _wal.LogDropTable(table);
            _schema.DeleteSchema(table);
            _records.DeleteTableData(table);
            _index.DropTableIndexes(table);
            return $"[OK] Table '{table}' dropped.";
        }

        // ---------------- DROP DATABASE ------------------------
        private string DropDatabase(string db)
        {
            db = Normalize(db);
            if (_db.GetActiveDatabaseName() == db)
                return "ERROR: Cannot drop active DB. Switch first.";

            string dbPath = _db.GetDbPath(db);
            _index.DropDatabaseIndexes(dbPath);
            _db.DropDatabase(db);

            return $"[OK] Database '{db}' dropped.";
        }

        // ---------------- FORMAT OUTPUT ------------------------
        private string Format(List<Dictionary<string, string>> rows)
        {
            if (rows.Count == 0) return "(no results)";
            return string.Join("\n", rows.Select(r =>
                "{ " + string.Join(", ", r.Select(k => k.Key + ":" + k.Value)) + " }"));
        }
    }
}
