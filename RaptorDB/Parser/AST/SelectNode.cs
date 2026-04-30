using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class SelectNode : AstNode
    {
        public string TableName { get; }
        public List<string> Columns { get; }
        public List<Condition> Conditions { get; } // Supports multiple filters

        // v2.0 — Optional JOIN clause(s), stored as a list to support
        // chained joins:
        //        SELECT ... FROM A JOIN B ON ... JOIN C ON ... [JOIN D ON ...]
        public List<JoinClause> Joins { get; }

        // v2.0 — Optional ORDER BY clause. Each item is a (column, descending?)
        // pair; absent / empty means "no sort". Multi-key sort follows the
        // declared order (first key is primary, subsequent keys are tie-breakers).
        public List<OrderByItem> OrderBy { get; }

        public SelectNode(
            string tableName,
            List<string> columns,
            List<Condition> conditions,
            List<JoinClause>? joins = null,
            List<OrderByItem>? orderBy = null)
        {
            TableName = tableName;
            Columns = columns ?? new List<string>();
            Conditions = conditions ?? new List<Condition>();
            Joins = joins ?? new List<JoinClause>();
            OrderBy = orderBy ?? new List<OrderByItem>();
        }
    }

    /// <summary>
    /// A single ORDER BY key — column name (may be qualified as "table.col")
    /// and a direction flag. Default direction is ASC (Descending = false).
    /// </summary>
    internal class OrderByItem
    {
        public string Column { get; }
        public bool Descending { get; }

        public OrderByItem(string column, bool descending)
        {
            Column = column;
            Descending = descending;
        }
    }
}