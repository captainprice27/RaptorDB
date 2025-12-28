using System;
using System.Collections.Generic;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.Parser
{
    internal class Parser
    {
        private readonly List<string> _tokens;
        private int _pos = 0;

        public Parser(List<string> tokens) => _tokens = tokens;

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

        private string StripSemicolon(string v) => v?.TrimEnd(';');

        // --- OPERATOR PARSING ---
        private bool IsOperator(string token)
            => token == "=" || token == "==" || token == ">" || token == "<" ||
               token == ">=" || token == "<=" || token == "!=";

        private string ParseOperator()
        {
            string op = Peek();
            if (IsOperator(op))
            {
                Pop();
                if (op == "==") return "=";
                return op;
            }
            throw new Exception($"Syntax error: expected operator, got '{op}'");
        }

        // --- WHERE CLAUSE PARSER (Supports AND, BETWEEN) ---
        private List<Condition> ParseWhereClause()
        {
            var conditions = new List<Condition>();

            // 1. Parse first condition: "age > 10"
            string activeCol = Pop();
            ParseConditionForColumn(activeCol, conditions);

            // 2. Loop for "AND"
            while (Match("and"))
            {
                string next = Peek();

                // FEATURE: Shorthand support ("age > 10 AND < 20")
                if (IsOperator(next) || next.Equals("between", StringComparison.OrdinalIgnoreCase))
                {
                    // Reuse the active column
                    ParseConditionForColumn(activeCol, conditions);
                }
                else
                {
                    // New column ("AND salary > 5000")
                    activeCol = Pop();
                    ParseConditionForColumn(activeCol, conditions);
                }
            }
            return conditions;
        }

        private void ParseConditionForColumn(string col, List<Condition> list)
        {
            if (Match("between"))
            {
                // "age BETWEEN 10 AND 20" -> "age >= 10" AND "age <= 20"
                string lower = StripSemicolon(Pop());
                Expect("and");
                string upper = StripSemicolon(Pop());

                list.Add(new Condition(col, ">=", lower));
                list.Add(new Condition(col, "<=", upper));
            }
            else
            {
                string op = ParseOperator();
                string val = StripSemicolon(Pop());
                list.Add(new Condition(col, op, val));
            }
        }

        // --- ENTRY POINT ---
        public AstNode Parse()
        {
            while (Peek() == ";") Pop();

            if (Match("list")) { Expect("tables"); return new ListTablesNode(); }
            if (Match("current")) { Expect("database"); return new CurrentDatabaseNode(); }

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

            if (Match("use")) return new UseDatabaseNode(StripSemicolon(Pop()));
            if (Match("insert")) return ParseInsert();
            if (Match("select")) return ParseSelect();
            if (Match("delete")) return ParseDelete();
            if (Match("update")) return ParseUpdate();

            throw new Exception($"Syntax error near '{Peek()}'");
        }

        private SelectNode ParseSelect()
        {
            var cols = new List<string>();
            if (Match("*")) { /* empty list = * */ }
            else { cols.Add(StripSemicolon(Pop())); }

            Expect("from");
            string table = StripSemicolon(Pop());
            var conditions = new List<Condition>();

            if (Match("where")) conditions = ParseWhereClause();

            return new SelectNode(table, cols, conditions);
        }

        private DeleteNode ParseDelete()
        {
            Expect("from");
            string table = StripSemicolon(Pop());
            var conditions = new List<Condition>();

            if (Match("where")) conditions = ParseWhereClause();

            return new DeleteNode(table, conditions);
        }

        private UpdateNode ParseUpdate()
        {
            string table = StripSemicolon(Pop());
            Expect("set");
            string setCol = Pop();
            Expect("=");
            string setVal = StripSemicolon(Pop());

            var conditions = new List<Condition>();

            if (Match("where")) conditions = ParseWhereClause();

            return new UpdateNode(table, setCol, setVal, conditions);
        }

        // ... Existing CreateTable / Insert ...
        private CreateTableNode ParseCreateTable()
        {
            string table = StripSemicolon(Pop());
            Expect("(");
            var cols = new List<(string, string, bool)>();
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

        private InsertNode ParseInsert()
        {
            Expect("into");
            string table = StripSemicolon(Pop());
            Expect("(");
            var cols = new List<string>();
            while (!Match(")")) { cols.Add(Pop()); Match(","); }
            Expect("values");
            Expect("(");
            var vals = new List<string>();
            while (!Match(")")) { vals.Add(Pop()); Match(","); }
            return new InsertNode(table, cols, vals);
        }
    }
}