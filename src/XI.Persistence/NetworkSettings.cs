using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace XI.Persistence
{
    // Loads the network settings the DB backend needs from settings/default/network.lua,
    // mirroring the keys the C++ engine reads (SQL_HOST/PORT/LOGIN/PASSWORD/DATABASE and
    // SQL_QUERY_RETRY_COUNT). Uses the same scalar Lua-subset parsing as the live checklist.
    public sealed class NetworkSettings
    {
        public string SqlHost = "127.0.0.1";
        public int SqlPort = 3306;
        public string SqlLogin = "root";
        public string SqlPassword = "root";
        public string SqlDatabase = "xidb";
        public int SqlQueryRetryCount = 1;

        public string ZmqTransport = "tcp";
        public string ZmqIp = "127.0.0.1";
        public int ZmqPort = 54003;

        private static readonly Regex LuaStr =
            new Regex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*'([^']*)'\s*,?\s*(?:--.*)?$", RegexOptions.Compiled);
        private static readonly Regex LuaNum =
            new Regex(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(-?\d+)\s*,?\s*(?:--.*)?$", RegexOptions.Compiled);

        public static NetworkSettings Parse(string luaText)
        {
            var s = new NetworkSettings();
            foreach (var line in luaText.Split('\n'))
            {
                var l = line.TrimEnd('\r');

                var mStr = LuaStr.Match(l);
                if (mStr.Success)
                {
                    ApplyString(s, mStr.Groups[1].Value, mStr.Groups[2].Value);
                    continue;
                }

                var mNum = LuaNum.Match(l);
                if (mNum.Success)
                {
                    ApplyNumber(s, mNum.Groups[1].Value, int.Parse(mNum.Groups[2].Value, CultureInfo.InvariantCulture));
                }
            }

            return s;
        }

        public static NetworkSettings LoadFile(string path) => Parse(File.ReadAllText(path));

        private static void ApplyString(NetworkSettings s, string key, string val)
        {
            switch (key)
            {
                case "SQL_HOST": s.SqlHost = val; break;
                case "SQL_LOGIN": s.SqlLogin = val; break;
                case "SQL_PASSWORD": s.SqlPassword = val; break;
                case "SQL_DATABASE": s.SqlDatabase = val; break;
                case "ZMQ_TRANSPORT": s.ZmqTransport = val; break;
                case "ZMQ_IP": s.ZmqIp = val; break;
            }
        }

        private static void ApplyNumber(NetworkSettings s, string key, int val)
        {
            switch (key)
            {
                case "SQL_PORT": s.SqlPort = val; break;
                case "SQL_QUERY_RETRY_COUNT": s.SqlQueryRetryCount = val; break;
                case "ZMQ_PORT": s.ZmqPort = val; break;
            }
        }

        // more MySqlConnector connection string options, appended as given (a test's small pool); none by default
        public string ConnectionOptions = "";

        // Builds a MySqlConnector connection string from these settings.
        public string BuildConnectionString()
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Server={SqlHost};Port={SqlPort};User ID={SqlLogin};Password={SqlPassword};Database={SqlDatabase};" +
                $"Charset=utf8mb4;") +
                ConnectionOptions;
        }
    }
}
