namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class DropTableNode : AstNode
    {
        public string TableName { get; set; }
        public DropTableNode(string name)
        {
            TableName = name;
        }
    }
}
