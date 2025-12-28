using System.Collections.Generic;

namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class UpdateNode : AstNode
    {
        public string TableName { get; }
        public string SetColumn { get; }
        public string SetValue { get; }
        public List<Condition> WhereConditions { get; }

        public UpdateNode(string table, string setCol, string setVal, List<Condition> whereConditions)
        {
            TableName = table;
            SetColumn = setCol;
            SetValue = setVal;
            WhereConditions = whereConditions ?? new List<Condition>();
        }
    }
}