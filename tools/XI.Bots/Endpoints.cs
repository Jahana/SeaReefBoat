using System;
using System.Collections.Generic;
using System.Net;

namespace XI.Bots
{
    // Where the bots connect: what xiloader is told (the lobby host and its three ports). The map and search addresses
    // come from the server itself (the 0x0B char select reply, every 0x00B), as they do for the retail client.
    public sealed class Endpoints
    {
        public string Host = "127.0.0.1";
        public int Auth = 55231, Data = 55230, View = 55001;
        // the address the bot's sockets bind to before they connect (another loopback address such as 127.0.0.77 makes
        // the lobby see a client of its own: accounts_sessions.client_addr, LOGIN_LIMIT); null = the system's choice
        public string LocalHost;

        public System.Net.Sockets.TcpClient NewTcpClient() =>
            LocalHost == null ? new System.Net.Sockets.TcpClient() : new System.Net.Sockets.TcpClient(new IPEndPoint(IPAddress.Parse(LocalHost), 0));

        // Ports a bot refuses to touch whatever the server tells it: the live stack's (login 54230/54231/54001, search
        // 54002, the world's router 54003, map 54230, profile 51220, IRC 51240). A bot sent there is a failed bot, never
        // a packet on someone's game.
        public static readonly HashSet<int> Forbidden = new HashSet<int> { 51220, 51240, 54001, 54002, 54003, 54230, 54231 };

        public static IPEndPoint Checked(string host, int port)
        {
            if (Forbidden.Contains(port)) throw new InvalidOperationException($"refusing to connect to {host}:{port} (a live server port)");
            return new IPEndPoint(IPAddress.Parse(host), port);
        }

        public static IPEndPoint Checked(uint ip, int port) =>
            Checked($"{ip & 0xFF}.{(ip >> 8) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 24) & 0xFF}", port);

        public void Validate()
        {
            Checked(Host, Auth);
            Checked(Host, Data);
            Checked(Host, View);
        }
    }
}
