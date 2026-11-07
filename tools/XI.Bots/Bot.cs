using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XI.Map;

namespace XI.Bots
{
    // One player: the account, the lobby, then a char walking about in zone. What it does is driven by Swarm; what it
    // saw is kept here for the checks.
    public sealed class Bot : IDisposable
    {
        public readonly int Index;
        public readonly string User, Password, BaseName;
        public string CharName; // BaseName, or with a letter more when the name filter refused it
        private readonly Swarm _swarm;
        private readonly Endpoints _ep;
        private readonly Random _rng;

        public uint AccountId, CharId;
        public LobbyClient.Selected Selection;
        public MapClient Map;

        // the char as the server last placed it
        public ushort Zone, TargId;
        public float X, Y, Z;
        public byte Dir;
        public bool InZone;
        public bool Alive = true;   // false once a step it needs failed: the others stop waiting for it
        public string Failure;

        // what came in
        public readonly HashSet<uint> SeenPcs = new HashSet<uint>();
        public readonly List<(byte Kind, string From, string Text)> Chat = new List<(byte, string, string)>();
        public readonly HashSet<uint> EmotesFrom = new HashSet<uint>();
        public (LogoutState State, uint Ip, ushort Port)? ZoneOut;
        public int ZoneIns, EventsSkipped;
        public readonly Dictionary<string, (bool Ok, string Detail)> Checks = new Dictionary<string, (bool, string)>();

        public Bot(Swarm swarm, int index, string user, string password, string charName)
        {
            _swarm = swarm;
            _ep = swarm.Config.Endpoints;
            Index = index;
            User = user;
            Password = password;
            BaseName = CharName = charName;
            _rng = new Random(index * 7919 + 17);
        }

        public void Check(string name, bool ok, string detail = "")
        {
            lock (Checks) Checks[name] = (ok, detail);
            if (!ok) _swarm.Log($"[{CharName}] FAIL {name}: {detail}");
        }

        public void Fail(string why)
        {
            if (Failure == null) Failure = why;
            Alive = false;
            _swarm.Log($"[{CharName}] stopped: {why}");
        }

        // ---------------------------------------------------------------- the account and the lobby

        // a new account (or the one already made by an earlier run), then a char on it
        public async Task CreateAccountAndChar(CancellationToken ct)
        {
            var created = await AuthClient.Send(_ep, AuthClient.CmdCreate, User, Password, ct);
            Check("account create", created.Result == AuthClient.ResultSuccessCreate || created.Result == AuthClient.ResultCreateTaken,
                  $"result {created.Result} {created.Error}");
            var login = await LogIn(ct);
            if (login == null) return;
            using var lobby = new LobbyClient(_ep, AccountId, login.SessionHash);
            await lobby.Connect(ct);
            await lobby.Version(ct);
            await lobby.WorldList(ct);
            var chars = await lobby.CharList(ct);
            var mine = Mine(chars);
            if (mine == null)
            {
                // a player whose name is taken or filtered tries another
                for (string name = BaseName; ; name += "e")
                {
                    try
                    {
                        await lobby.Create(name, ct);
                        break;
                    }
                    catch (System.IO.InvalidDataException e) when (e.Message.Contains("error 313") && name.Length < 12) { }
                }
                chars = await lobby.CharList(ct);
                mine = Mine(chars);
            }
            Check("char create", mine != null, $"list: {string.Join(",", chars.Select(c => c.Name))}");
            if (mine != null) CharId = mine.Id;
        }

        // the char this bot made: its base name, perhaps with letters after it
        private LobbyClient.Character Mine(List<LobbyClient.Character> chars)
        {
            var mine = chars.FirstOrDefault(c => c.Name == BaseName) ?? chars.FirstOrDefault(c => c.Name.StartsWith(BaseName) && c.Name.Trim('e') == BaseName.TrimEnd('e'));
            if (mine != null) CharName = mine.Name;
            return mine;
        }

        private async Task<AuthClient.Reply> LogIn(CancellationToken ct)
        {
            var r = await AuthClient.Send(_ep, AuthClient.CmdLogin, User, Password, ct);
            bool ok = r.Result == AuthClient.ResultSuccess && r.SessionHash?.Length == 16;
            Check("auth login", ok, $"result {r.Result} {r.Error}");
            if (!ok)
            {
                Fail("auth login refused");
                return null;
            }
            AccountId = r.AccountId;
            return r;
        }

        // auth, the lobby to the 0x0B, the map's 0x00A
        public async Task LogInToGame(CancellationToken ct)
        {
            var login = await LogIn(ct);
            if (login == null) return;
            using (var lobby = new LobbyClient(_ep, AccountId, login.SessionHash))
            {
                var sw = Stopwatch.StartNew();
                await lobby.Connect(ct);
                await lobby.Version(ct);
                var world = await lobby.WorldList(ct);
                var chars = await lobby.CharList(ct);
                var mine = Mine(chars);
                if (mine == null)
                {
                    Check("lobby select", false, $"{CharName} not in the list ({string.Join(",", chars.Select(c => c.Name))})");
                    Fail("no char");
                    return;
                }
                CharId = mine.Id;
                Selection = await lobby.Select(mine, ct);
                Check("lobby select", Selection.MapPort != 0, $"world {world}, map {IpText(Selection.MapIp)}:{Selection.MapPort}");
                _swarm.LobbyMs.Add(sw.Elapsed.TotalMilliseconds);
            }

            Map = new MapClient { ReplyTimeout = TimeSpan.FromMilliseconds(_swarm.Config.ReplyTimeoutMs) };
            Map.OnPacket = OnPacket;
            Map.OnRoundTrip = ms => _swarm.RoundTrips.Add(ms);
            await EnterZone(Endpoints.Checked(Selection.MapIp, Selection.MapPort), Selection.Key, ct);
            Check("map login", InZone, $"zone {Zone}");
        }

        public static string IpText(uint ip) => $"{ip & 0xFF}.{(ip >> 8) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 24) & 0xFF}";

        // the 0x00A, then the client's zone-in run (the reference capture's order): 0x011 once the zone loaded, then
        // 0x00C (GAMEOK) with the requests that follow it
        private async Task EnterZone(IPEndPoint map, uint[] key, CancellationToken ct)
        {
            InZone = false;
            ZoneOut = null;
            SeenPcs.Clear();
            if (!await Map.Login(CharId, key, map, TimeSpan.FromSeconds(_swarm.Config.LoginWithinSeconds), ct))
            {
                Fail($"no answer to 0x00A from {map}");
                return;
            }
            if (!InZone)
            {
                Fail("the 0x00A answer had no GP_SERV_COMMAND_LOGIN");
                return;
            }
            Map.Queue(0x011, 8, b => b[4] = 2);
            await Tick(ct, position: false);
            Map.Queue(0x00C, 12);
            Map.Queue(0x061, 8);
            Map.Queue(0x118, 8, b => b[4] = 1);
            Map.Queue(0x11B, 8, b => b[4] = 1);
            Map.Queue(0x0DB, 40);
            Map.Queue(0x05A, 4);
            await Tick(ct, position: false);
        }

        // ---------------------------------------------------------------- the server's packets

        private void OnPacket(byte[] p)
        {
            switch (MapClient.Opcode(p))
            {
                case 0x00A: // GP_SERV_COMMAND_LOGIN: where the char is
                    CharIdCheck(BitConverter.ToUInt32(p, 4));
                    TargId = BitConverter.ToUInt16(p, 8);
                    Dir = p[11];
                    X = BitConverter.ToSingle(p, 12);
                    Y = BitConverter.ToSingle(p, 16);
                    Z = BitConverter.ToSingle(p, 20);
                    Zone = (ushort)BitConverter.ToUInt32(p, 48);
                    InZone = true;
                    ZoneIns++;
                    // a zone-in cutscene comes with the login packet (EventNum / EventPara)
                    if (BitConverter.ToUInt16(p, 98) != 0) EndEvent(TargId, BitConverter.ToUInt16(p, 100));
                    break;
                case 0x00B: // GP_SERV_COMMAND_LOGOUT: a zone change or the logout
                {
                    var body = new ReadOnlySpan<byte>(p, 4, S2cLogout.SizeOf);
                    ZoneOut = (S2cLogout.GetLogoutState(body), S2cLogout.GetIp(body), (ushort)S2cLogout.GetPort(body));
                    InZone = false;
                    break;
                }
                case 0x00D: // GP_SERV_COMMAND_CHAR_PC: another char in range
                {
                    uint id = BitConverter.ToUInt32(p, 4);
                    if (id != CharId) SeenPcs.Add(id);
                    break;
                }
                case 0x017: // GP_SERV_COMMAND_CHAT_STD
                {
                    int n = 0;
                    while (n < 15 && p[8 + n] != 0) n++;
                    int m = 0;
                    while (23 + m < p.Length && p[23 + m] != 0) m++;
                    lock (Chat) Chat.Add((p[4], Encoding.ASCII.GetString(p, 8, n), Encoding.ASCII.GetString(p, 23, m)));
                    break;
                }
                case 0x05A: // GP_SERV_COMMAND_MOTIONMES
                {
                    uint caster = BitConverter.ToUInt32(p, 4);
                    if (caster != CharId) EmotesFrom.Add(caster);
                    break;
                }
                case 0x05B: // GP_SERV_COMMAND_WPOS: the server moves the char
                    if (BitConverter.ToUInt32(p, 4 + S2cWpos.OffUniqueNo) == CharId)
                    {
                        X = BitConverter.ToSingle(p, 4 + S2cWpos.OffX);
                        Y = BitConverter.ToSingle(p, 4 + S2cWpos.OffY);
                        Z = BitConverter.ToSingle(p, 4 + S2cWpos.OffZ);
                    }
                    break;
                case 0x065: // GP_SERV_COMMAND_WPOS2
                    if (BitConverter.ToUInt32(p, 4 + S2cWpos2.OffUniqueNo) == CharId)
                    {
                        X = BitConverter.ToSingle(p, 4 + S2cWpos2.OffX);
                        Y = BitConverter.ToSingle(p, 4 + S2cWpos2.OffY);
                        Z = BitConverter.ToSingle(p, 4 + S2cWpos2.OffZ);
                    }
                    break;
                // a cutscene: skipped as a player pressing through it would (GP_CLI_COMMAND_EVENTEND)
                case 0x032: EndEvent(BitConverter.ToUInt16(p, 8), BitConverter.ToUInt16(p, 12)); break;
                case 0x033: EndEvent(BitConverter.ToUInt16(p, 4 + S2cEventStr.OffActIndex), BitConverter.ToUInt16(p, 4 + S2cEventStr.OffEventPara)); break;
                case 0x034: EndEvent(BitConverter.ToUInt16(p, 4 + S2cEventNum.OffActIndex), BitConverter.ToUInt16(p, 4 + S2cEventNum.OffEventPara)); break;
            }
        }

        private void CharIdCheck(uint id)
        {
            if (id != CharId) Check("login packet char id", false, $"0x00A for {id}, expected {CharId}");
        }

        private void EndEvent(ushort actIndex, ushort eventId)
        {
            EventsSkipped++;
            Map.Queue(0x05B, 20, b =>
            {
                BitConverter.TryWriteBytes(new Span<byte>(b, 4, 4), CharId);
                BitConverter.TryWriteBytes(new Span<byte>(b, 8, 4), 0u);
                BitConverter.TryWriteBytes(new Span<byte>(b, 12, 2), actIndex);
                BitConverter.TryWriteBytes(new Span<byte>(b, 14, 2), (ushort)0);
                BitConverter.TryWriteBytes(new Span<byte>(b, 16, 2), Zone);
                BitConverter.TryWriteBytes(new Span<byte>(b, 18, 2), eventId);
            });
        }

        // ---------------------------------------------------------------- in zone

        private long _lastTick;

        // one datagram (with the char's position when in zone) and its answer, at the client's cadence
        public async Task Tick(CancellationToken ct, bool position = true)
        {
            if (Map == null) return;
            if (position && InZone) QueuePosition();
            await Map.Exchange(ct);
            long now = Environment.TickCount64;
            int wait = _swarm.Config.TickMs - (int)(now - _lastTick);
            if (wait > 0) await Task.Delay(wait, ct);
            _lastTick = Environment.TickCount64;
        }

        private void QueuePosition()
        {
            Map.Queue(0x015, 32, b =>
            {
                BitConverter.TryWriteBytes(new Span<byte>(b, C2sPos.OffX, 4), X);
                BitConverter.TryWriteBytes(new Span<byte>(b, C2sPos.OffZ, 4), Y); // the height
                BitConverter.TryWriteBytes(new Span<byte>(b, C2sPos.OffY, 4), Z);
                b[C2sPos.OffDir] = Dir;
                BitConverter.TryWriteBytes(new Span<byte>(b, C2sPos.OffTimeNow, 4), (uint)Environment.TickCount);
            });
        }

        public async Task<bool> TickUntil(Func<bool> done, TimeSpan within, CancellationToken ct)
        {
            var until = DateTime.UtcNow + within;
            while (!done())
            {
                if (DateTime.UtcNow > until || !Alive || (ZoneOut != null && !InZone)) return done();
                await Tick(ct);
            }
            return true;
        }

        // walks to (x, z) at a run's pace, the height eased along
        public async Task WalkTo(float x, float y, float z, CancellationToken ct, float speed = 5f)
        {
            float step = speed * _swarm.Config.TickMs / 1000f;
            for (int guard = 0; guard < 2000 && Alive && InZone; guard++)
            {
                float dx = x - X, dz = z - Z;
                float d = MathF.Sqrt(dx * dx + dz * dz);
                if (d < 0.5f) break;
                float f = Math.Min(1f, step / d);
                X += dx * f;
                Z += dz * f;
                Y += (y - Y) * f;
                Dir = (byte)(int)((MathF.Atan2(-dz, dx) / (2 * MathF.PI) * 256 + 256) % 256);
                await Tick(ct);
            }
        }

        // a few random steps around (cx, cz)
        public async Task Wander(float cx, float cy, float cz, float radius, int steps, CancellationToken ct)
        {
            for (int i = 0; i < steps && Alive && InZone; i++)
            {
                double a = _rng.NextDouble() * Math.PI * 2, r = _rng.NextDouble() * radius;
                await WalkTo(cx + (float)(Math.Cos(a) * r), cy, cz + (float)(Math.Sin(a) * r), ct);
            }
        }

        public void Say(byte kind, string text) => Map.Queue(0x0B5, (6 + text.Length + 1 + 3) & ~3, b =>
        {
            b[4] = kind;
            Encoding.ASCII.GetBytes(text).CopyTo(b, 6);
        });

        public void Tell(string to, string text) => Map.Queue(0x0B6, (0x15 + text.Length + 1 + 3) & ~3, b =>
        {
            b[4] = 3;
            Encoding.ASCII.GetBytes(to).CopyTo(b, 6);
            Encoding.ASCII.GetBytes(text).CopyTo(b, 0x15);
        });

        // GP_CLI_COMMAND_MOTION at the char itself
        public void Emote(byte number) => Map.Queue(0x05D, 16, b =>
        {
            BitConverter.TryWriteBytes(new Span<byte>(b, 4, 4), CharId);
            BitConverter.TryWriteBytes(new Span<byte>(b, 8, 2), TargId);
            b[10] = number;
            b[11] = 0;
        });

        // GP_CLI_COMMAND_MAPRECT, then the 0x00B and the next map's 0x00A
        public async Task<bool> TakeZoneLine(string line, ushort expectZone, CancellationToken ct)
        {
            uint rect = (uint)(byte)line[0] | (uint)(byte)line[1] << 8 | (uint)(byte)line[2] << 16 | (uint)(byte)line[3] << 24;
            Map.Queue(0x05E, 24, b => BitConverter.TryWriteBytes(new Span<byte>(b, 4, 4), rect));
            var sw = Stopwatch.StartNew();
            // the client stops reporting its position once it walked into the line (the server would refuse it:
            // the char is disappearing)
            var until = DateTime.UtcNow.AddSeconds(15);
            while (ZoneOut == null && Alive && DateTime.UtcNow < until) await Tick(ct, position: false);
            if (ZoneOut is not { State: LogoutState.Zonechange } zo)
            {
                Check($"zone line {line}", false, ZoneOut == null ? "no 0x00B" : $"0x00B state {ZoneOut.Value.State}");
                return false;
            }
            await Map.SendNetEnd();
            var key = (uint[])Map.Key.Clone();
            key[4] += 2; // the key counter steps twice per zone change (map_session incrementBlowfish)
            await EnterZone(Endpoints.Checked(zo.Ip, zo.Port), key, ct);
            _swarm.ZoneMs.Add(sw.Elapsed.TotalMilliseconds);
            bool ok = InZone && Zone == expectZone;
            Check($"zone line {line}", ok, $"in zone {Zone} via {IpText(zo.Ip)}:{zo.Port}");
            return ok;
        }

        // /logout: LEAVEGAME counts down, then the 0x00B (LGS_LOGOUT)
        public async Task<bool> LogOut(CancellationToken ct)
        {
            Map.Queue(0x0E7, 8, b =>
            {
                BitConverter.TryWriteBytes(new Span<byte>(b, C2sReqlogout.OffMode, 2), (ushort)ReqlogoutMode.LogoutOn);
                BitConverter.TryWriteBytes(new Span<byte>(b, C2sReqlogout.OffKind, 2), (ushort)ReqlogoutKind.Logout);
            });
            await TickUntil(() => ZoneOut != null, TimeSpan.FromSeconds(60), ct);
            bool ok = ZoneOut is { State: LogoutState.Logout };
            Check("logout", ok, ZoneOut == null ? "no 0x00B within 60 s" : $"0x00B state {ZoneOut.Value.State}");
            if (ok) await Map.SendNetEnd();
            InZone = false;
            return ok;
        }

        public void Dispose() => Map?.Dispose();
    }
}
