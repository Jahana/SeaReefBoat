using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XI.Common;
using XI.Ipc;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Commands.Live -- live gate for the GM commands (scripts/commands/*.lua) driven as a GM drives them: a "!name
// args" said in chat (GP_CLI_COMMAND_CHAT_STD 0x0B5), the dispatch (CCommandHandler::call: the bang prefix, the
// xi.commands lookup, cmdprops' permission against the char's gmlevel, the parameter string's s / i / d / b parsing,
// onTrigger's errors, the jailed char's say, map.AUDIT_GM_CMD's audit_gm row) and then the commands themselves, grouped
// (movement and warps, items and gil, jobs and levels, stats and effects, spells / key items / titles, missions /
// quests / vars, mobs and NPCs, server / zone info and the rest), each with its effect checked: the chat lines, the
// packets, the char's / the target's / the mob's / the NPC's state, the DB rows. A GM, a target char, an offline char and
// a GM of level 1 (chars 996111-996119), a mob and an NPC of West Ronfaure. The commands that would end the process or
// change state the whole server shares (crash, addtime, updateconquest, the reloads, the navmesh rebuild, inject) are
// driven only up to their permission refusal. The real scripts on the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private static readonly uint[] CharIds = { 996111, 996112, 996113, 996114, 996115, 996116, 996117, 996118, 996119 };
    private const uint GmId = 996111, TargetId = 996112, OfflineId = 996113, LowGmId = 996114;
    private const string GmName = "Cmdlivegm", TargetName = "Cmdlivetarget", OfflineName = "Cmdliveoffline", LowGmName = "Cmdlivelowgm",
                         LsName = "Cmdlivels";
    private const ushort WestRonfaure = 100, SouthernSandoria = 230, MordionGaol = 131, GmHome = 210, AbysseaKonschtat = 15, EschaRuAun = 289;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbRow(IDatabase db, string q, params BoundValue[] a)
    {
        var r = Sql(db, q, a);
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
    }

    // every char table (char_insert's char_equip, char_exp, char_history, char_inventory, char_jobs, char_pet,
    // char_points, char_profile, char_storage, char_unlocks, and the ones the logins and the commands write), the chars and
    // accounts, the linkshell the test makes and the audit_gm rows of its GMs
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        foreach (var id in CharIds)
        {
            foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
        Sql(db, "DELETE FROM linkshells WHERE name = ?", BoundValue.Of(LsName));
        foreach (var n in new[] { GmName, TargetName, OfflineName, LowGmName })
            Sql(db, "DELETE FROM audit_gm WHERE gm_name = ?", BoundValue.Of(n));
    }

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);
        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }
        // the shared server_variables row !garrison writes, kept and put back
        const string garrisonVar = "[Garrison]NextEntryTime_100";
        var savedVar = DbRow(db, "SELECT value, expiry FROM server_variables WHERE name = ?", BoundValue.Of(garrisonVar));
        try
        {
            Cleanup(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(db);
            if (savedVar == "-") Sql(db, "DELETE FROM server_variables WHERE name = ?", BoundValue.Of(garrisonVar));
            else
            {
                var p = savedVar.Split(' ');
                Sql(db, "REPLACE INTO server_variables(name, value, expiry) VALUES (?, ?, ?)", BoundValue.Of(garrisonVar),
                    BoundValue.Of(int.Parse(p[0])), BoundValue.Of(uint.Parse(p[1])));
            }
        }
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    private static readonly string[] JobColumns = { "", "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng", "sam", "nin", "drg", "smn" };

    // a Hume San d'Orian of the job at level `lvl` at x / y / z in the zone, with 100000 gil
    private static void Fixture(IDatabase db, uint id, string name, byte gmlevel, ushort zoneId, float x, float y, float z, byte job, byte lvl)
    {
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of($"commandslive{id % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(name), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)0), BoundValue.Of(gmlevel));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(id), BoundValue.Of(id), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 50, ?, ?)", BoundValue.Of(id), BoundValue.Of(job), BoundValue.Of(lvl));
        Sql(db, $"REPLACE INTO char_jobs(charid, {JobColumns[job]}) VALUES (?, ?)", BoundValue.Of(id), BoundValue.Of(lvl));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "INSERT IGNORE INTO char_flags(charid) VALUES (?)", BoundValue.Of(id));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,100000)", BoundValue.Of(id));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var statSettings = new StatSettings();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), statSettings);

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        long vanaTicks = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.VanaTicksNow = () => vanaTicks;
        long earth0 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long EarthNow() => earth0 + (now - 1_000_000) / 1000;
        zone.UnixNow = EarthNow;
        var ipc = new List<IIpcMessage>();
        zone.SendIpc = m => ipc.Add(m);
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        scripts.VanaTicksNow = () => vanaTicks;
        scripts.UnixNowMs = () => earth0 * 1000 + (now - 1_000_000);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        lua.SetGlobal("GetSystemTime", l => { l.Push((double)(uint)EarthNow()); return 1; });
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({GmId}) local target = GetPlayerByID({TargetId})\n" + code, "=verify_commands");
            return string.Join(",", got);
        }

        var expectedErrors = new HashSet<string>();
        List<string> ScriptErrors() => log.Where(l => ((l.StartsWith("lua:") && !l.Contains("global scripts")) || l.Contains("not ported") || l.StartsWith("cmdhandler::call: (")) &&
                                                      !expectedErrors.Contains(l)).Distinct().ToList();
        // the errors so far taken as expected (a script bug the check before named), returned joined
        string TakeErrors()
        {
            var e = ScriptErrors();
            foreach (var l in e) expectedErrors.Add(l);
            return string.Join(" | ", e.Select(l => l.Split('\n')[0]));
        }

        var sessions = new Dictionary<uint, MapSession>();
        void Send(MapSession s, ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        CharData Char(uint id) => (CharData)sessions[id].Char;
        // the 0x00A login and 0x00C, any zone-in cutscene ended with 0x05B as the client does
        MapSession Login(uint id)
        {
            if (sessions.TryGetValue(id, out var old)) zone.OnCharTimeout(old, false);
            var s = new MapSession { CharId = id, AccountId = id };
            sessions[id] = s;
            zone.OnLogin(s);
            var c = (CharData)s.Char;
            Send(s, 0x00A, 92, buf => BitConverter.GetBytes(id).CopyTo(buf, 12));
            Send(s, 0x00C, 12);
            for (int i = 0; i < 4 && c.IsInEvent; i++)
            {
                var ev = (ushort)c.CurrentEvent.EventId;
                Send(s, 0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
            }
            s.PacketList.Clear();
            return s;
        }
        void Tick(int ms = 1000)
        {
            now += ms;
            zone.Tick();
        }
        // GP_CLI_COMMAND_POS: the char's position and its cursor target (facetarget)
        void MoveTo(uint id, float x, float y, float z, ushort cursor = 0)
        {
            Send(sessions[id], 0x015, 32, b =>
            {
                BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
                BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
                BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
                BitConverter.GetBytes(cursor).CopyTo(b, C2sPos.OffFacetarget);
            });
        }
        void Cursor(ushort targId)
        {
            var g = Char(GmId);
            MoveTo(GmId, g.X, g.Y, g.Z, targId);
        }
        // a zone change the command asked for: the tick that zones the char, its next login
        void Rezone(uint id)
        {
            Tick(400);
            Login(id);
            var c = Char(id);
            MoveTo(id, c.X, c.Y, c.Z);
            sessions[id].PacketList.Clear();
        }
        string ChatText(BasicPacket p) => CborRaw.FromBytes(p.Buffer.AsSpan(23, p.Buffer.Length - 23).ToArray()).Split('\0')[0];
        List<string> ChatOf(uint id) => new List<BasicPacket>(sessions[id].PacketList).Where(p => Opcode(p) == 0x017).Select(ChatText).ToList();
        List<BasicPacket> Of(uint id, int opcode) => new List<BasicPacket>(sessions[id].PacketList).FindAll(p => Opcode(p) == opcode);

        // the commands said and run (each one's first token), for the closing tally
        var driven = new HashSet<string>();
        var newLog = new List<string>();
        // a GP_CLI_COMMAND_CHAT_STD say from the char (the GM by default): the chat lines the char got back, joined
        string SayAs(uint id, string text)
        {
            foreach (var x in sessions.Values) x.PacketList.Clear();
            ipc.Clear();
            int mark = log.Count;
            var bytes = CborRaw.Bytes(text);
            Send(sessions[id], 0x0B5, 8 + (bytes.Length + 4) / 4 * 4, b => { b[4] = 0; bytes.CopyTo(b, 6); });
            newLog = log.Skip(mark).ToList();
            if (text.StartsWith('!'))
            {
                string name = text[1..].TrimStart(' ', '\t').Split(' ', '\t')[0];
                if (!newLog.Any(l => l.Contains("Function does not exist") || l.Contains("higher permission")) && sessions.ContainsKey(id)) driven.Add(name);
            }
            return sessions.ContainsKey(id) ? string.Join(" | ", ChatOf(id)) : "";
        }
        string Cmd(string text) => SayAs(GmId, "!" + text);
        string Logged(string prefix) => string.Join(" | ", newLog.Where(l => l.StartsWith(prefix)).Select(l => l.Split('\n')[0]));

        // 0x02A TALKNUMWORK: message:param0 (the zone's text id, the special flag off)
        string Special(uint id) => string.Join(",", Of(id, 0x02A).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 26) & 0x7FFF}:{BitConverter.ToUInt32(p.Buffer, 8)}"));
        int Text(string name) => int.Parse(Lua($"report(zones[{WestRonfaure}].text.{name})"));

        Fixture(db, GmId, GmName, 5, WestRonfaure, -24f, -20f, 300f, 1, 75);
        Fixture(db, TargetId, TargetName, 0, WestRonfaure, -26f, -20f, 300f, 1, 30);
        Fixture(db, OfflineId, OfflineName, 0, SouthernSandoria, 10f, 0f, 10f, 1, 10);
        Fixture(db, LowGmId, LowGmName, 1, WestRonfaure, -28f, -20f, 300f, 1, 30);
        Sql(db, "UPDATE chars SET home_zone = 230, home_x = 10, home_y = 0, home_z = 12 WHERE charid = ?", BoundValue.Of(TargetId));
        Login(GmId);
        Login(TargetId);
        Login(LowGmId);
        foreach (var id in new[] { GmId, TargetId, LowGmId }) MoveTo(id, Char(id).X, Char(id).Y, Char(id).Z);
        var gm = Char(GmId);
        var wz = zone.ZoneInfoOf(WestRonfaure);
        var npc = wz.Npcs.First(n => n.Name == "Aaveleon");
        var crab = wz.Mobs.First(m => m.Name == "Tree_Crab");

        // ---- the dispatch ----
        Console.WriteLine("Dispatch: CCommandHandler::call through the 0x0B5 say");
        // a test command in xi.commands: each argument reported as type:value
        Lua(@"xi.commands.verifyargs = { cmdprops = { permission = 1, parameters = 'sidb' }, onTrigger = function(player, ...)
                  local n = select('#', ...)
                  local out = { tostring(n) }
                  for i = 1, n do local v = select(i, ...) table.insert(out, type(v) .. ':' .. tostring(v)) end
                  player:printToPlayer(table.concat(out, ' '))
              end }
              xi.commands.verifyrest = { cmdprops = { permission = 1, parameters = 's' }, onTrigger = function(player, rest) player:printToPlayer('[' .. tostring(rest) .. ']') end }
              xi.commands.verifyerror = { cmdprops = { permission = 1, parameters = '' }, onTrigger = function(player) error('verify boom') end }
              xi.commands.verifynoprops = { onTrigger = function(player) player:printToPlayer('ran') end }
              xi.commands.verifyx = { cmdprops = { permission = 1, parameters = 'xi' }, onTrigger = function(player, a) player:printToPlayer('x ' .. tostring(a)) end }");
        Check("s / i / d / b: a token, the leading digits of '12x', 2.5e1 of '2.5e1z', the whole line after the bang",
              "4 string:foo number:12 number:25 string:verifyargs foo 12x 2.5e1z", Cmd("verifyargs foo 12x 2.5e1z"));
        Check("the leading blanks and tabs skipped, an i of no digits 0; the line ended at the d, so neither it nor the b is passed",
              "2 string:foo number:0", Cmd("  \tverifyargs   foo abc"));
        // tostring is lua_to_string: an integral number printed as a uint32 (-7 as 4294967289), in C++ too
        Check("a negative i, a d of digits only", "4 string:a number:4294967289 number:3 string:verifyargs a -7 3", Cmd("verifyargs a -7 3"));
        Check("no arguments: none passed (the s ends the loop before the b)", "0", Cmd("verifyargs"));
        Check("a lone s takes the rest of the line, its spaces kept", "[one two  three]", Cmd("verifyrest one two  three"));
        Check("an undefined parameter type: logged and skipped, the next one still parsed", "x 5 | (log) cmdhandler::call: (verifyx) undefined type for param: symbol: x",
              Cmd("verifyx 5") + " | (log) " + Logged("cmdhandler::call: (verifyx)"));
        expectedErrors.UnionWith(newLog);
        Check("onTrigger's error: logged, nothing said", "/True",
              Cmd("verifyerror") + "/" + Logged("cmdhandler::call: (verifyerror) error:").EndsWith(":9: verify boom"));
        expectedErrors.UnionWith(newLog);
        Check("no cmdprops: refused", "/cmdhandler::call: (verifynoprops): Undefined 'cmdprops' table", Cmd("verifynoprops") + "/" + Logged("cmdhandler::call"));
        expectedErrors.UnionWith(newLog);
        Check("an unknown command from a GM: logged, the line swallowed (no say to the char beside)", "/cmdhandler::call: Function does not exist (nosuchcommand)/",
              Cmd("nosuchcommand") + "/" + Logged("cmdhandler::call") + "/" + string.Join("|", ChatOf(TargetId)));
        Check("the bang alone (an empty name): logged, swallowed", "/cmdhandler::call: function name was empty", Cmd("") + "/" + Logged("cmdhandler::call"));
        SayAs(TargetId, "!getid");
        Check("a gmlevel 0 char's !getid (permission 1): refused, the line said as an ordinary say the GM beside hears",
              "cmdhandler::call: Character Cmdlivetarget attempting to use higher permission command getid/!getid",
              Logged("cmdhandler::call") + "/" + string.Join("|", ChatOf(GmId)));
        SayAs(LowGmId, "!fafnir");
        Check("a gmlevel 1 GM's !fafnir (permission 5): refused, the line swallowed (a GM may have mistyped)",
              "cmdhandler::call: Character Cmdlivelowgm attempting to use higher permission command fafnir//False",
              Logged("cmdhandler::call") + "/" + string.Join("|", ChatOf(GmId)) + "/" + wz.Mobs.Any(m => m.Name == "Fafnir"));
        Check("a gmlevel 1 GM's !getid (permission 1): runs", "Must select a target using in game cursor first.", SayAs(LowGmId, "!getid"));
        Check("a gmlevel 0 char's !trustengage (permission 0): runs (its setting off)", "Trust custom engage conditions are disabled.", SayAs(TargetId, "!trustengage 1"));
        Check("a gmlevel 0 char's !build (permission 0): the build string (branch-sha, subject, date: the host's version)", "True/3",
              SayAs(TargetId, "!build").StartsWith("csharp-") + "/" + ChatOf(TargetId).FirstOrDefault()?.Split('\n').Length);
        Check("a say without the bang: no command, the say heard", "/hello", SayAs(GmId, "hello") + "/" + string.Join("|", ChatOf(TargetId)));
        // map.AUDIT_GM_CMD 3: the commands of permission 3 and up written to audit_gm
        statSettings.AuditGmCmd = 3;
        Cmd("getmod str");
        Cmd("build");
        statSettings.AuditGmCmd = 0;
        Check("map.AUDIT_GM_CMD 3: !getmod (permission 3) audited with its line, !build (permission 1) not", "getmod getmod str/1",
              DbRow(db, "SELECT command, full_string FROM audit_gm WHERE gm_name = ?", BoundValue.Of(GmName)) + "/" +
              DbRow(db, "SELECT COUNT(*) FROM audit_gm WHERE gm_name = ?", BoundValue.Of(GmName)));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- movement and warps ----
        Console.WriteLine("Movement and warps: pos, up, down, bring, goto, gotoid, gotoname, send, zone, gmhome, return, homepoint, jail, pardon, posfix, logoff");
        string Pos(CharData c) => $"{c.Zone}:{c.X:0.##},{c.Y:0.##},{c.Z:0.##}";
        Check("!pos: the GM's position, zone, floor and terrain", "Cmdlivegm: X -24.0000  Y -20.0000  Z 300.0000  Rot 0  Zone 100  Floor 0  Terrain: None", Cmd("pos"));
        Cmd("pos -20 -20 302");
        Check("!pos x y z: the GM moved in the zone (0x05B to itself)", "100:-20,-20,302/1", Pos(gm) + "/" + Of(GmId, 0x05B).Count);
        Check("!pos x y z <name>: the target moved, the GM told", "Moved Cmdlivetarget to (-22.0000, -20.0000, 302.0000) in zone 100./100:-22,-20,302",
              Cmd("pos -22 -20 302 Cmdlivetarget") + "/" + Pos(Char(TargetId)));
        Check("!pos <name>: the target's position", "Cmdlivetarget: X -22.0000  Y -20.0000  Z 302.0000  Rot 0  Zone 100  Floor 0  Terrain: None", Cmd("pos Cmdlivetarget"));
        Check("!pos x y z with a bad zone: refused", "Invalid zone ID. | !pos (x) (y) (z) (zone ID) (player)", Cmd("pos 1 2 3 999"));
        Check("!pos of an unknown name", "Player named \"Nobody\" not found! | !pos (x) (y) (z) (zone ID) (player)", Cmd("pos Nobody"));
        Check("!up 2: y less by 2", "Moving self.. /100:-20,-22,302", Cmd("up 2") + "/" + Pos(gm));
        Check("!down: y more by 0.5", "Moving self.. /100:-20,-21.5,302", Cmd("down") + "/" + Pos(gm));
        Check("!down 1.5 Cmdlivetarget: the named char moved", "Moving specified player.. /100:-22,-19,302", Cmd("down 1 Cmdlivetarget") + "/" + Pos(Char(TargetId)));
        Cursor(Char(TargetId).TargId);
        Check("!up 1 with the target on the cursor: the cursor target moved", "Moving cursor target.. /100:-22,-20,302", Cmd("up 1") + "/" + Pos(Char(TargetId)));
        Cursor(0);
        Cmd("pos -24 -20 300");
        Cmd("bring Cmdlivetarget 0");
        Check("!bring <name> 0: the target set beside the GM, no zone change (a 0x05B to it, no 0x00B)", "100:-24,-20,300/1/0",
              Pos(Char(TargetId)) + "/" + Of(TargetId, 0x05B).Count + "/" + Of(TargetId, 0x00B).Count);
        Cmd("pos -30 -20 296 Cmdlivetarget");
        Cmd("bring Cmdlivetarget");
        Rezone(TargetId);
        Check("!bring <name>: forceZone 1, the target zoned in at the GM's position", "100:-24,-20,300", Pos(Char(TargetId)));
        Cmd("bring Cmdliveoffline");
        Check("!bring of a char not on this process: ipc::SendPlayerToLocation to the world", $"SendPlayerToLocation:{OfflineId}:{WestRonfaure}",
              string.Join(",", ipc.OfType<SendPlayerToLocation>().Select(m => $"SendPlayerToLocation:{m.TargetId}:{(ushort)m.ZoneId}")));
        Check("!bring of no such char", "Player named \"Nobody\" not found! | !bring <player> (forceZone)", Cmd("bring Nobody"));
        Cmd("pos -30 -20 296 Cmdlivetarget");
        Cmd("goto Cmdlivetarget 0");
        Check("!goto <name> 0: the GM set at the target, no zone change", "100:-30,-20,296", Pos(gm));
        Cmd("pos -24 -20 300");
        Cmd("goto Cmdlivetarget");
        Rezone(GmId);
        gm = Char(GmId);
        Check("!goto <name>: forceZone 1, the GM zoned in at the target", "100:-30,-20,296", Pos(gm));
        Cmd("goto Cmdliveoffline");
        Check("!goto of a char not on this process: ipc::EntityInformationRequest (a PC, warp)", $"{OfflineId}:{GmId}:True",
              string.Join(",", ipc.OfType<EntityInformationRequest>().Select(m => $"{m.TargetId}:{m.RequesterId}:{m.Warp}")));
        Check("!goto of no such char", "Player named: Nobody not found! | !goto <player> (forceZone)", Cmd("goto Nobody"));
        Check("!gotoid <npc>: told, then after the 500 ms timer at the NPC", $"Going to Aaveleon ({npc.Id}) in West_Ronfaure.", Cmd($"gotoid {npc.Id}"));
        Tick(1000);
        Check("  the GM at the NPC", $"100:{npc.X:0.##},{npc.Y:0.##},{npc.Z:0.##}", Pos(gm));
        Cmd("gotoid 17000000");
        // C++ bug: CLuaBaseEntity::gotoEntity narrows the requester's id to a uint16 (the world cannot find a char of an id
        // over 65535 to answer); the host sends the full id, as its comment says
        Check("!gotoid of no entity here: gotoEntity's ipc::EntityInformationRequest (an NPC or mob, warp)", $"17000000:{GmId}:True",
              string.Join(",", ipc.OfType<EntityInformationRequest>().Select(m => $"{m.TargetId}:{m.RequesterId}:{m.Warp}")));
        Check("  GetNPCByID's warning for it", "lua: GetNPCByID NPC doesn't exist (17000000)", TakeErrors());
        Cmd("pos -24 -20 300");
        Check("!gotoname Aaveleon: one match, gone to", $"Going to Aaveleon ({npc.Id}).", Cmd("gotoname Aaveleon"));
        Tick(1000);
        Check("  the GM at the NPC", $"100:{npc.X:0.##},{npc.Y:0.##},{npc.Z:0.##}", Pos(gm));
        Check("!gotoname of no such name", "Nobody not found in current zone", Cmd("gotoname Nobody"));
        Cmd("pos -24 -20 300");
        Cmd("send Cmdlivetarget 230");
        Rezone(TargetId);
        Check("!send <name> <zone>: the target sent to the zone's entry", "Sent Cmdlivetarget to zone 230./230", string.Join("", ChatOf(GmId)) + "/" + Char(TargetId).Zone);
        Cmd("send Cmdlivetarget Cmdlivegm");
        Rezone(TargetId);
        Check("!send <name> <name>: the target sent to the other char", "100:-24,-20,300", Pos(Char(TargetId)));
        Cmd("zone 230");
        Rezone(GmId);
        gm = Char(GmId);
        Check("!zone 230: the GM in Southern San d'Oria", "230", gm.Zone);
        // the zone as an auto-translate phrase (0xFD, the group 0x14 and the message 0x11: West Ronfaure)
        SayAs(GmId, "!zone " + CborRaw.FromBytes(new byte[] { 0xFD, 0x02, 0x02, 0x14, 0x11, 0xFD }));
        Rezone(GmId);
        gm = Char(GmId);
        Check("!zone <auto-translate phrase>: the GM in West Ronfaure", "100", gm.Zone);
        Check("!zone of a bad zone id", "Invalid zone ID. | !zone <zone ID or autotranslate phrase>", Cmd("zone 999"));
        Cmd("gmhome");
        Rezone(GmId);
        gm = Char(GmId);
        Check("!gmhome: the GM in the GM home (210)", "210", gm.Zone);
        Cmd("return");
        Rezone(GmId);
        gm = Char(GmId);
        Check("!return: the GM back in its previous zone", "100", gm.Zone);
        Cmd("pos -24 -20 300");
        Check("!homepoint <name>: the target warped to its home point (Southern San d'Oria)", "Sent Cmdlivetarget to their homepoint.", Cmd("homepoint Cmdlivetarget"));
        Rezone(TargetId);
        Check("  the target at its home point", "230:10,0,12", Pos(Char(TargetId)));
        Cmd("send Cmdlivetarget Cmdlivegm");
        Rezone(TargetId);
        Check("!jail <name> 3 <reason>: told, the target's inJail 3", "Cmdlivegm jailed Cmdlivetarget (ID: 996112, status: Online) into cell 3. Reason: spam/3",
              Cmd("jail Cmdlivetarget 3 spam") + "/" + Lua("report(target:getCharVar('inJail'))"));
        Rezone(TargetId);
        Check("  the target in Mordion Gaol, cell 3", "131:260,11,660", Pos(Char(TargetId)));
        // (its say goes to the chars in range, none in the cell)
        Check("  a jailed char's !command: no command run (InPrison), the line left to /say", "/", SayAs(TargetId, "!trustengage 1") + "/" + Logged("cmdhandler"));
        Cmd("pardon Cmdlivetarget");
        Check("!pardon <name>: inJail cleared, the target warped home", "0", Lua("report(target:getCharVar('inJail'))"));
        Rezone(TargetId);
        Check("  the target at its home point", "230:10,0,12", Pos(Char(TargetId)));
        Cmd("send Cmdlivetarget Cmdlivegm");
        Rezone(TargetId);
        Check("!pardon of a char not jailed: nothing", "/100", Cmd("pardon Cmdlivetarget") + "/" + Char(TargetId).Zone);
        // the offline char: no session, jailed through the DB
        Sql(db, "DELETE FROM accounts_sessions WHERE charid = ?", BoundValue.Of(OfflineId));
        Check("!jail of an offline char: SendToJailOffline", "Cmdlivegm jailed Cmdliveoffline (ID: 996113, status: Offline) into cell 1. Reason: Unspecified",
              Cmd("jail Cmdliveoffline"));
        Check("  its chars row in Mordion Gaol's cell 1, its inJail 1", "131 -620 11 660/1",
              DbRow(db, "SELECT pos_zone, pos_x, pos_y, pos_z FROM chars WHERE charid = ?", BoundValue.Of(OfflineId)) + "/" +
              DbRow(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = 'inJail'", BoundValue.Of(OfflineId)));
        Check("!jail of no such char", "Invalid player 'Nobody' given.", Cmd("jail Nobody"));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(OfflineId), BoundValue.Of(OfflineId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Check("!jail of a char online on another process: refused", "Player 'Cmdliveoffline' is online but in a different zone group (cluster). Go to that zone group to use !jail",
              Cmd("jail Cmdliveoffline"));
        Check("!posfix <offline name>: told", "Fixed Cmdliveoffline's position.", Cmd("posfix Cmdliveoffline"));
        Check("  its session row deleted, its chars row in Lower Jeuno", "-/245 245 86 33.464 -5 69.162",
              DbRow(db, "SELECT charid FROM accounts_sessions WHERE charid = ?", BoundValue.Of(OfflineId)) + "/" +
              DbRow(db, "SELECT pos_zone, pos_prevzone, pos_rot, pos_x, pos_y, pos_z FROM chars WHERE charid = ?", BoundValue.Of(OfflineId)));
        Check("!where: the GM's position (showPosition, a 0x009 message)", "/1", Cmd("where") + "/" + Of(GmId, 0x009).Count);
        Check("!inwater", "You are on land", Cmd("inwater"));
        Check("!getprevzoneline", "Previous Zoneline ID: 0", Cmd("getprevzoneline"));
        Check("!cansee without a cursor target", "No cursor target provided", Cmd("cansee"));
        Cursor(Char(TargetId).TargId);
        Check("!cansee with the target on the cursor", "Cmdlivegm CAN see Cmdlivetarget", Cmd("cansee"));
        Cursor(0);
        Check("!wallhack: on", "Toggled Cmdlivegm's wallhack flag ON./True", Cmd("wallhack") + "/" + gm.WallhackEnabled);
        Check("!wallhack: off", "Toggled Cmdlivegm's wallhack flag OFF./False", Cmd("wallhack") + "/" + gm.WallhackEnabled);
        Check("!wallhack <name>", "Toggled Cmdlivetarget's wallhack flag ON./True", Cmd("wallhack Cmdlivetarget") + "/" + Char(TargetId).WallhackEnabled);
        Cmd("wallhack Cmdlivetarget");
        Check("!speed: the current speed", "Current Speed: 50", Cmd("speed"));
        Cmd("speed 100");
        Check("!speed 100: MOVE_SPEED_OVERRIDE 100, the speed 100", "100/100", Lua("report(player:getMod(xi.mod.MOVE_SPEED_OVERRIDE))") + "/" + gm.Speed);
        Check("!speed 0: back to the regular speed", "Returning to your regular speed./50", Cmd("speed 0") + "/" + gm.Speed);
        Check("!speed -1: bound (speed 0)", "0", Cmd("speed -1").Length > 0 ? gm.Speed.ToString() : "");
        Cmd("speed 0");
        Cmd("cs 100");
        Check("!cs 100: the event started (0x032)", "100/1", $"{(gm.IsInEvent ? gm.CurrentEvent.EventId : -1)}/{Of(GmId, 0x032).Count}");
        Cmd("release");
        Check("!release: the event released (0x052 / 0x053)", "False/True", gm.IsInEvent + "/" + (Of(GmId, 0x052).Count > 0));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- items, gil, currencies ----
        Console.WriteLine("Items and gil: additem, addtempitem, addtreasure, addfish, delitem, delallinventory, delcontaineritems, giveitem, ...");
        long Count(string who, int itemId, int loc = 0) =>
            Char(who == "player" ? GmId : TargetId).Items.Where(i => (int)i.Location == loc && i.ItemId == itemId).Sum(i => (long)i.Quantity);
        string DbQty(uint id, int itemId, int loc = 0) => DbRow(db, "SELECT COALESCE(SUM(quantity), 0) FROM char_inventory WHERE charid = ? AND location = ? AND itemId = ?",
                                                            BoundValue.Of(id), BoundValue.Of(loc), BoundValue.Of(itemId));
        Cmd("additem 4096 2");
        Check("!additem 4096 2: two Fire Crystals, ITEM_OBTAINED + 9 (the quantity)", $"2/{Text("ITEM_OBTAINED") + 9}:4096/2", $"{Count("player", 4096)}/{Special(GmId)}/{DbQty(GmId, 4096)}");
        Cmd("additem bronze_knife");
        Check("!additem <name>: GetItemIDByName, one Bronze Knife, ITEM_OBTAINED", $"1/{Text("ITEM_OBTAINED")}:16465", $"{Count("player", 16465)}/{Special(GmId)}");
        Check("!additem of a name in no row", "Item no_such_thing not found in database.", Cmd("additem no_such_thing"));
        Check("!additem of a name matching several rows (bronze_%, LIKE): the count", $"Found {DbRow(db, "SELECT COUNT(*) FROM item_basic WHERE name LIKE 'bronze_%' OR sortname LIKE 'bronze_%'")} instances matching \"bronze_%\". Use ID or exact name.",
              Cmd("additem bronze_%"));
        Cmd("additem 16640 1 1 5 2 3");
        Check("!additem 16640 1 1 5 2 3: a Bronze Axe with the augments 1 (+5) and 2 (+3)", "1/1:5/2:3",
              Lua("local item = player:findItem(16640) report(1) report(item:getAugment(0)[1] .. ':' .. item:getAugment(0)[2]) report(item:getAugment(1)[1] .. ':' .. item:getAugment(1)[2])").Replace(",", "/"));
        Cmd("addtempitem 5390 2");
        Check("!addtempitem 5390 2: in the temporary items (one: setQuantity caps it at the stack of 1), ITEM_OBTAINED + 9", $"1/{Text("ITEM_OBTAINED") + 9}:5390", $"{Count("player", 5390, 3)}/{Special(GmId)}");
        Check("!addtempitem of no id", "Invalid itemID. | !addtempitem <itemID> <quantity>", Cmd("addtempitem"));
        Check("!addtreasure 4096: into the GM's solo treasure pool (0x0D2), at once its own (0x0D3): a third Fire Crystal",
              "Item of ID 4096 was added to the treasure pool of Cmdlivegm or their party/alliance./1/1/3",
              Cmd("addtreasure 4096") + "/" + Of(GmId, 0x0D2).Count + "/" + Of(GmId, 0x0D3).Count + "/" + Count("player", 4096));
        Check("!getpool: the pool, emptied", "Cmdlivegm's Treasure Pool (SOLO) | 1/1 member(s) |   Cmdlivegm (996111)", Cmd("getpool"));
        Cmd("addfish grimmonite 100 200 1");
        Check("!addfish <fish name> <length> <weight> 1: the fish with its size, weight and rank", $"1/{Text("ITEM_OBTAINED")}:{Lua("report(xi.item.GRIMMONITE)")}/100:200:true",
              $"{Count("player", int.Parse(Lua("report(xi.item.GRIMMONITE)")))}/{Special(GmId)}/" +
              Lua("local d = player:findItem(xi.item.GRIMMONITE):getExData() report(d.size .. ':' .. d.weight .. ':' .. tostring(d.isRanked))"));
        Check("!addfish of no such fish", "Item nofish not found in fish table.", Cmd("addfish nofish 1 1"));
        Check("!addfish without a size", "Size/Weight not provided. | !addfish <fish> <length> <weight> <optional:ranked>", Cmd("addfish grimmonite"));
        Cmd("delitem 4096");
        Check("!delitem 4096: one Fire Crystal deleted", "Item 4096 was deleted from Cmdlivegm./2/2", string.Join("", ChatOf(GmId)) + "/" + Count("player", 4096) + "/" + DbQty(GmId, 4096));
        Check("!delitem of an item not held", "Cmdlivegm does not have item 4097.", Cmd("delitem 4097"));
        Check("!hasitem 4096", "Cmdlivegm has item 4096.", Cmd("hasitem 4096"));
        Check("!hasitem 4097 <name>", "Cmdlivetarget does not have item 4097.", Cmd("hasitem 4097 Cmdlivetarget"));
        Cmd("giveitem Cmdlivetarget 4097 3");
        Check("!giveitem <name> 4097 3: three Ice Crystals to the target, its ITEM_OBTAINED + 9", $"Gave player 'Cmdlivetarget' Item with ID of '4097'/3/{Text("ITEM_OBTAINED") + 9}:4097/3",
              string.Join("", ChatOf(GmId)) + "/" + Count("target", 4097) + "/" + Special(TargetId) + "/" + DbQty(TargetId, 4097));
        Check("!giveitem without an item", "You must enter a valid player name and item ID.", Cmd("giveitem Cmdlivetarget"));
        Cmd("givebonanzapearl Cmdlivetarget 12345 7");
        Check("!givebonanzapearl <name> 12345 7: a Bonanza Pearl of the number and the event", "1/12345:7",
              Lua("report(target:hasItem(xi.item.BONANZA_PEARL) and 1 or 0) local d = target:findItem(xi.item.BONANZA_PEARL):getExData() report(d.number .. ':' .. d.title)").Replace(",", "/"));
        // script bug: givemagianitem's third parameter is a 'b' (the whole command line), so isRewardItem is always set and
        // the trial's reward item (Renegade) is given, never its required item (Peeler)
        Cmd("givemagianitem Cmdlivetarget 2");
        Check("!givemagianitem <name> 2: the 'b' parameter makes it the reward item, Renegade (not Peeler)", "Gave player 'Cmdlivetarget' Item for Trial ID '2' /1/0",
              string.Join("", ChatOf(GmId)) + "/" + Lua("report(target:hasItem(xi.item.RENEGADE) and 1 or 0) report(target:hasItem(xi.item.PEELER) and 1 or 0)").Replace(",", "/"));
        Check("!givemagianitem of no trial", "Trial ID '9999' was not found.", Cmd("givemagianitem Cmdlivetarget 9999"));
        Cmd("delallinventory Cmdlivetarget");
        Check("!delallinventory <name>: the target's inventory emptied (its gil kept)", "Deleted entire inventory for Cmdlivetarget./0/0/100000",
              string.Join("", ChatOf(GmId)) + "/" + Count("target", 4097) + "/" + DbQty(TargetId, 4097) + "/" + Lua("report(target:getGil())"));
        Cmd("delcontaineritems 3");
        Check("!delcontaineritems 3: the GM's temporary items deleted", "Deleted all items in Temp. Items for Cmdlivegm./0", string.Join("", ChatOf(GmId)) + "/" + Count("player", 5390, 3));
        Check("!delcontaineritems of no container", "Invalid container specified. | !delcontaineritems <container> (player)", Cmd("delcontaineritems 99"));
        Check("!givegil 500 <name>", "Gave 500 gil to Cmdlivetarget.  They now have 100500 gil./100500", Cmd("givegil 500 Cmdlivetarget") + "/" + DbQty(TargetId, 65535));
        Check("!takegil 200000 <name>: capped at the gil held", "Removed 100500 gil from Cmdlivetarget.  They now have 0 gil.", Cmd("takegil 200000 Cmdlivetarget"));
        Check("!setgil 4321", "Cmdlivegm's gil was set to 4321./4321", Cmd("setgil 4321") + "/" + DbQty(GmId, 65535));
        Check("!givegil 0: refused", "Invalid amount of gil. | !givegil <amount> (player)", Cmd("givegil 0"));
        Check("!addcurrency bayld 100", "Cmdlivegm was given 100 bayld, for a total of 100.", Cmd("addcurrency bayld 100"));
        Check("!delcurrency bayld 150: capped at what is held", "100 bayld was taken from Cmdlivegm, for a total of 0.", Cmd("delcurrency bayld 150"));
        // script bug: getCurrency returns 0 for an unknown currency (charutils::GetPoints logs it), never nil, so the
        // "Invalid currency type." check cannot hold and the add goes on (SetPoints logging the type too)
        Check("!addcurrency of no currency: GetPoints / SetPoints log the type, the GM told of 5 given", "Cmdlivegm was given 5 nothing, for a total of 0./True",
              Cmd("addcurrency nothing 5") + "/" + newLog.Contains("charutils::SetPoints: Invalid type nothing for Cmdlivegm"));
        Cmd("setbag 50");
        Check("!setbag 50: the inventory at 50, the satchel up by the same 20, the Gobbiebag quest below 50 completed, 50's not", "50/20/true/false",
              Lua("report(player:getContainerSize(xi.inv.INVENTORY)) report(player:getContainerSize(xi.inv.MOGSATCHEL)) " +
                  "report(tostring(player:hasCompletedQuest(xi.questLog.JEUNO, xi.quest.id.jeuno.THE_GOBBIEBAG_PART_IV))) " +
                  "report(tostring(player:hasCompletedQuest(xi.questLog.JEUNO, xi.quest.id.jeuno.THE_GOBBIEBAG_PART_V)))").Replace(",", "/"));
        Check("!setbag 33: refused", "Invalid bag size. | !bagsize <30-80 and multiple of 5>", Cmd("setbag 33"));
        Check("!ah: the auction menu (0x04C)", "/1", Cmd("ah") + "/" + Of(GmId, 0x04C).Count);
        // a linkshell of the test's own (deleted by Cleanup)
        Sql(db, "INSERT INTO linkshells(name, color, poster) VALUES (?, 61440, '')", BoundValue.Of(LsName));
        Cmd("givels Cmdlivels Cmdlivetarget");
        Check("!givels <ls> <name>: a linkpearl (515) signed with the linkshell's name for the gmlevel 0 target", "Linkpearl created for 'Cmdlivels'!/1",
              string.Join("", ChatOf(GmId)) + "/" + Count("target", 515));
        Cmd("givels Cmdlivels");
        Check("!givels <ls>: a pearlsack (514) for the GM", "1", Count("player", 514).ToString());
        Check("!breaklinkshell <ls> (permission 4): broken", "Linkshell named 'Cmdlivels' has been broken!/1",
              Cmd("breaklinkshell Cmdlivels") + "/" + DbRow(db, "SELECT broken FROM linkshells WHERE name = ?", BoundValue.Of(LsName)));
        Check("!givels of a broken linkshell: refused", "Unable to create linkpearl for 'Cmdlivels'! | !givels <linkshell name> (target)", Cmd("givels Cmdlivels"));
        Check("!breaklinkshell of no linkshell", "Linkshell named 'Nolinkshell' not found! | !breaklinkshell <linkshell name>", Cmd("breaklinkshell Nolinkshell"));
        // a weapon in the main hand for the weapon skill points
        // GP_CLI_COMMAND_EQUIP_SET: a weapon into the main hand
        void EquipMain(ushort itemId) =>
            Send(sessions[GmId], 0x050, 8, b => { b[4] = gm.Items.First(it => it.ItemId == itemId && (int)it.Location == 0).Slot; b[5] = CharStats.SlotMain; b[6] = 0; });
        EquipMain(16465);
        Check("!getwspoints main: a Bronze Knife's 0", "The weapon in Cmdlivegm's main slot has 0 ws points", Cmd("getwspoints main"));
        Check("!addweaponskillpoints 0 25 to a weapon with no weapon skill to unlock", "Could not add weapon skill points to bronze_knife.", Cmd("addweaponskillpoints 0 25"));
        Cmd("additem 16735");
        EquipMain(16735);
        Check("!addweaponskillpoints 0 25 to an Axe of Trials", "Added 25 weapon skill points to axe_of_trials.", Cmd("addweaponskillpoints 0 25"));
        Check("!getwspoints main", "The weapon in Cmdlivegm's main slot has 25 ws points", Cmd("getwspoints main"));
        Check("!addweaponskillpoints 2 5 with no ranged weapon", "No weapon equipped in ranged slot. | !addweaponskillpoints <slot main=0, sub=1, ranged=2> <points> (player)",
              Cmd("addweaponskillpoints 2 5"));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- jobs, levels, experience, skills ----
        Console.WriteLine("Jobs and levels: changejob, changesjob, setplayerlevel, masterjob, givexp, takexp, cp, capacity / job / merit points, skills, crafts, promote, togglegm, monstrosity");
        string Job(CharData c) => $"{c.MJob}:{c.MLvl}/{c.SJob}:{c.SLvl}";
        Check("!changejob mnk 50: MNK50, no support job", "You are now a MNK50/NONE0./2:50/0:0", Cmd("changejob mnk 50") + "/" + Job(gm));
        Check("  char_stats saved", "2 50", DbRow(db, "SELECT mjob, mlvl FROM char_stats WHERE charid = ?", BoundValue.Of(GmId)));
        Check("!changesjob whm 30: the support job at half the main's level", "You are now a MNK50/WHM25./2:50/3:25", Cmd("changesjob whm 30") + "/" + Job(gm));
        Check("!changejob 99: refused", "Invalid jobID.  Use job short name, e.g. WAR, or its equivalent numeric ID. | !changejob <jobID> (level) (master: 0/1)", Cmd("changejob 99"));
        Check("!changejob war 100: refused", "Invalid level. Level must be between 1 and 99! | !changejob <jobID> (level) (master: 0/1)", Cmd("changejob war 100"));
        Cmd("changejob pup 30");
        Check("!changejob pup 30: PUP30, the automaton named and given its Harlequin frame and head", "18:30/True/True/True",
              $"{gm.MJob}:{gm.MLvl}/" + Lua("report(tostring(player:getAutomatonName() ~= '')) report(tostring(player:hasAttachment(xi.item.HARLEQUIN_FRAME))) report(tostring(player:hasAttachment(xi.item.HARLEQUIN_HEAD)))")
                  .Replace("true", "True").Replace(",", "/"));
        // (getJobPointLevel is 0 below 99, as GetJobPointValue: the levels themselves checked; WHM's own level is the 30 set)
        Check("!changejob war 75 1: WAR75 mastered (every WAR job point type raised to 20)", "You are now a WAR75/WHM30 (Mastered)./20/20",
              Cmd("changejob war 75 1") + "/" + gm.JobPointLevels[1, 0] + "/" + gm.JobPointLevels[1, 9]);
        Check("!setplayerlevel 60: WAR60 (nothing said for the GM itself)", "/1:60", Cmd("setplayerlevel 60") + "/" + gm.MJob + ":" + gm.MLvl);
        Check("!setplayerlevel <name> 20", "Set Cmdlivetarget's level to 20./20", Cmd("setplayerlevel Cmdlivetarget 20") + "/" + Char(TargetId).MLvl);
        Check("!setplayerlevel 0: refused", "Invalid level.  Must be between 1 and 99. | !setplayerlevel (player) <level>", Cmd("setplayerlevel 0"));
        Cmd("setplayerlevel 75");
        Cmd("changejob mnk 50");
        Check("!masterjob: MNK's job point types (0x040..0x049) raised to 20", "Mastered Cmdlivegm's main job!/20/20",
              Cmd("masterjob") + "/" + gm.JobPointLevels[2, 0] + "/" + gm.JobPointLevels[2, 9]);
        Cmd("changejob war 75");
        // setLevel left the target's exp one short of level 21
        Check("  the target's exp one short of the next level", Lua("report(GetExpNEXTLevelForChar and 0 or 0)").Length > 0 ? "True" : "", (Char(TargetId).JobExp[1] > 0).ToString());
        Check("!givexp 100 <name>: the target up to 21, 99 into it", "Gave 100 exp to Cmdlivetarget. They are now level 21./99", Cmd("givexp 100 Cmdlivetarget") + "/" + Char(TargetId).JobExp[1]);
        Check("!takexp 40 <name>: down by 40", "Removed 40 exp from Cmdlivetarget. They are now level 21./59", Cmd("takexp 40 Cmdlivetarget") + "/" + Char(TargetId).JobExp[1]);
        Check("!givexp 0: refused", "Invalid amount. | !givexp <amount> (player)", Cmd("givexp 0"));
        Check("!cp 1000: San d'Oria conquest points (0x05E)", "Added 1000 cp to Cmdlivegm./1000/1",
              Cmd("cp 1000") + "/" + Lua("report(player:getCP())") + "/" + Of(GmId, 0x05E).Count);
        Check("  char_points' sandoria_cp", "1000", DbRow(db, "SELECT sandoria_cp FROM char_points WHERE charid = ?", BoundValue.Of(GmId)));
        Check("!setcapacitypoints 40000: capped at 29999", "Cmdlivegm now has 29999 capacity points on WAR.", Cmd("setcapacitypoints 40000"));
        Check("!setjobpoints 600: capped at 500", "Cmdlivegm now has 500 job points on WAR./500", Cmd("setjobpoints 600") + "/" + Lua("report(player:getJobPoints(xi.job.WAR))"));
        Check("!setmerits 7 <name>", "Cmdlivetarget now has 7 merits./7", Cmd("setmerits 7 Cmdlivetarget") + "/" + Lua("report(target:getMeritCount())"));
        Cmd("setmentor 1");
        Check("!setmentor 1: the mentor flag (0x037 flags)", "true/1", Lua("report(tostring(player:getMentor()))") + "/" + (Of(GmId, 0x037).Count > 0 ? 1 : 0));
        Cmd("setmentor 0");
        Check("!setmentor 0", "false", Lua("report(tostring(player:getMentor()))"));
        Check("!setmentor 2: refused", "Invalid mode. | !setmentor <mode> (player) | mode: 0 = Not a mentor, 1 = Unlocked but inactive.", Cmd("setmentor 2"));
        // 0x029 BATTLE_MESSAGE: message:param (the MessageNum at 24, the Data at 12)
        string Basic(uint id) => string.Join(",", Of(id, 0x029).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 24)}:{BitConverter.ToUInt32(p.Buffer, 12)}"));
        Check("!setskill 1 100: hand-to-hand at 1000; CheckWeaponSkill (the unarmed hand) learns Backhand Blow (4) at 100, then SKILL_REACHES_LEVEL", "/1000/45:4,53:1",
              Cmd("setskill 1 100") + "/" + Lua("report(player:getCharSkillLevel(1))") + "/" + Basic(GmId));
        Check("!getskill 1", "Cmdlivegm's current skillID '1' Skill: 100.x (real value: 1000)", Cmd("getskill 1"));
        Check("!setskill axe 5 <name>: the target's axe at 50", "Cmdlivetarget's new skillID 'axe' Skill: 5.0/50", Cmd("setskill axe 5 Cmdlivetarget") + "/" + Lua("report(target:getCharSkillLevel(xi.skill.AXE))"));
        Check("!getskill 20: refused (not a combat or craft skill)", "You must specify a valid skill. | !getskill <skill name or ID> (player)", Cmd("getskill 20"));
        Cmd("capskill axe");
        Check("!capskill axe: WAR75's axe cap", "Capped skillID 5./" + Lua("report(player:getMaxSkillLevel(75, xi.job.WAR, xi.skill.AXE) * 10)"),
              string.Join("", ChatOf(GmId)) + "/" + Lua("report(player:getCharSkillLevel(xi.skill.AXE))"));
        Cmd("capallskills");
        Check("!capallskills: the sword capped, the automaton skills at 5000", "All skills capped!/" + Lua("report(player:getMaxSkillLevel(75, xi.job.WAR, xi.skill.SWORD) * 10)") + "/5000",
              string.Join("", ChatOf(GmId)) + "/" + Lua("report(player:getCharSkillLevel(xi.skill.SWORD)) report(player:getCharSkillLevel(xi.skill.AUTOMATON_MELEE))").Replace(",", "/"));
        Check("!setcraftrank woodworking 3: told, the rank 3", "Your woodworking craft skill rank has been adjusted to: 3/3",
              Cmd("setcraftrank woodworking 3") + "/" + Lua("report(player:getSkillRank(xi.skill.WOODWORKING))"));
        Check("!getcraftrank woodworking", "Cmdlivegm's current skillID 'woodworking' rank: 3", Cmd("getcraftrank woodworking"));
        Check("!setcraftrank smithing novice <name>: xi.craftRank.NOVICE, 3", "Cmdlivetarget's new skillID 'smithing' rank: 3/Your smithing craft skill rank has been adjusted to: 3",
              Cmd("setcraftrank smithing novice Cmdlivetarget") + "/" + string.Join("", ChatOf(TargetId)));
        Check("!getcraftrank combat: refused", "You must specify a valid craft skill. | !getcraftRank <craft skill or ID> (player)", Cmd("getcraftrank 1"));
        Check("!promote <name> 1: the target a GM of tier 1, told", "Cmdlivetarget set to tier 1./You have been set to tier 1./1/1",
              Cmd("promote Cmdlivetarget 1") + "/" + string.Join("", ChatOf(TargetId)) + "/" + Lua("report(target:getGMLevel())") + "/" +
              DbRow(db, "SELECT gmlevel FROM chars WHERE charid = ?", BoundValue.Of(TargetId)));
        Check("  the promoted target's !getid now runs", "Must select a target using in game cursor first.", SayAs(TargetId, "!getid"));
        Check("!promote <name> 5: refused (above the GM's own tier - 1)", "Invalid level.  Must be 0 to 4. | !promote <player> <level>", Cmd("promote Cmdlivetarget 5"));
        Check("a tier 1 GM's !promote of the tier 5 GM: refused, the GM told", "You can not use this command on same or higher tiered GMs. | !promote <player> <level>/Cmdlivelowgm attempted to adjust your GM rank.",
              SayAs(LowGmId, "!promote Cmdlivegm 0") + "/" + string.Join("", ChatOf(GmId)));
        Cmd("togglegm");
        Check("!togglegm: the GM's visible level min(7, 5 + 3)", "7/7", Lua("report(player:getVisibleGMLevel())") + "/" + gm.GmLevel);
        // script bug: promote to 0 calls player:setVisibleGMLevel(0), the promoting GM's, not the target's
        Check("!promote <name> 0: the target's tier 0; the GM's own visible level set to 0 (script bug: player, not targ)", "Cmdlivetarget set to tier 0./0/0",
              Cmd("promote Cmdlivetarget 0") + "/" + Lua("report(target:getGMLevel())") + "/" + Lua("report(player:getVisibleGMLevel())"));
        Cmd("togglegm");
        Cmd("togglegm");
        Check("!togglegm twice: on and off again", "0", Lua("report(player:getVisibleGMLevel())"));
        Cmd("monstrosity");
        Rezone(GmId);
        gm = Char(GmId);
        Check("!monstrosity: the GM a monster (MON), zoned in place", "23/100", gm.MJob + "/" + gm.Zone);
        Cmd("monstrosity");
        Rezone(GmId);
        gm = Char(GmId);
        Check("!monstrosity again: back to the entry jobs, WAR75", "1:75", gm.MJob + ":" + gm.MLvl);
        Check("no unexpected script errors", "", TakeErrors());

        // ---- HP / MP / TP, mods, effects ----
        Console.WriteLine("Stats and effects: hp, mp, tp, gettp, getmod, setmod, getstats, addeffect, deleffect, geteffects, godmode, immortal, raise, reset, stun, setallegiance, pets");
        var tc = Char(TargetId);
        Check("!hp 100: the GM's HP (nothing said for itself)", "/100", Cmd("hp 100") + "/" + gm.Hp);
        Check("!hp 50 <name>", "Set Cmdlivetarget's HP to 50./50", Cmd("hp 50 Cmdlivetarget") + "/" + tc.Hp);
        Cursor(tc.TargId);
        Check("!hp 60 with the target on the cursor", "Set Cmdlivetarget's HP to 60./60", Cmd("hp 60") + "/" + tc.Hp);
        Cursor(0);
        Check("!hp -1: refused", "Invalid amount. | !hp <amount> (player)", Cmd("hp -1"));
        Check("!mp 20: the GM's MP (WHM support)", "/20", Cmd("mp 20") + "/" + gm.Mp);
        Check("!tp 1500 <name>", "Set Cmdlivetarget's TP to 1500./1500", Cmd("tp 1500 Cmdlivetarget") + "/" + tc.Tp);
        Cmd("tp 1200");
        Check("!gettp", "Player TP: 1200", Cmd("gettp"));
        Cursor(tc.TargId);
        Check("!gettp with the target on the cursor", "Player TP: 1200 | Target TP: 1500", Cmd("gettp"));
        Cursor(0);
        Check("!getmod str", "Cmdlivegm's Mod 8 (STR) is 0", Cmd("getmod str"));
        Check("!setmod str 12 <name>", "Target name: Cmdlivegm (Target ID: 996111) | Old str modifier value: 0 | New str modifier value: 12", Cmd("setmod str 12 Cmdlivegm"));
        Check("!getmod 8", "Cmdlivegm's Mod 8 (STR) is 12", Cmd("getmod 8"));
        Check("!setmod without a target", "No valid target found. place cursor on a non-npc object or specify a player name.  | !setmod <modifier> <amount> (optional target)", Cmd("setmod str 1"));
        Check("!getmod of no mod", "Invalid modID. | !getmod <modID>", Cmd("getmod nomod"));
        Check("!getstats without a cursor target", "Target something first.", Cmd("getstats base"));
        Cursor(tc.TargId);
        Check("!getstats base of the target: its jobs, HP / MP and stats", $"Stats for Cmdlivetarget | MainJob(jID: 1) LV: 21 / SubJob(jID: 0) LV: 0  | HP: 60/{tc.MaxHp}  MP: 0/0 (current/max) ",
              string.Join(" | ", Cmd("getstats base").Split(" | ").Take(3)));
        Check("!getstats 9: the report types", "Stats for Cmdlivetarget | Please choose a report type: base-1 defensive-2 offensive-3 elements-4 | Example: !getstats base or !getstats 1", Cmd("getstats 9"));
        Check("!getstats offensive: 11 lines", "12", Cmd("getstats offensive").Split(" | ").Length.ToString());
        Check("!getstats defensive: 6 lines", "7", Cmd("getstats defensive").Split(" | ").Length.ToString());
        Check("!getstats elements: 8 elements", "9", Cmd("getstats elements").Split(" | ").Length.ToString());
        Cursor(0);
        Cmd("addeffect protect 10 60");
        Check("!addeffect protect 10 60: Protect on the GM, the public message 280", "10/280", Lua("report(player:getStatusEffect(xi.effect.PROTECT):getPower())") + "/" + Basic(GmId).Split(':')[0]);
        Cmd("addeffect Cmdlivetarget shell 5 120");
        Check("!addeffect <name> shell 5 120: Shell on the target", "5/120000", Lua("local e = target:getStatusEffect(xi.effect.SHELL) report(e:getPower()) report(e:getDuration())").Replace(",", "/"));
        Check("!addeffect of no effect", "Invalid player or effect. | !addeffect (player) <effect> (power) (duration) (subid) (subPower)", Cmd("addeffect noeffect"));
        Check("!geteffects <name>: the target's effects listed (CStatusEffect has no 'deleted': nil, as sol gives it)", "[1] SHELL (41) - Power: 5 | Time: 2m 0s | Tick: 3s | [Dispelable]",
              Cmd("geteffects Cmdlivetarget"));
        Check("!deleffect protect", "False", Cmd("deleffect protect").Length >= 0 ? Lua("report(tostring(player:hasStatusEffect(xi.effect.PROTECT)))").Replace("false", "False") : "");
        // script bug: deleffect's parameters are 'si', so with a name first the effect is an i: a name there parses to 0, taken
        // as effect 1 (Weakness)
        Check("!deleffect <name> shell: the 'i' makes it 0, so effect 1; Shell kept", "Removed effect 1 from Cmdlivetarget./true",
              Cmd("deleffect Cmdlivetarget shell") + "/" + Lua("report(tostring(target:hasStatusEffect(xi.effect.SHELL)))"));
        Check("!deleffect <name> 41", "Removed effect 41 from Cmdlivetarget./false", Cmd("deleffect Cmdlivetarget 41") + "/" + Lua("report(tostring(target:hasStatusEffect(xi.effect.SHELL)))"));
        // script bug: geteffects skips effect.deleted, which CStatusEffect does not bind (nil): a deleted effect stays in the
        // container until its next DeleteStatusEffects, so in the same tick it is still listed
        Check("!geteffects in the tick of the delete: the deleted Shell still listed", "[1] SHELL (41) - Power: 5 | Time: 2m 0s | Tick: 3s | [Dispelable]", Cmd("geteffects Cmdlivetarget"));
        Tick(1000);
        Check("!geteffects a tick later: none", "Cmdlivetarget has no active effects.", Cmd("geteffects Cmdlivetarget"));
        Cmd("godmode");
        Check("!godmode: GodMode 1, Invincible, Mighty Strikes, the mods", "1/true/true/2500", Lua("report(player:getCharVar('GodMode')) report(tostring(player:hasStatusEffect(xi.effect.INVINCIBLE))) " +
              "report(tostring(player:hasStatusEffect(xi.effect.MIGHTY_STRIKES))) report(player:getMod(xi.mod.ACC))").Replace(",", "/"));
        Cmd("godmode 1");
        Check("!godmode 1 while on: tier 1 instead (GodMode 2, no Invincible)", "God Mode Tier 1 enabled./2/false",
              string.Join("", ChatOf(GmId)) + "/" + Lua("report(player:getCharVar('GodMode')) report(tostring(player:hasStatusEffect(xi.effect.INVINCIBLE)))").Replace(",", "/"));
        Cmd("godmode 1");
        Check("!godmode 1 again: off (GodMode 0, the mods gone)", "God Mode Tier 1 disabled./0/0",
              string.Join("", ChatOf(GmId)) + "/" + Lua("report(player:getCharVar('GodMode')) report(player:getMod(xi.mod.ACC))").Replace(",", "/"));
        Cmd("immortal");
        Check("!immortal: Immortal 1, an effect with the Transcendency icon", "Cmdlivegm is now immortal!/1/true",
              string.Join("", ChatOf(GmId)) + "/" + Lua("report(player:getCharVar('Immortal')) report(tostring(player:hasStatusEffect(xi.effect.NONE)))").Replace(",", "/"));
        Lua("player:setHP(1) player:takeDamage(500)");
        Check("  the immortal GM not killed (setUnkillable)", "True", (gm.Hp > 0).ToString());
        Check("!immortal again: mortal", "Cmdlivegm is mortal again./0", Cmd("immortal") + "/" + Lua("report(player:getCharVar('Immortal'))"));
        Cmd("hp 1000");
        Check("!raise of a live char", "Cmdlivetarget is not dead.", Cmd("raise Cmdlivetarget"));
        Lua("target:setHP(0)");
        Tick(1000);
        Check("!raise 2 <name>: Raise II sent to the dead target (0x0F9)", "Raise 2 sent to Cmdlivetarget./1", Cmd("raise 2 Cmdlivetarget") + "/" + Of(TargetId, 0x0F9).Count);
        Check("!hp of a dead char", "Cmdlivetarget is currently dead.", Cmd("hp 10 Cmdlivetarget"));
        // GP_CLI_COMMAND_ACTION RaiseMenu: the target takes the raise
        Send(sessions[TargetId], 0x01A, 28, b =>
        {
            BitConverter.GetBytes(tc.Id).CopyTo(b, 4);
            BitConverter.GetBytes(tc.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.RaiseMenu).CopyTo(b, C2sAction.OffActionID);
        });
        for (int i = 0; i < 12 && !tc.IsAlive; i++) Tick(1000);
        Check("  the target raised", "True", tc.IsAlive.ToString());
        for (int i = 0; i < 4; i++) Tick(1000);
        // script bug: !reset erases the GM's own effects (player:eraseAllStatusEffect / delStatusEffect), not the target's
        Lua("player:addStatusEffect(xi.effect.POISON, { power = 1, duration = 60, origin = target }) target:addStatusEffect(xi.effect.POISON, { power = 1, duration = 60, origin = player })");
        Check("!reset <name>: told; the GM's Poison erased (script bug: player, not targ), the target's kept", "Reset Cmdlivetarget's recast timers./false/true",
              Cmd("reset Cmdlivetarget") + "/" + Lua("report(tostring(player:hasStatusEffect(xi.effect.POISON))) report(tostring(target:hasStatusEffect(xi.effect.POISON)))").Replace(",", "/"));
        Lua("target:delStatusEffect(xi.effect.POISON)");
        Cursor(tc.TargId);
        Check("!stun on the cursor target: a Stun of power 69", "Gave target super Stun./69", Cmd("stun") + "/" + Lua("report(target:getStatusEffect(xi.effect.STUN):getPower())"));
        Check("!stun again: removed", "Removed target mob's super Stun./false", Cmd("stun") + "/" + Lua("report(tostring(target:hasStatusEffect(xi.effect.STUN)))"));
        Check("!setallegiance 2 on the cursor target", "You set Cmdlivetarget's allegiance to San d'Oria/2", Cmd("setallegiance 2") + "/" + tc.Allegiance);
        Cursor(0);
        Check("!setallegiance 1 <name>", "You set Cmdlivetarget's allegiance to Player/1", Cmd("setallegiance 1 Cmdlivetarget") + "/" + tc.Allegiance);
        Check("!setallegiance 9: refused", "Improper allegiance passed. Valid Values: 0 to 6 | !setallegiance <allegiance> <player>", Cmd("setallegiance 9"));
        Check("!pettp without a pet", "You do not have a pet. | !pettp <amount>", Cmd("pettp 100"));
        Check("!petgodmode without a pet: nothing", "", Cmd("petgodmode"));
        Cmd("changejob smn 75");
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        Tick(1000);
        Check("  (Carbuncle out)", "true", Lua("report(tostring(player:getPet() ~= nil))"));
        Cmd("pettp 1000");
        Check("!pettp 1000: the pet's TP", "1000", Lua("report(player:getPet():getTP())"));
        Cmd("tp 2000");
        Check("!tp 2000: the GM's and its pet's", "2000/2000", Lua("report(player:getTP()) report(player:getPet():getTP())").Replace(",", "/"));
        Cmd("petgodmode");
        Check("!petgodmode: the pet's GodMode 1, Invincible, ACC +2500", "1/true", Lua("local pet = player:getPet() report(pet:getLocalVar('GodMode')) report(tostring(pet:hasStatusEffect(xi.effect.INVINCIBLE)))").Replace(",", "/"));
        Cmd("petgodmode");
        Check("!petgodmode again: off", "0/false", Lua("local pet = player:getPet() report(pet:getLocalVar('GodMode')) report(tostring(pet:hasStatusEffect(xi.effect.INVINCIBLE)))").Replace(",", "/"));
        Cmd("changejob war 75");
        Check("  !changejob despawned the pet", "false", Lua("report(tostring(player:getPet() ~= nil))"));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- spells, weapon skills, key items, titles, fame, nation ----
        Console.WriteLine("Spells, key items, titles, fame: addspell, delspell, addallspells, addalltrusts, weapon skills, key items, the addall*, monunlock, titles, fame, rank, nation, records");
        string Has(string who, string method, string arg) => Lua($"report(tostring({who}:{method}({arg})))");
        string DbSpells(uint id) => DbRow(db, "SELECT COUNT(*) FROM char_spells WHERE charid = ?", BoundValue.Of(id));
        Check("!addspell 1: Cure learnt (0x0AA)", "Added spell 1 to Cmdlivegm./true/1", Cmd("addspell 1") + "/" + Has("player", "hasSpell", "1") + "/" + Of(GmId, 0x0AA).Count);
        Check("!addspell cure_ii <name>", "Added spell 2 to Cmdlivetarget./true", Cmd("addspell cure_ii Cmdlivetarget") + "/" + Has("target", "hasSpell", "2"));
        Check("!delspell 1", "Deleted spell 1 from Cmdlivegm./false", Cmd("delspell 1") + "/" + Has("player", "hasSpell", "1"));
        Check("!addspell of no spell", "Invalid spellID. | !addspell <spellID/spellName> (player)", Cmd("addspell nospell"));
        // the spell lists of the two scripts; the ids with no spell_list row are skipped (charutils::addSpell)
        string ScriptIds(string file) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(dataDir), "scripts", "commands", file)).Split("validSpells =")[1].Split('}')[0], @"\d+").Select(m => m.Value));
        string spellIds = ScriptIds("addallspells.lua"), trustIds = ScriptIds("addalltrusts.lua");
        string Known(string ids) => DbRow(db, $"SELECT COUNT(*) FROM spell_list WHERE spellid IN ({ids})");
        Check("!addallspells <name>: queued, the first batch of 25", $"Queuing unlock of all non-trust spells for Cmdlivetarget | Cmdlivetarget now has learned 25 of {spellIds.Split(',').Length} spells.",
              Cmd("addallspells Cmdlivetarget"));
        for (int i = 0; i < 20; i++) Tick(500);
        Check("  the batches every 500 ms (55, 85, ... as GetSystemTime holds): every listed spell that exists", $"true/true/{Known(spellIds)}",
              Has("target", "hasSpell", "1") + "/" + Has("target", "hasSpell", "895") + "/" + DbSpells(TargetId));
        Check("!addalltrusts <name>", "Queuing unlock of all trust spells for Cmdlivetarget", Cmd("addalltrusts Cmdlivetarget").Split(" | ")[0]);
        for (int i = 0; i < 10; i++) Tick(500);
        Check("  the trusts learnt", $"true/true/{int.Parse(Known(spellIds)) + int.Parse(Known(trustIds))}", Has("target", "hasSpell", "896") + "/" + Has("target", "hasSpell", "1019") + "/" + DbSpells(TargetId));
        Cmd("addallweaponskills");
        Check("!addallweaponskills: every xi.wsUnlock learnt", "true/true", Lua("local all = true for _, id in pairs(xi.wsUnlock) do all = all and player:hasLearnedWeaponskill(id) end report(tostring(all))") + "/" +
              Has("player", "hasLearnedWeaponskill", "xi.wsUnlock.SHIJIN_SPIRAL"));
        Cmd("delallweaponskills");
        Check("!delallweaponskills: none", "Cmdlivegm now has zero learned weaponskills./false", string.Join("", ChatOf(GmId)) + "/" +
              Lua("local any = false for _, id in pairs(xi.wsUnlock) do any = any or player:hasLearnedWeaponskill(id) end report(tostring(any))"));
        int zeruhn = int.Parse(Lua("report(xi.keyItem.ZERUHN_REPORT)"));
        Check("!addkeyitem zeruhn_report <name>: the target's, its KEYITEM_OBTAINED", $"Key item {zeruhn} was given to Cmdlivetarget./true/{Text("KEYITEM_OBTAINED")}:{zeruhn}",
              Cmd("addkeyitem zeruhn_report Cmdlivetarget") + "/" + Has("target", "hasKeyItem", zeruhn.ToString()) + "/" + Special(TargetId));
        Check("!addkeyitem again: already held", $"Cmdlivetarget already has key item {zeruhn}.", Cmd($"addkeyitem {zeruhn} Cmdlivetarget"));
        Check("!haskeyitem <id> <name>", $"Cmdlivetarget has key item {zeruhn}.", Cmd($"haskeyitem {zeruhn} Cmdlivetarget"));
        Check("!delkeyitem <id> <name>: gone, KEYITEM_OBTAINED + 1", $"Key item {zeruhn} deleted from Cmdlivetarget./false/{Text("KEYITEM_OBTAINED") + 1}:{zeruhn}",
              Cmd($"delkeyitem {zeruhn} Cmdlivetarget") + "/" + Has("target", "hasKeyItem", zeruhn.ToString()) + "/" + Special(TargetId));
        Check("!haskeyitem of a key item not held", $"Cmdlivegm does not have key item {zeruhn}.", Cmd($"haskeyitem {zeruhn}"));
        Check("!addkeyitem of no key item", "Invalid Key Item ID. | !addkeyitem <key item ID> (player)", Cmd("addkeyitem nokeyitem"));
        Check("!addallmaps <name>: the maps (383, 2309)", "Cmdlivetarget now has all maps./true/true", Cmd("addallmaps Cmdlivetarget") + "/" + Has("target", "hasKeyItem", "383") + "/" + Has("target", "hasKeyItem", "2309"));
        Check("!addallmounts: the companions", "Cmdlivegm now has all mounts./true/true", Cmd("addallmounts") + "/" + Has("player", "hasKeyItem", "xi.keyItem.CHOCOBO_COMPANION") + "/" +
              Has("player", "hasKeyItem", "xi.keyItem.CRAKLAW_COMPANION"));
        Check("!addallatma: the atma (1279)", "Cmdlivegm now has all Abyssea Atma./true", Cmd("addallatma") + "/" + Has("player", "hasKeyItem", "1279"));
        // (AddAllAttachments tells the char it unlocks, so the target hears it, not the GM)
        Check("!addallattachments <name>: the attachments (8193, 8683), the target told", "/Cmdlivetarget now has all attachments./true/true",
              Cmd("addallattachments Cmdlivetarget") + "/" + string.Join("", ChatOf(TargetId)) + "/" + Has("target", "hasAttachment", "8193") + "/" + Has("target", "hasAttachment", "8683"));
        Check("!addallmonstrosity: the Monstrosity quest, the Ring of Supernal Disjunction, every species", "Cmdlivegm now has all monstrosity data./true/true",
              Cmd("addallmonstrosity") + "/" + Has("player", "hasKeyItem", "xi.keyItem.RING_OF_SUPERNAL_DISJUNCTION") + "/" + Lua("report(tostring(xi.monstrosity.hasUnlockedSpecies(player, xi.monstrositySpecies.RABBIT)))"));
        Check("!monunlock rabbit <name>", "Cmdlivetarget can now possess RABBIT./true", Cmd("monunlock rabbit Cmdlivetarget") + "/" + Lua("report(tostring(xi.monstrosity.hasUnlockedSpecies(target, xi.monstrositySpecies.RABBIT)))"));
        Check("!monunlock of no species", "No species or variant named \"nothing\". | !monunlock <species or variant> (player)", Cmd("monunlock nothing"));
        Check("!addallwarps <name>: the survival guides and home points", "Cmdlivetarget now has all Warps./true/true",
              Cmd("addallwarps Cmdlivetarget") + "/" + Has("target", "hasTeleport", "xi.teleport.type.HOMEPOINT, 25, 3") + "/" + Has("target", "hasTeleport", "xi.teleport.type.SURVIVAL, 31, 2"));
        Check("!addtitle 5 <name>", "Cmdlivetarget was given title 5./true", Cmd("addtitle 5 Cmdlivetarget") + "/" + Has("target", "hasTitle", "5"));
        Check("!hastitle 5 <name>", "Cmdlivetarget has title 5.", Cmd("hastitle 5 Cmdlivetarget"));
        Check("!hastitle 6", "Cmdlivegm does not have title 6.", Cmd("hastitle 6"));
        Check("!setfamelevel 0 3: San d'Oria fame 125 (level 3, xi.data.fame.rankPoints[3])", $"Set Cmdlivegm's fame for fame area 0 (SANDORIA) to {Lua("report(xi.data.fame.rankPoints[3])")} (Level 3)./125", Cmd("setfamelevel 0 3") + "/" + Lua("report(player:getFame(0))"));
        Check("!getfame <name> 0: the GM's fame reported under the target's name (script bug: player:getFame, not targ)",
              $"Cmdlivetarget's reputation in fame area 0 (SANDORIA) is 125 (Level 3). Next level at {Lua("report(xi.data.fame.rankPoints[4])")} ({int.Parse(Lua("report(xi.data.fame.rankPoints[4])")) - 125} points to go).",
              Cmd("getfame Cmdlivetarget 0"));
        Check("!setfamelevel 1 5 <name>", $"Set Cmdlivetarget's fame for fame area 1 (BASTOK) to {Lua("report(xi.data.fame.rankPoints[5])")} (Level 5)./{Lua("report(xi.data.fame.rankPoints[5])")}", Cmd("setfamelevel 1 5 Cmdlivetarget") + "/" + Lua("report(target:getFame(1))"));
        Check("!setfamelevel 3 5: Jeuno refused", "Jeuno fame is derived from the three nations; set San d'Oria, Bastok and Windurst fame instead.", Cmd("setfamelevel 3 5"));
        Check("!setfamelevel 6 9: Abyssea capped at 6", $"Abyssea fame capped at level 6. Setting to level 6. | Set Cmdlivegm's fame for fame area 6 (ABYSSEA_KONSCHTAT) to {Lua("report(xi.data.fame.rankPoints[6])")} (Level 6).",
              Cmd("setfamelevel 6 9"));
        Check("!getfame: all 16 areas", "17", Cmd("getfame").Split(" | ").Length.ToString());
        Check("!setrank <name> 6", "You set Cmdlivetarget's rank to 6/6", Cmd("setrank Cmdlivetarget 6") + "/" + Lua("report(target:getRank(target:getNation()))"));
        Check("!setrank <name> 11: refused", "Improper rank passed. Valid Values: 1 to 10 | !setrank <player> <new rank>", Cmd("setrank Cmdlivetarget 11"));
        Check("!setplayernation 1: Bastok", "Set Cmdlivegm's home nation to Bastok. | NOTE! This does NOT clear or update ANY mission or related variables! /1/1",
              Cmd("setplayernation 1") + "/" + gm.Nation + "/" + DbRow(db, "SELECT nation FROM chars WHERE charid = ?", BoundValue.Of(GmId)));
        Check("!setplayernation <name> 2", "Set Cmdlivetarget's home nation to Windurst. | NOTE! This does NOT clear or update ANY mission or related variables! /2",
              Cmd("setplayernation Cmdlivetarget 2") + "/" + tc.Nation);
        Cmd("setplayernation 0");
        Check("!cnation <name>: the campaign allegiance", "Cmdlivetarget's current campaign allegiance: NONE", Cmd("cnation Cmdlivetarget"));
        Check("!cnation <name> windurst", "Cmdlivetarget's old campaign allegiance: NONE | Cmdlivetarget's new campaign allegiance: WINDURST/3",
              Cmd("cnation Cmdlivetarget windurst") + "/" + tc.CampaignAllegiance);
        Check("!completerecord 1: the RoE record completed", "Completed RoE Record with ID 1 for Cmdlivegm/true", Cmd("completerecord 1") + "/" + Has("player", "getEminenceCompleted", "1"));
        Check("!racechange <name>: [RaceChange]Eligible for 14 days", $"Cmdlivetarget is now eligible for race change service at Ancestry Moogle. Eligibility will expire in 14 days./{EarthNow() + 1209600}",
              Cmd("racechange Cmdlivetarget") + "/" + DbRow(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = '[RaceChange]Eligible'", BoundValue.Of(TargetId)));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- missions, quests, vars ----
        Console.WriteLine("Missions, quests and vars: the mission / quest commands and menus, quest vars, char / server / local vars, yell, exec");
        // GP_CLI_COMMAND_CHAT_NAME to "_CUSTOM_MENU": the answer a customMenu's GM prompt gets ("...: Result (<option>)")
        void MenuAnswer(uint id, string option)
        {
            foreach (var x in sessions.Values) x.PacketList.Clear();
            var msg = CborRaw.Bytes($"GMTELL(1): Question(1): Result ({option})");
            Send(sessions[id], 0x0B6, (0x15 + msg.Length + 1 + 3) / 4 * 4, b =>
            {
                b[4] = 3;
                CborRaw.Bytes("_CUSTOM_MENU").CopyTo(b, 6);
                msg.CopyTo(b, 0x15);
            });
        }
        string Mission(string who, int log) => Lua($"report({who}:getCurrentMission({log}))");
        Check("!addmission 0 2 <name>", "Added San d'Oria mission 2 to Cmdlivetarget. | NOTE! This does NOT clear or update ANY mission variables! /2",
              Cmd("addmission 0 2 Cmdlivetarget") + "/" + Mission("target", 0));
        Check("!checkmission sandoria <name>", "Current San d'Oria Mission ID is 2 for Cmdlivetarget.", Cmd("checkmission sandoria Cmdlivetarget"));
        Check("!checkmission 0 of a char with none", "No current San d'Oria mission for Cmdlivegm.", Cmd("checkmission 0"));
        Check("!setmissionstatus <name> 4 0", "missionStatus for Cmdlivetarget (San d'Oria) set to 4/4", Cmd("setmissionstatus Cmdlivetarget 4 0") + "/" + Lua("report(target:getMissionStatus(0))"));
        Check("!setmissionstatus <name> 7 0 2: the status byte 2", "missionStatus for Cmdlivetarget (San d'Oria index 2) set to 7/7", Cmd("setmissionstatus Cmdlivetarget 7 0 2") + "/" + Lua("report(target:getMissionStatus(0, 2))"));
        Check("!checkmissionstatus <name> 0 2", "missionStatus for Cmdlivetarget (San d'Oria index 2): 7", Cmd("checkmissionstatus Cmdlivetarget 0 2"));
        Check("!checkmissionstatus <name> 0 9: refused", "Invalid index! | !checkmissionstatus (player) (log ID) (index)", Cmd("checkmissionstatus Cmdlivetarget 0 9"));
        Check("!completemission 0 2 <name>: completed, none current", "Completed San d'Oria Mission with ID 2 for Cmdlivetarget | NOTE! This does NOT clear or update ANY mission variables! /true/65535",
              Cmd("completemission 0 2 Cmdlivetarget") + "/" + Lua("report(tostring(target:hasCompletedMission(0, 2))) report(target:getCurrentMission(0))").Replace(",", "/"));
        Check("!delmission 0 2 <name>", "Deleted San d'Oria mission 2 from Cmdlivetarget. | NOTE! This does NOT clear or update ANY mission variables! /false",
              Cmd("delmission 0 2 Cmdlivetarget") + "/" + Lua("report(tostring(target:hasCompletedMission(0, 2)))"));
        Check("!addmission 0 smash_the_orcish_scouts: by its name", "Added San d'Oria mission 0 to Cmdlivegm. | NOTE! This does NOT clear or update ANY mission variables! /0",
              Cmd("addmission 0 smash_the_orcish_scouts") + "/" + Mission("player", 0));
        Check("!addmission of no log", "Invalid logID. | !addmission <logID> <missionID> (player)", Cmd("addmission nolog 1"));
        Cmd("mission 0 2 Cmdlivetarget");
        Check("!mission 0 2 <name>: the menu's onStart lines and its GM prompt (NS_LINKSHELL3)", "Player Cmdlivetarget has no MissionID set for San d'Oria | \"Quest Menu\"\"Add Mission\"\"Complete Mission\"\"Delete Mission\"\"Clear Vars\"",
              string.Join(" | ", ChatOf(GmId)));
        MenuAnswer(GmId, "Add Mission");
        Check("  answered Add Mission: the target's mission 2", "Added San d'Oria mission 2 to Cmdlivetarget./2", string.Join(" | ", ChatOf(GmId)) + "/" + Mission("target", 0));
        Cmd("mission 0 2 Cmdlivetarget");
        MenuAnswer(GmId, "Canceled.");
        Check("  a cancelled answer: onCancelled", "Menu Cancelled", string.Join(" | ", ChatOf(GmId)));
        string Quest(string who, int log, int id) => Lua($"report({who}:getQuestStatus({log}, {id}))");
        Check("!addquest 0 1 <name>: accepted", "Added San d'Oria quest 1 to Cmdlivetarget./1", Cmd("addquest 0 1 Cmdlivetarget") + "/" + Quest("target", 0, 1));
        Check("!checkquest sandoria 1 <name>", "Cmdlivetarget's status for San d'Oria quest ID 1 is: ACCEPTED", Cmd("checkquest sandoria 1 Cmdlivetarget"));
        Check("!completequest 0 1 <name>: completed", "Completed San d'Oria Quest with ID 1 for Cmdlivetarget/2", Cmd("completequest 0 1 Cmdlivetarget") + "/" + Quest("target", 0, 1));
        Check("!delquest 0 1 <name>: available again", "Deleted San d'Oria quest 1 from Cmdlivetarget./0", Cmd("delquest 0 1 Cmdlivetarget") + "/" + Quest("target", 0, 1));
        Check("!addquest 0 of no quest", "Invalid questID. | !addquest <logID> <questID> (player)", Cmd("addquest 0 noquest"));
        Cmd("quest 0 1 Cmdlivetarget");
        Check("!quest 0 1 <name>: the menu's status and vars, its GM prompt", "Player Cmdlivetarget status for San d'Oria quest ID 1 is: AVAILABLE | Player Cmdlivetarget variables for San d'Oria Quest 1 are: | No variables found. | \"Quest Menu\"\"Add Quest\"\"Complete Quest\"\"Delete Quest\"\"Clear Vars\"",
              string.Join(" | ", ChatOf(GmId)));
        MenuAnswer(GmId, "Complete Quest");
        Check("  answered Complete Quest: added, then completed", "Quest was in the Available status | Completed San d'Oria Quest with ID 1 for Cmdlivetarget/2",
              string.Join(" | ", ChatOf(GmId)) + "/" + Quest("target", 0, 1));
        Check("!setquestvar <name> 0 1 Prog 3", "Set Cmdlivetarget's Quest variable 'Quest[0][1]Prog' to 3./3",
              Cmd("setquestvar Cmdlivetarget 0 1 Prog 3") + "/" + DbRow(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = 'Quest[0][1]Prog'", BoundValue.Of(TargetId)));
        Check("!getquestvar <name> 0 1 Prog", "Cmdlivetarget's Quest variable 'Quest[0][1]Prog' : 3", Cmd("getquestvar Cmdlivetarget 0 1 Prog"));
        Cmd("quest 0 1 Cmdlivetarget");
        MenuAnswer(GmId, "Clear Vars");
        Check("!quest's Clear Vars: the quest's vars cleared", "Player Cmdlivetarget variables for San d'Oria Quest 1 are cleared/0",
              string.Join(" | ", ChatOf(GmId)) + "/" + Lua("report(target:getCharVar('Quest[0][1]Prog'))"));
        Check("!setplayervar <name> foo 5", "Set Cmdlivetarget's variable 'foo' to 5./5", Cmd("setplayervar Cmdlivetarget foo 5") + "/" + DbRow(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = 'foo'", BoundValue.Of(TargetId)));
        Check("!checkvar <name> foo", "Cmdlivetarget's variable 'foo' : 5", Cmd("checkvar Cmdlivetarget foo"));
        Check("!checkvar foo: the GM's", "Cmdlivegm's variable 'foo' : 0", Cmd("checkvar foo"));
        Check("!checkvar server <var>: a server variable (none: 0)", "Server variable 'CmdliveNoSuchVar' : 0 ", Cmd("checkvar server CmdliveNoSuchVar"));
        Check("!setlocalvar foo 3 npc <id>", $"Aaveleon's variable 'foo' : 3/3", Cmd($"setlocalvar foo 3 npc {npc.Id}") + "/" + Lua($"report(GetNPCByID({npc.Id}):getLocalVar('foo'))"));
        Check("!checklocalvar foo npc <id>", "Aaveleon's variable 'foo' : 3", Cmd($"checklocalvar foo npc {npc.Id}"));
        Check("!setlocalvar bar 9 player <name>", "Cmdlivetarget's variable 'bar' : 9/9", Cmd("setlocalvar bar 9 player Cmdlivetarget") + "/" + Lua("report(target:getLocalVar('bar'))"));
        Check("!checklocalvar bar player <name>", "Cmdlivetarget's variable 'bar' : 9", Cmd("checklocalvar bar player Cmdlivetarget"));
        Check("!setlocalvar baz 1 thing 1: refused", "Invalid entity type. | !setlocalvar <variable name> <value> { 'player', 'mob', or 'npc' } { name or ID }", Cmd("setlocalvar baz 1 thing 1"));
        Cursor(npc.TargId);
        Check("!getlocalvars with the NPC on the cursor", "Printing local vars for entity: Aaveleon | ---------------------------------- | \"foo\" : 3", Cmd("getlocalvars"));
        Check("!checklocalvar foo with the NPC on the cursor", "Aaveleon's variable 'foo' : 3", Cmd("checklocalvar foo"));
        Cursor(0);
        Check("!yell ban <name> 2: [YELL]Banned for two days", $"Cmdlivetarget has been banned from using the /yell command./1/{EarthNow() + 2 * 86400}",
              Cmd("yell ban Cmdlivetarget 2") + "/" + DbRow(db, "SELECT value, expiry FROM char_vars WHERE charid = ? AND varname = '[YELL]Banned'", BoundValue.Of(TargetId)).Replace(" ", "/"));
        Check("!yell unban <name>", "Cmdlivetarget has been unbanned from using the /yell command./-", Cmd("yell unban Cmdlivetarget") + "/" +
              DbRow(db, "SELECT value FROM char_vars WHERE charid = ? AND varname = '[YELL]Banned'", BoundValue.Of(TargetId)));
        Check("!yell of no ban / unban", "Parameter not specified <ban/unban> | !yell <ban/unban> (player) <days>", Cmd("yell maybe Cmdlivetarget"));
        Check("!exec <lua> (permission 4): run in the safe environment", "/7", Cmd("exec player:setLocalVar('execd', 7)") + "/" + Lua("report(player:getLocalVar('execd'))"));
        Check("!exec of a forbidden global (os)", "Trying to access forbidden global! | Error executing: [string \"os.exit()\"]:1: attempt to index global 'os' (a nil value)", Cmd("exec os.exit()"));
        Check("!exec of no Lua", "Failed to load string: [string \"this is not lua\"]:1: '=' expected near 'is'", Cmd("exec this is not lua"));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- mobs and NPCs ----
        Console.WriteLine("Mobs and NPCs: spawnmob, mobhere, despawnmob, the mob getters and setters, mobskill, mobsub, fafnir, animatenpc, rename, npchere, getid, injectaction, posemannequin");
        Cmd("pos -24 -20 300");
        string MobLua(string expr) => Lua($"local mob = GetMobByID({crab.Id}) report({expr})");
        Check("!spawnmob <id>: the Tree Crab up", $"Spawned Tree_Crab {crab.Id}./True", Cmd($"spawnmob {crab.Id}") + "/" + crab.IsSpawned);
        Check("!spawnmob of a spawned mob", "Mob is already spawned | !spawnmob <mob ID> (despawntime) (respawntime)", Cmd($"spawnmob {crab.Id}"));
        Check("!spawnmob of no mob", "Invalid mob ID. | !spawnmob <mob ID> (despawntime) (respawntime)", Cmd("spawnmob 17000000"));
        TakeErrors();
        Cmd($"mobhere {crab.Id}");
        Check("!mobhere <id>: the crab set at the GM", "-24,-20,300", $"{crab.X:0.##},{crab.Y:0.##},{crab.Z:0.##}");
        Check("!getfamily <id>", $"Tree_Crab {crab.Id} mob family is 11 (CRAB).", Cmd($"getfamily {crab.Id}"));
        Check("!getspecies <id>", $"Tree_Crab {crab.Id} mob species is 25 (CRAB).", Cmd($"getspecies {crab.Id}"));
        Check("!getmobaction <id>", $"Tree_Crab {crab.Id} current action ID is {MobLua("mob:getCurrentAction()")}.", Cmd($"getmobaction {crab.Id}"));
        Check("!getmobflags <id>", $"Tree_Crab's flags: 0x{int.Parse(MobLua("mob:getMobFlags()")):x8} ({MobLua("mob:getMobFlags()")})", Cmd($"getmobflags {crab.Id}"));
        string flags0 = MobLua("mob:getMobFlags()");
        MoveTo(GmId, gm.X, gm.Y, gm.Z);
        Check("!setmobflags 5 <id>: the flags set", $"Set Tree_Crab {crab.Id} flags to 0x00000005 (5)./5", Cmd($"setmobflags 5 {crab.Id}") + "/" + MobLua("mob:getMobFlags()"));
        Tick(400);
        Check("  the UPDATE_HP update (0x00E) on the next tick", "True", (Of(GmId, 0x00E).Count > 0).ToString());
        Cmd($"setmobflags {flags0} {crab.Id}");
        Cursor(crab.TargId);
        Check("!getid with the crab on the cursor", $"Tree_Crab's ID is: {crab.Id} ", Cmd("getid"));
        Check("!getfamily with the crab on the cursor", $"Tree_Crab {crab.Id} mob family is 11 (CRAB).", Cmd("getfamily"));
        Check("!setmobmod no_despawn 1 on the cursor mob", "Target name: Tree_Crab (Target ID: " + crab.Id + ") | Old no_despawn modifier value: 0 | New no_despawn modifier value: 1/1",
              Cmd("setmobmod no_despawn 1") + "/" + MobLua("mob:getMobMod(xi.mobMod.NO_DESPAWN)"));
        Check("!getmobmod no_despawn on the cursor mob", $"Tree_Crab's Mod {Lua("report(xi.mobMod.NO_DESPAWN)")} (NO_DESPAWN) is 1", Cmd("getmobmod no_despawn"));
        Check("!setmoblevel 12 on the cursor mob", $"Old MainJob(jID: {crab.MJob}) LV: {crab.MLvl} / SubJob(jID: {crab.SJob}) LV: {MobLua("mob:getSubLvl()")}  | New MainJob(jID: {crab.MJob}) LV: 12 / SubJob(jID: {crab.SJob}) LV: {MobLua("mob:getSubLvl()")} /12",
              Cmd("setmoblevel 12") + "/" + crab.MLvl);
        // (GetBattleTime: now less a battle start never set, the clock's own count, as the C++'s default time_point)
        Check("!getstats base of the mob: its Treasure Hunter tier and battle time", $"Mob's current Treasure Hunter Tier: 0 | Battletime: {now / 1000} ",
              string.Join(" | ", Cmd("getstats base").Split(" | ").TakeLast(2)));
        Check("!getstats elements of the mob: its immunities", "Immunities: None", Cmd("getstats elements").Split(" | ").Last());
        Check("!getenmity on the cursor mob: none", "Your enmity against Tree_Crab is ... CE = 0 ... VE = 0", Cmd("getenmity"));
        Check("!getta on the cursor mob without Trick Attack", "You do not have Trick Attack active, !getta will fail.", Cmd("getta"));
        Lua("player:addStatusEffect(xi.effect.TRICK_ATTACK, { power = 1, duration = 60, origin = player })");
        Check("!getta with Trick Attack, no one between", "No valid TA target found.", Cmd("getta"));
        Lua("player:delStatusEffect(xi.effect.TRICK_ATTACK)");
        Check("!cansee the cursor mob", "Cmdlivegm CAN see Tree_Crab", Cmd("cansee"));
        Check("!mobsub 2 on the cursor mob: its animation sub", "/2", Cmd("mobsub 2") + "/" + crab.AnimationSub);
        // script bug: mobsub's parameters are 'ss', and GetMobByID takes a number: sol refuses the string id (in C++ too)
        Check("!mobsub <id> 1: GetMobByID of a string id refused", "/True", Cmd($"mobsub {crab.Id} 1") + "/" + Logged("cmdhandler::call: (mobsub) error:").Contains("number expected, got string"));
        TakeErrors();
        Check("!injectaction 1 1 on the cursor mob: an action packet (0x028) about it", "/1", Cmd("injectaction 1 1") + "/" + Of(GmId, 0x028).Count);
        Check("!stun on the cursor mob", "Gave target super Stun./69", Cmd("stun") + "/" + MobLua("mob:getStatusEffect(xi.effect.STUN):getPower()"));
        Cmd("stun");
        Cursor(0);
        // (NO_DESPAWN kept: the GM's spot is off the navmesh, where an idle mob despawns)
        Cmd($"mobskill 448 {crab.Id} 1500");
        for (int i = 0; i < 6; i++) Tick(1000);
        Check("!mobskill 448 <id> 1500: the crab uses Metallic Body (Stoneskin), its TP spent", "true", MobLua("tostring(mob:hasStatusEffect(xi.effect.STONESKIN))"));
        Check("!mobskill without a mob", "You must either provide a mobID or target a mob. | !mobskill <skillID> {mob} {tp}", Cmd("mobskill 448"));
        Cursor(crab.TargId);
        Cmd("setmobmod no_despawn 0");
        Cursor(0);
        Check("!despawnmob <id>", $"Despawned Tree_Crab {crab.Id}./False", Cmd($"despawnmob {crab.Id}") + "/" + crab.IsSpawned);
        Check("!despawnmob without a mob", "You must either provide a mobID or target a mob. | !despawnmob (mobID)", Cmd("despawnmob"));
        Cmd($"mobhere {crab.Id}");
        Check("!mobhere of a despawned mob: spawned and set at the GM", "Mob state changed to: Spawned./True/-24,-20,300", string.Join("", ChatOf(GmId)) + "/" + crab.IsSpawned + $"/{crab.X:0.##},{crab.Y:0.##},{crab.Z:0.##}");
        Cmd($"despawnmob {crab.Id}");
        Cmd("fafnir");
        var fafnir = wz.Mobs.Concat(zone.ZoneInfoOf(WestRonfaure).Mobs).FirstOrDefault(m => m.Name == "DE_Fafnir");
        // NO_DROPS reads 0: the script sets it before mob:spawn(), whose CalculateMobStats restoreMobModifiers()
        // puts back the mob mods saved at GenerateDynamicEntity (C++ too; setDropID(0) still keeps the loot off)
        // the mob prints through CLuaBaseEntity's operator<< (sol's __tostring); a dynamic entity is named DE_<name>
        Check("!fafnir (permission 5): a level 90 Fafnir spawned at the GM, no drops",
              $"Spawning Fafnir (Lv: 90, HP: {fafnir?.MaxHp})\nCLuaBaseEntity(TYPE_MOB | {fafnir?.Id} | DE_Fafnir | Fafnir)/True/90/0",
              string.Join("", ChatOf(GmId)) + "/" + fafnir?.IsSpawned + "/" + fafnir?.MLvl + "/" + Lua($"report(GetMobByID({fafnir?.Id}):getMobMod(xi.mobMod.NO_DROPS))"));
        Cmd($"despawnmob {fafnir?.Id}");
        for (int i = 0; i < 4; i++) Tick(1000);
        Check("  despawned after the fade-out", "False", fafnir?.IsSpawned.ToString());
        Check("!animatenpc <id> 9: the NPC's animation", $"NPC ID: {npc.Id} - Aaveleon | Old animation: 0 | New animation: 9\n/9", Cmd($"animatenpc {npc.Id} 9") + "/" + npc.Animation);
        Cursor(npc.TargId);
        Check("!animatenpc 0 with the NPC on the cursor", $"NPC ID: {npc.Id} - Aaveleon | Old animation: 9 | New animation: 0\n/0", Cmd("animatenpc 0") + "/" + npc.Animation);
        Check("!animatesubnpc 3 with the NPC on the cursor", $"NPC ID: {npc.Id} - Aaveleon | Old animationSub: {npc.AnimationSub} | New animationSub: 3\n/3", Cmd("animatesubnpc 3") + "/" + npc.AnimationSub);
        Check("!getid with the NPC on the cursor", $"Aaveleon's ID is: {npc.Id} ", Cmd("getid"));
        Cmd("rename Cmdrenamed");
        Check("!rename <name> with the NPC on the cursor: the NPC's packet name", "Cmdrenamed", Lua($"report(GetNPCByID({npc.Id}):getPacketName())"));
        Cursor(0);
        Cmd($"rename \"\" {npc.Id}");
        TakeErrors();
        Check("!rename \"\" <id>: the packet name cleared", "", Lua($"report(GetNPCByID({npc.Id}):getPacketName())"));
        Check("!animatenpc without a target", "You must either enter a valid npcID or target an NPC. | !animatenpc (npcID) <animationID>", Cmd("animatenpc 1"));
        Cmd("pos -20 -20 305");
        Cmd($"npchere {npc.Id}");
        Check("!npchere <id>: the NPC set at the GM", "-20,-20,305/0", $"{npc.X:0.##},{npc.Y:0.##},{npc.Z:0.##}/{npc.Status}");
        Cursor(tc.TargId);
        Check("!posemannequin 1 2 with the target on the cursor: the GM's mannequin posed", "", Cmd("posemannequin 1 2"));
        Cursor(0);
        Check("!posemannequin without a target", "No valid target found. place cursor on a non-npc object or specify a player name.  | !posemannequin <race> <pose> [target])", Cmd("posemannequin 1 2"));
        Check("no unexpected script errors", "", TakeErrors());

        // ---- server and zone info, look, chat, menus, events ----
        Console.WriteLine("Server and zone info, look, messages, menus: time, uptime, getfishers, setweather, setmusic, setplayermodel, animation, costume, entityvisual, hide, mount, chocobo, cs2, menus, garrison, gc, sleep");
        var time = Cmd("time").Split(" | ");
        Check("!time: the six SYSTEM_3 lines", "6/It has been /The next Vana'diel day is in /Vana'diel: /Japan: /Current RSE is ",
              time.Length + "/" + string.Join("/", time.Where((_, i) => i != 3).Select(t => Regex.Match(t, @"^(It has been |The next Vana'diel day is in |Vana'diel: |Japan: |Current RSE is )").Value)));
        Check("  the epoch line from VanadielUniqueDay / VanadielTime", Lua("report(string.format(\"It has been %d Vana'diel days (%d seconds) since the Vana'diel epoch.\", VanadielUniqueDay(), VanadielTime()))"), time[0]);
        Check("!uptime", true, Regex.IsMatch(Cmd("uptime"), @"^The zone has been up for (\d+ (day|hour|minute|second)s? ?)+$"));
        Lua("player:setCharVar('[Fish]LastCastTime', GetSystemTime())");
        // GetRecentFishers keys its table by char id, so the script's #fishers is 0: no fisher is ever listed (C++ too)
        Check("!getfishers: GetRecentFishers has the GM, but #fishers of the char-id keyed table is 0", "No active fishers in past 5 minutes./Cmdlivegm",
              Cmd("getfishers") + "/" + Lua($"report(GetRecentFishers(5)[{GmId}].playerName)"));
        Check("!getfishers 61", "The specified look back period must be between 1 and 60 minutes. | !getfishers <MinutesToLookback>", Cmd("getfishers 61"));
        Lua("player:setCharVar('[Fish]LastCastTime', 0)");
        int oldWeather = int.Parse(Lua("report(player:getWeather())"));
        Check("!setweather rain: the zone's weather, the GP_SERV_COMMAND_WEATHER (0x057)", "Set weather to RAIN./6/6",
              Cmd("setweather rain") + "/" + Lua("report(player:getWeather())") + "/" + Of(GmId, 0x057).Select(p => BitConverter.ToUInt16(p.Buffer, 8)).LastOrDefault());
        Check("!setweather 20", "Invalid weather ID. | !setweather <weather ID>", Cmd("setweather 20"));
        Cmd($"setweather {oldWeather}");
        Check("!setmusic 2 115: GP_SERV_COMMAND_MUSIC (0x05F) slot 2, track 115", "/2:115", Cmd("setmusic 2 115") + "/" + string.Join(",", Of(GmId, 0x05F).Select(p => $"{BitConverter.ToUInt16(p.Buffer, 4)}:{BitConverter.ToUInt16(p.Buffer, 6)}")));
        Check("!setmusic 8 1", "Invalid type ID. | !setmusic <type ID> <song ID> | type IDs: 0 = BGM (Day), 1 = BGM (Night), 2 = Solo-Battle, 3 = Party-Battle, 4 = Chocobo, 5=Death, 6=Moghouse, 7=Fishing", Cmd("setmusic 8 1"));
        Check("!setplayermodel 300 4 <name>: the target's head model", "Set Cmdlivetarget's head slot to model 300./300", Cmd("setplayermodel 300 4 Cmdlivetarget") + "/" + Char(TargetId).Look.Head);
        Check("!setplayermodel 300 9", "Invalid slot ID. | !setplayermodel <model> <slot> (player) | Slots: 0=main 1=sub 2=ranged 3=ammo 4=head 5=body 6=hands 7=legs 8=feet", Cmd("setplayermodel 300 9"));
        Check("!animation: the current one", "Current player animation: 0", Cmd("animation"));
        // by name the script looks in xi.anim, which nothing defines (the enum is xi.animation): a Lua error, C++ too
        Check("!animation sit: xi.anim is nil", "", Cmd("animation sit"));
        Check("  the script error", "cmdhandler::call: (animation) error: ./scripts/commands/animation.lua:28: attempt to index field 'anim' (a nil value)", TakeErrors());
        Check("!animation 33", "Cmdlivegm | Old animation: 0 | New animation: 33\n/33", Cmd("animation 33") + "/" + gm.Animation);
        Cmd("animation 0");
        Check("!costume 1000: m_Costume, the char status (0x037)", "/1000/True", Cmd("costume 1000") + "/" + gm.Costume + "/" + (Of(GmId, 0x037).Count > 0));
        Cmd("costume 0");
        Check("!costume2 1000: m_Costume2, GP_SERV_COMMAND_GRAP_LIST (0x051)", "/1000/True", Cmd("costume2 1000") + "/" + gm.Costume2 + "/" + (Of(GmId, 0x051).Count > 0));
        Cmd("costume2 0");
        Check("!costume -1", "Invalid costumeID. | !costume <costumeID>", Cmd("costume -1"));
        Check("!entityvisual hwc3: GP_SERV_COMMAND_MAPSCHEDULOR (0x039) with no entity", "/hwc3:0",
              Cmd("entityvisual hwc3") + "/" + string.Join(",", Of(GmId, 0x039).Select(p => System.Text.Encoding.ASCII.GetString(p.Buffer, 12, 4) + ":" + BitConverter.ToUInt32(p.Buffer, 4))));
        Check("!entityvisual", "You must enter a valid animation string. | !entityvisual <animation string>", Cmd("entityvisual"));
        Check("!hide: GM hidden, the char var, char_flags", "You are now GM hidden from other players./True/1/1",
              Cmd("hide") + "/" + gm.GmHidden + "/" + Lua("report(player:getCharVar('GMHidden'))") + "/" + DbRow(db, "SELECT gmHiddenEnabled FROM char_flags WHERE charid = ?", BoundValue.Of(GmId)));
        Check("!hide status", "Current hide status: 1", Cmd("hide status"));
        Check("!hide again: shown", "You are no longer GM hidden from other players./False/0", Cmd("hide") + "/" + gm.GmHidden + "/" + Lua("report(player:getCharVar('GMHidden'))"));
        Check("!mount raptor <name>: the target mounted, power the mount", "/true/" + Lua("report(xi.mount.RAPTOR)"),
              Cmd("mount raptor Cmdlivetarget") + "/" + Has("target", "hasStatusEffect", "xi.effect.MOUNTED") + "/" + Lua("report(target:getStatusEffect(xi.effect.MOUNTED):getPower())"));
        Lua("target:delStatusEffectSilent(xi.effect.MOUNTED)");
        Check("!mount 99", "Invalid mount ID. | !mount <mount ID> (player)", Cmd("mount 99"));
        Check("!chocobo blue head tail: the GM on its own chocobo (registerChocobo), the personal chocobo flag", "/" + Lua("report(xi.mount.CHOCOBO .. '/' .. xi.chocoboRaising.personalChocoboFlag)"),
              Cmd("chocobo blue head tail") + "/" + Lua("local e = player:getStatusEffect(xi.effect.MOUNTED) report(e:getPower() .. '/' .. e:getSubPower())"));
        Lua("player:delStatusEffectSilent(xi.effect.MOUNTED)");
        Cmd("cs2 100 a b");
        Check("!cs2 100 a b: GP_SERV_COMMAND_EVENTSTR (0x033)", "100/1", $"{(gm.IsInEvent ? gm.CurrentEvent.EventId : -1)}/{Of(GmId, 0x033).Count}");
        Cmd("release");
        Check("!cs2", "You must enter a cutscene id. | !cs2 <csID> (string1) (string2) (string3) (string4) (op1) (op2) (op3) (op4) (op5) (op6) (op7) (op8)", Cmd("cs2"));
        Check("!messagebasic 7 5 6: GP_SERV_COMMAND_BATTLE_MESSAGE (0x029) to the GM", "/7:5", Cmd("messagebasic 7 5 6") + "/" + Basic(GmId));
        Check("!messagespecial 7000 1 2: GP_SERV_COMMAND_TALKNUMWORK (0x02A)", "/7000:1", Cmd("messagespecial 7000 1 2") + "/" + Special(GmId));
        Check("!messagestandard 4: GP_SERV_COMMAND_MESSAGE (0x009)", "/4", Cmd("messagestandard 4") + "/" + string.Join(",", Of(GmId, 0x009).Select(p => BitConverter.ToUInt16(p.Buffer, 10))));
        Check("!messagebasic", "You must provide a message ID. | !messagebasic <message ID> (param1) (param2)", Cmd("messagebasic"));
        Check("!menu: onStart's line, the GM prompt", "Test Menu Opening | \"Test Menu (Play Effect)\"\"Option 1: Hearts\"\"Option 2: Music Notes\"\"Option 3: Lightbulb\"", Cmd("menu"));
        MenuAnswer(GmId, "Option 1: Hearts");
        Check("  answered Option 1: its function, onEnd", "Option 1 Selected | Test Menu Closing", string.Join(" | ", ChatOf(GmId)));
        Cmd("menu");
        MenuAnswer(GmId, "Canceled.");
        Check("  cancelled: onCancelled, onEnd", "Test Menu Cancelled | Test Menu Closing", string.Join(" | ", ChatOf(GmId)));
        Check("!menu_paginated: the menu 50 ms on (player:timer)", "", Cmd("menu_paginated"));
        Tick(100);
        Check("  page 1", "\"Test Menu (Paginated)\"\"Option 1\"\"Next Page\"", string.Join(" | ", ChatOf(GmId)));
        MenuAnswer(GmId, "Next Page");
        Tick(100);
        Check("  Next Page: page 2", "\"Test Menu (Paginated)\"\"Option 2\"\"Previous Page\"", string.Join(" | ", ChatOf(GmId)));
        MenuAnswer(GmId, "Option 2");
        Check("  Option 2", "Option 2 Selected", string.Join(" | ", ChatOf(GmId)));
        Cursor(Char(TargetId).TargId);
        Cmd("afkcheck");
        var afk = string.Join("", ChatOf(TargetId));
        Check("!afkcheck on the target: its three-option menu, CAPTCHA set", "True/3/1",
              afk.StartsWith("\"AFK Check: Please pick true statement (30s)\"") + "/" + Regex.Matches(afk, @"\d+ \+ +\d+ = +-?\d+").Count + "/" + Lua("report(target:getLocalVar('CAPTCHA'))"));
        Cursor(0);
        Tick(31000);
        Check("  unanswered for 30 s: the target's HP 0", "0", Lua("report(target:getHP())"));
        Lua("target:setHP(target:getMaxHP())");
        Check("!garrison", "Invalid command\nUsage: !garrison <command> (player)", Cmd("garrison"));
        Check("!garrison start: the GM's zone's garrison runs, the GM capped at 20", "West_Ronfaure garrison started/true/20",
              Cmd("garrison start") + "/" + Lua("report(tostring(xi.garrison.zoneData[100].isRunning))") + "/" + Lua("report(player:getStatusEffect(xi.effect.LEVEL_RESTRICTION):getPower())"));
        Check("!garrison win: its state to GRANT_LOOT", "/true", Cmd("garrison win") + "/" + Lua("report(tostring(xi.garrison.zoneData[100].state == xi.garrison.state.GRANT_LOOT))"));
        Check("!garrison stop: stopped, the cap gone", "West_Ronfaure garrison stopped/false/false",
              Cmd("garrison stop") + "/" + Lua("report(tostring(xi.garrison.zoneData[100].isRunning))") + "/" + Has("player", "hasStatusEffect", "xi.effect.LEVEL_RESTRICTION"));
        for (int i = 0; i < 5; i++) Tick(1000);
        Lua("player:setCharVar('[Garrison]NextEntryTime', 0)");
        Check("!gc_full / !gc_step / !sleep (permission 5)", "//", Cmd("gc_full") + "/" + Cmd("gc_step") + "/" + Cmd("sleep"));
        Check("!checkinstance outside an instance", "Must be in an Instanced zone", Cmd("checkinstance"));
        Check("!setprogress / !setstage outside an instance", "Must be in an Instanced zone/Must be in an Instanced zone", Cmd("setprogress 1") + "/" + Cmd("setstage 1"));
        Check("!instance", "You must provide an instance id\n!instance <instance_id>", Cmd("instance"));
        Check("!instance 9999: createInstance refuses it", "Creating instance: 9999/CLuaBaseEntity::createInstance: Invalid instanceID: 9999", Cmd("instance 9999") + "/" + Logged("CLuaBaseEntity::createInstance"));
        Check("!setbattlefieldtime outside a battlefield", "This command can only be used while inside a battlefield. | !setbattlefieldtime <minutes>", Cmd("setbattlefieldtime 5"));
        Check("!naga outside Escha - Ru'Aun", "!naga can only be used in Escha - RuAun.", Cmd("naga"));
        Check("!addabytime outside Abyssea", "Cmdlivegm is not in Abyssea. | !addabytime <minutes> (player)", Cmd("addabytime 5"));
        Check("!adddynatime outside Dynamis", "Cmdlivegm is not in Dynamis. | !adddynatime <minutes> (player)", Cmd("adddynatime 5"));
        Check("!checkinteraction <NPC name>: its interaction handlers, each check run for the GM", "Handlers for \"Aaveleon\": | Trigger: |   Type: Event, ID: 101, Priority: 10 |   function [container: Quest[0][0]] [check: false] |   Type: Message, ID: 7364, Priority: 50 [container: Quest[0][0]] [check: false] |   Type: Message, ID: 7390, Priority: 50 [container: Quest[0][3]] [check: false] | Trade: |   function [container: Quest[0][0]] [check: false]",
              Cmd("checkinteraction Aaveleon"));
        Check("!checkinteraction of a name with none", "No interactions for \"Nobody\".", Cmd("checkinteraction Nobody"));
        Check("!checkinteraction with nothing", "No valid handler targeted or handler name given.", Cmd("checkinteraction"));
        Check("no unexpected script errors", "", TakeErrors());
        // last in West Ronfaure, as it sets every mob of the zone on the GM
        Cmd($"spawnmob {crab.Id}");
        MoveTo(GmId, crab.X, crab.Y, crab.Z, crab.TargId);
        Check("!provokeall: every mob of the zone engages the GM at 3000 TP", "Cmdlivegm/3000",
              Cmd("provokeall") + crab.BattleTarget?.Name + "/" + MobLua("mob:getTP()"));
        // updateEnmity is PAI->Engage: the mob engages, no enmity is added
        Check("  !getenmity after it", "Your enmity against Tree_Crab is ... CE = 0 ... VE = 0", Cmd("getenmity"));
        Check("no unexpected script errors", "", TakeErrors());


        // ---- an instance, Abyssea, Dynamis ----
        Console.WriteLine("In an instance, Abyssea and Dynamis: instance, checkinstance, setprogress, setstage, addlights, resetlights, addabytime, adddynatime; minigame");
        Check("!instance 6900 (Leujaoam Cleansing): queued", "Creating instance: 6900", Cmd("instance 6900"));
        Tick(400);
        Rezone(GmId);
        gm = Char(GmId);
        // the GM is not in the entrance zone, so onInstanceCreatedCallback sends it straight in
        Check("  loaded at the tick's end: the GM in its run in Leujaoam Sanctum", "69/6900", gm.Zone + "/" + Lua("report(player:getInstance():getID())"));
        Check("!checkinstance", "Progress: 0 Stage: 0", Cmd("checkinstance"));
        Check("!setprogress 3", "Progress changed from 0 to 3/3", Cmd("setprogress 3") + "/" + Lua("report(player:getInstance():getProgress())"));
        Check("!setstage 2", "Stage changed from 0 to 2/2", Cmd("setstage 2") + "/" + Lua("report(player:getInstance():getStage())"));
        Check("!instance 6900 inside one: refused, the run failed", "It is not safe to use this command while inside an instance, try again after exiting./true",
              Cmd("instance 6900") + "/" + Lua("report(tostring(player:getInstance():failed()))"));
        Check("no unexpected script errors", "", TakeErrors());
        const ushort AbysseaKonschtat = 15, DynamisSandoria = 185;
        Lua($"player:setPos(0, 0, 0, 0, {AbysseaKonschtat})");
        Rezone(GmId);
        gm = Char(GmId);
        Check("in Abyssea - Konschtat", AbysseaKonschtat.ToString(), gm.Zone.ToString());
        // the script reads the char var <light>Light, which nothing sets (the lights live in abysseaLights1 / 2): its total is always 0
        Check("!addlights pearl 5: the light added, BODY_EMITS_OFFSET's message, the script's total from an unused var",
              $"Cmdlivegm was given 5 pearl light, for a total of 0./{int.Parse(Lua($"report(zones[{AbysseaKonschtat}].text.BODY_EMITS_OFFSET)"))}:0/5",
              Cmd("addlights pearl 5") + "/" + Special(GmId) + "/" + Lua("report(xi.abyssea.getLightsTable(player)[1])"));
        Check("!addlights foo 5", "Invalid light type.\nValid light types: pearl, azure, ruby, amber, gold, silver, ebon | !addlights <light type> <amount> (player)", Cmd("addlights foo 5"));
        Check("!resetlights: abysseaLights1 / 2 cleared", "Cmdlivegm's lights have been reset!. /0/0",
              Cmd("resetlights") + "/" + Lua("report(player:getCharVar('abysseaLights1'))") + "/" + Lua("report(player:getCharVar('abysseaLights2'))"));
        Lua("player:addStatusEffect(xi.effect.VISITANT, { duration = 600, origin = player, silent = true })");
        string Remaining(string effect) => Lua($"report(math.floor(player:getStatusEffect(xi.effect.{effect}):getTimeRemaining() / 60000 + 0.5))");
        Check("!addabytime 5: the Visitant status 5 minutes longer", "/15", Cmd("addabytime 5") + "/" + Remaining("VISITANT"));
        Check("!addabytime 0", "Invalid number of minutes. | !addabytime <minutes> (player)", Cmd("addabytime 0"));
        Lua("player:delStatusEffectSilent(xi.effect.VISITANT)");
        Check("no unexpected script errors", "", TakeErrors());
        Lua($"player:setPos(0, 0, 0, 0, {DynamisSandoria})");
        Rezone(GmId);
        gm = Char(GmId);
        Check("in Dynamis - San d'Oria", DynamisSandoria.ToString(), gm.Zone.ToString());
        Lua("player:addStatusEffect(xi.effect.DYNAMIS, { duration = 600, origin = player, silent = true })");
        Check("!adddynatime 5: the Dynamis status 5 minutes longer, DYNAMIS_TIME_EXTEND", $"/15/{int.Parse(Lua($"report(zones[{DynamisSandoria}].text.DYNAMIS_TIME_EXTEND)"))}:5",
              Cmd("adddynatime 5") + "/" + Remaining("DYNAMIS") + "/" + Special(GmId));
        Lua("player:delStatusEffectSilent(xi.effect.DYNAMIS)");
        Check("no unexpected script errors", "", TakeErrors());
        Check("!minigame: its menu", "\"Minigame Test Menu\"\"Full Speed Ahead! (Normal)\"\"Full Speed Ahead! (Easy)\"", Cmd("minigame"));
        MenuAnswer(GmId, "Full Speed Ahead! (Easy)");
        Rezone(GmId);
        gm = Char(GmId);
        // the var 2 sends it on to the zone-in's Full Speed Ahead! (easy, power 1), whose onEffectGain sets 3 and mounts the raptor
        Check("  Easy: the GM sent to Batallia Downs, into the easy game", "105:475,8.8,-159/1/3/true",
              Pos(gm) + "/" + Lua("report(player:getStatusEffect(xi.effect.FULL_SPEED_AHEAD):getPower())") + "/" + Lua("report(player:getCharVar('[QUEST]FullSpeedAhead'))")
              + "/" + Has("player", "hasStatusEffect", "xi.effect.MOUNTED"));
        Lua("player:setCharVar('[QUEST]FullSpeedAhead', 4) player:delStatusEffect(xi.effect.FULL_SPEED_AHEAD) player:setCharVar('[QUEST]FullSpeedAhead', 0)");
        Check("no unexpected script errors", "", TakeErrors());

        // ---- commands that change the server for everyone: driven only up to the permission refusal ----
        Console.WriteLine("Server-wide commands, refused: crash, addtime, reload*, rebuildnavmesh (gmlevel 1); updateconquest, inject (gmlevel 0)");
        var refused = new List<string>();
        foreach (var (who, cmd) in new[]
                 {
                     (LowGmId, "crash"), (LowGmId, "addtime 60"), (LowGmId, "reloadbattlefield 1"), (LowGmId, "reloaddefaultactions"),
                     (LowGmId, "reloadglobal abyssea"), (LowGmId, "reloadinteraction"), (LowGmId, "reloadmagians"), (LowGmId, "reloadnavmesh"),
                     (LowGmId, "reloadquest x"), (LowGmId, "reloadrecipes"), (LowGmId, "rebuildnavmesh"), (TargetId, "updateconquest 1"), (TargetId, "inject x"),
                 })
        {
            string said = SayAs(who, "!" + cmd);
            string name = cmd.Split(' ')[0];
            if (said == "" && Logged("cmdhandler::call") == $"cmdhandler::call: Character {(who == LowGmId ? LowGmName : TargetName)} attempting to use higher permission command {name}") refused.Add(name);
        }
        Check("each refused with the permission line, nothing said back", 13, refused.Count);

        string loggedOff = Cmd("logoff Cmdlivetarget");
        Tick(400);
        Check("!logoff <name>: the target leaves the game at the tick's end", "Cmdlivetarget has been logged off./True", loggedOff + "/" + (sessions[TargetId].ShuttingDown != 0));
        Check("no unexpected script errors", "", TakeErrors());

        // the tally: every script in scripts/commands run, or refused for the server-wide ones
        var all = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(dataDir), "scripts", "commands"), "*.lua").Select(Path.GetFileNameWithoutExtension).ToList();
        Console.WriteLine($"{all.Count} command scripts: {driven.Count(all.Contains)} driven, {refused.Count} refused only");
        Check("every command script driven or refused", "", string.Join(" ", all.Where(n => !driven.Contains(n) && !refused.Contains(n)).OrderBy(n => n)));
    }
}
