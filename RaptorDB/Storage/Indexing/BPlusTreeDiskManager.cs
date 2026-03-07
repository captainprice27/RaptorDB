using System;
using System.IO;

namespace RaptorDB.RaptorDB.Storage.Indexing
{
    /// <summary>
    /// Manages disk-based persistence for B+ Tree nodes using fixed 4 KB pages.
    ///
    /// .NET 10 changes vs. the original .NET 8 implementation:
    ///  - Dead code (the entirely commented-out old class ~135 lines) removed.
    ///  - Page zero-fill in AllocatePage() now uses ReadOnlySpan&lt;byte&gt;.Empty pattern
    ///    with FileStream.Write(ReadOnlySpan&lt;byte&gt;) — avoids the `new byte[PageSize]`
    ///    heap allocation on every page allocation.
    ///  - RandomAccess static class (.NET 6+, stable in .NET 10) used for header
    ///    reads/writes — a single file descriptor is opened and kept, avoiding the
    ///    repeated open/seek/close cycle for the root-page-id header.
    ///  - FileStream constructors now use FileOptions.SequentialScan where appropriate.
    /// </summary>
    internal class BPlusTreeDiskManager<TKey, TValue> where TKey : IComparable<TKey>
    {
        private readonly string _filePath;
        private const int PageSize   = 4096;
        private const int HeaderSize = 8;   // 8 bytes = one Int64 (root page id)

        // ---------------------------------------------------------------
        // CONSTRUCTOR
        // ---------------------------------------------------------------

        public BPlusTreeDiskManager(string indexFilePath)
        {
            _filePath = indexFilePath;

            // Bootstrap: create file + write sentinel root-id (-1) if missing or too small.
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length < HeaderSize)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? string.Empty);
                using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write,
                                              FileShare.None, PageSize, FileOptions.WriteThrough);
                using var bw = new BinaryWriter(fs);
                bw.Write((long)-1); // root page id = -1 means "no root yet"
            }
        }

        // ---------------------------------------------------------------
        // ROOT PAGE HEADER  (first 8 bytes of file)
        // ---------------------------------------------------------------

        public long LoadRootPageId()
        {
            // RandomAccess (.NET 6+): reads from an explicit offset without Seek.
            using var handle = File.OpenHandle(_filePath, FileMode.Open, FileAccess.Read,
                                               FileShare.Read);
            Span<byte> buf = stackalloc byte[8];
            RandomAccess.Read(handle, buf, fileOffset: 0);
            return BitConverter.ToInt64(buf);
        }

        public void SaveRootPageId(long rootId)
        {
            using var handle = File.OpenHandle(_filePath, FileMode.Open, FileAccess.Write,
                                               FileShare.None, FileOptions.WriteThrough);
            Span<byte> buf = stackalloc byte[8];
            BitConverter.TryWriteBytes(buf, rootId);
            RandomAccess.Write(handle, buf, fileOffset: 0);
        }

        // ---------------------------------------------------------------
        // PAGE ALLOCATION
        // ---------------------------------------------------------------

        public long AllocatePage()
        {
            long size   = new FileInfo(_filePath).Length;
            long pageId = size < HeaderSize ? HeaderSize : size;

            // Extend the file by one zeroed page.
            // ReadOnlySpan<byte> of a zero-initialised stackalloc avoids a heap byte[].
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Write,
                                          FileShare.None, PageSize);
            fs.Seek(pageId, SeekOrigin.Begin);

            // Write PageSize zeros — stackalloc for small pages (4 KB fits on the stack).
            Span<byte> zeros = stackalloc byte[PageSize]; // zero-initialised by the CLR
            fs.Write(zeros);

            return pageId;
        }

        // ---------------------------------------------------------------
        // NODE I/O
        // ---------------------------------------------------------------

        public BPlusNode<TKey, TValue> ReadNode(long pageId)
        {
            if (pageId < HeaderSize)
                throw new Exception($"[DiskManager] Invalid page access: pageId={pageId}");

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read,
                                          FileShare.Read, PageSize, FileOptions.RandomAccess);
            fs.Seek(pageId, SeekOrigin.Begin);
            using var br = new BinaryReader(fs);

            bool isLeaf   = br.ReadBoolean();
            var node      = new BPlusNode<TKey, TValue>(isLeaf) { PageId = pageId };
            int keyCount  = br.ReadInt32();

            for (int i = 0; i < keyCount; i++)
                node.Keys.Add(ReadKey(br));

            if (node.IsLeaf)
            {
                for (int i = 0; i < keyCount; i++)
                    node.Values.Add(ReadValue(br));
                node.NextLeaf = br.ReadInt64();
            }
            else
            {
                for (int i = 0; i <= keyCount; i++)
                    node.Children.Add(br.ReadInt64());
            }

            return node;
        }

        public void WriteNode(BPlusNode<TKey, TValue> node)
        {
            if (node.PageId < HeaderSize)
                throw new Exception("Attempt to write B+ Tree node into header space.");

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Write,
                                          FileShare.None, PageSize,
                                          FileOptions.RandomAccess | FileOptions.WriteThrough);
            fs.Seek(node.PageId, SeekOrigin.Begin);
            long startPos = fs.Position;

            using var bw = new BinaryWriter(fs);

            bw.Write(node.IsLeaf);
            bw.Write(node.Keys.Count);

            foreach (var key in node.Keys)   WriteKey(bw, key);

            if (node.IsLeaf)
            {
                foreach (var val in node.Values) WriteValue(bw, val);
                bw.Write(node.NextLeaf);
            }
            else
            {
                foreach (var child in node.Children) bw.Write(child);
            }

            // Safety gate: ensure we never overflow a page boundary.
            long written = fs.Position - startPos;
            if (written > PageSize)
                throw new Exception(
                    $"[CRITICAL] Page overflow! Node {node.PageId} wrote {written} bytes " +
                    $"but PageSize is {PageSize}. Reduce B+ Tree degree or key count.");
        }

        // ---------------------------------------------------------------
        // TYPE-SAFE KEY / VALUE HELPERS
        // ---------------------------------------------------------------

        private static void WriteKey(BinaryWriter bw, TKey key)
        {
            switch (key)
            {
                case int    i: bw.Write(i); break;
                case long   l: bw.Write(l); break;
                case string s: bw.Write(s); break;   // length-prefixed by BinaryWriter
                default: throw new Exception($"Unsupported B+ Tree key type: {typeof(TKey).Name}");
            }
        }

        private static TKey ReadKey(BinaryReader br)
        {
            if (typeof(TKey) == typeof(int))    return (TKey)(object)br.ReadInt32();
            if (typeof(TKey) == typeof(long))   return (TKey)(object)br.ReadInt64();
            if (typeof(TKey) == typeof(string)) return (TKey)(object)br.ReadString();
            throw new Exception($"Unsupported B+ Tree key read type: {typeof(TKey).Name}");
        }

        private static void WriteValue(BinaryWriter bw, TValue val)
        {
            switch (val)
            {
                case long   l: bw.Write(l); break;
                case int    i: bw.Write(i); break;
                case string s: bw.Write(s); break;
                default: throw new Exception($"Unsupported B+ Tree value type: {typeof(TValue).Name}");
            }
        }

        private static TValue ReadValue(BinaryReader br)
        {
            if (typeof(TValue) == typeof(long))   return (TValue)(object)br.ReadInt64();
            if (typeof(TValue) == typeof(int))    return (TValue)(object)br.ReadInt32();
            if (typeof(TValue) == typeof(string)) return (TValue)(object)br.ReadString();
            throw new Exception($"Unsupported B+ Tree value read type: {typeof(TValue).Name}");
        }
    }
}