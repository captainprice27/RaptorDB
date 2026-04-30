using System;
using System.Collections.Generic;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.Parser
{
    /// <summary>
    /// Recursive-descent parser. Converts a flat token list from the Lexer into a typed AST node.
    /// Updated for .NET 10: all Pop() call-sites are guarded via Require() so the nullable
    /// flow analysis is satisfied without suppression operators.
    /// </summary>
    internal class Parser
    {
        private readonly List<string> _tokens;
        private int _pos = 0;

        public Parser(List<string> tokens) => _tokens = tokens;

        // ---------------------------------------------------------------
        // CORE TOKEN HELPERS
        // ---------------------------------------------------------------

        /// <summary>Peeks at the next token without consuming it. Returns null at end-of-stream.</summary>
        private string? Peek() => _pos < _tokens.Count ? _tokens[_pos] : null;

        /// <summary>Consumes and returns the next token. Returns null at end-of-stream.</summary>
        private string? Pop() => _pos < _tokens.Count ? _tokens[_pos++] : null;

        /// <summary>
        /// Consumes the next token and throws a descriptive parse error if the stream is exhausted.
        /// Use this instead of Pop() whenever a token is required.
        /// </summary>
        private string Require(string context)
        {
            string? tok = Pop();
            if (tok is null)
                throw new Exception($"Syntax error: unexpected end of input (expected {context})");
            return tok;
        }

        private static string StripSemicolon(string v) => v.TrimEnd(';');

        /// <summary>
        /// Reads either a bare identifier ("id") or a qualified one ("students.id").
        /// Used in SELECT column lists, WHERE conditions and JOIN ... ON clauses.
        /// Added in v2.0 alongside JOIN support.
        /// </summary>
        private string ReadQualifiedIdentifier(string context)
        {
            string head = StripSemicolon(Require(context));
            if (Peek() == ".")
            {
                Pop(); // consume '.'
                string tail = StripSemicolon(Require("column name after '.'"));
                return head + "." + tail;
            }
            return head;
        }

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
                throw new Exception($"Syntax error: expected '{token}', got '{Peek() ?? "<end>"}'");
        }

        // ---------------------------------------------------------------
        // OPERATOR HELPERS
        // ---------------------------------------------------------------

        private static bool IsOperator(string? token)
            => token is "=" or "==" or ">" or "<" or ">=" or "<=" or "!=";

        private string ParseOperator()
        {
            string? op = Peek();
            if (IsOperator(op))
            {
                Pop();
                return op == "==" ? "=" : op!;
            }
            throw new Exception($"Syntax error: expected operator, got '{op ?? "<end>"}'");
        }

        // ---------------------------------------------------------------
        // WHERE CLAUSE  (supports AND, BETWEEN, shorthand AND <op>)
        // ---------------------------------------------------------------

        private List<Condition> ParseWhereClause()
        {
            var conditions = new List<Condition>();

            string activeCol = ReadQualifiedIdentifier("column name");
            ParseConditionForColumn(activeCol, conditions);

            while (Match("and"))
            {
                string? next = Peek();

                // Shorthand: "gpa > 3.0 AND < 4.0" — reuse previous column
                if (IsOperator(next) || string.Equals(next, "between", StringComparison.OrdinalIgnoreCase))
                {
                    ParseConditionForColumn(activeCol, conditions);
                }
                else
                {
                    activeCol = ReadQualifiedIdentifier("column name");
                    ParseConditionForColumn(activeCol, conditions);
                }
            }
            return conditions;
        }

        private void ParseConditionForColumn(string col, List<Condition> list)
        {
            if (Match("between"))
            {
                // "age BETWEEN 10 AND 20"  →  age >= 10 AND age <= 20
                string lower = StripSemicolon(Require("lower bound"));
                Expect("and");
                string upper = StripSemicolon(Require("upper bound"));
                list.Add(new Condition(col, ">=", lower));
                list.Add(new Condition(col, "<=", upper));
            }
            else
            {
                string op  = ParseOperator();
                string val = StripSemicolon(Require("value"));
                list.Add(new Condition(col, op, val));
            }
        }

        // ---------------------------------------------------------------
        // ENTRY POINT
        // ---------------------------------------------------------------

        public AstNode Parse()
        {
            // Skip any stray leading semicolons
            while (Peek() == ";") Pop();

            if (Match("list"))    { Expect("tables");   return new ListTablesNode(); }
            if (Match("current")) { Expect("database"); return new CurrentDatabaseNode(); }

            if (Match("create"))
            {
                if (Match("database")) return new CreateDatabaseNode(StripSemicolon(Require("database name")));
                if (Match("table"))    return ParseCreateTable();
            }

            if (Match("drop"))
            {
                if (Match("database")) return new DropDatabaseNode(StripSemicolon(Require("database name")));
                if (Match("table"))    return new DropTableNode(StripSemicolon(Require("table name")));
            }

            if (Match("use"))    return new UseDatabaseNode(StripSemicolon(Require("database name")));
            if (Match("insert")) return ParseInsert();
            if (Match("select")) return ParseSelect();
            if (Match("delete")) return ParseDelete();
            if (Match("update")) return ParseUpdate();

            throw new Exception($"Syntax error near '{Peek() ?? "<end>"}'");
        }

        // ---------------------------------------------------------------
        // STATEMENT PARSERS
        // ---------------------------------------------------------------

        private SelectNode ParseSelect()
        {
            var cols = new List<string>();

            if (Match("*"))
            {
                // empty list = SELECT *
            }
            else
            {
                cols.Add(ReadQualifiedIdentifier("column name"));
                // Consume additional comma-separated columns: SELECT a, b, c FROM ...
                while (Match(","))
                    cols.Add(ReadQualifiedIdentifier("column name"));
            }

            Expect("from");
            string table = StripSemicolon(Require("table name"));

            // v2.0 — any number of JOIN ... ON ... clauses in sequence
            var joins = new List<JoinClause>();
            JoinClause? next;
            while ((next = ParseOptionalJoin()) != null)
                joins.Add(next);

            var conditions = new List<Condition>();
            if (Match("where")) conditions = ParseWhereClause();

            return new SelectNode(table, cols, conditions, joins);
        }

        // ---------------------------------------------------------------
        // JOIN CLAUSE  (v2.0 — supports INNER, LEFT, RIGHT)
        //
        //   [INNER|LEFT|RIGHT] JOIN <table> ON <col> = <col>
        //
        // The JOIN keyword may be preceded by an explicit qualifier; a bare
        // "JOIN" is treated as INNER JOIN (SQL standard).
        // ---------------------------------------------------------------
        private JoinClause? ParseOptionalJoin()
        {
            JoinType? type = null;

            if (Match("inner"))      { type = JoinType.Inner; }
            else if (Match("left"))  { type = JoinType.Left;  Match("outer"); }
            else if (Match("right")) { type = JoinType.Right; Match("outer"); }

            // Either an explicit qualifier was consumed (type != null) and JOIN must follow,
            // or a bare JOIN is treated as INNER.
            if (type is null)
            {
                if (!Match("join")) return null;
                type = JoinType.Inner;
            }
            else
            {
                Expect("join");
            }

            string joinTable = StripSemicolon(Require("table name after JOIN"));
            Expect("on");

            string leftCol = ReadQualifiedIdentifier("left column in ON clause");
            // ON clause is an equi-join; only '=' is supported in v2.0.
            string op = ParseOperator();
            if (op != "=")
                throw new Exception($"Syntax error: JOIN ... ON only supports '=' (got '{op}')");
            string rightCol = StripSemicolon(ReadQualifiedIdentifier("right column in ON clause"));

            return new JoinClause(type.Value, joinTable, leftCol, rightCol);
        }

        private DeleteNode ParseDelete()
        {
            Expect("from");
            string table     = StripSemicolon(Require("table name"));
            var    conditions = new List<Condition>();
            if (Match("where")) conditions = ParseWhereClause();

            return new DeleteNode(table, conditions);
        }

        private UpdateNode ParseUpdate()
        {
            string table  = StripSemicolon(Require("table name"));
            Expect("set");
            string setCol = Require("column name");
            Expect("=");
            string setVal = StripSemicolon(Require("value"));

            var conditions = new List<Condition>();
            if (Match("where")) conditions = ParseWhereClause();

            return new UpdateNode(table, setCol, setVal, conditions);
        }

        private CreateTableNode ParseCreateTable()
        {
            string table = StripSemicolon(Require("table name"));
            Expect("(");

            var cols = new List<(string, string, bool)>();
            while (true)
            {
                string col  = Require("column name");
                string type = Require("column type");
                bool   pk   = Match("pk");
                cols.Add((col, type.ToUpper(), pk));
                if (Match(")")) break;
                Expect(",");
            }
            return new CreateTableNode(table, cols);
        }

        private InsertNode ParseInsert()
        {
            Expect("into");
            string table = StripSemicolon(Require("table name"));
            Expect("(");
            var cols = new List<string>();
            while (!Match(")")) { cols.Add(Require("column name")); Match(","); }

            Expect("values");
            Expect("(");
            var vals = new List<string>();
            while (!Match(")")) { vals.Add(Require("value")); Match(","); }

            return new InsertNode(table, cols, vals);
        }
    }
}