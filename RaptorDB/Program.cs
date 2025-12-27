using RaptorDB.RaptorDB.Core;
using RaptorDB.RaptorDB.REPL;

namespace RaptorDB.RaptorDB
{
    internal class Program
    {
        static void Main()
        {
            var engine = new DBEngine();
            var repl = new ReplShell(engine);
            repl.Start();
        }
    }
}
