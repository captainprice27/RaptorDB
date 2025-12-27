using System;
using System.IO;
using System.Collections.Generic;
using RaptorDB.RaptorDB.Parser;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.Core
{
    internal class DBEngine
    {
        private readonly ExecutionEngine _executor;

        // Root path for all databases
        private readonly string _rootPath = @"E:\VISUAL-STUDIO\codes\RaptorDB\RaptorDB\Databases";

        // Active database context
        private string _activeDatabase = "default_db";

        // Normalize names (strip semicolons + lowercase)
        private string Normalize(string name) =>
            name?.Trim().TrimEnd(';').ToLower() ?? "";

        // ---------------------------------------------------------------
        // CONSTRUCTOR
        // ---------------------------------------------------------------
        public DBEngine()
        {
            _executor = new ExecutionEngine(this);

            // Ensure root + default database exist
            Directory.CreateDirectory(_rootPath);
            Directory.CreateDirectory(Path.Combine(_rootPath, "default_db"));
        }

        // ---------------------------------------------------------------
        // MAIN QUERY ENTRY
        // ---------------------------------------------------------------
        public string Process(string query)
        {
            try
            {
                // 1️⃣ Lexing
                var lexer = new Lexer(query);
                List<string> tokens = lexer.Tokenize();
                if (tokens.Count == 0) return "";

                // 2️⃣ Parsing
                var parser = new Parser.Parser(tokens);
                AstNode ast = parser.Parse(); // <-- updated: no args

                // 3️⃣ Execution
                return _executor.Execute(ast);
            }
            catch (Exception ex)
            {
                return $"[Engine Error] {ex.Message}";
            }
        }

        // ---------------------------------------------------------------
        // DATABASE CONTEXT HELPERS
        // ---------------------------------------------------------------
        public string GetActiveDbPath() =>
            Path.Combine(_rootPath, _activeDatabase);

        public string GetActiveDatabaseName() =>
            _activeDatabase;

        public string GetRootPath() =>
            _rootPath;

        public string GetDbPath(string dbName) =>
            Path.Combine(_rootPath, Normalize(dbName));

        // ---------------------------------------------------------------
        // DATABASE OPERATIONS
        // ---------------------------------------------------------------
        public string SwitchDatabase(string dbName)
        {
            dbName = Normalize(dbName);
            string target = GetDbPath(dbName);

            if (!Directory.Exists(target))
                return $"ERROR: Database '{dbName}' does not exist.";

            _activeDatabase = dbName;
            return $"Switched to database '{dbName}'.";
        }

        public string CreateDatabase(string dbName)
        {
            dbName = Normalize(dbName);
            string target = GetDbPath(dbName);

            if (Directory.Exists(target))
                return $"Database '{dbName}' already exists.";

            Directory.CreateDirectory(target);
            return $"Database '{dbName}' created.";
        }

        public string DropDatabase(string dbName)
        {
            dbName = Normalize(dbName);
            string path = GetDbPath(dbName);

            if (!Directory.Exists(path))
                return $"ERROR: Database '{dbName}' does not exist.";

            // Prevent dropping currently active DB
            if (_activeDatabase == dbName)
            {
                _activeDatabase = "default_db"; // auto-fallback
            }

            Directory.Delete(path, true);
            return $"Database '{dbName}' dropped.";
        }
    }
}
