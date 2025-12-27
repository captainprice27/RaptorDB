namespace RaptorDB.RaptorDB.Parser.AST
{
    /// <summary>
    /// Represents: DELETE table WHERE column = value
    /// </summary>
    internal class DeleteNode : AstNode
    {
        public string TableName { get; }
        public string Column { get; }
        public string Value { get; }

        public DeleteNode(string tableName, string column, string value)
        {
            TableName = tableName;
            Column = column;
            Value = value;
        }
    }
}
