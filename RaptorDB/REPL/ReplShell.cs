using System;
using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.Parser.AST;

namespace RaptorDB.RaptorDB.REPL
{
    internal class ReplShell
    {
        private readonly DBEngine _db;

        //public ReplShell()
        //{
        //    _db = new DBEngine();
        //}

        public ReplShell(DBEngine engine)
        {
            _db = engine;
        }


        public void Start()
        {
            Console.WriteLine("RaptorDB Ready.");
            Console.WriteLine("Type commands. Use CTRL+C to exit.\n");

            while (true)
            {
                Console.Write("RaptorDB> ");
                string input = Console.ReadLine()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // --- Confirmation for DROP TABLE ---
                if (input.StartsWith("DROP TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write("Are you sure? (yes/no): ");
                    var confirm = Console.ReadLine()?.Trim().ToLower();

                    if (confirm != "yes")
                    {
                        Console.WriteLine("Operation cancelled.");
                        continue;
                    }
                }

                // --- Confirmation for DROP DATABASE ---
                if (input.StartsWith("DROP DATABASE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Write("Are you sure you want to drop this database? (yes/no): ");
                    var confirm = Console.ReadLine()?.Trim().ToLower();

                    if (confirm != "yes")
                    {
                        Console.WriteLine("Operation cancelled.");
                        continue;
                    }
                }

                // ---- Execute Query ----
                string result = _db.Process(input);
                Console.WriteLine(result);
            }
        }
    }
}
