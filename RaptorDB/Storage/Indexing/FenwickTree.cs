using System;

namespace RaptorDB.RaptorDB.Storage.Indexing
{
    /// <summary>
    /// A Fenwick Tree (a.k.a. Binary Indexed Tree / BIT).
    ///
    /// Supports:
    ///   - Point update : <see cref="Update(int, long)"/>          → O(log n)
    ///   - Prefix sum   : <see cref="PrefixSum(int)"/>             → O(log n)
    ///   - Range sum    : <see cref="RangeSum(int, int)"/>         → O(log n)
    ///   - Bulk build   : <see cref="Build(long[])"/>              → O(n)
    ///
    /// The structure is 1-indexed internally; callers pass 1-based positions.
    ///
    /// In RaptorDB this is used (with coordinate-compression on the sorted PK
    /// list) to answer "how many primary keys lie in [low, high]?" in
    /// O(log n) — a standard textbook range-query accelerator. It is
    /// intentionally a small, hackable building block: production engines
    /// achieve the same goal with covering indexes, summary pages, or
    /// columnar precomputed aggregates. The Fenwick Tree is the cleanest
    /// O(log n) primitive for the family of "prefix/range count" and
    /// "prefix/range sum" queries, and is therefore a fitting educational
    /// addition to the engine.
    /// </summary>
    internal class FenwickTree
    {
        private readonly long[] _bit;     // 1-indexed; _bit[0] is unused
        public int Size { get; }

        public FenwickTree(int size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            _bit = new long[size + 1];
        }

        /// <summary>
        /// Bulk-construct a Fenwick Tree from a 0-indexed value array
        /// in O(n). Equivalent to calling <see cref="Update(int, long)"/>
        /// on each element but ~log n faster for large inputs.
        /// </summary>
        public static FenwickTree Build(long[] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            var ft = new FenwickTree(values.Length);

            // Copy values into 1-indexed slot first
            for (int i = 0; i < values.Length; i++)
                ft._bit[i + 1] = values[i];

            // Standard O(n) BIT construction:
            // each cell propagates its (already-aggregated) value to its parent.
            for (int i = 1; i <= ft.Size; i++)
            {
                int parent = i + (i & -i);
                if (parent <= ft.Size)
                    ft._bit[parent] += ft._bit[i];
            }
            return ft;
        }

        /// <summary>
        /// Add <paramref name="delta"/> to position <paramref name="index"/> (1-based).
        /// </summary>
        public void Update(int index, long delta)
        {
            if (index < 1 || index > Size)
                throw new ArgumentOutOfRangeException(nameof(index));

            for (int i = index; i <= Size; i += i & -i)
                _bit[i] += delta;
        }

        /// <summary>
        /// Sum of positions 1..<paramref name="index"/> (inclusive).
        /// Returns 0 if index &lt;= 0.
        /// </summary>
        public long PrefixSum(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index > Size) index = Size;

            long sum = 0;
            for (int i = index; i > 0; i -= i & -i)
                sum += _bit[i];
            return sum;
        }

        /// <summary>
        /// Sum of positions <paramref name="left"/>..<paramref name="right"/>
        /// inclusive (both 1-based). Returns 0 when the range is empty.
        /// </summary>
        public long RangeSum(int left, int right)
        {
            if (right < left) return 0;
            if (left < 1) left = 1;
            if (right > Size) right = Size;
            return PrefixSum(right) - PrefixSum(left - 1);
        }
    }
}
