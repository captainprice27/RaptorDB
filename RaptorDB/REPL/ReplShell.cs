//using System;
//using RaptorDB.RaptorDB.Core;
//using RaptorDB.RaptorDB.Parser.AST;

//namespace RaptorDB.RaptorDB.REPL
//{
//    internal class ReplShell
//    {
//        private readonly DBEngine _db;

//        //public ReplShell()
//        //{
//        //    _db = new DBEngine();
//        //}

//        public ReplShell(DBEngine engine)
//        {
//            _db = engine;
//        }


//        public void Start()
//        {
//            Console.WriteLine("RaptorDB Locked and Loaded >> 🦖🦖🦖");
//            Console.WriteLine("© Prayas@captainprice27 2025-2026");
//            Console.WriteLine("Type commands. Use CTRL+C to exit.\n");

//            while (true)
//            {
//                Console.Write("RaptorDB> ");
//                string input = Console.ReadLine()?.Trim() ?? "";

//                if (string.IsNullOrWhiteSpace(input))
//                    continue;

//                // --- Confirmation for DROP TABLE ---
//                if (input.StartsWith("DROP TABLE", StringComparison.OrdinalIgnoreCase))
//                {
//                    Console.Write("Are you sure? (yes/no): ");
//                    var confirm = Console.ReadLine()?.Trim().ToLower();

//                    if (confirm != "yes")
//                    {
//                        Console.WriteLine("Operation cancelled.");
//                        continue;
//                    }
//                }

//                // --- Confirmation for DROP DATABASE ---
//                if (input.StartsWith("DROP DATABASE", StringComparison.OrdinalIgnoreCase))
//                {
//                    Console.Write("Are you sure you want to drop this database? (yes/no): ");
//                    var confirm = Console.ReadLine()?.Trim().ToLower();

//                    if (confirm != "yes")
//                    {
//                        Console.WriteLine("Operation cancelled.");
//                        continue;
//                    }
//                }

//                // ---- Execute Query ----
//                string result = _db.Process(input);
//                Console.WriteLine(result);
//            }
//        }
//    }
//}
using System;
using RaptorDB.RaptorDB.Core;

namespace RaptorDB.RaptorDB.REPL
{
    internal class ReplShell
    {
        private readonly DBEngine _db;

        public ReplShell(DBEngine engine)
        {
            _db = engine;
        }

        public void Start()
        {
            // Force the console to support Unicode/Emojis/Special Symbols
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            // Set the header color
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("RaptorDB Locked and Loaded >> 🦖🦖🦖");
            Console.WriteLine("© Prayas@captainprice27 2025-2026");
            Console.WriteLine("Type 'exit'/'quit' to quit.\n");
            Console.ResetColor();

            while (true)
            {
                //// 1. Make the PROMPT Blue/Cyan
                //Console.ForegroundColor = ConsoleColor.Cyan;
                //Console.Write("RaptorDB> ");
                //Console.ResetColor(); // Reset so the user's typing is white/gray

                // --- NEW: DYNAMIC PROMPT ---
                string dbName = _db.GetActiveDatabaseName();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"RaptorDB ");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{dbName}]"); // Shows [school] in Yellow

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("> ");
                Console.ResetColor();
                // ---------------------------

                string input = Console.ReadLine()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // 2. Add an explicit EXIT command
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye! 👋");
                    break;
                }

                // --- Confirmation for DROP TABLE ---
                if (input.StartsWith("DROP TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Red; // Warning color
                    Console.Write("Are you sure? (yes/no): ");
                    Console.ResetColor();

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
                    Console.ForegroundColor = ConsoleColor.Red; // Danger color
                    Console.Write("Are you sure you want to drop this database? (yes/no): ");
                    Console.ResetColor();

                    var confirm = Console.ReadLine()?.Trim().ToLower();
                    if (confirm != "yes")
                    {
                        Console.WriteLine("Operation cancelled.");
                        continue;
                    }
                }

                // ---- Execute Query with SAFETY ----
                try
                {
                    string result = _db.Process(input);

                    // Optional: Make success output Green
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(result);
                }
                catch (Exception ex)
                {
                    // 3. Catch errors (Lexer/Parser) so the shell doesn't crash
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error] {ex.Message}");
                }
                finally
                {
                    Console.ResetColor();
                }
            }
        }
    }
}