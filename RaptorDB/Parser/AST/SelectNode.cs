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

        public SelectNode(string tableName, List<string> columns, List<Condition> conditions, List<JoinClause>? joins = null)
        {
            TableName = tableName;
            Columns = columns ?? new List<string>();
            Conditions = conditions ?? new List<Condition>();
            Joins = joins ?? new List<JoinClause>();
        }
    }
}