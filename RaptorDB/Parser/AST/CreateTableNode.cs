using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class CreateTableNode : AstNode
    {
        public string TableName { get; }
        public List<(string Name, string Type, bool IsPK)> Columns { get; }

        public CreateTableNode(string tableName, List<(string, string, bool)> columns)
        {
            TableName = tableName;
            Columns = columns;
        }
    }
}
