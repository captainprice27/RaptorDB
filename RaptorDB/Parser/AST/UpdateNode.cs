namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class UpdateNode : AstNode
    {
        public string TableName { get; }
        public string SetColumn { get; }
        public string SetValue { get; }
        public string WhereColumn { get; }
        public string WhereValue { get; }

        public UpdateNode(string table, string setCol, string setVal, string whereCol, string whereVal)
        {
            TableName = table;
            SetColumn = setCol;
            SetValue = setVal;
            WhereColumn = whereCol;
            WhereValue = whereVal;
        }
    }
}
