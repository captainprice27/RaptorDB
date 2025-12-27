using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class InsertNode : AstNode
    {
        public string TableName { get; }
        public List<string>? Columns { get; }
        public List<string> Values { get; }

        // For INSERT INTO table (colA,colB) VALUES (x,y)
        public InsertNode(string table, List<string>? columns, List<string> values)
        {
            TableName = table;
            Columns = columns;
            Values = values;
        }
    }
}
