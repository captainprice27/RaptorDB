namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class CreateDatabaseNode : AstNode
    {
        public string Name { get; }
        public CreateDatabaseNode(string dbName) => Name = dbName;
    }
}
