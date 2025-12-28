using System;
using System.Linq;
using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Storage.Indexing
{
    internal class BPlusTree<TKey, TValue> where TKey : IComparable<TKey>
    {
        private readonly int _degree;
        private readonly BPlusTreeDiskManager<TKey, TValue> _disk;
        private long _rootPageId;

        public BPlusTree(int degree, BPlusTreeDiskManager<TKey, TValue> disk)
        {
            _degree = degree;
            _disk = disk;

            _rootPageId = disk.LoadRootPageId();
            if (_rootPageId == -1)
            {
                long newPage = _disk.AllocatePage();
                var root = new BPlusNode<TKey, TValue>(true) { PageId = newPage };
                _disk.WriteNode(root);
                _rootPageId = newPage;
                _disk.SaveRootPageId(_rootPageId);
            }
        }

        // --- FIX: Returns true/false so we don't confuse "Offset 0" with "Not Found" ---
        public bool TryFind(TKey key, out TValue value)
        {
            var node = _disk.ReadNode(_rootPageId);

            while (!node.IsLeaf)
            {
                // Navigate down the tree
                int i = node.Keys.TakeWhile(k => key.CompareTo(k) >= 0).Count();
                i = Math.Min(i, node.Children.Count - 1);
                node = _disk.ReadNode(node.Children[i]);
            }

            int index = node.Keys.IndexOf(key);
            if (index != -1)
            {
                value = node.Values[index];
                return true; // Found!
            }

            value = default;
            return false; // Not Found
        }

        public void Insert(TKey key, TValue value)
        {
            // --- FIX: Strict check using TryFind ---
            if (TryFind(key, out _))
                throw new Exception($"Primary Key violation: '{key}' already exists.");

            var root = _disk.ReadNode(_rootPageId);

            if (root.Keys.Count == _degree * 2 - 1)
            {
                var newRoot = new BPlusNode<TKey, TValue>(false)
                {
                    PageId = _disk.AllocatePage()
                };

                newRoot.Children.Add(_rootPageId);
                SplitChild(newRoot, 0, root);

                _rootPageId = newRoot.PageId;
                _disk.SaveRootPageId(_rootPageId);
                _disk.WriteNode(newRoot);

                InsertNonFull(newRoot, key, value);
            }
            else
            {
                InsertNonFull(root, key, value);
            }
        }

        // Helper to expose simple lookup for IndexManager
        public TValue Search(TKey key)
        {
            if (TryFind(key, out var val)) return val;

            // If TValue is long (offset), return -1 to indicate missing
            if (typeof(TValue) == typeof(long)) return (TValue)(object)-1L;

            return default;
        }

        private void InsertNonFull(BPlusNode<TKey, TValue> node, TKey key, TValue value)
        {
            if (node.IsLeaf)
            {
                int pos = node.Keys.TakeWhile(k => k.CompareTo(key) < 0).Count();
                node.Keys.Insert(pos, key);
                node.Values.Insert(pos, value);
                _disk.WriteNode(node);
            }
            else
            {
                int i = node.Keys.TakeWhile(k => key.CompareTo(k) >= 0).Count();
                var child = _disk.ReadNode(node.Children[i]);

                if (child.Keys.Count == _degree * 2 - 1)
                {
                    SplitChild(node, i, child);
                    _disk.WriteNode(node);

                    if (key.CompareTo(node.Keys[i]) > 0)
                        i++;
                }

                InsertNonFull(_disk.ReadNode(node.Children[i]), key, value);
            }
        }

        private void SplitChild(BPlusNode<TKey, TValue> parent, int index, BPlusNode<TKey, TValue> fullChild)
        {
            int t = _degree;
            var newNode = new BPlusNode<TKey, TValue>(fullChild.IsLeaf)
            {
                PageId = _disk.AllocatePage()
            };

            newNode.Keys = fullChild.Keys.Skip(t).ToList();
            fullChild.Keys = fullChild.Keys.Take(t - 1).ToList();

            if (fullChild.IsLeaf)
            {
                newNode.Values = fullChild.Values.Skip(t).ToList();
                fullChild.Values = fullChild.Values.Take(t - 1).ToList();

                newNode.NextLeaf = fullChild.NextLeaf;
                fullChild.NextLeaf = newNode.PageId;
            }
            else
            {
                newNode.Children = fullChild.Children.Skip(t).ToList();
                fullChild.Children = fullChild.Children.Take(t).ToList();
            }

            parent.Keys.Insert(index, newNode.Keys.First());
            parent.Children.Insert(index + 1, newNode.PageId);

            _disk.WriteNode(fullChild);
            _disk.WriteNode(newNode);
            _disk.WriteNode(parent);
        }
    }
}