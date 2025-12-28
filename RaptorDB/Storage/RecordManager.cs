//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using RaptorDB.RaptorDB.Core;

//namespace RaptorDB.RaptorDB.Storage
//{
//    internal class RecordManager
//    {
//        private readonly DBEngine _engine;
//        private string BasePath => _engine.GetActiveDbPath();

//        // Normalize identifiers to match parser/engine rules
//        private string Normalize(string name) =>
//            name?.Trim().TrimEnd(';').ToLower();

//        public RecordManager(DBEngine engine)
//        {
//            _engine = engine;
//        }

//        // ------------------------------------------------------------
//        // INSERT ROW
//        // ------------------------------------------------------------
//        public long InsertRecord(string table, Dictionary<string, string> row)
//        {
//            table = Normalize(table);
//            Directory.CreateDirectory(BasePath);

//            string dataPath = Path.Combine(BasePath, $"{table}.data");
//            string content = string.Join("|", row.Select(kv => $"{kv.Key}={kv.Value}"));

//            using var fs = new FileStream(dataPath, FileMode.Append, FileAccess.Write);
//            long offset = fs.Position;

//            using var writer = new StreamWriter(fs);
//            writer.WriteLine(content);

//            return offset;
//        }

//        // ------------------------------------------------------------
//        // READ ALL RECORDS
//        // ------------------------------------------------------------
//        public List<Dictionary<string, string>> ReadAll(string table)
//        {
//            table = Normalize(table);
//            string dataPath = Path.Combine(BasePath, $"{table}.data");

//            var result = new List<Dictionary<string, string>>();
//            if (!File.Exists(dataPath)) return result;

//            foreach (var line in File.ReadAllLines(dataPath))
//            {
//                var row = new Dictionary<string, string>();
//                foreach (var part in line.Split('|'))
//                {
//                    var kv = part.Split('=');
//                    if (kv.Length == 2)
//                        row[kv[0].Trim()] = kv[1].Trim();
//                }
//                result.Add(row);
//            }

//            return result;
//        }

//        // ------------------------------------------------------------
//        // REWRITE TABLE (after DELETE/UPDATE)
//        // ------------------------------------------------------------
//        public void RewriteTable(string table, List<Dictionary<string, string>> rows)
//        {
//            table = Normalize(table);
//            string path = Path.Combine(BasePath, $"{table}.data");

//            using var writer = new StreamWriter(path, false);
//            foreach (var row in rows)
//                writer.WriteLine(string.Join("|", row.Select(kv => $"{kv.Key}={kv.Value}")));
//        }

//        // ------------------------------------------------------------
//        // DROP TABLE (DATA ONLY - schema removed elsewhere)
//        // ------------------------------------------------------------
//        public void DeleteTableData(string table)
//        {
//            table = Normalize(table);
//            string data = Path.Combine(BasePath, $"{table}.data");
//            if (File.Exists(data))
//                File.Delete(data);
//        }
//    }
//}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.Utils;

namespace RaptorDB.RaptorDB.Storage
{
    internal class RecordManager
    {
        private readonly DBEngine _engine;
        private string BasePath => _engine.GetActiveDbPath();

        private string Normalize(string name) =>
            name?.Trim().TrimEnd(';').ToLower();

        public RecordManager(DBEngine engine)
        {
            _engine = engine;
        }

        // ------------------------------------------------------------
        // INSERT ROW (Safe Mode)
        // ------------------------------------------------------------
        public long InsertRecord(string table, Dictionary<string, string> row)
        {
            table = Normalize(table);
            FileHelper.EnsureDirectory(BasePath); // Helper usage for safety

            string dataPath = Path.Combine(BasePath, $"{table}.data");

            // Use the FIXED serializer (handles | and = inside data)
            string content = ByteSerializer.SerializeRow(row);

            using var fs = new FileStream(dataPath, FileMode.Append, FileAccess.Write);
            long offset = fs.Position;

            using var writer = new StreamWriter(fs);
            writer.WriteLine(content);

            return offset;
        }

        // ------------------------------------------------------------
        // READ ALL RECORDS (Safe Mode)
        // ------------------------------------------------------------
        public List<Dictionary<string, string>> ReadAll(string table)
        {
            table = Normalize(table);
            string dataPath = Path.Combine(BasePath, $"{table}.data");

            var result = new List<Dictionary<string, string>>();
            if (!File.Exists(dataPath)) return result;

            foreach (var line in File.ReadAllLines(dataPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Use the FIXED deserializer
                result.Add(ByteSerializer.DeserializeRow(line));
            }

            return result;
        }

        // ------------------------------------------------------------
        // FUTURE-PROOFING: READ SINGLE RECORD (By Offset)
        // This doesn't break existing code, but is ready for the future.
        // ------------------------------------------------------------
        public Dictionary<string, string> ReadRecord(string table, long offset)
        {
            table = Normalize(table);
            string dataPath = Path.Combine(BasePath, $"{table}.data");

            using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
            fs.Seek(offset, SeekOrigin.Begin);

            using var reader = new StreamReader(fs);
            string line = reader.ReadLine();

            return ByteSerializer.DeserializeRow(line);
        }

        // ------------------------------------------------------------
        // REWRITE TABLE
        // ------------------------------------------------------------
        public void RewriteTable(string table, List<Dictionary<string, string>> rows)
        {
            table = Normalize(table);
            string path = Path.Combine(BasePath, $"{table}.data");

            using var writer = new StreamWriter(path, false);
            foreach (var row in rows)
            {
                // Write safely encoded lines
                writer.WriteLine(ByteSerializer.SerializeRow(row));
            }
        }

        // ------------------------------------------------------------
        // DROP TABLE
        // ------------------------------------------------------------
        public void DeleteTableData(string table)
        {
            table = Normalize(table);
            string data = Path.Combine(BasePath, $"{table}.data");
            FileHelper.SafeDelete(data);
        }
    }
}