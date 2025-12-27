namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class DropDatabaseNode : AstNode
    {
        public string DatabaseName { get; set; }
        public DropDatabaseNode(string name)
        {
            DatabaseName = name;
        }
    }
}
