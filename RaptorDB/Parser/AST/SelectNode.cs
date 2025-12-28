using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class SelectNode : AstNode
    {
        public string TableName { get; }
        public List<string> Columns { get; }
        public List<Condition> Conditions { get; } // Supports multiple filters

        public SelectNode(string tableName, List<string> columns, List<Condition> conditions)
        {
            TableName = tableName;
            Columns = columns ?? new List<string>();
            Conditions = conditions ?? new List<Condition>();
        }
    }
}