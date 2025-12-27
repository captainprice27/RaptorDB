using System;
using System.Collections.Generic;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.Parser
{
    internal class Parser
    {
        private readonly List<string> _tokens;
        private int _pos = 0;

        public Parser(List<string> tokens)
        {
            _tokens = tokens;
        }

        private string Peek() => _pos < _tokens.Count ? _tokens[_pos] : null;
        private string Pop() => _pos < _tokens.Count ? _tokens[_pos++] : null;
        private bool Match(string keyword)
        {
            if (Peek()?.Equals(keyword, StringComparison.OrdinalIgnoreCase) == true)
            {
                Pop();
                return true;
            }
            return false;
        }

        private void Expect(string token)
        {
            if (!Match(token))
                throw new Exception($"Syntax error: expected '{token}', got '{Peek()}'");
        }

        // ================================================================
        // ENTRY POINT
        // ================================================================
        public AstNode Parse()
        {
            // Strip trailing semicolons BEFORE parsing
            while (Peek() == ";") Pop();

            if (Match("create"))
            {
                if (Match("database")) return new CreateDatabaseNode(StripSemicolon(Pop()));
                if (Match("table")) return ParseCreateTable();
            }

            if (Match("drop"))
            {
                if (Match("database")) return new DropDatabaseNode(StripSemicolon(Pop()));
                if (Match("table")) return new DropTableNode(StripSemicolon(Pop()));
            }

            if (Match("use"))
                return new UseDatabaseNode(StripSemicolon(Pop()));

            if (Match("insert"))
                return ParseInsert();

            if (Match("select"))
                return ParseSelect();

            if (Match("delete"))
                return ParseDelete();

            if (Match("update"))
                return ParseUpdate();

            throw new Exception($"Syntax error near '{Peek()}'");
        }

        // ================================================================
        // CREATE TABLE
        // ================================================================
        private CreateTableNode ParseCreateTable()
        {
            string table = StripSemicolon(Pop());
            Expect("(");

            var cols = new List<(string Name, string Type, bool IsPK)>();

            while (true)
            {
                string col = Pop();
                string type = Pop();
                bool pk = Match("pk");
                cols.Add((col, type.ToUpper(), pk));

                if (Match(")")) break;
                Expect(",");
            }
            return new CreateTableNode(table, cols);
        }

        // ================================================================
        // INSERT
        // ================================================================
        private InsertNode ParseInsert()
        {
            Expect("into");
            string table = StripSemicolon(Pop());
            Expect("(");

            var cols = new List<string>();
            while (!Match(")"))
            {
                cols.Add(Pop());
                Match(",");
            }

            Expect("values");
            Expect("(");

            var vals = new List<string>();
            while (!Match(")"))
            {
                vals.Add(Pop());
                Match(",");
            }

            return new InsertNode(table, cols, vals);
        }

        // ================================================================
        // SELECT  (FIXED!)
        // ================================================================
        private SelectNode ParseSelect()
        {
            string column = "*";
            if (!Match("*")) column = Pop();     // SELECT col FROM...

            Expect("from");                      // <== THIS WAS FAILING
            string table = StripSemicolon(Pop());

            if (Match("where"))
            {
                string col = Pop();
                Expect("=");
                string val = StripSemicolon(Pop());
                return new SelectNode(table, col, val);
            }

            return new SelectNode(table, null, null);
        }

        // ================================================================
        // DELETE
        // ================================================================
        private DeleteNode ParseDelete()
        {
            Expect("from");
            string table = StripSemicolon(Pop());
            Expect("where");
            string col = Pop();
            Expect("=");
            string val = StripSemicolon(Pop());

            return new DeleteNode(table, col, val);
        }

        // ================================================================
        // UPDATE
        // ================================================================
        private UpdateNode ParseUpdate()
        {
            string table = StripSemicolon(Pop());
            Expect("set");
            string setCol = Pop();
            Expect("=");
            string setVal = Pop();
            Expect("where");
            string whereCol = Pop();
            Expect("=");
            string whereVal = StripSemicolon(Pop());

            return new UpdateNode(table, setCol, setVal, whereCol, whereVal);
        }

        // ================================================================
        // UTIL
        // ================================================================
        private string StripSemicolon(string v)
        {
            if (v == null) return v;
            return v.TrimEnd(';');
        }
    }
}
