//using System;
//using System.Linq;
//using System.Collections.Generic;

//namespace RaptorDB.RaptorDB.Storage.Indexing
//{
//    /// <summary>
//    /// Core in-memory B+ Tree logic.
//    /// Supports:
//    /// - Insert(key, offset) with unique key enforcement
//    /// - Find(key) lookup
//    /// - Node splitting when full
//    /// 
//    /// NOTE:
//    /// Persistence is handled by BPlusTreeDiskManager.
//    /// This class focuses only on algorithm / structure.
//    /// </summary>
//    internal class BPlusTree
//    {
//        private readonly int _degree; // minimum degree (t)
//        private readonly BPlusTreeDiskManager _disk;
//        private long _rootPageId;

//        public BPlusTree(int degree, BPlusTreeDiskManager disk)
//        {
//            _degree = degree;
//            _disk = disk;

//            // Load existing root or create initial root
//            _rootPageId = disk.LoadRootPageId();
//            if (_rootPageId == -1)
//            {
//                long newPage = _disk.AllocatePage();
//                var root = new BPlusNode(true) { PageId = newPage };
//                _disk.WriteNode(root);
//                _rootPageId = newPage;
//                _disk.SaveRootPageId(_rootPageId);
//            }

//        }

//        // ----------------------------------------------------
//        // SEARCH: returns offset or -1 if not found
//        // ----------------------------------------------------
//        public long Find(int key)
//        {
//            var node = _disk.ReadNode(_rootPageId);

//            while (!node.IsLeaf)
//            {
//                int i = node.Keys.TakeWhile(k => key >= k).Count();
//                i = Math.Min(i, node.Children.Count - 1);
//                node = _disk.ReadNode(node.Children[i]);
//            }

//            int index = node.Keys.IndexOf(key);
//            return index == -1 ? -1 : node.Offsets[index];
//        }

//        // ----------------------------------------------------
//        // INSERT with strict uniqueness
//        // ----------------------------------------------------
//        public void Insert(int key, long offset)
//        {
//            // Check if key already exists
//            if (Find(key) != -1)
//                throw new Exception($"Primary Key violation: '{key}' already exists!");

//            var root = _disk.ReadNode(_rootPageId);

//            if (root.Keys.Count == _degree * 2 - 1) // Node full → split root
//            {
//                var newRoot = new BPlusNode(false)
//                {
//                    PageId = _disk.AllocatePage()
//                };

//                newRoot.Children.Add(_rootPageId);
//                SplitChild(newRoot, 0, root);

//                _rootPageId = newRoot.PageId;
//                _disk.SaveRootPageId(_rootPageId);
//                _disk.WriteNode(newRoot);

//                InsertNonFull(newRoot, key, offset);
//            }
//            else
//            {
//                InsertNonFull(root, key, offset);
//            }
//        }

//        // ----------------------------------------------------
//        // Insert when node is confirmed to have space
//        // ----------------------------------------------------
//        private void InsertNonFull(BPlusNode node, int key, long offset)
//        {
//            if (node.IsLeaf)
//            {
//                int pos = node.Keys.TakeWhile(k => k < key).Count();
//                node.Keys.Insert(pos, key);
//                node.Offsets.Insert(pos, offset);
//                _disk.WriteNode(node);
//            }
//            else
//            {
//                int i = node.Keys.TakeWhile(k => key >= k).Count();
//                var child = _disk.ReadNode(node.Children[i]);

//                if (child.Keys.Count == _degree * 2 - 1)
//                {
//                    SplitChild(node, i, child);
//                    _disk.WriteNode(node);

//                    if (key > node.Keys[i])
//                        i++;
//                }

//                InsertNonFull(_disk.ReadNode(node.Children[i]), key, offset);
//            }
//        }

//        // ----------------------------------------------------
//        // Split a full child node into two
//        // ----------------------------------------------------
//        private void SplitChild(BPlusNode parent, int index, BPlusNode fullChild)
//        {
//            int t = _degree;
//            var newNode = new BPlusNode(fullChild.IsLeaf)
//            {
//                PageId = _disk.AllocatePage()
//            };

//            // Move half keys to new node
//            newNode.Keys = fullChild.Keys.Skip(t).ToList();
//            fullChild.Keys = fullChild.Keys.Take(t - 1).ToList();

//            if (fullChild.IsLeaf)
//            {
//                newNode.Offsets = fullChild.Offsets.Skip(t).ToList();
//                fullChild.Offsets = fullChild.Offsets.Take(t - 1).ToList();

//                // Maintain leaf linked-list
//                newNode.NextLeaf = fullChild.NextLeaf;
//                fullChild.NextLeaf = newNode.PageId;
//            }
//            else
//            {
//                newNode.Children = fullChild.Children.Skip(t).ToList();
//                fullChild.Children = fullChild.Children.Take(t).ToList();
//            }

//            // Promote median key to parent
//            parent.Keys.Insert(index, newNode.Keys.First());
//            parent.Children.Insert(index + 1, newNode.PageId);

//            _disk.WriteNode(fullChild);
//            _disk.WriteNode(newNode);
//            _disk.WriteNode(parent);
//        }
//    }
//}
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

        public TValue Find(TKey key)
        {
            var node = _disk.ReadNode(_rootPageId);

            while (!node.IsLeaf)
            {
                int i = node.Keys.TakeWhile(k => key.CompareTo(k) >= 0).Count();
                i = Math.Min(i, node.Children.Count - 1);
                node = _disk.ReadNode(node.Children[i]);
            }

            int index = node.Keys.IndexOf(key);
            return index == -1 ? default : node.Values[index];
        }

        public void Insert(TKey key, TValue value)
        {
            if (!EqualityComparer<TValue>.Default.Equals(Find(key), default))
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
