using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// The client side of XI.Verify.QuestGolden.Live (copied from XI.Verify.QuestScripts.Live's): the zone host on the real
// database with its script engine and the clocks they read, the session of the char being driven, the packets a client
// sends (login, moves, talk, event ends, trades, GM warps) and the reads of what came back. Unlike the B2 copy every
// clock is a constant at the start of a world (the wall clock included), so a trace recorded in one process is the same
// in another: the twin's two runs shared a process, a trace and its replay do not.
internal static partial class Program
{
    private static IDatabase db;
    private static ZoneHost zone;
    private static ScriptEngine scripts;
    private static LuaState lua;
    private static readonly List<string> log = new();
    private static readonly List<string> got = new();

    // a char of the flow (quest rework P3): its name (Flow.MainChar for the first), its id, its session, and the packets it
    // got that the session no longer holds (the harness takes them out before each client packet and each tick, a zone
    // change replaces the session): a step's record holds them all
    internal sealed class Actor
    {
        public string Name = Flow.MainChar;
        public uint Id;
        public MapSession S;
        public CharData C;
        public readonly List<BasicPacket> Pending = new();
    }
    // the flow's chars, main first; the one a step runs as. Outside a flow (startup, the self-test) main alone
    private static readonly List<Actor> actors_ = new() { new Actor() };
    private static Actor actor_ = actors_[0];
    private static Actor MainActor => actors_[0];
    // the char the harness drives now: its session, char, packets and id
    private static MapSession s { get => actor_.S; set => actor_.S = value; }
    private static CharData c { get => actor_.C; set => actor_.C = value; }
    private static List<BasicPacket> pending_ => actor_.Pending;
    private static uint CharId { get => actor_.Id; set => actor_.Id = value; }

    // the flow's chars with their ids (main first), main the one driven
    private static void SetActors(IReadOnlyList<string> names, IReadOnlyList<uint> ids)
    {
        actors_.Clear();
        for (int i = 0; i < names.Count; i++) actors_.Add(new Actor { Name = names[i], Id = ids[i] });
        actor_ = actors_[0];
        joined_.Clear();
    }

    // the char the next client packets and GM calls are for
    private static void Use(Actor a) => actor_ = a;

    // the world's routing of the party's IPC (a flow of more than one char): what XI.World's IpcServer sends back to the
    // map process that holds the recipient (every char here is on this one), each through its CBOR framing as the dealer
    // carries it. Every other message stays dropped, as in a one-char flow (whose SendIpc drops all)
    private static readonly Queue<XI.Ipc.IIpcMessage> ipc_ = new();
    internal static bool RoutedIpc(XI.Ipc.IIpcMessage m) => m is XI.Ipc.PartyInvite or XI.Ipc.PartyInviteResponse or XI.Ipc.PartyRequest
        or XI.Ipc.PartyReload or XI.Ipc.PartyDisband or XI.Ipc.AllianceReload or XI.Ipc.AllianceDissolve or XI.Ipc.PlayerKick
        or XI.Ipc.MessageStandard or XI.Ipc.MessageSystem;

    private static void RouteIpc()
    {
        ipc_.Clear();
        zone.SendIpc = m => { if (RoutedIpc(m)) ipc_.Enqueue(m); };
    }

    // the routed messages delivered, those they send in turn too (a bound against a loop)
    private static void PumpIpc()
    {
        for (int i = 0; ipc_.Count > 0; i++)
        {
            if (i == 1000) throw new InvalidOperationException("the IPC routing does not settle (1000 messages)");
            zone.OnIpc(XI.Ipc.IpcFraming.Decode(XI.Ipc.IpcFraming.ToBytesWithHeader(ipc_.Dequeue())));
        }
    }
    // the host's clocks, back at their starts in each new world and moved only by the flow (tick, wait)
    private static long now = StartNow;
    private static long vanaTicks = VanaStart;
    // the wall clock (unix seconds: the 0x037's dead_counter2, the char_vars expiries, GetSystemTime, the lifts) is a
    // constant, not the process's clock; `wait jst=<days>` moves it
    private const long UnixStart = 1_800_000_000;
    private static long unixNow = UnixStart;
    private const long VanaStart = 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static void Setup(IDatabase database)
    {
        // the world tables go to the run's private copies once they are open (World.cs)
        world_ = new WorldTablesDatabase(database);
        db = world_;
        dataDir_ = FindDataDir();
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());
        loader_ = new CharLoader(db, Grades.Load(dataDir_), AbilityTable.Load(db, _ => true, 1.0), stats);
    }

    private static string dataDir_;
    // the scripts folder a new world's engine runs: null for the root's, a record's overlay (Program.Overlay) otherwise
    private static string scriptsDir_;
    private static System.Diagnostics.Stopwatch worldClock_;
    private static CharLoader loader_;
    // the csharp folder (scripts/, tests/golden/)
    private static string Root => Path.GetDirectoryName(dataDir_);
    private const long StartNow = 1_000_000;
    private const int Seed = 7;

    // a new zone host and script engine: no zone loaded yet, every clock back at its start (the wall clock a constant),
    // the mobs', the scripts', the weather's, the status effects', synthesis', nominations' and the fishing contest's
    // random numbers from the same seed, the hidden require names skipped by the scripts engine (a Lua file standing in
    // for a deleted one) and the given quest keys in CSHARP_QUESTS_OFF before the containers load. Each trace is
    // recorded and replayed in such a world, so the mobs, the weather and the zone scripts behave the same each time.
    private static void NewWorld(IEnumerable<string> hidden, params string[] csharpQuestsOff)
    {
        scripts?.Dispose();
        // no routed message of the last world (its logouts' PartyReload) reaches this one
        ipc_.Clear();
        // the world tables back to the snapshot before the zone host reads them
        RestoreWorldTables();
        log.Clear();
        threw_.Clear();
        now = StartNow;
        vanaTicks = VanaStart;
        unixNow = UnixStart;
        zone = new ZoneHost(db, loader_, dataDir_, true, log.Add);
        // the coverage hook before the containers are built (a fixed reply is a counting closure only while it is set)
        zone.HandlerCalled = handlerCalled_;
        zone.HandlerThrew = l => threw_.Add(l);
        // startup --synthetic: the synthetic quests, registered after the real ones
        if (synthetic_ != null) zone.TestQuestScripts.AddRange(synthetic_);
        // a probe's C# run: its twins (Twins.cs), registered after the real quests
        if (twins_ != null) zone.TestQuestScripts.AddRange(twins_);
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => vanaTicks;
        zone.UnixNow = () => unixNow;
        zone.UnixNowMs = () => unixNow * 1000;
        zone.SendIpc = _ => { };
        zone.MobRng = new Random(Seed);
        var weather = new Random(Seed);
        zone.WeatherRandom = (min, max) => weather.Next(min, max);
        zone.SeedEffectRng(Seed);
        var synth = new Random(Seed);
        zone.Random01 = synth.NextDouble;
        zone.RandomBelow = new Random(Seed).Next;
        var contest = new Random(Seed);
        zone.ContestRandom = (min, max) => contest.Next(min, max);
        foreach (var key in csharpQuestsOff) zone.CSharpQuestsOff.Add(key);
        // no module from the local modules/init.txt: a flow's module lines are its only modules (Modules.cs)
        zone.ModuleInitLines = Array.Empty<string>();
        // the startup mode measures from here to the first zone loaded
        worldClock_ = System.Diagnostics.Stopwatch.StartNew();
        scripts = new ScriptEngine(Root, log.Add, log.Add, scriptsDir_);
        if (hidden != null) scripts.HiddenContainerFiles = new HashSet<string>(hidden, StringComparer.Ordinal);
        scripts.SeedRandom(Seed);
        scripts.VanaTicksNow = () => vanaTicks;
        scripts.UnixNowMs = () => unixNow * 1000;
        zone.InitScripts(scripts);
        // quest rework P9 (D5): the time server's hour primed, so a flow's first tick that crosses an hour runs the hour
        // hooks (onGameHour, the hour spawns, the time of day's), as every later tick does
        if (PrimeHour) zone.PrimeGameHour();
        lua = scripts.Lua;
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        InstallReturnRecorder();
    }

    // D5: prime the hour at world setup (false: the first tick only records it, as before P9)
    internal static bool PrimeHour = true;

    private static string Lua(string code)
    {
        got.Clear();
        lua.DoString($"local player = GetPlayerByID({CharId})\n" + code, "=verify_questgolden");
        return string.Join(",", got);
    }
    // a Lua string literal of the text (quotes, backslashes and control characters escaped)
    private static string LuaStr(string text)
    {
        var sb = new StringBuilder("'");
        foreach (char ch in text)
        {
            if (ch == '\\' || ch == '\'') sb.Append('\\').Append(ch);
            else if (ch < ' ') sb.Append('\\').Append(((int)ch).ToString("D3", CultureInfo.InvariantCulture));
            else sb.Append(ch);
        }
        return sb.Append('\'').ToString();
    }

    private static List<string> ScriptErrors(IEnumerable<string> logged) =>
        logged.Where(IsScriptError).Distinct().ToList();

    // a log line that is a script error: the engine's lua: lines (not the global scripts' load), a not-ported call, a
    // luautils:: failure, the lookup's "Error running handler" / "Error running check"
    internal static bool IsScriptError(string l) =>
        (l.StartsWith("lua:", StringComparison.Ordinal) && !l.Contains("global scripts")) || l.Contains("not ported") ||
        l.Contains("luautils::") || l.Contains("Error running");

    // the lines a C# handler's throw logged in this world (ZoneHost.HandlerThrew: "quests: <key> <handler>: <message>")
    private static readonly HashSet<string> threw_ = new(StringComparer.Ordinal);

    // ProgressRows' failure lines ("progress: char <id>: ..."): a quest / mission row read or write failed and the char ran
    // on the legacy state from there. Its other lines (imported, re-imported, re-keyed, a retry that went on) have no
    // colon after the id
    private static readonly System.Text.RegularExpressions.Regex ProgressFailureLine = new(@"^progress: char \d+: ");
    private static bool IsProgressFailure(string line) => ProgressFailureLine.IsMatch(line);
    private static List<string> ProgressFailures() => log.Where(IsProgressFailure).Distinct().ToList();

    // the persist sweep's failure lines ("persist: char <id>: ..."): a char_effects write rolled back on every attempt
    // (a lock conflict with another job's write), so the step's snapshot misses the char's effect rows. Its retry lines
    // (no colon after the id) lost nothing
    private static readonly System.Text.RegularExpressions.Regex PersistFailureLine = new(@"^persist: char \d+: ");
    private static List<string> PersistFailures() => log.Where(l => PersistFailureLine.IsMatch(l)).Distinct().ToList();

    private static ResultSet Sql(IDatabase database, string q, params BoundValue[] a) => database.Execute(q, a);

    private static byte[] DbBlob(string col)
    {
        var r = Sql(db, $"SELECT {col} FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.GetBlobBytes(col) ?? Array.Empty<byte>() : Array.Empty<byte>();
    }

    // every char table (the new ones included), the chars row and the account; and every party row of the party or
    // alliance the char led (a member's row is its own charid's: a flow on these ids may have led a member whose id this
    // run does not clean, and the engine lists a party by partyid, so its row would come back as a ghost member)
    private static void Cleanup(IDatabase database, uint id)
    {
        Sql(database, "DELETE FROM accounts_parties WHERE partyid = ? OR allianceid = ?", BoundValue.Of(id), BoundValue.Of(id));
        var tables = new List<string>();
        var r = Sql(database, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in tables.OrderBy(t => t, StringComparer.Ordinal)) Sql(database, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
        Sql(database, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
        Sql(database, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
    }

    // run after the startup gate on the run's two lowest locked ids (P3 review): a party row another char of an earlier
    // flow left with these ids, in the party one of them led, goes with that leader's Cleanup (as a party flow on these
    // ids leaves it: ReleaseFromParty keeps the rows). Before the fix such a row came back as a ghost member of the next
    // party that id led
    private static void CleanupPartyCheck(IReadOnlyList<uint> ids)
    {
        var two = ids.Distinct().OrderBy(i => i).Take(2).ToList();
        if (two.Count < 2)
        {
            Console.WriteLine("SKIP gate: Cleanup drops a led party's rows (one char id locked; it needs two)");
            return;
        }
        uint leader = two[0], member = two[1];
        Cleanup(db, leader);
        Cleanup(db, member);
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(member), BoundValue.Of(member), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "INSERT INTO accounts_parties(charid, partyid, allianceid, partyflag) VALUES (?, ?, ?, 0)",
            BoundValue.Of(member), BoundValue.Of(leader), BoundValue.Of(leader));
        Cleanup(db, leader);
        var r = Sql(db, "SELECT COUNT(*) AS n FROM accounts_parties WHERE partyid = ? OR allianceid = ?", BoundValue.Of(leader), BoundValue.Of(leader));
        long left = r != null && r.Next() ? r.Get<long>("n") : -1;
        Cleanup(db, member);
        Check("gate: Cleanup drops a led party's rows of another char (no ghost member)", 0, left);
    }

    // ---- the fixture -------------------------------------------------------------------------------------------------

    private const int TitleNewAdventurer = 206;

    // the same length for every char, so a name in a packet sits at the same offsets in every trace
    private static string CharName(uint charId) => $"Qgolden{charId % 1000:D3}";

    // B2's Fixture and Level: a Hume (or the race given) past its first login (NEW_ADVENTURER) at x / y / z in the zone,
    // of the nation, with the main job at the level (HP filled up at the login when a level is given), the gil and the
    // items from inventory slot 1 on
    private static void Fixture(uint charId, ushort zoneId, float x, float y, float z, int job, int? level, int nation, uint gil,
                                IReadOnlyList<(int Item, uint Qty)> items, int race = 1)
    {
        CharId = charId;
        int lvl = level ?? 10;
        string jobCol = Jobs[job].ToLowerInvariant();
        var titles = new byte[143];
        titles[TitleNewAdventurer >> 3] |= 1 << (TitleNewAdventurer & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of($"qgolden{charId % 1000}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of(charId), BoundValue.Of(CharName(charId)), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)nation), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(charId), BoundValue.Of(charId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, ?, 50, ?, ?)",
            BoundValue.Of(charId), BoundValue.Of(level == null ? 300 : 3000), BoundValue.Of((byte)job), BoundValue.Of((byte)lvl));
        Sql(db, $"REPLACE INTO char_jobs(charid, {jobCol}) VALUES (?, ?)", BoundValue.Of(charId), BoundValue.Of((byte)lvl));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, ?)", BoundValue.Of(charId), BoundValue.Of((byte)race));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(charId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(charId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(charId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,?)", BoundValue.Of(charId), BoundValue.Of(gil));
        for (int i = 0; i < items.Count; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(charId), BoundValue.Of((byte)(i + 1)), BoundValue.Of((ushort)items[i].Item), BoundValue.Of(items[i].Qty));
    }

    // xi.job by id (0 is NONE)
    internal static readonly string[] Jobs =
    {
        "NONE", "WAR", "MNK", "WHM", "BLM", "RDM", "THF", "PLD", "DRK", "BST", "BRD", "RNG", "SAM", "NIN", "DRG", "SMN", "BLU", "COR",
        "PUP", "DNC", "SCH", "GEO", "RUN",
    };

    // the setup statements before the login, on the rows: char_profile's fame column, the quest log bits (current, then
    // complete 32 bytes on, per log), the key item bit, a char var
    private static void SetFameRow(int fameArea, int value) =>
        Sql(db, $"UPDATE char_profile SET {CharLoader.FameColumns[fameArea]} = ? WHERE charid = ?", BoundValue.Of(value), BoundValue.Of(CharId));

    private static void SetQuestRow(int log, int quest, string status)
    {
        var blob = new byte[CharData.MaxQuestArea * CharData.QuestLogSize];
        var old = DbBlob("quests");
        Array.Copy(old, blob, Math.Min(old.Length, blob.Length));
        int off = log * CharData.QuestLogSize + quest / 8;
        byte bit = (byte)(1 << quest % 8);
        blob[off] = (byte)(status == "accepted" ? blob[off] | bit : blob[off] & ~bit);
        blob[off + 32] = (byte)(status == "completed" ? blob[off + 32] | bit : blob[off + 32] & ~bit);
        Sql(db, "UPDATE chars SET quests = ? WHERE charid = ?", BoundValue.Of(blob), BoundValue.Of(CharId));
    }

    // addMission (current) / completeMission (completed) as the rows: the chars.missions blob of the char's fresh mission log
    // (none current in the nation and Zilart logs) with the log's current mission set, or cleared with its complete flag.
    // `completed` is the setup of an end state: the mission need not be current, as completeMission requires
    private static void SetMissionRow(int log, int mission, string status)
    {
        var data = new CharData();
        var old = DbBlob("missions");
        Array.Copy(old, data.MissionLog, Math.Min(old.Length, data.MissionLog.Length));
        ApplyMission(data.MissionLog, log, mission, status == "completed");
        Sql(db, "UPDATE chars SET missions = ? WHERE charid = ?", BoundValue.Of(data.MissionLog), BoundValue.Of(CharId));
    }

    // the blob after addMission / completeMission; completeMission writes the flag only when ZoneHost.CompleteMission does
    // (log != CoP (6) && mission < 64: the log holds 64 flag bytes), else the write would hit the next log's bytes
    internal static void ApplyMission(byte[] blob, int log, int mission, bool completed)
    {
        int cur = log * CharData.MissionLogSize;
        int value = completed ? (log <= 2 ? 0xFFFF : 0) : mission;
        blob[cur] = (byte)value;
        blob[cur + 1] = (byte)(value >> 8);
        if (completed && log != 6 && mission < 64) blob[cur + 6 + mission] = 1;
    }

    // setRank / setRankPoints as char_profile's rank column of the char's nation (0 San d'Oria, 1 Bastok, 2 Windurst); points < 0: unchanged
    private static void SetRankRow(int nation, int rank, int points)
    {
        string col = nation switch { 0 => "rank_sandoria", 1 => "rank_bastok", _ => "rank_windurst" };
        Sql(db, $"UPDATE char_profile SET {col} = ? WHERE charid = ?", BoundValue.Of((byte)rank), BoundValue.Of(CharId));
        if (points >= 0) Sql(db, "UPDATE char_profile SET rank_points = ? WHERE charid = ?", BoundValue.Of((ushort)Math.Min(points, 4000)), BoundValue.Of(CharId));
    }

    // addTitle as the chars.titles blob bit; unlike player:addTitle it does not set the current title (char_stats.title)
    private static void AddTitleRow(int title)
    {
        var blob = new byte[143];
        var old = DbBlob("titles");
        Array.Copy(old, blob, Math.Min(old.Length, blob.Length));
        blob[title >> 3] |= (byte)(1 << (title & 7));
        Sql(db, "UPDATE chars SET titles = ? WHERE charid = ?", BoundValue.Of(blob), BoundValue.Of(CharId));
    }

    // xi.settings.main[NAME] = value in the world's Lua (before and after the login alike); the name must exist. A map
    // setting (setting map.<NAME>, quest rework P6) is xi.settings.map[NAME] = value, and its value must be of the kind
    // the setting has (a boolean for a boolean, a number for a number): Lua's `FISHING_ENABLE == true` is false for the
    // number 1, so a number written into a boolean setting would leave the arm untaken while the flow reads as if taken.
    // Only the Lua table changes: the engine's own copy of a map setting (StatSettings, read once at the server's start;
    // the harness's are its defaults) stays as it is, so a map setting reaches the scripts and the C# quests' Settings.Map,
    // not the engine's systems (fishing itself stays off)
    private static void SetSetting(Statement st)
    {
        string slot = $"xi.settings.{st.Table}[{LuaStr(st.Name)}]";
        if (SettingProblem(st, Lua($"report(type({slot}))")) is { } problem) throw new InvalidOperationException(problem);
        Lua($"{slot} = {SettingLuaValue(st)}");
    }

    // why the setting statement cannot be written, given the Lua type of the setting's current value; null when it can
    internal static string SettingProblem(Statement st, string luaType)
    {
        string label = st.Table == "main" ? st.Name : $"{st.Table}.{st.Name}";
        if (luaType == "nil") return $"unknown setting {label}";
        string kind = st.Status == "bool" ? "boolean" : "number";
        if (st.Table != "main" && luaType != kind) return $"setting {label} is a {luaType}, not a {kind}: write it as one";
        return null;
    }

    // a setting statement's value as Lua reads it: true / false, a whole number, or a decimal written invariantly (1.5,
    // never 1,5; "R": the double that parsed, digit for digit)
    internal static string SettingLuaValue(Statement st) =>
        st.Status == "bool" ? (st.Amount == 1 ? "true" : "false")
        : st.Real is double real ? real.ToString("R", CultureInfo.InvariantCulture)
        : st.Amount.ToString(CultureInfo.InvariantCulture);

    private static void AddKeyItemRow(int keyItem)
    {
        var blob = new XI.Quests.KeyItemsBitBlob();
        blob.LoadFromBlob(DbBlob("keyitems"));
        blob.AddKeyItem((ushort)keyItem);
        Sql(db, "UPDATE chars SET keyitems = ? WHERE charid = ?", BoundValue.Of(blob.Bytes), BoundValue.Of(CharId));
    }

    // setSkillLevel as the char_skills row: the stored value (10x the level), the rank kept (0 for a fresh char)
    private static void SetSkillRow(int skill, int value) =>
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, `rank`) VALUES (?, ?, ?, 0) ON DUPLICATE KEY UPDATE value = ?",
            BoundValue.Of(CharId), BoundValue.Of((byte)skill), BoundValue.Of((ushort)value), BoundValue.Of((ushort)value));

    // item wsp=<n> as the inventory row's exdata: Exdata::WeaponUnlock's unlockPoints, its first 16 bits (what
    // getWeaponskillPoints reads, WeaponInfo.CurrentUnlockPoints), the rest of the 24 bytes zero
    private static void SetWsPointsRow(byte slot, int points)
    {
        var extra = new byte[24];
        extra[0] = (byte)points;
        extra[1] = (byte)(points >> 8);
        Sql(db, "UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = 0 AND slot = ?",
            BoundValue.Of(extra), BoundValue.Of(CharId), BoundValue.Of(slot));
    }

    // setLevelCap as the char_jobs row (genkai)
    // storage <inventory|satchel> <n> before the login: the char_storage row's column (quest rework P6), which the loader
    // reads as the container's size
    private static void SetStorageRow(string column, int size) =>
        Sql(db, $"UPDATE char_storage SET {column} = ? WHERE charid = ?", BoundValue.Of((byte)size), BoundValue.Of(CharId));

    // storage <inventory|satchel> <n> after the login: a GM's player:changeContainerSize by the difference (quest rework
    // P6); the run fails when the size is not n after it (the engine keeps a size that would not hold the items in it)
    private static void SetStorageStep(Statement st)
    {
        string got = Lua($"local now = player:getContainerSize({st.Value}) player:changeContainerSize({st.Value}, {st.Amount} - now) " +
                         $"report(tostring(player:getContainerSize({st.Value})))");
        if (StorageProblem(st, got) is { } problem) throw new InvalidOperationException(problem);
    }

    // why a storage step failed, given the size getContainerSize reads after it; null when it is the statement's
    internal static string StorageProblem(Statement st, string got) =>
        got == st.Amount.ToString(CultureInfo.InvariantCulture) ? null
            : $"storage {st.Name} {st.Amount}: the size is {got} after changeContainerSize (the items in it?)";

    // ---- quest rework P8: the new statements' rows and GM steps ----

    // sjob=<JOB> slvl=<n> on the char line: char_stats' sjob and that job's char_jobs level (the loader caps the level the
    // char gets: CharLoader.SubLevel)
    private static void SetSupportJobRow(int job, int level)
    {
        Sql(db, "UPDATE char_stats SET sjob = ? WHERE charid = ?", BoundValue.Of((byte)job), BoundValue.Of(CharId));
        Sql(db, $"UPDATE char_jobs SET {Jobs[job].ToLowerInvariant()} = ? WHERE charid = ?", BoundValue.Of((byte)level), BoundValue.Of(CharId));
    }

    // spell <id> before the login: a char_spells row; the id must be a spell_list row (the loader joins on it)
    private static void SetSpellRow(int spell)
    {
        var r = Sql(db, "SELECT 1 AS s FROM spell_list WHERE spellid = ?", BoundValue.Of((ushort)spell));
        if (r == null || !r.Next()) throw new InvalidOperationException($"spell {spell}: no spell_list row of that id");
        Sql(db, "INSERT IGNORE INTO char_spells(charid, spellid) VALUES (?, ?)", BoundValue.Of(CharId), BoundValue.Of((ushort)spell));
    }

    // spell <id> after the login: a GM's player:addSpell; the char knows it after, or the run fails (addSpell ignores an
    // unknown id)
    private static void AddSpellStep(int spell)
    {
        string got = Lua($"player:addSpell({spell}) report(tostring(player:hasSpell({spell})))");
        if (got != "true") throw new InvalidOperationException($"spell {spell}: hasSpell is {got} after addSpell (no such spell?)");
    }

    // effect <id>: a GM's addStatusEffect in godmode's form (power 1, no duration: until removed); the run fails when the
    // engine refuses it
    private static void AddEffectStep(int effect)
    {
        string got = Lua($"report(tostring(player:addStatusEffect({effect}, {{ power = 1, origin = player }})))");
        if (EffectProblem(effect, got) is { } problem) throw new InvalidOperationException(problem);
    }
    internal static string EffectProblem(int effect, string got) =>
        got == "true" ? null : $"effect {effect}: addStatusEffect returned {got} (the char has it, or one that overwrites it?)";

    // npcstatus <Npc> <normal|disappear>: a GM's GetNPCByID(id):setStatus; the status read back must be it
    private static void NpcStatusStep(uint npc, string status)
    {
        string name = status.ToUpperInvariant();
        string got = Lua($"local n = GetNPCByID({npc}) n:setStatus(xi.status.{name}) report(tostring(n:getStatus() == xi.status.{name}))");
        if (got != "true") throw new InvalidOperationException($"npcstatus {npc} {status}: the NPC's status is not {name} after setStatus");
    }

    // missionstatus <MissionLog> <value> [pos=<n>]: a GM's setMissionStatus; getMissionStatus reads the value back
    private static void MissionStatusStep(int log, int value, int pos)
    {
        string set = pos < 0 ? $"player:setMissionStatus({log}, {value})" : $"player:setMissionStatus({log}, {value}, {pos})";
        string get = pos < 0 ? $"player:getMissionStatus({log})" : $"player:getMissionStatus({log}, {pos})";
        string got = Lua($"{set} report(tostring({get}))");
        if (got != value.ToString(CultureInfo.InvariantCulture))
            throw new InvalidOperationException($"missionstatus {log} {value}{(pos < 0 ? "" : $" pos={pos}")}: getMissionStatus reads {got} after it");
    }

    // currency <name> <n>: a GM's addCurrency; getCurrency must rise by n (an unknown name stays 0, the cap holds it)
    private static void CurrencyStep(string name, int amount)
    {
        string got = Lua($"local before = player:getCurrency({LuaStr(name)}) player:addCurrency({LuaStr(name)}, {amount}) " +
                         $"report(tostring(before)) report(tostring(player:getCurrency({LuaStr(name)})))");
        if (CurrencyProblem(name, amount, got) is { } problem) throw new InvalidOperationException(problem);
    }
    internal static string CurrencyProblem(string name, int amount, string got)
    {
        var p = got.Split(',');
        return p.Length == 2 && long.TryParse(p[0], out var before) && long.TryParse(p[1], out var after) && after == before + amount ? null
            : $"currency {name} {amount}: getCurrency was {(p.Length == 2 ? $"{p[0]}, then {p[1]}" : got)} (an unknown currency, or its cap?)";
    }

    // the run's step warnings (ItemStep's), "<line>: [<label>] <problem>", printed after the flow (WARN), not failed
    internal static readonly List<string> stepWarnings_ = new();

    // item <Item> [x<n>] [wsp=<n>] after the login: a GM's addItem; the char's count of it must rise by n, else the
    // step is warned of (a Rare item the char holds, or a full bag, adds nothing and addItem says so to no one: the
    // flow would go on as if the item had been given). A warning, not a failure, so a new flow's such step shows in
    // the replay's output without changing the run. `item <Item> ... held` (carry-harness-3) declares a step that
    // adds nothing on purpose (the char holds the Rare item from a drop or a reward, the GM's give a no-op): the
    // char must hold one before it and the count must stay as it was, else the run fails
    private static void ItemStep(Statement st)
    {
        int amount = st.WsPoints is int ? 1 : st.Amount;
        string add = st.WsPoints is int points
            ? $"player:addItem({{ id = {st.Value}, exdata = {{ unlockPoints = {points} }} }})"
            : $"player:addItem({st.Value}, {st.Amount})";
        // the char's count of the item in every container, as the zone holds it (no player:getItemCount binding)
        long Count() => c.Items.Where(i => i.ItemId == st.Value).Sum(i => (long)i.Quantity);
        long before = Count();
        Lua(add);
        if (st.Held)
        {
            if (HeldItemProblem(st.Value, amount, $"{before},{Count()}") is { } held)
                throw new InvalidOperationException(held);
        }
        else if (ItemProblem(st.Value, amount, $"{before},{Count()}") is { } problem)
            stepWarnings_.Add($"{st.Line}: [{st.Label}] {problem}");
    }
    // item ... held: the char holds the item before the addItem (one at least), and the count stays the same (a
    // full bag with none held adds nothing too, but is not what held declares)
    internal static string HeldItemProblem(int item, int amount, string got)
    {
        var p = got.Split(',');
        return p.Length == 2 && long.TryParse(p[0], out var before) && long.TryParse(p[1], out var after) &&
               before >= 1 && after == before ? null
            : $"item {item} x{amount} held: the char held {(p.Length == 2 ? $"{p[0]}, then {p[1]}" : got)}, " +
              "and a held step's item is held already and adds nothing (the item not held before it?)";
    }
    internal static string ItemProblem(int item, int amount, string got)
    {
        var p = got.Split(',');
        return p.Length == 2 && long.TryParse(p[0], out var before) && long.TryParse(p[1], out var after) &&
               after == before + amount ? null
            : $"item {item} x{amount}: the char held {(p.Length == 2 ? $"{p[0]}, then {p[1]}" : got)} " +
              "(a Rare item the char holds, or a full bag?)";
    }

    // bfwin: a GM's setStatus(WON) on the char's battlefield, then the two ticks a kill takes (the win's 32001 opens in them)
    private static void BattlefieldWinStep()
    {
        Collect();
        string got = Lua("local bf = player:getBattlefield() if bf == nil then report('none') else bf:setStatus(xi.battlefield.status.WON) report('won') end");
        if (got != "won") throw new InvalidOperationException("bfwin: the char is in no battlefield");
        Tick(1000);
        Tick(1000);
    }

    // heal | heal stop (B3b Adoulin group 4): GP_CLI_COMMAND_CAMP (0x0E8), mode On (1) or Off (2), as the client's /heal
    // sends it, through the engine's own path (ZoneHost.OnCamp). The healing effect must then be on (heal) or gone (heal
    // stop): a 0x0E8 the engine drops (already resting or not resting, engaged, dead, a blocking status) fails the run
    // instead of recording nothing
    private static void HealStep(int mode)
    {
        Collect();
        Send(0x0E8, C2sCamp.SizeOf, b => C2sCamp.SetMode(b, (uint)mode));
        if (HealProblem(mode, c.StatusEffects.HasStatusEffect(EffectId.Healing)) is { } problem) throw new InvalidOperationException(problem);
    }
    internal static string HealProblem(int mode, bool healing) => healing == (mode == (int)CampMode.On) ? null
        : mode == (int)CampMode.On ? "heal: the char is not resting after the 0x0E8 (resting already, engaged, dead, or a status that blocks it?)"
        : "heal stop: the char is still resting after the 0x0E8 (was it resting?)";

    // kill / claim's mob: #<id> (quest rework P8: a mob of the char's zone by id), else the IDs.lua key and offset
    private static uint StatementMob(Statement st)
    {
        if (st.MobNumber is not uint id) return MobId(st.Name, st.Offset);
        return Here.MobById.ContainsKey(id) ? id : throw new InvalidOperationException($"{st.Verb} #{id}: no mob of zone {c.Zone} has that id");
    }

    private static void SetLevelCapRow(int cap) =>
        Sql(db, "UPDATE char_jobs SET genkai = ? WHERE charid = ?", BoundValue.Of((byte)cap), BoundValue.Of(CharId));

    // setMerits as the char_exp row's merits (getMeritCount; the loader caps them at the bank, as setMerits does)
    private static void SetMeritsRow(int merits) =>
        Sql(db, "INSERT INTO char_exp(charid, merits) VALUES (?, ?) ON DUPLICATE KEY UPDATE merits = ?",
            BoundValue.Of(CharId), BoundValue.Of((byte)merits), BoundValue.Of((byte)merits));

    private static void SetCharVarRow(string name, int value) =>
        Sql(db, "REPLACE INTO char_vars(charid, varname, value, expiry) VALUES (?, ?, ?, 0)", BoundValue.Of(CharId), BoundValue.Of(name), BoundValue.Of(value));

    // ---- the client --------------------------------------------------------------------------------------------------

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    private static void Send(ushort opcode, int size, Action<byte[]> fill = null)
    {
        var buf = new byte[size];
        buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
        fill?.Invoke(buf);
        zone.Dispatch(s, opcode, new BasicPacket(buf));
        zone.AfterParse(s);
        PumpIpc();
    }
    // each char's session's packets so far, kept for the step's record, the sessions' lists emptied
    private static void Collect()
    {
        foreach (var a in actors_)
        {
            if (a.S == null) continue;
            a.Pending.AddRange(a.S.PacketList);
            a.S.PacketList.Clear();
        }
    }

    // the 0x00A login and 0x00C, the zone-in cutscene ended with 0x05B as the client does
    // every event open after the zone-in (a cutscene onZoneIn returned; up to 4) ended as the client answers it: with the
    // option given. open (login / rezone / warp ... open, quest rework P5): none is answered, the zone-in's event stays
    // open for the flow's update / finish steps
    private static void Login(uint option = 0, bool open = false)
    {
        Collect();
        if (s != null) zone.OnCharTimeout(s, false);
        Collect();
        s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        c = (CharData)s.Char;
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        for (int i = 0; !open && i < 4 && c.IsInEvent; i++)
        {
            var ev = (ushort)c.CurrentEvent.EventId;
            Send(0x05B, 20, b =>
            {
                BitConverter.GetBytes(option).CopyTo(b, 8);
                BitConverter.GetBytes(ev).CopyTo(b, 0x12);
            });
        }
        Collect();
    }
    private static void Logout()
    {
        Collect();
        if (s != null) zone.OnCharTimeout(s, false);
        Collect();
        pending_.Clear();
        s = null;
        c = null;
    }
    // the zone's timers: the clock moved on, then a tick
    private static void Tick(int ms = 1000)
    {
        now += ms;
        PumpIpc();
        zone.Tick();
    }
    // GP_CLI_COMMAND_POS: walk to the place
    private static void MoveTo(float x, float y, float z)
    {
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(x).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(z).CopyTo(b, C2sPos.OffY);
        });
    }
    private static ZoneInfo Here => c.Instance?.Info ?? zone.ZoneInfoOf(c.Zone);
    private static NpcEntity Npc(uint id) => Here.NpcById[id];
    private static uint NpcId(string name) =>
        Here.Npcs.FirstOrDefault(n => n.Name == name)?.Id ?? throw new InvalidOperationException($"no NPC named {name} in zone {c.Zone}");
    // the char stands at yalms east of the NPC (Flow.TriggerDistance by default)
    private static void Near(uint id, float yalms = Flow.TriggerDistance)
    {
        var n = Npc(id);
        MoveTo(n.X + yalms, n.Y, n.Z);
    }
    // GP_CLI_COMMAND_ACTION Talk
    private static void Talk(uint id)
    {
        var n = Npc(id);
        Collect();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(n.Id).CopyTo(b, 4);
            BitConverter.GetBytes(n.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
        });
    }
    // trigger <Npc> here (quest rework P6): talk to the NPC from where the char stands. The run fails when the NPC is out
    // of the engine's talk range (ZoneHost.InTalkRange, OnTalk's own test): the engine would answer with a release only, on
    // both sides alike, and a step meant to take a position's arm would pass with no handler run
    private static uint TalkHere(uint id)
    {
        if (TalkHereProblem(c, Npc(id)) is { } problem) throw new InvalidOperationException(problem);
        Talk(id);
        return id;
    }

    // why the char cannot talk to the NPC from where it stands; null when the NPC is in the engine's talk range
    internal static string TalkHereProblem(CharData ch, NpcEntity n)
    {
        if (ZoneHost.InTalkRange(ch, n)) return null;
        var inv = CultureInfo.InvariantCulture;
        double d = Math.Sqrt((n.X - ch.X) * (n.X - ch.X) + (n.Y - ch.Y) * (n.Y - ch.Y) + (n.Z - ch.Z) * (n.Z - ch.Z));
        return $"trigger {n.Name} here: the char at {ch.X.ToString("0.###", inv)},{ch.Y.ToString("0.###", inv)},{ch.Z.ToString("0.###", inv)} is " +
               $"{d.ToString("0.##", inv)} yalms from it, out of talk range ({ZoneHost.TalkRange} yalms): walk closer first";
    }
    // walk up to the NPC (by name), to yalms east of it, and talk to it
    private static uint TalkTo(string name, float yalms = Flow.TriggerDistance) => TalkTo(NpcId(name), yalms);
    // the same by id
    private static uint TalkTo(uint id, float yalms)
    {
        Near(id, yalms);
        Talk(id);
        return id;
    }
    // the NPC of a trigger / trade statement. <Npc>: the first of that name (NpcId, as always); <Npc>+<n>: the first of
    // that name in id order, plus n (NPCs that share a name: the Braziers); <KEY>[+<n>]: zones[zone].npc[KEY] (the first of
    // a list), plus n. An id that names no NPC of the zone, or one named otherwise than the first, is refused
    private static uint StatementNpc(Statement st)
    {
        if (!st.NpcKey && st.Offset == 0) return NpcId(st.Name);
        uint first;
        if (st.NpcKey)
        {
            var v = Lua($"local n = zones[{c.Zone}].npc[{LuaStr(st.Name)}] if type(n) == 'table' then n = n[1] end report(tostring(n))");
            if (!uint.TryParse(v, out first)) throw new InvalidOperationException($"zones[{c.Zone}].npc.{st.Name} is {v}");
        }
        else
            first = Here.Npcs.Where(n => n.Name == st.Name).Select(n => (uint?)n.Id).Min()
                    ?? throw new InvalidOperationException($"no NPC named {st.Name} in zone {c.Zone}");
        return NpcAtOffset(st.Name, first, st.Offset, id => Here.NpcById.TryGetValue(id, out var n) ? n.Name : null);
    }
    // the first NPC's id plus the offset, when an NPC of the zone has it and bears the first's name
    internal static uint NpcAtOffset(string label, uint first, int offset, Func<uint, string> nameOf)
    {
        string name = nameOf(first) ?? throw new InvalidOperationException($"{label} ({first}) names no NPC of zone {c?.Zone}");
        long at = (long)first + offset;
        string there = at is >= 0 and <= uint.MaxValue ? nameOf((uint)at) : null;
        if (there == null) throw new InvalidOperationException($"{label}+{offset} ({at}) names no NPC of zone {c?.Zone}");
        if (there != name) throw new InvalidOperationException($"{label}+{offset} ({at}) is {there}, not a {name}");
        return (uint)at;
    }
    // GP_CLI_COMMAND_EVENTEND for the current event, from its own entity (the NPC, or the char for its own cutscene);
    // mode 1 is an event update
    private static void EndEvent(int eventId, uint option = 0, ushort mode = 0)
    {
        var (id, targ) = c.CurrentEvent?.TargetEntity switch
        {
            NpcEntity n => (n.Id, n.TargId),
            BattleEntity b => (b.Id, b.TargId),
            _ => (c.Id, c.TargId),
        };
        Collect();
        Send(0x05B, 20, b =>
        {
            BitConverter.GetBytes(id).CopyTo(b, 4);
            BitConverter.GetBytes(option).CopyTo(b, 0x08);
            BitConverter.GetBytes(targ).CopyTo(b, 0x0C);
            BitConverter.GetBytes(mode).CopyTo(b, 0x0E);
            BitConverter.GetBytes((ushort)eventId).CopyTo(b, 0x12);
        });
    }
    // GP_CLI_COMMAND_PASSWARDS (0x060): the text the client typed into the current event (16 bytes, NUL-padded), from the
    // event's own entity; it reaches onEventUpdate as a Lua string. The packet carries no event id, so the flow's csid
    // must be the current event
    private static void UpdateText(int eventId, string text)
    {
        if (!c.IsInEvent || c.CurrentEvent.EventId != eventId)
            throw new InvalidOperationException($"update {eventId} option=\"{text}\": the current event is {(c.IsInEvent ? c.CurrentEvent.EventId.ToString(CultureInfo.InvariantCulture) : "none")}");
        var (id, targ) = c.CurrentEvent.TargetEntity switch
        {
            NpcEntity n => (n.Id, n.TargId),
            BattleEntity b => (b.Id, b.TargId),
            _ => (c.Id, c.TargId),
        };
        Collect();
        Send(0x060, 28, b =>
        {
            BitConverter.GetBytes(id).CopyTo(b, 4);
            BitConverter.GetBytes(targ).CopyTo(b, 8);
            Encoding.ASCII.GetBytes(text).CopyTo(b, 12);
        });
    }
    // the current event finished with the option, whatever it is
    private static void Finish(uint option = 0) => EndEvent(c.IsInEvent ? c.CurrentEvent.EventId : 0, option);
    // the current event finished (option 0 unless given), when there is one: an NPC's default may be a message
    private static void FinishIfEvent(uint option = 0)
    {
        if (c.IsInEvent) Finish(option);
    }
    // GP_CLI_COMMAND_ITEM_TRANSFER: (inventory slot, quantity) offers
    private static void Trade(uint id, params (byte Slot, uint Qty)[] items)
    {
        var n = Npc(id);
        // quest rework P9 (D3): the engine drops a trade from past 6.0 yalms without a word (ZoneHost.Trade.cs, C++
        // 0x036_item_transfer.cpp), which would record an empty step; the step fails instead (the trade does not walk)
        float dx = n.X - c.X, dy = n.Y - c.Y, dz = n.Z - c.Z;
        if (TradeRangeProblem(id, MathF.Sqrt(dx * dx + dy * dy + dz * dz)) is { } far) throw new InvalidOperationException(far);
        Collect();
        Send(0x036, 64, b =>
        {
            BitConverter.GetBytes(n.Id).CopyTo(b, 4);
            for (int i = 0; i < items.Length; i++)
            {
                BitConverter.GetBytes(items[i].Qty).CopyTo(b, 8 + i * 4);
                b[48 + i] = items[i].Slot;
            }
            BitConverter.GetBytes(n.TargId).CopyTo(b, 58);
            b[60] = (byte)items.Length;
        });
    }
    // a trade past the engine's 6.0 yalms (the same float distance it measures), null within
    internal static string TradeRangeProblem(uint npc, float distance) =>
        distance > 6.0f ? $"trade {npc}: the char stands {distance.ToString("0.##", CultureInfo.InvariantCulture)} yalms off, past the engine's 6.0: " +
                          "it would drop the trade (trigger the NPC first, or walk)" : null;
    // a trade of items by id: each quantity from the inventory slots that hold the item, in slot order
    private static void TradeItems(uint id, params (int Item, uint Qty)[] items)
    {
        var offers = new List<(byte, uint)>();
        foreach (var (item, qty) in items)
        {
            uint left = qty;
            foreach (var i in c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == item).OrderBy(i => i.Slot))
            {
                if (left == 0) break;
                uint take = Math.Min(left, i.Quantity);
                offers.Add((i.Slot, take));
                left -= take;
            }
        }
        Trade(id, offers.ToArray());
    }
    // a GM !pos: the char set down at the place, in another zone through the tick that zones it and the next login (the
    // numbers written invariantly: Lua reads 1.5, never 1,5)
    private static void Warp(float x, float y, float z, ushort zoneId, uint option = 0, bool open = false)
    {
        Lua(string.Format(CultureInfo.InvariantCulture, "player:setPos({0}, {1}, {2}, 0, {3})", x, y, z, zoneId));
        Tick(400);
        Login(option, open);
    }
    // GP_CLI_COMMAND_MAPRECT: the zone line at its origin, then the tick that zones the char and the next login. The
    // line's arrival slot is put back to the first before (it rotates per use), so a take lands on the same spot
    // however often the line was taken before
    private static void TakeZoneLine(string rect)
    {
        var line = zone.ZoneInfoOf(c.Zone).ZoneLines[ZoneLine.PackId(rect)];
        line.ResetSpawnSlot();
        MoveTo(line.OriginX, line.OriginY, line.OriginZ);
        Send(0x05E, 24, b => BitConverter.GetBytes(line.Id).CopyTo(b, 4));
        zone.Tick();
        Login();
    }
    // the first zone line from here to the zone
    private static string LineTo(ushort to) =>
        ZoneLine.IdText((zone.ZoneInfoOf(c.Zone).ZoneLines.Values.FirstOrDefault(l => l.DestinationZone == to)
                         ?? throw new InvalidOperationException($"no zone line from zone {c.Zone} to {to}")).Id);
    // through the zone lines, zone by zone, to the last one given
    private static void Route(params ushort[] zones)
    {
        foreach (var to in zones) TakeZoneLine(LineTo(to));
    }
    // GP_CLI_COMMAND_GROUP_SOLICIT_REQ (0x06E): a party invite to the char, by its id and, when it stands in this char's
    // zone, its act index (0 otherwise: the zone host finds it by id)
    private static void Invite(Actor to)
    {
        if (to.C == null) throw new InvalidOperationException($"invite {to.Name}: {to.Name} is not logged in");
        ushort targ = to.C.Zone == c.Zone && to.C.Instance == c.Instance ? to.C.TargId : (ushort)0;
        Collect();
        Send(0x06E, 12, b =>
        {
            BitConverter.GetBytes(to.Id).CopyTo(b, 4);
            BitConverter.GetBytes(targ).CopyTo(b, 8);
            b[10] = 0;
        });
    }
    // GP_CLI_COMMAND_GROUP_SOLICIT_RES (0x074): the answer to the invite pending; an acceptance then ticks once, when
    // the party's reload (the PartyReload the world routes back) sends the lists
    private static void AnswerInvite(bool accept)
    {
        uint inviter = c.InvitePending.UniqueNo;
        Collect();
        Send(0x074, 8, b => b[4] = (byte)(accept ? 1 : 0));
        if (!accept) return;
        PinPartyOrder(inviter, c.Id);
        Tick(400);
    }

    // the chars of the flow in the order they joined a party (the first inviter first: it leads)
    private static readonly List<uint> joined_ = new();

    // the engine lists a party (GROUP_TBL, GROUP_LIST: ORDER BY partyflag & 3, timestamp) by accounts_parties.timestamp,
    // the database's wall clock in seconds at the row's insert; members that join in one second tie, and MariaDB orders a
    // tie as its plan has it. Each acceptance (before the tick that sends the lists: the engine reads no list order before
    // it) sets every joined char's row to a fixed second in join order, so the lists follow the flow, not the clock. The
    // column is a timestamp, which no trace records (bound as an int: BoundValue has no long, and a long would go as a float)
    private static void PinPartyOrder(uint inviter, uint invitee)
    {
        if (inviter != 0 && !joined_.Contains(inviter)) joined_.Add(inviter);
        if (!joined_.Contains(invitee)) joined_.Add(invitee);
        for (int k = 0; k < joined_.Count; k++)
            Sql(db, "UPDATE accounts_parties SET timestamp = FROM_UNIXTIME(?) WHERE charid = ?", BoundValue.Of((int)(UnixStart + k)), BoundValue.Of(joined_[k]));
    }
    // GP_CLI_COMMAND_EQUIP_SET: the inventory slot's item worn in the equipment slot
    private static void Equip(byte invSlot, byte equipSlot)
    {
        Collect();
        Send(0x050, 8, b => { b[4] = invSlot; b[5] = equipSlot; b[6] = 0; });
    }
    // the first inventory slot that holds the item
    private static byte InventorySlotOf(int item) =>
        c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == item).OrderBy(i => i.Slot).Select(i => (byte?)i.Slot).FirstOrDefault()
        ?? throw new InvalidOperationException($"item {item} is not in the inventory");
    // GP_CLI_COMMAND_SCENARIOITEM: the key item looked at in the key item menu (seen)
    private static void SeeKeyItem(int keyItem)
    {
        Collect();
        Send(0x064, 76, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(1u << keyItem % 32).CopyTo(b, 8 + keyItem % 512 / 32 * 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, 72);
            BitConverter.GetBytes((ushort)(keyItem / 512)).CopyTo(b, 74);
        });
    }
    // the inventory filled with Bronze Swords (one a slot), and those taken out again: a GM's items
    private static void FillInventory()
    {
        Collect();
        Lua("for i = 1, 80 do if player:getFreeSlotsCount() == 0 then break end player:addItem(xi.item.BRONZE_SWORD) end");
    }
    private static void FreeInventory()
    {
        Collect();
        Lua("for i = 1, 80 do if not player:hasItem(xi.item.BRONZE_SWORD) then break end player:delItem(xi.item.BRONZE_SWORD, 1) end");
    }
    // walked to the place, then the zone's ticks (a trigger area is entered on the tick after the move)
    private static void WalkTo(float x, float y, float z, int ticks = 2)
    {
        Collect();
        MoveTo(x, y, z);
        for (int i = 0; i < ticks; i++) Tick(400);
    }
    // the mob's id from its zone's IDs.lua mob table (the first of a list), plus the offset (kill <Mob>+<n>, <Mob>-<n>); an
    // id that names no mob of the zone is refused
    private static uint MobId(string key, int offset = 0)
    {
        var v = Lua($"local m = zones[{c.Zone}].mob[{LuaStr(key)}] if type(m) == 'table' then m = m[1] end report(tostring(m))");
        if (!uint.TryParse(v, out var id)) throw new InvalidOperationException($"zones[{c.Zone}].mob.{key} is {v}");
        return MobAtOffset(key, id, offset, Here.MobById.ContainsKey);
    }
    // the key's id plus the offset (negative: minus), when a mob of the zone has it
    internal static uint MobAtOffset(string key, uint id, int offset, Func<uint, bool> isMob)
    {
        long at = (long)id + offset;
        return offset == 0 || at is >= 0 and <= uint.MaxValue && isMob((uint)at) ? (uint)at
            : throw new InvalidOperationException($"{key}{offset:+0;-0} ({at}) names no mob of zone {c?.Zone}");
    }
    // a mob the scripts spawned killed by the char: claimed first if it is not (the char's swing; onMobDeath gets a
    // player only through the claim), then the damage, the death on the next tick, the defeat (onMobDeath) a second on
    // kill <Mob> nokiller (quest rework P9, D2): the mob dies with no killer, as an unclaimed death does
    // (CMobEntity::DistributeRewards, ZoneHost.Exp.cs: OnMobDeath(m, nil)): a GM's mob:setHP(0) on an unclaimed mob, the
    // death on the next tick, the defeat a second on. A claimed mob is refused (its death has its owner as the killer)
    private static void KillNoKiller(uint mobId)
    {
        Collect();
        var m = Here.MobById[mobId];
        if (NoKillerProblem(mobId, m.OwnerId, m.IsSpawned, m.IsDead) is { } problem) throw new InvalidOperationException(problem);
        Lua($"local mob = GetMobByID({mobId}, player:getInstance()) mob:setHP(0)");
        Tick(1000);
        Tick(1000);
        if (!m.IsDead && m.IsSpawned) throw new InvalidOperationException($"kill {mobId} nokiller: the mob did not die");
    }
    internal static string NoKillerProblem(uint mob, uint owner, bool spawned, bool dead) =>
        !spawned ? $"kill {mob} nokiller: the mob is not spawned (pop it first)"
        : dead ? $"kill {mob} nokiller: the mob is dead"
        : owner != 0 ? $"kill {mob} nokiller: claimed by {owner}: its death would have a killer"
        : null;
    private static void KillByChar(uint mobId)
    {
        Collect();
        ClaimBySwing(mobId);
        Lua($"local mob = GetMobByID({mobId}, player:getInstance()) mob:takeDamage(mob:getHP(), player, xi.attackType.PHYSICAL, xi.damageType.SLASHING)");
        Tick(1000);
        Tick(1000);
    }
    // the char walks to 2 yalms west of the mob and swings (0x015, then the attack 0x01A), then a tick a second until the
    // claim takes. Quest rework P9 (D1): a claim that has not taken in 30 s fails the step (ClaimProblem), where it used
    // to go on, and a kill then killed the mob unclaimed: no killer, no exp, the quest's onMobDeath never reached by the
    // char, a step that recorded nothing and passed
    private static void ClaimBySwing(uint mobId)
    {
        var m = Here.MobById[mobId];
        if (m.OwnerId == c.Id) return;
        SwingAt(m);
        int ticks = 0;
        for (; m.OwnerId != c.Id && ticks < 30; ticks++)
        {
            Tick(1000);
            // D4: the engine refused the attack (the engage lockout after the last kill's disengage: CCharEntity::
            // OnDisengage's 1.5 s or more), so the char is not engaged: the player presses attack again. Or the mob
            // walked out of the char's reach or from in front of it (a pathing NM; the engine's swing wants the target in
            // front: facing(A, B, 64)): the player follows it, and swings again
            if (ResendAttack && m.OwnerId != c.Id && m.IsSpawned && !m.IsDead &&
                SwingAgain(c.AttackTarget != null, DistanceTo(m), SwingReach(c.ModelHitboxSize10, m.ModelHitboxSize), FacingMob(m))) SwingAt(m);
        }
        if (ClaimProblem(m.Id, m.OwnerId, c.Id, m.IsSpawned, m.IsDead) is { } problem) throw new InvalidOperationException(problem);
    }
    // D4 (quest rework P9): resend a refused attack, and follow a mob that walked off, as a player would (false: the one
    // swing of old, for a comparison run)
    internal static bool ResendAttack = true;
    // past this the char walks to the mob again (the walk sets it down 2 yalms off): the engine's swing reach,
    // CCharEntity::CanAttack's (ZoneHost.Combat.cs CanAttack: the char's hitbox + 2 + the mob's)
    internal static float SwingReach(byte charHitbox10, float mobHitbox) => charHitbox10 / 10.0f + 2.0f + mobHitbox;
    // the char swings again: not engaged (the engine refused the attack), or the mob past the reach or out of its front
    internal static bool SwingAgain(bool engaged, float distance, float reach, bool facing) => !engaged || distance > reach || !facing;
    // facing(char, mob, 64), the engine's swing test (ZoneHost.Combat.cs Facing)
    private static bool FacingMob(MobEntity m)
    {
        int diff = ZoneHost.WorldAngle(c.X, c.Z, c.Rotation, m.X, m.Z) - c.Rotation;
        if (diff > 128) diff -= 256;
        else if (diff < -128) diff += 256;
        return Math.Abs(diff) < 32;
    }
    private static float DistanceTo(MobEntity m)
    {
        float dx = m.X - c.X, dy = m.Y - c.Y, dz = m.Z - c.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    private static void SwingAt(MobEntity m)
    {
        Send(0x015, 32, b =>
        {
            BitConverter.GetBytes(m.X - 2.0f).CopyTo(b, C2sPos.OffX);
            BitConverter.GetBytes(m.Y).CopyTo(b, C2sPos.OffZ);
            BitConverter.GetBytes(m.Z).CopyTo(b, C2sPos.OffY);
            b[C2sPos.OffDir] = 0;
        });
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(m.Id).CopyTo(b, 4);
            BitConverter.GetBytes(m.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Attack).CopyTo(b, C2sAction.OffActionID);
        });
    }
    // why a claim did not take after its 30 s (null: it took): not spawned, dead, another's, or nobody's (out of reach)
    internal static string ClaimProblem(uint mob, uint owner, uint charId, bool spawned, bool dead) =>
        owner == charId ? null
        : !spawned ? $"claim {mob}: the mob is not spawned (pop it first)"
        : dead ? $"claim {mob}: the mob is dead"
        : owner != 0 ? $"claim {mob}: claimed by {owner}, not the char"
        : $"claim {mob}: the char's swing did not claim it in 30 s (out of reach? a kill would die unclaimed, with no killer)";

    // ---- the step's record (B2's Step) ---------------------------------------------------------------------------------

    // InteractionGlobal's entry points keep what each call returned to the engine (type and value) until the next step.
    // Installed again after a flow's modules load (Modules.cs): scripts/globals load through DoFile, with no
    // package.loaded (as C++ luautils.cpp:557), so a module's require('scripts/globals/interaction/interaction_global')
    // runs that file again and puts the plain entry points back over the wrappers. An entry point still wrapped
    // (__verifyWrapped) is left alone, so a second install never wraps one twice, and the returns kept so far stay
    private static void InstallReturnRecorder()
    {
        lua.DoString(@"
            __verifyRets = __verifyRets or {}
            __verifyWrapped = __verifyWrapped or setmetatable({}, { __mode = 'k' })
            local function pack(...) return { n = select('#', ...), ... } end
            for _, name in ipairs({ 'onTrigger', 'onTrade', 'onEventFinish', 'onEventUpdate', 'onZoneIn', 'onZoneOut',
                                    'afterZoneIn', 'onMobDeath', 'onTriggerAreaEnter', 'onTriggerAreaLeave' }) do
                local f = InteractionGlobal[name]
                if not __verifyWrapped[f] then
                    local function wrapper(...)
                        local r = pack(f(...))
                        local parts = {}
                        for i = 1, r.n do
                            local v = r[i]
                            parts[#parts + 1] = type(v) == 'table' and 'table' or (type(v) .. ':' .. tostring(v))
                        end
                        __verifyRets[#__verifyRets + 1] = name .. '=' .. table.concat(parts, '/')
                        return unpack(r, 1, r.n)
                    end
                    __verifyWrapped[wrapper] = true
                    InteractionGlobal[name] = wrapper
                end
            end", "=verify_returns");
    }

    private static string TakeReturns()
    {
        got.Clear();
        lua.DoString("report(table.concat(__verifyRets, ',')) __verifyRets = {}", "=verify_returns");
        return got.Count > 0 ? got[0] : "";
    }

    // the step's record: the packets each char got since the last step (main's, then each other char's that got any),
    // what InteractionGlobal returned to the engine, each char's state in the database (every StateTables table's rows;
    // another char's under <name>:<table>) and the order of each lookup list named
    private static TraceStep RecordStep(string label, IReadOnlyList<(ushort Zone, string Second, string Third)> lists, IReadOnlyList<Mask> masks)
    {
        var step = new TraceStep { Label = label };
        PumpIpc();
        Collect();
        step.Packets.AddRange(MainActor.Pending.Select(p => Normalise(p, masks)));
        MainActor.Pending.Clear();
        foreach (var a in actors_.Skip(1))
        {
            if (a.Pending.Count > 0) step.Others.Add((a.Name, a.Pending.Select(p => Normalise(p, masks)).ToList()));
            a.Pending.Clear();
        }
        var tables = StateTables();
        foreach (var a in actors_)
        {
            var all = SnapshotRows(tables, a.Id);
            string prefix = a == MainActor ? "" : a.Name + TraceStep.CharTableMark;
            for (int i = 0; i < tables.Count; i++)
                foreach (var e in StateElements(tables[i].Columns.Select(c => c.Name).ToList(), tables[i].Single, all[i])) step.Set(prefix + tables[i].Name).Add(e);
        }
        step.Ret = TakeReturns();
        foreach (var (z, second, third) in lists) step.Orders.Add(($"{z}/{second}/{third}", Order(z, second, third)));
        return step;
    }

    // the columns a step leaves out, each for a reason: `table` (the whole table) or `table.column`. Only for a difference
    // between two identical runs (`stable`, both sides), and kept only while `stable all` fails without it
    private static readonly (string Name, string Reason)[] Ignored =
    {
    };

    // the tables no step records, each for a reason: three tables more would change the header of every trace, and the
    // state is already recorded through the chars blobs. Unlike Ignored, these are not a difference between two runs
    internal static readonly (string Name, string Reason)[] NotRecorded =
    {
        ("char_assault_state", "chars.assault holds the same state; XI.Verify.QuestState.Live and each load's drift check prove they agree"),
        ("char_campaign_state", "chars.campaign holds the same state; XI.Verify.QuestState.Live and each load's drift check prove they agree"),
        ("char_eminence_state", "chars.eminence holds the same state; XI.Verify.QuestState.Live and each load's drift check prove they agree"),
    };

    // a table a step records: its columns in ordinal order (Blob: rendered as hex); Single: charid is its only primary-key
    // column (one row at most), recorded a col=value element per column
    internal sealed record StateTable(string Name, IReadOnlyList<(string Name, bool Blob)> Columns, bool Single);

    // the data types read as bytes and rendered as hex, and those of the wall clock (never recorded)
    private static readonly HashSet<string> BlobTypes = new(StringComparer.OrdinalIgnoreCase) { "tinyblob", "blob", "mediumblob", "longblob", "binary", "varbinary" };
    private static readonly HashSet<string> ClockTypes = new(StringComparer.OrdinalIgnoreCase) { "timestamp", "datetime", "date" };

    // the database's columns (table, column, data type, column key) in ordinal order, read once per process
    private static List<(string Table, string Column, string Type, string Key)> schema_;
    private static List<StateTable> stateTables_;

    private static List<(string Table, string Column, string Type, string Key)> Schema()
    {
        if (schema_ != null) return schema_;
        schema_ = new();
        var r = Sql(db, "SELECT table_name AS t, column_name AS c, data_type AS d, column_key AS k FROM information_schema.columns " +
                        "WHERE table_schema = DATABASE() ORDER BY table_name, ordinal_position");
        while (r != null && r.Next()) schema_.Add((r.Get<string>("t"), r.Get<string>("c"), r.Get<string>("d"), r.Get<string>("k")));
        return schema_;
    }

    private static List<StateTable> StateTables() => stateTables_ ??= StateTablesFrom(Schema(), Ignored.Concat(NotRecorded).ToArray());

    // the blob columns of the single-row tables ("table.column"): a trace writes a same-length change to one as byte runs
    private static IEnumerable<string> BlobColumns() =>
        StateTables().Where(t => t.Single).SelectMany(t => t.Columns.Where(c => c.Blob).Select(c => $"{t.Name}.{c.Name}"));

    // the tables a step records: chars and every table with a charid column, in name order, each with its columns in
    // ordinal order but charid (the key every row has), the wall-clock types and the ignored ones; an ignored table gone
    internal static List<StateTable> StateTablesFrom(IEnumerable<(string Table, string Column, string Type, string Key)> schema, (string Name, string Reason)[] ignored)
    {
        var names = new HashSet<string>(ignored.Select(i => i.Name), StringComparer.Ordinal);
        var tables = new List<StateTable>();
        foreach (var g in schema.GroupBy(c => c.Table).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (names.Contains(g.Key) || (g.Key != "chars" && g.All(c => c.Column != "charid"))) continue;
            var keys = g.Where(c => c.Key == "PRI").Select(c => c.Column).ToList();
            var cols = g.Where(c => c.Column != "charid" && !ClockTypes.Contains(c.Type) && !names.Contains($"{g.Key}.{c.Column}"))
                .Select(c => (c.Column, BlobTypes.Contains(c.Type))).ToList();
            tables.Add(new StateTable(g.Key, cols, keys.Count == 1 && keys[0] == "charid"));
        }
        return tables;
    }

    // the snapshot's own connection (Program sets its connection string): every table's SELECT of a step in one batch, one
    // round trip, where a query each through the pooled IDatabase costs a connection reset each
    internal static string StateConnectionString;
    private static MySqlConnector.MySqlConnection stateDb_;
    private static string stateBatch_;

    // each table's rows for the char (its id and name, and every other char's of the flow, as placeholders), in the
    // tables' order, each its columns rendered (RenderValue) from the cells as MySqlDatabase reads them (GetValue) and
    // InMemoryResultSet hands them out: a blob column's bytes (a text cell as its UTF-8), another column's text (a byte
    // cell decoded as UTF-8)
    private static List<List<string[]>> SnapshotRows(List<StateTable> tables, uint charId)
    {
        try
        {
            return SnapshotRowsOnce(tables, charId);
        }
        catch (Exception ex) when (ex is MySqlConnector.MySqlException or InvalidOperationException
                                       or System.IO.IOException)
        {
            // a connection the server dropped (a long run's wait_timeout, a restart) stays dropped: open a new one and
            // read again, once (an error of the query itself fails again and goes on up)
            Console.WriteLine($"state connection: {ex.GetType().Name}: {ex.Message}; reopened");
            CloseSnapshot();
            return SnapshotRowsOnce(tables, charId);
        }
    }
    private static List<List<string[]>> SnapshotRowsOnce(List<StateTable> tables, uint charId)
    {
        if (stateDb_ == null)
        {
            stateDb_ = new MySqlConnector.MySqlConnection(StateConnectionString);
            stateDb_.Open();
            stateBatch_ = string.Concat(tables.Select(t => t.Columns.Count == 0 ? "SELECT 1 FROM DUAL WHERE FALSE;"
                : $"SELECT {string.Join(", ", t.Columns.Select(c => $"`{c.Name}`"))} FROM `{t.Name}` WHERE charid = @c;"));
        }
        using var cmd = new MySqlConnector.MySqlCommand(stateBatch_, stateDb_);
        cmd.Parameters.AddWithValue("@c", charId);
        using var reader = cmd.ExecuteReader();
        var all = new List<List<string[]>>();
        foreach (var t in tables)
        {
            var rows = new List<string[]>();
            while (reader.Read())
            {
                var row = new string[t.Columns.Count];
                for (int i = 0; i < row.Length; i++)
                {
                    object v = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    if (v != null && t.Columns[i].Blob && v is not byte[]) v = Encoding.UTF8.GetBytes(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "");
                    else if (v is byte[] bytes && !t.Columns[i].Blob) v = Encoding.UTF8.GetString(bytes);
                    row[i] = RenderCell(t.Name, t.Columns[i].Name, v, MainActor.Id);
                }
                rows.Add(row);
            }
            all.Add(rows);
            reader.NextResult();
        }
        return all;
    }

    private static void CloseSnapshot()
    {
        stateDb_?.Dispose();
        stateDb_ = null;
    }

    // the table's rows (every row: a world table's), each its columns rendered (RenderValue, with no char)
    private static List<string[]> ReadRows(string table, IReadOnlyList<(string Name, bool Blob)> cols)
    {
        var rows = new List<string[]>();
        if (cols.Count == 0) return rows;
        var r = Sql(db, $"SELECT {string.Join(", ", cols.Select(c => $"`{c.Name}`"))} FROM `{table}`");
        while (r != null && r.Next())
            rows.Add(cols.Select(c => RenderCell(table, c.Name, r.IsNull(c.Name) ? null
                : c.Blob ? r.GetBlobBytes(c.Name) ?? Array.Empty<byte>() : r.Get<string>(c.Name), null)).ToArray());
        return rows;
    }

    // a cell as a trace writes it (RenderValue), but a status effect's subid that is a char's id cut to 16 bits (the
    // zone reads char_effects.subid back as a uint16: the Battlefield effect names its initiator by id, and after a zone
    // change the reloaded effect saves the cut id) as $char16 / $char16:<name>: the char ids move from run to run with
    // the flows' order (996161 + index % 39), the cut id with them
    internal static string RenderCell(string table, string column, object value, uint? charId)
    {
        if (charId is { } id && table == "char_effects" && column == "subid" && value != null && value is not byte[] &&
            uint.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.None, CultureInfo.InvariantCulture, out uint subId) &&
            subId <= ushort.MaxValue)
        {
            if (subId == (id & 0xFFFF)) return "$char16";
            foreach (var a in actors_.Skip(1))
                if (subId == (a.Id & 0xFFFF)) return "$char16:" + a.Name;
        }
        return RenderValue(value, charId);
    }

    // a value as a trace writes it: NULL as null, bytes as hex; with a char (main), its id and its name (each the whole
    // value) as $char and $charname (they differ from slot to slot), another char's of the flow as $char:<name> and
    // $charname:<name>; else its text with backslash, newline, return and tab escaped (as \\, \n, \r, \t) so it stays on
    // its line
    internal static string RenderValue(object value, uint? charId)
    {
        if (value == null) return "null";
        if (value is byte[] b) return Convert.ToHexString(b);
        string s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (charId is { } id)
        {
            if (s == id.ToString(CultureInfo.InvariantCulture)) return "$char";
            if (s == CharName(id)) return "$charname";
            // another char of the flow: $char:<name> / $charname:<name>
            foreach (var a in actors_.Skip(1))
            {
                if (s == a.Id.ToString(CultureInfo.InvariantCulture)) return "$char:" + a.Name;
                if (s == CharName(a.Id)) return "$charname:" + a.Name;
            }
        }
        return s.IndexOfAny(Escaped) < 0 ? s : s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
    private static readonly char[] Escaped = { '\\', '\n', '\r', '\t' };

    // a table's rendered rows as a step's elements: a single-row table's col=value per column, another's one per row,
    // its col=value by '|'
    internal static IEnumerable<string> StateElements(IReadOnlyList<string> cols, bool single, IEnumerable<IReadOnlyList<string>> rows)
    {
        foreach (var row in rows)
        {
            if (single)
                for (int i = 0; i < cols.Count; i++) yield return $"{cols[i]}={row[i]}";
            else yield return string.Join("|", cols.Select((c, i) => $"{c}={row[i]}"));
        }
    }

    // the world tables a trace's header hashes (a change to their snapshot rows changes every trace's header)
    private static readonly string[] HashedWorldTables = { "server_variables", "conquest_system" };
    // the world tables whose snapshot rows are the sql/ seed's, not a database's, and which no header hashes: a
    // battlefield's record (bcnm_records: the 32000 menu shows its holder, a win writes it), so a trace sees the seed's
    // 'nobody' whatever an earlier run or the lane's history won
    private static readonly string[] SeededWorldTables = { "bcnm_records" };
    // the sql/ file holding each seeded table's INSERTs
    private static readonly Dictionary<string, string> SeedFiles = new(StringComparer.Ordinal) { ["bcnm_records"] = "bcnm_info.sql" };
    // the world tables a run copies privately and restores from the snapshot before each world (World.cs): the hashed
    // ones, then the seeded ones (after both, in this file: static initialisers run in text order)
    internal static readonly string[] WorldTables = HashedWorldTables.Concat(SeededWorldTables).ToArray();

    // the table's rows (every column, rendered as a step's but with no char placeholders, so the hash is the same whatever
    // the slot's char id; col=value by '|', sorted): the first 16 hex of their sha256
    private static string WorldHash(string table)
    {
        var cols = Schema().Where(c => c.Table == table).Select(c => (c.Column, BlobTypes.Contains(c.Type))).ToList();
        var rows = StateElements(cols.Select(c => c.Column).ToList(), false, ReadRows(table, cols))
            .OrderBy(r => r, StringComparer.Ordinal);
        return PacketStore.Hash(string.Join("\n", rows));
    }

    // a packet as hex: the masked byte ranges of its opcode as "**" (byte offsets, so before the placeholders), then the
    // chars' ids and names replaced by placeholders (they differ from trace to trace): main's <CHARID> / <CHARNAME>,
    // another char's of the flow <CHARID:<name>> / <CHARNAME:<name>>, whichever char got the packet. In a 0x028 a char's
    // id as a target is bit-packed off the byte grid: those bits are zeroed and a <CHARID@<bit>> (<CHARID:<name>@<bit>>)
    // per target follows the hex (CharTargetBits)
    internal static string Normalise(BasicPacket p, IReadOnlyList<Mask> masks)
    {
        var b = (byte[])p.Buffer.Clone();
        int size = (b[1] >> 1) * 4;
        if (size <= 0 || size > b.Length) size = b.Length;
        int op = Opcode(p);
        var charBits = new List<string>();
        if (op == 0x028)
            foreach (var a in actors_)
                charBits.AddRange(CharTargetBits(b, size, a.Id).Select(bit => a == MainActor ? $"{bit}" : $"{a.Name}@{bit}"));
        string hex = Convert.ToHexString(b, 0, size);
        if (masks.Count > 0)
        {
            var chars = hex.ToCharArray();
            foreach (var m in masks.Where(m => m.Opcode == op))
                for (int i = m.Offset; i < m.Offset + m.Length && i < size; i++) chars[2 * i] = chars[2 * i + 1] = '*';
            hex = new string(chars);
        }
        // every char's id and name, in one pass over the bytes: a named placeholder is not two characters a byte, so a
        // second pass would lose the byte grid after it
        hex = ReplaceAligned(hex, actors_.SelectMany(a => new[]
        {
            (Convert.ToHexString(BitConverter.GetBytes(a.Id)), a == MainActor ? "<CHARID>" : $"<CHARID:{a.Name}>"),
            (Convert.ToHexString(Encoding.ASCII.GetBytes(CharName(a.Id))), a == MainActor ? "<CHARNAME>" : $"<CHARNAME:{a.Name}>"),
        }).ToList());
        // main's bit-packed targets <CHARID@<bit>>, another char's <CHARID:<name>@<bit>>
        return $"{op:X3}:{hex}" + string.Concat(charBits.Select(bit => bit.Contains('@') ? $"<CHARID:{bit}>" : $"<CHARID@{bit}>"));
    }

    // 0x028 (Battle2Packets.Battle2): the bit offset of each target id field that holds the char's id, those 32 bits
    // zeroed in the buffer. The layout is walked with the engine's own bit reader (Battle2Packets.Unpack), from the
    // WorkSize byte on: the actor (byte-aligned at bit 40: the <CHARID> placeholder covers it), the target count, then
    // per target its id (32 bits) and result count (4), per result 85 bits, an added-effect flag (+37 bits when set) and
    // a spikes flag (+34 when set). A walk that would run past the packet's size stops there
    internal static List<int> CharTargetBits(byte[] b, int size, uint charId)
    {
        var at = new List<int>();
        int end = Math.Min(size, b.Length) * 8;
        int bit = 40 + 32;
        if (bit + 6 > end) return at;
        int targets = (int)Battle2Packets.Unpack(b, bit, 6);
        bit += 6 + 4 + 4 + 32 + 32;
        for (int t = 0; t < targets; t++)
        {
            if (bit + 36 > end) break;
            if ((uint)Battle2Packets.Unpack(b, bit, 32) == charId)
            {
                at.Add(bit);
                Battle2Packets.Pack(b, 0, bit, 32);
            }
            int results = (int)Battle2Packets.Unpack(b, bit + 32, 4);
            bit += 36;
            for (int r = 0; r < results; r++)
            {
                bit += 85;
                if (bit + 1 > end) return at;
                if (Battle2Packets.Unpack(b, bit++, 1) != 0) bit += 37;
                if (bit + 1 > end) return at;
                if (Battle2Packets.Unpack(b, bit++, 1) != 0) bit += 34;
            }
        }
        return at;
    }

    // replaces each hex pattern (the first that matches, in the list's order) only where it starts on a byte boundary of
    // the hex (two characters a byte), in one pass
    internal static string ReplaceAligned(string hex, IReadOnlyList<(string Pattern, string With)> patterns)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < hex.Length)
        {
            int hit = -1;
            if (i % 2 == 0)
                for (int k = 0; k < patterns.Count && hit < 0; k++)
                    if (i + patterns[k].Pattern.Length <= hex.Length && string.CompareOrdinal(hex, i, patterns[k].Pattern, 0, patterns[k].Pattern.Length) == 0) hit = k;
            if (hit >= 0)
            {
                sb.Append(patterns[hit].With);
                i += patterns[hit].Pattern.Length;
            }
            else sb.Append(hex[i++]);
        }
        return sb.ToString();
    }

    // the container ids of a lookup list in its order ('default' for a zone's DefaultActions entry): which handler the
    // lookup tries first, and which one its cycling picks next
    private static string Order(ushort zoneId, string second, string third)
    {
        string thirdKey = int.TryParse(third, out _) ? third : $"'{third}'";
        return Lua($@"
            local ids = {{}}
            local z = InteractionGlobal.lookup.data[{zoneId}]
            local list = z and z['{second}'] and z['{second}'][{thirdKey}] or {{}}
            for _, e in ipairs(list) do ids[#ids + 1] = e.container and e.container.id or 'default' end
            report(table.concat(ids, ','))");
    }
}
