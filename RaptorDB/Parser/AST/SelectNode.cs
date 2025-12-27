namespace RaptorDB.RaptorDB.Parser.AST
{
    /// <summary>
    /// Represents: GET table WHERE column = value
    /// </summary>
    internal class SelectNode : AstNode
    {
        public string TableName { get; }
        public string Column { get; }
        public string Value { get; }

        public SelectNode(string tableName, string column, string value)
        {
            TableName = tableName;
            Column = column;
            Value = value;
        }
    }
}
