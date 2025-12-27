using System;
using System.IO;

namespace RaptorDB.RaptorDB.Storage.Indexing
{
    internal class BPlusTreeDiskManager<TKey, TValue> where TKey : IComparable<TKey>
    {
        private readonly string _filePath;
        private const int PageSize = 4096;
        private const int HeaderSize = 8;

        public BPlusTreeDiskManager(string indexFilePath)
        {
            _filePath = indexFilePath;

            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length < HeaderSize)
            {
                using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);
                bw.Write((long)-1);
            }
        }

        public long LoadRootPageId()
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            return br.ReadInt64();
        }

        public void SaveRootPageId(long rootId)
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Write);
            using var bw = new BinaryWriter(fs);
            bw.Write(rootId);
        }

        public long AllocatePage()
        {
            long size = new FileInfo(_filePath).Length;
            long pageId = size < HeaderSize ? HeaderSize : size;

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Write);
            fs.Seek(pageId, SeekOrigin.Begin);
            fs.Write(new byte[PageSize], 0, PageSize);
            return pageId;
        }

        public BPlusNode<TKey, TValue> ReadNode(long pageId)
        {
            if (pageId < HeaderSize)
                throw new Exception($"Invalid page access {pageId}");

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            fs.Seek(pageId, SeekOrigin.Begin);
            using var br = new BinaryReader(fs);

            var node = new BPlusNode<TKey, TValue>(br.ReadBoolean()) { PageId = pageId };
            int keyCount = br.ReadInt32();

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
                throw new Exception("Attempt to write node into header space.");

            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Write);
            fs.Seek(node.PageId, SeekOrigin.Begin);
            using var bw = new BinaryWriter(fs);

            bw.Write(node.IsLeaf);
            bw.Write(node.Keys.Count);

            foreach (var key in node.Keys) WriteKey(bw, key);

            if (node.IsLeaf)
            {
                foreach (var val in node.Values) WriteValue(bw, val);
                bw.Write(node.NextLeaf);
            }
            else
            {
                foreach (var c in node.Children) bw.Write(c);
            }
        }

        private void WriteKey(BinaryWriter bw, TKey key)
        {
            if (typeof(TKey) == typeof(int)) bw.Write((int)(object)key);
            else if (typeof(TKey) == typeof(long)) bw.Write((long)(object)key);
            else if (typeof(TKey) == typeof(string)) bw.Write((string)(object)key);
            else throw new Exception("Unsupported key type for disk serialization.");
        }

        private TKey ReadKey(BinaryReader br)
        {
            if (typeof(TKey) == typeof(int)) return (TKey)(object)br.ReadInt32();
            if (typeof(TKey) == typeof(long)) return (TKey)(object)br.ReadInt64();
            if (typeof(TKey) == typeof(string)) return (TKey)(object)br.ReadString();
            throw new Exception("Unsupported key type for disk load.");
        }

        private void WriteValue(BinaryWriter bw, TValue val)
        {
            if (typeof(TValue) == typeof(long)) bw.Write((long)(object)val);
            else if (typeof(TValue) == typeof(int)) bw.Write((int)(object)val);
            else bw.Write(val.ToString());
        }

        private TValue ReadValue(BinaryReader br)
        {
            if (typeof(TValue) == typeof(long)) return (TValue)(object)br.ReadInt64();
            if (typeof(TValue) == typeof(int)) return (TValue)(object)br.ReadInt32();
            throw new Exception("Unsupported value type for disk load.");
        }
    }
}
