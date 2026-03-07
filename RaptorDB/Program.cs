// RaptorDB — Entry Point (.NET 10)
// Uses top-level statements (C# 9+) — no boilerplate class/Main needed.

using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.REPL;

var engine = new DBEngine();
var repl = new ReplShell(engine);
repl.Start();
