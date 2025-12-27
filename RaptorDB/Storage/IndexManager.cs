using System;
using System.IO;
using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.Models;
using RaptorDB.RaptorDB.Storage.Indexing;

namespace RaptorDB.RaptorDB.Storage
{
    internal class IndexManager
    {
        private readonly DBEngine _engine;
        private string BasePath => _engine.GetActiveDbPath();

        public IndexManager(DBEngine engine)
        {
            _engine = engine;
        }

        private string GetIndexPath(string table, DataType type) =>
            Path.Combine(BasePath,
                type == DataType.INT ? $"{table}.bpt" : $"{table}.bpt64");

        // --------------------------------------------------------------------
        // INSERT INDEX ENTRY
        // --------------------------------------------------------------------
        public void AddIndexEntry(string table, string keyStr, long offset, DataType keyType)
        {
            Directory.CreateDirectory(BasePath);

            if (keyType == DataType.INT)
            {
                if (!int.TryParse(keyStr, out var k))
                    throw new Exception($"PK expected INT but got '{keyStr}'.");

                var disk = new BPlusTreeDiskManager<int, long>(GetIndexPath(table, keyType));
                var tree = new BPlusTree<int, long>(3, disk);
                tree.Insert(k, offset);
            }
            else // LONG, DATE, DATETIME → stored as long/epoch
            {
                if (!long.TryParse(keyStr, out var k))
                    throw new Exception($"PK expected LONG-compatible value but got '{keyStr}'.");

                var disk = new BPlusTreeDiskManager<long, long>(GetIndexPath(table, keyType));
                var tree = new BPlusTree<long, long>(3, disk);
                tree.Insert(k, offset);
            }

            // keep .idx as fallback backup
            File.AppendAllText(Path.Combine(BasePath, $"{table}.idx"), $"{keyStr}={offset}\n");
        }

        // --------------------------------------------------------------------
        // LOOKUP
        // --------------------------------------------------------------------
        public long Lookup(string table, string keyStr, DataType type)
        {
            string path = GetIndexPath(table, type);
            if (!File.Exists(path)) return -1;

            if (type == DataType.INT)
            {
                if (!int.TryParse(keyStr, out var k)) return -1;
                var disk = new BPlusTreeDiskManager<int, long>(path);
                var tree = new BPlusTree<int, long>(3, disk);
                return tree.Find(k);
            }

            if (!long.TryParse(keyStr, out var l)) return -1;
            {
                var disk = new BPlusTreeDiskManager<long, long>(path);
                var tree = new BPlusTree<long, long>(3, disk);
                return tree.Find(l);
            }
        }

        // --------------------------------------------------------------------
        public void DropTableIndexes(string table)
        {
            foreach (var ext in new[] { ".bpt", ".bpt64", ".idx" })
            {
                string p = Path.Combine(BasePath, table + ext);
                if (File.Exists(p)) File.Delete(p);
            }
        }

        // --------------------------------------------------------------------
        public void DropDatabaseIndexes(string dbPath)
        {
            foreach (var f in Directory.GetFiles(dbPath))
            {
                if (f.EndsWith(".bpt") || f.EndsWith(".bpt64") || f.EndsWith(".idx"))
                    File.Delete(f);
            }
        }
    }
}
