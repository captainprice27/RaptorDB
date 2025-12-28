
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

        // --- FIX: PATH IS NOW GENERIC ---
        // It looks for a "Databases" folder right next to RaptorDB.exe
        private readonly string _rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Databases");

        // Active database context
        private string _activeDatabase = "default_db";

        // Normalize names (strip semicolons + lowercase)
        private string Normalize(string name) =>
            name?.Trim().TrimEnd(';').ToLower() ?? "";

        // ---------------------------------------------------------------
        // CONSTRUCTOR
        
        // updated on 27-12-2025 to support env variable for path for future Azure deployments
        public DBEngine()
        {
            _executor = new ExecutionEngine(this);

            // 1. Check if an Environment Variable is set (For Azure/Prod)
            string? envPath = Environment.GetEnvironmentVariable("RAPTOR_DB_PATH");

            if (!string.IsNullOrEmpty(envPath))
            {
                _rootPath = envPath;
            }
            else
            {
                // 2. Fallback to local 'bin' folder (For Local Dev)
                _rootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Databases");
            }

            Console.WriteLine($"[Storage] Data located at: {_rootPath}");

            // Ensure root + default database exist
            if (!Directory.Exists(_rootPath))
                Directory.CreateDirectory(_rootPath);

            string defaultDb = Path.Combine(_rootPath, "default_db");
            if (!Directory.Exists(defaultDb))
                Directory.CreateDirectory(defaultDb);
        }

        // ---------------------------------------------------------------
        // MAIN QUERY ENTRY
        // ---------------------------------------------------------------
        public string Process(string query)
        {
            try
            {
                // 1️D Lexing
                // Uses the fixed Lexer (returns List<string> to match your current Parser)
                var lexer = new Lexer(query);
                List<string> tokens = lexer.Tokenize();

                if (tokens.Count == 0) return "";

                // 2️⃣ Parsing
                var parser = new Parser.Parser(tokens);
                AstNode ast = parser.Parse();

                // 3️⃣ Execution
                return _executor.Execute(ast);
            }
            catch (Exception ex)
            {
                // Returns clean error messages to the Shell instead of crashing
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