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

        private static string Normalize(string? name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return name.Trim().TrimEnd(';').ToLower();
        }

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
            // v2.0 — JOIN path is handled separately to keep the legacy
            // single-table path identical to v1.3 behaviour. The JOIN path
            // is taken whenever one or more joins are present.
            if (node.Joins.Count > 0) return ExecuteSelectWithJoin(node);

            string table = Normalize(node.TableName);
            var rows = _records.ReadAll(table);

            if (node.Conditions.Count > 0)
            {
                var schema = _schema.Load(table);
                rows = rows.Where(r => MatchesAllConditions(r, node.Conditions, schema)).ToList();
            }

            // v2.0 — ORDER BY (single-table path).
            if (node.OrderBy.Count > 0)
            {
                var schema = _schema.Load(table);
                rows = ApplyOrderBy(rows, node.OrderBy, schema);
            }

            // Apply column projection if specific columns were requested.
            if (node.Columns.Count > 0)
                rows = ProjectColumns(rows, node.Columns);

            return Format(rows);
        }

        // ---------------- SELECT WITH JOIN (v2.0) ----------------
        //
        // v2.0 supports INNER / LEFT / RIGHT across both two-table and
        // N-table chained join queries:
        //
        //     SELECT ... FROM A
        //     INNER JOIN B ON A.x = B.x
        //     LEFT  JOIN C ON B.y = C.y
        //     RIGHT JOIN D ON A.z = D.z;
        //
        // Algorithm (iterative Nested-Loop):
        //   1. Start with the FROM table's rows, qualified as "A.<col>".
        //   2. For each JOIN clause in order, take the running result set as
        //      the "left" side and the new table as the "right" side, then
        //      perform a single-step Nested-Loop join. The output replaces
        //      the running set so the next join can build on top of it.
        //   3. After all joins are applied, the WHERE filter and column
        //      projection run against fully-qualified rows.
        //
        // RaptorDB has no native NULL storage. For unmatched rows in
        // LEFT/RIGHT joins, the missing side's columns are filled with the
        // literal "<NULL>" placeholder in the output only.
        private string ExecuteSelectWithJoin(SelectNode node)
        {
            string baseTable = Normalize(node.TableName);
            var baseSchema = _schema.Load(baseTable);

            // Track every schema seen so far so subsequent JOIN ... ON
            // clauses can reference any previously-joined table.
            var schemas = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase)
            {
                [baseTable] = baseSchema
            };

            // Prime the running result set with qualified keys.
            var current = _records.ReadAll(baseTable)
                .Select(r => QualifyRow(r, baseTable))
                .ToList();

            foreach (var join in node.Joins)
            {
                string rightTable = Normalize(join.TableName);
                var rightSchema = _schema.Load(rightTable);
                var rightRows = _records.ReadAll(rightTable);

                // Resolve the ON clause to a (currentKey, rightCol) pair.
                (string currentKey, string rightCol) =
                    ResolveJoinKeys(join, schemas, rightTable, rightSchema);

                current = ApplyJoinStep(
                    current, schemas,
                    rightRows, rightTable, rightSchema,
                    currentKey, rightCol, join.Type);

                schemas[rightTable] = rightSchema;
            }

            // Apply WHERE on joined rows (qualified-aware evaluator).
            if (node.Conditions.Count > 0)
            {
                current = current.Where(jr =>
                    MatchesAllConditionsJoined(jr, node.Conditions, schemas)).ToList();
            }

            // v2.0 — ORDER BY on joined rows (qualified-aware).
            if (node.OrderBy.Count > 0)
                current = ApplyOrderByJoined(current, node.OrderBy, schemas);

            // Apply projection — supports "*", "table.*", "table.col", "col".
            if (node.Columns.Count > 0)
                current = ProjectJoinedColumns(current, node.Columns, schemas);

            return Format(current);
        }

        // ---------- JOIN HELPERS ----------

        // Re-keys a raw row so all column names are qualified with the
        // owning table (e.g. "id" → "students.id").
        private static Dictionary<string, string> QualifyRow(
            Dictionary<string, string> row, string table)
        {
            var qualified = new Dictionary<string, string>(row.Count);
            foreach (var (k, v) in row) qualified[$"{table}.{k}"] = v;
            return qualified;
        }

        // Performs a single Nested-Loop join step between the current
        // (already-qualified) row set and one new right-hand table.
        private static List<Dictionary<string, string>> ApplyJoinStep(
            List<Dictionary<string, string>> current,
            Dictionary<string, TableSchema> previousSchemas,
            List<Dictionary<string, string>> rightRows,
            string rightTable, TableSchema rightSchema,
            string currentKey, string rightCol,
            JoinType type)
        {
            var output = new List<Dictionary<string, string>>();

            switch (type)
            {
                case JoinType.Inner:
                    foreach (var l in current)
                        foreach (var r in rightRows)
                            if (l.TryGetValue(currentKey, out var lv) &&
                                r.TryGetValue(rightCol, out var rv) &&
                                lv == rv)
                                output.Add(MergeJoined(l, r, rightTable));
                    break;

                case JoinType.Left:
                    foreach (var l in current)
                    {
                        bool matched = false;
                        foreach (var r in rightRows)
                        {
                            if (l.TryGetValue(currentKey, out var lv) &&
                                r.TryGetValue(rightCol, out var rv) &&
                                lv == rv)
                            {
                                output.Add(MergeJoined(l, r, rightTable));
                                matched = true;
                            }
                        }
                        if (!matched)
                            output.Add(MergeJoined(l, null, rightTable, rightSchema));
                    }
                    break;

                case JoinType.Right:
                    foreach (var r in rightRows)
                    {
                        bool matched = false;
                        foreach (var l in current)
                        {
                            if (l.TryGetValue(currentKey, out var lv) &&
                                r.TryGetValue(rightCol, out var rv) &&
                                lv == rv)
                            {
                                output.Add(MergeJoined(l, r, rightTable));
                                matched = true;
                            }
                        }
                        if (!matched)
                        {
                            // Build a "<NULL>" shell for every previously-
                            // joined column so the output row shape stays uniform.
                            var nullLeft = new Dictionary<string, string>();
                            foreach (var (tbl, sch) in previousSchemas)
                                foreach (var c in sch.Columns)
                                    nullLeft[$"{tbl}.{c.Name}"] = "<NULL>";
                            output.Add(MergeJoined(nullLeft, r, rightTable));
                        }
                    }
                    break;
            }

            return output;
        }

        // Merges a left (already-qualified) row with a right (raw) row.
        // If 'right' is null, 'rightSchema' must be supplied so missing
        // columns can be filled with the "<NULL>" placeholder.
        private static Dictionary<string, string> MergeJoined(
            Dictionary<string, string> left,
            Dictionary<string, string>? right, string rightTable,
            TableSchema? rightSchema = null)
        {
            var merged = new Dictionary<string, string>(left);

            if (right != null)
            {
                foreach (var (k, v) in right) merged[$"{rightTable}.{k}"] = v;
            }
            else if (rightSchema != null)
            {
                foreach (var c in rightSchema.Columns)
                    merged[$"{rightTable}.{c.Name}"] = "<NULL>";
            }

            return merged;
        }

        // Resolves the two ON-clause column references for a single join
        // step into a (currentKey, rightCol) pair, where currentKey is a
        // fully-qualified key already present in the running result set
        // and rightCol is an unqualified column name on the new table.
        private static (string currentKey, string rightCol) ResolveJoinKeys(
            JoinClause join,
            Dictionary<string, TableSchema> previousSchemas,
            string rightTable, TableSchema rightSchema)
        {
            var (aTable, aCol) = ParseRef(Normalize(join.LeftColumn),  previousSchemas, rightTable, rightSchema);
            var (bTable, bCol) = ParseRef(Normalize(join.RightColumn), previousSchemas, rightTable, rightSchema);

            bool aIsRight = aTable == rightTable;
            bool bIsRight = bTable == rightTable;

            if (aIsRight == bIsRight)
                throw new Exception(
                    $"JOIN ERROR: ON clause must reference one column from '{rightTable}' " +
                    $"and one from a previously-joined table " +
                    $"('{join.LeftColumn}' and '{join.RightColumn}' resolve to the same side).");

            return aIsRight
                ? ($"{bTable}.{bCol}", aCol)
                : ($"{aTable}.{aCol}", bCol);
        }

        // Parses a (possibly-qualified) column reference and resolves it
        // to (table, column). Errors on ambiguity or unknown columns.
        private static (string table, string col) ParseRef(
            string col,
            Dictionary<string, TableSchema> previousSchemas,
            string rightTable, TableSchema rightSchema)
        {
            int dot = col.IndexOf('.');
            if (dot >= 0)
            {
                string tbl = col[..dot];
                string c   = col[(dot + 1)..];
                if (string.Equals(tbl, rightTable, StringComparison.OrdinalIgnoreCase) &&
                    rightSchema.Columns.Any(x => x.Name == c))
                    return (rightTable, c);
                if (previousSchemas.TryGetValue(tbl, out var s) &&
                    s.Columns.Any(x => x.Name == c))
                    return (tbl, c);
                throw new Exception($"JOIN ERROR: Unknown column '{col}'.");
            }

            var matches = new List<(string, string)>();
            if (rightSchema.Columns.Any(x => x.Name == col)) matches.Add((rightTable, col));
            foreach (var (t, sch) in previousSchemas)
                if (sch.Columns.Any(x => x.Name == col)) matches.Add((t, col));

            if (matches.Count == 0)
                throw new Exception($"JOIN ERROR: Column '{col}' not found in any joined table.");
            if (matches.Count > 1)
                throw new Exception($"JOIN ERROR: Column '{col}' is ambiguous; qualify it as 'table.{col}'.");
            return matches[0];
        }

        // WHERE evaluator for joined rows — supports both qualified
        // ("students.id") and unqualified ("id") references across N tables.
        private bool MatchesAllConditionsJoined(
            Dictionary<string, string> row, List<Condition> conditions,
            Dictionary<string, TableSchema> schemas)
        {
            if (conditions == null || conditions.Count == 0) return true;

            foreach (var cond in conditions)
            {
                string col = Normalize(cond.Column);
                string fullKey;
                ColumnDefinition colDef;

                if (col.Contains('.'))
                {
                    int dot = col.IndexOf('.');
                    string tbl = col[..dot];
                    string c   = col[(dot + 1)..];
                    if (!schemas.TryGetValue(tbl, out var sch))
                        throw new Exception($"Unknown table qualifier '{tbl}' in WHERE.");
                    colDef = sch.Columns.FirstOrDefault(x => x.Name == c)
                        ?? throw new Exception($"Column '{cond.Column}' not found.");
                    fullKey = $"{tbl}.{c}";
                }
                else
                {
                    var matches = schemas
                        .Where(kv => kv.Value.Columns.Any(x => x.Name == col))
                        .Select(kv => kv.Key)
                        .ToList();
                    if (matches.Count == 0) return false;
                    if (matches.Count > 1)
                        throw new Exception($"Column '{col}' is ambiguous in joined query; qualify it.");
                    string tbl = matches[0];
                    fullKey = $"{tbl}.{col}";
                    colDef  = schemas[tbl].Columns.First(x => x.Name == col);
                }

                if (!row.TryGetValue(fullKey, out var recordValue)) return false;
                if (recordValue == "<NULL>") return false; // SQL-like: NULL never matches a predicate

                if (!EvaluateSingle(recordValue, cond.Value, cond.Operator, colDef.Type))
                    return false;
            }
            return true;
        }

        // Projects a list of joined rows down to the user-requested columns.
        // Supports "*" (all), "table.*" (all of one side), "table.col"
        // (qualified) and "col" (resolved by unique-match lookup across
        // every joined table).
        private static List<Dictionary<string, string>> ProjectJoinedColumns(
            List<Dictionary<string, string>> rows, List<string> requested,
            Dictionary<string, TableSchema> schemas)
        {
            var keys = new List<string>();
            foreach (var raw in requested)
            {
                string c = Normalize(raw);

                if (c.EndsWith(".*"))
                {
                    string tbl = c[..^2];
                    if (!schemas.TryGetValue(tbl, out var sch))
                        throw new Exception($"Unknown table qualifier '{tbl}.*' in SELECT.");
                    foreach (var def in sch.Columns) keys.Add($"{tbl}.{def.Name}");
                    continue;
                }

                if (c.Contains('.'))
                {
                    keys.Add(c);
                    continue;
                }

                // Unqualified — must match exactly one joined table.
                var matches = schemas
                    .Where(kv => kv.Value.Columns.Any(x => x.Name == c))
                    .Select(kv => kv.Key)
                    .ToList();
                if (matches.Count == 0)
                    throw new Exception($"Column '{c}' not found in joined query.");
                if (matches.Count > 1)
                    throw new Exception($"Column '{c}' is ambiguous in joined query; qualify it.");
                keys.Add($"{matches[0]}.{c}");
            }

            return rows.Select(r =>
            {
                var projected = new Dictionary<string, string>();
                foreach (var k in keys)
                    if (r.TryGetValue(k, out var v)) projected[k] = v;
                return projected;
            }).ToList();
        }

        // Single-table projection — used by the legacy non-join SELECT path.
        private static List<Dictionary<string, string>> ProjectColumns(
            List<Dictionary<string, string>> rows, List<string> requested)
        {
            var keys = requested.Select(Normalize).ToList();
            return rows.Select(r =>
            {
                var projected = new Dictionary<string, string>();
                foreach (var k in keys)
                    if (r.TryGetValue(k, out var v)) projected[k] = v;
                return projected;
            }).ToList();
        }

        // ---------------- ORDER BY (v2.0) ----------------
        //
        // RaptorDB delegates sorting to LINQ's OrderBy / ThenBy chain, which
        // is implemented as a *stable* O(n log n) sort (an enumerable variant
        // of merge-sort) inside the BCL. This means:
        //
        //   • Time complexity  : O(n log n) for n rows, k sort keys → O(k·n log n).
        //   • Space complexity : O(n)       (the result set is materialised once).
        //   • Stability        : equal-key rows preserve their original order,
        //                        which is what makes multi-key ORDER BY
        //                        (`ORDER BY a ASC, b DESC`) behave correctly.
        //
        // Comparisons are *typed* — INT/LONG are parsed as integers, FLOAT as
        // double, DATE/DATETIME as DateTime, STR as ordinal-ignore-case — so
        // "10" sorts after "9" (numeric) instead of before (lexical).
        //
        // Single-table path.
        private static List<Dictionary<string, string>> ApplyOrderBy(
            List<Dictionary<string, string>> rows,
            List<OrderByItem> orderBy,
            TableSchema schema)
        {
            IOrderedEnumerable<Dictionary<string, string>>? ordered = null;
            for (int i = 0; i < orderBy.Count; i++)
            {
                var item = orderBy[i];
                string col = Normalize(item.Column);
                // Strip optional table qualifier (single-table path)
                int dot = col.IndexOf('.');
                if (dot >= 0) col = col[(dot + 1)..];

                var def = schema.Columns.FirstOrDefault(c => c.Name == col)
                    ?? throw new Exception($"ORDER BY column '{item.Column}' not found.");
                var type = def.Type;
                string keyName = col;

                Comparer<Dictionary<string, string>> cmp = Comparer<Dictionary<string, string>>.Create(
                    (a, b) => CompareTyped(
                        a.TryGetValue(keyName, out var va) ? va : null,
                        b.TryGetValue(keyName, out var vb) ? vb : null,
                        type) * (item.Descending ? -1 : 1));

                ordered = i == 0
                    ? rows.OrderBy(r => r, cmp)
                    : ordered!.ThenBy(r => r, cmp);
            }
            return ordered!.ToList();
        }

        // Joined / multi-table path — resolves qualified-vs-unqualified
        // column refs the same way the WHERE evaluator does.
        private static List<Dictionary<string, string>> ApplyOrderByJoined(
            List<Dictionary<string, string>> rows,
            List<OrderByItem> orderBy,
            Dictionary<string, TableSchema> schemas)
        {
            IOrderedEnumerable<Dictionary<string, string>>? ordered = null;
            for (int i = 0; i < orderBy.Count; i++)
            {
                var item = orderBy[i];
                string raw = Normalize(item.Column);
                string fullKey;
                DataType type;

                if (raw.Contains('.'))
                {
                    int dot = raw.IndexOf('.');
                    string tbl = raw[..dot];
                    string c   = raw[(dot + 1)..];
                    if (!schemas.TryGetValue(tbl, out var sch))
                        throw new Exception($"Unknown table qualifier '{tbl}' in ORDER BY.");
                    var def = sch.Columns.FirstOrDefault(x => x.Name == c)
                        ?? throw new Exception($"ORDER BY column '{item.Column}' not found.");
                    fullKey = $"{tbl}.{c}";
                    type    = def.Type;
                }
                else
                {
                    var matches = schemas
                        .Where(kv => kv.Value.Columns.Any(x => x.Name == raw))
                        .Select(kv => kv.Key)
                        .ToList();
                    if (matches.Count == 0)
                        throw new Exception($"ORDER BY column '{raw}' not found in joined query.");
                    if (matches.Count > 1)
                        throw new Exception($"ORDER BY column '{raw}' is ambiguous; qualify it.");
                    fullKey = $"{matches[0]}.{raw}";
                    type    = schemas[matches[0]].Columns.First(x => x.Name == raw).Type;
                }

                string keyName = fullKey;
                DataType keyType = type;
                bool desc = item.Descending;

                Comparer<Dictionary<string, string>> cmp = Comparer<Dictionary<string, string>>.Create(
                    (a, b) => CompareTyped(
                        a.TryGetValue(keyName, out var va) ? va : null,
                        b.TryGetValue(keyName, out var vb) ? vb : null,
                        keyType) * (desc ? -1 : 1));

                ordered = i == 0
                    ? rows.OrderBy(r => r, cmp)
                    : ordered!.ThenBy(r => r, cmp);
            }
            return ordered!.ToList();
        }

        // Typed three-way comparison used by ORDER BY. "<NULL>" placeholders
        // (from LEFT/RIGHT joins) and missing keys sort *last* — same as
        // PostgreSQL's default `NULLS LAST` for ASC.
        private static int CompareTyped(string? a, string? b, DataType type)
        {
            bool aNull = a is null || a == "<NULL>";
            bool bNull = b is null || b == "<NULL>";
            if (aNull && bNull) return 0;
            if (aNull) return 1;
            if (bNull) return -1;

            switch (type)
            {
                case DataType.INT:
                    if (int.TryParse(a, out var ai) && int.TryParse(b, out var bi))
                        return ai.CompareTo(bi);
                    break;
                case DataType.LONG:
                    if (long.TryParse(a, out var al) && long.TryParse(b, out var bl))
                        return al.CompareTo(bl);
                    break;
                case DataType.FLOAT:
                    if (double.TryParse(a, out var ad) && double.TryParse(b, out var bd))
                        return ad.CompareTo(bd);
                    break;
                case DataType.DATE:
                case DataType.DATETIME:
                    if (DateTime.TryParse(a, out var adt) && DateTime.TryParse(b, out var bdt))
                        return adt.CompareTo(bdt);
                    break;
            }
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
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