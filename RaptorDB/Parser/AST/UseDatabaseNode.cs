namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class UseDatabaseNode : AstNode
    {
        public string DatabaseName { get; }
        public UseDatabaseNode(string db) => DatabaseName = db;
    }
}
