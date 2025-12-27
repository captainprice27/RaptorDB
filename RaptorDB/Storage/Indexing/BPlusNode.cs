using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Storage.Indexing
{
    internal class BPlusNode<TKey, TValue> where TKey : System.IComparable<TKey>
    {
        public long PageId;
        public bool IsLeaf;

        public List<TKey> Keys = new();
        public List<TValue> Values = new();           // Replaces Offsets
        public List<long> Children = new();           // Child pointers for internal nodes

        public long NextLeaf = -1;                    // For leaf chaining

        public BPlusNode(bool leaf)
        {
            IsLeaf = leaf;
        }
    }
}
