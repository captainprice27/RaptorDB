using System;
using System.IO;
using RaptorDB.RaptorDB.Core;

namespace RaptorDB.RaptorDB.Storage
{
    internal class WALManager
    {
        private readonly DBEngine _engine;
        private string BasePath => _engine.GetActiveDbPath();
        private string WalPath => Path.Combine(BasePath, "wal.log");

        public WALManager(DBEngine engine)
        {
            _engine = engine;
        }

        public void Log(string action, string table, string details)
        {
            Directory.CreateDirectory(BasePath);
            string entry = $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}|{action}|{table}|{details}";
            File.AppendAllText(WalPath, entry + Environment.NewLine);
        }

        public void LogDropTable(string table)
        {
            File.AppendAllText(WalPath,
                $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}|DROP_TABLE|{table}|TABLE REMOVED\n");
        }

        public void LogDropDatabase(string db)
        {
            // NOTE: We keep WAL as requested (no deletion)
            File.AppendAllText(WalPath,
                $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}|DROP_DATABASE|{db}|DATABASE REMOVED\n");
        }
    }
}
