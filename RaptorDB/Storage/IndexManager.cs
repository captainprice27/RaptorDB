using System;
using System.Collections.Generic;
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

        // ----------------------------------------------------------------
        // FENWICK-TREE RANGE-COUNT CACHE
        // ----------------------------------------------------------------
        // Keyed by absolute index-file path. Each entry holds:
        //   - sortedKeys : the PK values in ascending order (long-typed
        //                  for both INT and LONG indexes; INT keys widen
        //                  losslessly into long).
        //   - bit        : a Fenwick Tree of all-1s over those positions,
        //                  so RangeSum(lo..hi) == count of PKs in [lo..hi].
        // The cache is invalidated on AddIndexEntry / DropTableIndexes /
        // DropDatabaseIndexes so range-count answers stay correct.
        private static readonly Dictionary<string, (long[] sortedKeys, FenwickTree bit)> _rangeCache = new();

        public IndexManager(DBEngine engine)
        {
            _engine = engine;
        }

        private string GetIndexPath(string table, DataType type) =>
            Path.Combine(BasePath, type == DataType.INT ? $"{table}.bpt" : $"{table}.bpt64");

        // --------------------------------------------------------------------
        // INSERT INDEX ENTRY
        // --------------------------------------------------------------------
        public void AddIndexEntry(string table, string keyStr, long offset, DataType keyType)
        {
            // Removed directory creation here (RecordManager handles it)

            string path = GetIndexPath(table, keyType);
            // Any mutation invalidates the Fenwick range-count cache for this index.
            _rangeCache.Remove(path);

            if (keyType == DataType.INT)
            {
                if (!int.TryParse(keyStr, out var k))
                    throw new Exception($"PK expected INT but got '{keyStr}'.");

                var disk = new BPlusTreeDiskManager<int, long>(path);
                var tree = new BPlusTree<int, long>(3, disk);
                tree.Insert(k, offset);
            }
            else
            {
                if (!long.TryParse(keyStr, out var k))
                    throw new Exception($"PK expected LONG-compatible value but got '{keyStr}'.");

                var disk = new BPlusTreeDiskManager<long, long>(path);
                var tree = new BPlusTree<long, long>(3, disk);
                tree.Insert(k, offset);
            }
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

                // FIX: Use Search which returns -1 on failure
                return tree.Search(k);
            }
            else
            {
                if (!long.TryParse(keyStr, out var l)) return -1;
                var disk = new BPlusTreeDiskManager<long, long>(path);
                var tree = new BPlusTree<long, long>(3, disk);

                // FIX: Use Search which returns -1 on failure
                return tree.Search(l);
            }
        }

        // --------------------------------------------------------------------
        public void DropTableIndexes(string table)
        {
            foreach (var ext in new[] { ".bpt", ".bpt64" }) // Removed .idx
            {
                string p = Path.Combine(BasePath, table + ext);
                if (File.Exists(p)) File.Delete(p);
                _rangeCache.Remove(p);
            }
        }

        public void DropDatabaseIndexes(string dbPath)
        {
            if (!Directory.Exists(dbPath)) return;
            foreach (var f in Directory.GetFiles(dbPath))
            {
                if (f.EndsWith(".bpt") || f.EndsWith(".bpt64"))
                {
                    File.Delete(f);
                    _rangeCache.Remove(f);
                }
            }
        }

        // --------------------------------------------------------------------
        // FENWICK-TREE RANGE-COUNT ACCELERATOR
        // --------------------------------------------------------------------
        // Returns the number of primary-key values in the inclusive range
        // [lowKey, highKey] for an INT or LONG/DATE/DATETIME indexed table.
        //
        //   - Build cost (first call after a mutation) : O(n)
        //       (one B+ Tree leaf-chain scan + Fenwick bulk Build)
        //   - Subsequent queries                       : O(log n)
        //   - Memory                                   : O(n) longs per cached table
        //
        // Returns -1 when the index file does not exist OR when the supplied
        // bounds cannot be parsed against the index's key type. Callers can
        // therefore treat -1 as "not applicable, fall back to full scan".
        public long CountKeysInRange(string table, DataType type, string lowKey, string highKey)
        {
            string path = GetIndexPath(table, type);
            if (!File.Exists(path)) return -1;

            long lo, hi;
            try
            {
                if (type == DataType.INT)
                {
                    if (!int.TryParse(lowKey, out var li) || !int.TryParse(highKey, out var hi32))
                        return -1;
                    lo = li; hi = hi32;
                }
                else
                {
                    if (!long.TryParse(lowKey, out lo) || !long.TryParse(highKey, out hi))
                        return -1;
                }
            }
            catch { return -1; }

            if (hi < lo) return 0;

            var (sortedKeys, bit) = GetOrBuildFenwick(path, type);
            if (sortedKeys.Length == 0) return 0;

            // Coordinate-compressed lookups: lower_bound(lo) .. upper_bound(hi)-1
            int left = LowerBound(sortedKeys, lo);          // first index >= lo
            int right = UpperBound(sortedKeys, hi) - 1;     // last index  <= hi
            if (left > right) return 0;

            // Fenwick uses 1-based positions.
            return bit.RangeSum(left + 1, right + 1);
        }

        private (long[] sortedKeys, FenwickTree bit) GetOrBuildFenwick(string indexPath, DataType type)
        {
            if (_rangeCache.TryGetValue(indexPath, out var cached))
                return cached;

            // Drain the B+ Tree leaves in ascending order, widen INT → long.
            List<long> keys;
            if (type == DataType.INT)
            {
                var disk = new BPlusTreeDiskManager<int, long>(indexPath);
                var tree = new BPlusTree<int, long>(3, disk);
                keys = new List<long>();
                foreach (var k in tree.EnumerateKeysInOrder())
                    keys.Add(k);
            }
            else
            {
                var disk = new BPlusTreeDiskManager<long, long>(indexPath);
                var tree = new BPlusTree<long, long>(3, disk);
                keys = new List<long>();
                foreach (var k in tree.EnumerateKeysInOrder())
                    keys.Add(k);
            }

            var sortedKeys = keys.ToArray();
            // Each existing PK contributes a count of 1; RangeSum then yields
            // the cardinality of the queried PK range in O(log n).
            var weights = new long[sortedKeys.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = 1;

            var bit = FenwickTree.Build(weights);
            cached = (sortedKeys, bit);
            _rangeCache[indexPath] = cached;
            return cached;
        }

        private static int LowerBound(long[] arr, long target)
        {
            int lo = 0, hi = arr.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >>> 1;
                if (arr[mid] < target) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        private static int UpperBound(long[] arr, long target)
        {
            int lo = 0, hi = arr.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) >>> 1;
                if (arr[mid] <= target) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}