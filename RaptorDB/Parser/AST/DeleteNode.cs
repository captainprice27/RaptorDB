using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class DeleteNode : AstNode
    {
        public string TableName { get; }
        public List<Condition> Conditions { get; }

        public DeleteNode(string tableName, List<Condition> conditions)
        {
            TableName = tableName;
            Conditions = conditions ?? new List<Condition>();
        }
    }
}