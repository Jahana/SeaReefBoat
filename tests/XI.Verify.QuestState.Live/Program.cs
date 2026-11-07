using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Quests;
using XI.Zone;

// XI.Verify.QuestState.Live -- live gate for quest rework piece A (docs/superpowers/specs/2026-10-20-quest-storage-design.md):
// the char_quest_state / char_mission_state rows on the real MariaDB (xidb, or --db <name>), chars 996131-996143. The store's round trip
// (QuestStateStore), the char's load (ProgressRows: the first import, the drift check and its re-import, the prune of
// expired vars, the switch) and its writes (addQuest / completeQuest / delQuest, the missions, the Quest / Mission
// char vars through setCharVar, incrementCharVar, clearVarsWithPrefix and ClearCharVarFromAll), each landing in the
// rows and the legacy columns alike.
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static partial class Program
{
    private static int _pass, _fail;

    private const uint StoreChar = 996131;
    private static readonly uint[] CharIds = { 996131, 996132, 996133, 996134, 996135, 996136, 996137, 996138, 996139, 996140, 996141, 996142, 996143, 996144, 996145, 996146, 996147, 996148 };

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    // every char table (the new ones included), the chars row and the account
    private static void Cleanup(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var id in CharIds)
        {
            foreach (var t in tables) Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
    }

    private static int Main(string[] argv)
    {
        // [<port>] [--db <name>]: the port (3307) and the database (xidb; a golden lane's copy, tools/golden_lane_db.sh, when
        // another worktree runs on xidb)
        var args = new List<string>(argv);
        string dbName = "xidb";
        int at = args.IndexOf("--db");
        if (at >= 0)
        {
            if (at + 1 >= args.Count || !System.Text.RegularExpressions.Regex.IsMatch(args[at + 1], "^[A-Za-z0-9_]{1,48}$"))
            {
                Console.WriteLine("--db takes a database name (letters, digits, _)");
                return 1;
            }
            dbName = args[at + 1];
            args.RemoveRange(at, 2);
        }
        int dbPort = args.Count > 0 ? int.Parse(args[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = dbName };
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

        try
        {
            Cleanup(db);
            RunStore(db);
            RunConcurrentFirstLogins(db);
            Setup(db);
            RunLoad();
            RunWrites();
            RunKeys();
            RunPersistFailures();
            RunC1();
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(db);
            scripts?.Dispose();
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    // quest rework B1: the host's registry from xi.quest.id, real keys on imported and new rows, a stored legacy key
    // re-keyed at login (and two swapped names), an id outside xi.quest.id kept synthetic
    private static void RunKeys()
    {
        Console.WriteLine("quest keys");
        Check("the host logged its registry", "True", log.Contains("quests: 1084 quest keys"));
        var qb = new QuestBitBlob();
        qb.AddQuest(1, 13); qb.AddQuest(1, 4); qb.AddQuest(1, 200);
        Fixture(996135, qb.Bytes, null);
        Mark();
        Login();
        Check("first import: real keys, the unknown id synthetic", "bastok.the_elvaan_goldsmith/bastok.the_quadavs_curse/legacy:1/200",
              $"{Key(1, 13)}/{Key(1, 4)}/{Key(1, 200)}");
        Lua("player:addQuest(1, 18)");
        Check("addQuest writes the real key", "bastok.till_death_do_us_part", Key(1, 18));

        Logout();
        Sql(db, "UPDATE char_quest_state SET quest_key = 'legacy:1/13' WHERE charid = ? AND area = 1 AND quest_id = 13", BoundValue.Of(CharId));
        Mark();
        Login();
        Check("a stored legacy key: re-keyed at login, logged", "progress: char 996135 re-keyed 1 quests/bastok.the_elvaan_goldsmith",
              $"{ProgressLines()}/{Key(1, 13)}");
        Check("  the projection == the legacy columns", "True/True", RowsMatchLegacy());

        Logout();
        // the two rows' keys swapped by hand: the re-key cannot collide on (charid, quest_key)
        Sql(db, "UPDATE char_quest_state SET quest_key = 'tmp' WHERE charid = ? AND area = 1 AND quest_id = 13", BoundValue.Of(CharId));
        Sql(db, "UPDATE char_quest_state SET quest_key = 'bastok.the_elvaan_goldsmith' WHERE charid = ? AND area = 1 AND quest_id = 4", BoundValue.Of(CharId));
        Sql(db, "UPDATE char_quest_state SET quest_key = 'bastok.the_quadavs_curse' WHERE charid = ? AND area = 1 AND quest_id = 13", BoundValue.Of(CharId));
        Mark();
        Login();
        Check("two swapped keys: both re-keyed", "progress: char 996135 re-keyed 2 quests/bastok.the_elvaan_goldsmith/bastok.the_quadavs_curse",
              $"{ProgressLines()}/{Key(1, 13)}/{Key(1, 4)}");
        Mark();
        Login();
        Check("the next login: nothing logged", "", ProgressLines());
        Check("  Lua reads the same statuses", "1,1,1,1",
              Lua("report(player:getQuestStatus(1, 13)) report(player:getQuestStatus(1, 4)) report(player:getQuestStatus(1, 200)) report(player:getQuestStatus(1, 18))"));
        Logout();

        // two names swapped between script versions, the stored 1/5 row holding only an expired soft var: its pruned delete
        // runs after the re-key, so the re-key must not meet its stored key (the registry key of 1/4) on the primary key
        Sql(db, "UPDATE char_quest_state SET quest_key = 'bastok.out_of_ones_shell' WHERE charid = ? AND area = 1 AND quest_id = 4", BoundValue.Of(CharId));
        Sql(db, "INSERT INTO char_quest_state (charid, quest_key, area, quest_id, status, step, progress) VALUES (?, 'bastok.the_quadavs_curse', 1, 5, 0, 0, '{\"@expiry\":{\"Wait\":1},\"Wait\":5}')", BoundValue.Of(CharId));
        Mark();
        Login();
        Check("a pruned row holding a re-keyed row's new key: re-keyed, then pruned away", "progress: char 996135 re-keyed 2 quests/bastok.the_quadavs_curse/-/True",
              $"{ProgressLines()}/{Key(1, 4)}/{QuestRow(1, 5)}/{c.Progress != null}");
        Logout();
    }

    // quest rework B1: ProgressRows.Persist's failures, over the real tables with failures on demand (char 996135, logged in)
    private static void RunPersistFailures()
    {
        Console.WriteLine("Persist: a failed transaction never loses the legacy write");
        CharId = 996135;
        Login();
        var flaky = new FlakyDatabase(db);
        var rows = new ProgressRows(flaky) { Log = log.Add, Registry = loader.Progress.Registry };
        var progress = c.Progress;
        bool Legacy(string name, int value) =>
            flaky.Execute("REPLACE INTO char_vars(charid, varname, value, expiry) VALUES (?, ?, ?, 0)",
                          new[] { BoundValue.Of(CharId), BoundValue.Of(name), BoundValue.Of(value) }) != null;

        // the transaction cannot start: the legacy write runs on its own, the char's rows go off
        flaky.ThrowOnNextTransaction = true;
        Mark();
        rows.Persist(c, () => Legacy("Quest[1][30]Prog", 4), p => p.SetVar("Quest[1][30]Prog", 4, 0));
        Check("the transaction cannot start: legacy written alone, rows off, logged",
              "4/-/True/progress: char 996135: the transaction failed; the legacy write ran on its own and its rows are off until its next load",
              $"{Var("Quest[1][30]Prog")}/{QuestRow(1, 30)}/{c.Progress == null}/{ProgressLines()}");
        c.Progress = progress;

        // the legacy statement fails in the transaction and again on its own: logged, nothing written, rows stay on
        flaky.FailWhen = q => q.StartsWith("REPLACE INTO char_vars");
        Mark();
        rows.Persist(c, () => Legacy("Quest[1][31]Prog", 5), p => p.SetVar("Quest[1][31]Prog", 5, 0));
        Check("the legacy write fails twice: logged, nothing written, rows on",
              "-/-/True/progress: char 996135: its legacy write failed",
              $"{Var("Quest[1][31]Prog")}/{QuestRow(1, 31)}/{c.Progress != null}/{ProgressLines()}");

        // the rows fail after a good legacy write (piece A's rule): rolled back, the legacy write again alone, rows off
        flaky.FailWhen = q => q.StartsWith("INSERT INTO char_quest_state");
        Mark();
        rows.Persist(c, () => Legacy("Quest[1][32]Prog", 6), p => p.SetVar("Quest[1][32]Prog", 6, 0));
        Check("the rows fail: legacy written, no row, rows off, logged",
              "6/-/True/progress: char 996135: writing its rows failed; its rows are off until its next load",
              $"{Var("Quest[1][32]Prog")}/{QuestRow(1, 32)}/{c.Progress == null}/{ProgressLines()}");
        flaky.FailWhen = _ => false;

        // the next login's drift check rebuilds the rows from the legacy side
        Mark();
        Login();
        Check("the next login re-imports the vars written alone", "progress: char 996135 re-imported (legacy vars changed)/0|4|-/0|6|-",
              $"{ProgressLines()}/{QuestRow(1, 30)}/{QuestRow(1, 32)}");
        Logout();
    }

    // the writes: each lands in the rows and the legacy columns alike, and a login afterwards finds them in agreement
    private static void RunWrites()
    {
        Console.WriteLine("quests through Lua");
        Fixture(996134, null, null);
        Login();
        Mark();
        Lua("player:addQuest(1, 18)");
        Check("addQuest: the row Accepted, the projection == chars.quests", "1|0|-/True/True", $"{QuestRow(1, 18)}/{RowsMatchLegacy()}");
        Lua("player:setCharVar('Quest[1][18]Prog', 2)");
        Check("setCharVar Prog: the row's step, the char_var row, the getter", "1|2|-/2/2",
              $"{QuestRow(1, 18)}/{Var("Quest[1][18]Prog")}/{Lua("report(player:getCharVar('Quest[1][18]Prog'))")}");
        Lua("player:incrementCharVar('Quest[1][18]Prog', 3)");
        Check("incrementCharVar: step 5 in both", "1|5|-/5/5",
              $"{QuestRow(1, 18)}/{Var("Quest[1][18]Prog")}/{Lua("report(player:getCharVar('Quest[1][18]Prog'))")}");
        Lua("player:setCharVar('Quest[1][18]Wait', 7, 2000000000)");
        Check("setCharVar with an expiry: a soft var with its expiry", "1|5|{\"@expiry\":{\"Wait\":2000000000},\"Wait\":7}", QuestRow(1, 18));
        Lua("player:completeQuest(1, 18)");
        Check("completeQuest: Completed, step kept, projection == chars.quests", "2|5|{\"@expiry\":{\"Wait\":2000000000},\"Wait\":7}/True/True",
              $"{QuestRow(1, 18)}/{RowsMatchLegacy()}");
        Lua("player:clearVarsWithPrefix('Quest[1][18]')");
        Check("clearVarsWithPrefix: the row's vars gone (still Completed), the char_vars rows gone", "2|0|-/-/-",
              $"{QuestRow(1, 18)}/{Var("Quest[1][18]Prog")}/{Var("Quest[1][18]Wait")}");
        Lua("player:delQuest(1, 18)");
        Check("delQuest: the emptied row deleted, projection == chars.quests", "-/True/True", $"{QuestRow(1, 18)}/{RowsMatchLegacy()}");
        Lua("player:setCharVar('Quest[1][19]Prog', 1)");
        Check("a var on a quest never started: an Available row", "0|1|-", QuestRow(1, 19));
        Lua("player:setCharVar('Quest[1][19]Prog', 0)");
        Check("  set to 0: row and char_var gone", "-/-", $"{QuestRow(1, 19)}/{Var("Quest[1][19]Prog")}");
        Lua("player:setCharVar('Quest[1][20]Prog', 6)");
        Lua("ClearCharVarFromAll('Quest[1][20]Prog')");
        Check("ClearCharVarFromAll: the online char's row follows", "-/-", $"{QuestRow(1, 20)}/{Var("Quest[1][20]Prog")}");

        Console.WriteLine("missions through Lua");
        Lua("player:addMission(3, 1)");
        Check("addMission: the Zilart row current 1, projection == chars.missions", "1|0|0|-/True/True", $"{MissionRow(3)}/{RowsMatchLegacy()}");
        Lua("player:setMissionStatus(3, 5, 1)");
        Check("setMissionStatus nibble 1 = 5", "1|80|0|-", MissionRow(3));
        Lua("player:setCharVar('Mission[3][1]Prog', 4)");
        Check("a mission var: in the area row", "1|80|0|{\"1:Prog\":4}", MissionRow(3));
        Lua("player:completeMission(3, 1)");
        Check("completeMission: current 0, the completion bit, projection == chars.missions", "0|80|2|{\"1:Prog\":4}/True/True",
              $"{MissionRow(3)}/{RowsMatchLegacy()}");
        Lua("player:setRank(2)");
        Check("setRank (SaveMissionsList, no mission change): rows unchanged", "0|80|2|{\"1:Prog\":4}/True/True",
              $"{MissionRow(3)}/{RowsMatchLegacy()}");

        Check("no progress line: no failed write, no re-import", "", ProgressLines());
        Mark();
        Login();
        Check("a login after all that: the rows agree, nothing logged", "", ProgressLines());
        Check("  Lua reads the same state", "0,0,0,2",
              Lua("report(player:getQuestStatus(1, 18)) report(player:getCurrentMission(3)) report(player:getCharVar('Quest[1][18]Prog')) report(player:getMissionStatus(3, 1) == 5 and 2 or -1)"));
        Logout();
    }

    // ProgressRows.Load: the first import, a quiet second login, the re-import on a legacy change, the canonical
    // (1,1) pair, the prune of an expired var, the switch
    private static void RunLoad()
    {
        Console.WriteLine("a first login imports the legacy state");
        var qb = new QuestBitBlob();
        qb.AddQuest(1, 4); qb.AddQuest(1, 5); qb.CompleteQuest(1, 5);
        qb.AddQuest(8, 160); // A_JOURNEY_BEGINS: the login script (global player onGameIn) adds it, and no write mirrors into the rows until PersistProgress
        uint later = (uint)(Now() + 3600);
        Fixture(996132, qb.Bytes, null, ("Quest[1][4]Prog", 3, 0), ("Quest[1][4]Wait", 236, later), ("Mission[1][2]Prog", 4, 0));
        Mark();
        Login();
        Check("logged: imported", "progress: char 996132 imported", ProgressLines());
        Check("  the quest rows", $"1|3|{{\"@expiry\":{{\"Wait\":{later}}},\"Wait\":236}}/2|0|-", $"{QuestRow(1, 4)}/{QuestRow(1, 5)}");
        Check("  15 mission rows, the NULL missions column as the new char's default (Bastok 0xFFFF), the mission var", "15/65535|0|0|{\"2:Prog\":4}",
              $"{Count("char_mission_state")}/{MissionRow(1)}");
        Check("  the rows' projection == the legacy columns", "True/True", RowsMatchLegacy());
        Check("  Lua reads the same statuses", "1,2,0", Lua("report(player:getQuestStatus(1, 4)) report(player:getQuestStatus(1, 5)) report(player:getQuestStatus(1, 6))"));
        Check("  c.Progress set", "True", c.Progress != null);

        Console.WriteLine("a second login: the rows agree, nothing logged");
        Mark();
        Login();
        Check("no progress line", "", ProgressLines());

        Console.WriteLine("chars.quests edited in SQL between logins: re-imported");
        Logout();
        var edited = DbBlob("quests");
        edited[1 * 64 + 6 / 8] |= 1 << 6 % 8; // quest 1/6 current bit
        Sql(db, "UPDATE chars SET quests = ? WHERE charid = ?", BoundValue.Of(edited), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("logged: re-imported (quests)", "progress: char 996132 re-imported (legacy quests changed)", ProgressLines());
        Check("  the new row", "1|0|-", QuestRow(1, 6));
        Check("  the projection == the legacy columns", "True/True", RowsMatchLegacy());

        Console.WriteLine("a char_var added in SQL: re-imported (vars), then pruned quietly once expired");
        Logout();
        uint soon = (uint)(Now() + 10);
        Sql(db, "REPLACE INTO char_vars(charid, varname, value, expiry) VALUES (?, 'Quest[1][9]Timer', 1, ?)", BoundValue.Of(CharId), BoundValue.Of(soon));
        Mark();
        Login();
        Check("logged: re-imported (vars)", "progress: char 996132 re-imported (legacy vars changed)", ProgressLines());
        Check("  the soft-only row", "0|0|{\"@expiry\":{\"Timer\":" + soon + "},\"Timer\":1}", QuestRow(1, 9));
        Logout();
        unixOffset += 20;
        Mark();
        Login();
        Check("expired by the next login: no re-import, the row pruned away", "/-", $"{ProgressLines()}/{QuestRow(1, 9)}");

        Console.WriteLine("a raw (1,1) quest pair: re-imported once, then loads canonical without re-importing");
        Logout();
        var raw = DbBlob("quests");
        raw[1 * 64 + 4 / 8] |= 1 << 4 % 8;      // 1/4 current (already)
        raw[1 * 64 + 32 + 4 / 8] |= 1 << 4 % 8; // 1/4 complete too
        Sql(db, "UPDATE chars SET quests = ? WHERE charid = ?", BoundValue.Of(raw), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("re-imported (quests): 1/4 now Completed", "progress: char 996132 re-imported (legacy quests changed)/2", $"{ProgressLines()}/{QuestRow(1, 4).Split('|')[0]}");
        Check("  the char's quest log is canonical (current bit cleared)", "0", (c.QuestLog[1 * 64 + 4 / 8] >> 4 % 8 & 1).ToString());
        Mark();
        Login();
        Check("the next login: nothing logged", "", ProgressLines());

        Console.WriteLine("a malformed stored row: the login works, the legacy side rebuilds the row");
        string status44 = Lua("report(player:getQuestStatus(1, 4))");
        string goodRow = QuestRow(1, 4);
        Logout();
        Sql(db, "UPDATE char_quest_state SET progress = '{\"Wait\":\"x\"}' WHERE charid = ? AND area = 1 AND quest_id = 4", BoundValue.Of(CharId));
        Mark();
        Login();
        Check("login works, Lua reads the same status", "True/" + status44, $"{c != null}/{Lua("report(player:getQuestStatus(1, 4))")}");
        Check("  one re-import line naming the unreadable rows", "1", CountLines("progress: char 996132 re-imported (its rows could not be read:").ToString());
        Check("  nothing else logged", "1", CountLines("progress:").ToString());
        Check("  the row repaired", goodRow, QuestRow(1, 4));
        Check("  c.Progress set", "True", c.Progress != null);
        Mark();
        Login();
        Check("the next login: nothing logged", "", ProgressLines());

        Console.WriteLine("the switch off: no rows read or written");
        Logout();
        loader.Progress.Enabled = false;
        Fixture(996133, qb.Bytes, null);
        Mark();
        Login();
        Check("no rows, nothing logged, c.Progress null", "0/0//True", $"{Count("char_quest_state")}/{Count("char_mission_state")}/{ProgressLines()}/{c.Progress == null}");
        Check("  the legacy state as before", "1,2", Lua("report(player:getQuestStatus(1, 4)) report(player:getQuestStatus(1, 5))"));
        Logout();
        loader.Progress.Enabled = true;
    }

    private static string StoredQuests(IDatabase db, uint charId)
    {
        var rows = QuestStateStore.LoadQuests(db, charId);
        if (rows == null) return "null";
        var parts = new List<string>();
        foreach (var r in rows) parts.Add($"{(int)r.Area}/{r.QuestId}|{r.QuestKey}|{(int)r.Status}|{r.Step}|{r.ProgressJson ?? "-"}|{r.RepeatByVanaTime}");
        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    // QuestStateStore against the real tables: upsert, read back (JSON with @expiry), update, delete, replace
    private static void RunStore(IDatabase db)
    {
        Console.WriteLine("the store's round trip");
        var row = new CharQuestStateRow { CharId = StoreChar, QuestKey = "legacy:1/18", Area = QuestLog.Bastok, QuestId = 18, Status = QuestStatus.Accepted, Step = 3, RepeatByVanaTime = 5_000_000_000 };
        row.SoftVars["mustZone"] = 236;
        row.Expiry["Prog"] = 1900000000;
        Check("upsert a quest row", "True", QuestStateStore.Upsert(db, row));
        Check("  read back", "1/18|legacy:1/18|1|3|{\"@expiry\":{\"Prog\":1900000000},\"mustZone\":236}|5000000000", StoredQuests(db, StoreChar));
        row.Status = QuestStatus.Completed;
        row.SoftVars.Clear();
        row.Expiry.Clear();
        Check("upsert again: updated in place, the empty JSON stored as NULL", "True", QuestStateStore.Upsert(db, row));
        Check("  read back", "1/18|legacy:1/18|2|3|-|5000000000", StoredQuests(db, StoreChar));
        Check("  progress is SQL NULL", "1",
              Sql(db, "SELECT COUNT(*) AS n FROM char_quest_state WHERE charid = ? AND progress IS NULL", BoundValue.Of(StoreChar)) is { } n && n.Next() ? n.Get<long>("n") : -1);
        Check("delete it", "True/", $"{QuestStateStore.Delete(db, row)}/{StoredQuests(db, StoreChar)}");

        var m = new CharMissionStateRow { CharId = StoreChar, Area = MissionLog.CoP, Current = 101, Status = 0xFFFF0001, CompletedMask = 0x8000000000000001UL };
        m.SoftVars["10:Prog"] = 9;
        Check("upsert a mission row", "True", QuestStateStore.Upsert(db, m));
        var ms = QuestStateStore.LoadMissions(db, StoreChar);
        Check("  read back: the full 32-bit status and 64-bit mask", "6|101|4294901761|9223372036854775809|{\"10:Prog\":9}",
              ms.Count == 1 ? $"{(int)ms[0].Area}|{ms[0].Current}|{ms[0].Status}|{ms[0].CompletedMask}|{ms[0].ProgressJson}" : $"{ms.Count} rows");

        var qb = new QuestBitBlob();
        qb.AddQuest(0, 3);
        var p = CharProgress.Import(StoreChar, qb.Bytes, new MissionBitBlob().Bytes, new[] { new CharVar("Quest[2][7]mustZone", 230) });
        Check("ReplaceAll: the char's rows are the state's", "True", QuestStateStore.ReplaceAll(db, p));
        Check("  quests", "0/3|legacy:0/3|1|0|-|0;2/7|legacy:2/7|0|0|{\"mustZone\":230}|0", StoredQuests(db, StoreChar));
        Check("  missions: all 15, the CoP row replaced", "15/101/0",
              $"{QuestStateStore.LoadMissions(db, StoreChar).Count}/{QuestStateStore.LoadMissions(db, StoreChar)[6].Current}/{QuestStateStore.LoadMissions(db, StoreChar)[6].Status}");

        var ch = p.SetVar("Quest[2][7]mustZone", 0, 0);
        Check("Apply: the emptied row deleted", "True/0/3|legacy:0/3|1|0|-|0", $"{QuestStateStore.Apply(db, ch)}/{StoredQuests(db, StoreChar)}");
        Console.WriteLine("batched writes on the real tables");
        var big = new QuestBitBlob();
        for (int id = 0; id < 250; id++) big.AddQuest(4, (ushort)id);
        for (int id = 0; id < 200; id++) big.AddQuest(5, (ushort)id);
        var bp = CharProgress.Import(StoreChar, big.Bytes, new MissionBitBlob().Bytes, new[] { new CharVar("Quest[4][1]mustZone", 230, 1900000000) });
        Check("ReplaceAll of 450 quest rows (3 batches) and 15 missions", "True", QuestStateStore.ReplaceAll(db, bp));
        var back = QuestStateStore.LoadQuests(db, StoreChar);
        Check("  read back: 450 rows, the soft var and its expiry", "450/{\"@expiry\":{\"mustZone\":1900000000},\"mustZone\":230}",
              $"{back.Count}/{back.Find(r => (int)r.Area == 4 && r.QuestId == 1)?.ProgressJson}");
        Check("  the projection == the state's", bp.ProjectQuests().ToHex(),
              CharProgress.FromRows(StoreChar, back, QuestStateStore.LoadMissions(db, StoreChar)).ProjectQuests().ToHex());

        var reg = new QuestKeyRegistry();
        reg.Register("t.a", new QuestWireRef((QuestLog)4, 0));
        reg.Register("t.b", new QuestWireRef((QuestLog)4, 1));
        var keyed = CharProgress.FromRows(StoreChar, QuestStateStore.LoadQuests(db, StoreChar), QuestStateStore.LoadMissions(db, StoreChar), reg);
        var first = keyed.Rekey().FindAll(r => (int)r.Area == 4 && r.QuestId <= 1);
        Check("Rekey on the real table: legacy keys -> t.a / t.b", "True", QuestStateStore.Rekey(db, first));
        var swap = new QuestKeyRegistry();
        swap.Register("t.b", new QuestWireRef((QuestLog)4, 0));
        swap.Register("t.a", new QuestWireRef((QuestLog)4, 1));
        var swapped = CharProgress.FromRows(StoreChar, QuestStateStore.LoadQuests(db, StoreChar), QuestStateStore.LoadMissions(db, StoreChar), swap)
                                  .Rekey().FindAll(r => (int)r.Area == 4 && r.QuestId <= 1);
        Check("  two names swapped: no key collision", "True", QuestStateStore.Rekey(db, swapped));
        Check("  read back", "4/0=t.b,4/1=t.a",
              string.Join(",", QuestStateStore.LoadQuests(db, StoreChar).FindAll(r => (int)r.Area == 4 && r.QuestId <= 1).ConvertAll(r => $"{(int)r.Area}/{r.QuestId}={r.QuestKey}")));
        Sql(db, "DELETE FROM char_quest_state WHERE charid = ?", BoundValue.Of(StoreChar));
        Sql(db, "DELETE FROM char_mission_state WHERE charid = ?", BoundValue.Of(StoreChar));
    }
}
