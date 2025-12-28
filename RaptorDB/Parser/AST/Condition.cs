namespace RaptorDB.RaptorDB.Parser.AST
{
    internal class Condition
    {
        public string Column { get; }
        public string Operator { get; }
        public string Value { get; }

        public Condition(string col, string op, string val)
        {
            Column = col;
            Operator = op;
            Value = val;
        }
    }
}