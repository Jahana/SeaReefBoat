using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using XI.Persistence;
using XI.QuestContent;

// the flow parser's and the trace format's self-test (--selftest): no database, the NPCs from the Npc constants only
internal static partial class Program
{
    // the pinned world tables (World.cs): the private names a run's queries go to, the snapshot file
    private static void SelfTestWorld()
    {
        Console.WriteLine("the world tables");
        Check("  a query naming a world table goes to the run's copy",
              "SELECT value FROM golden_world_c996161_server_variables WHERE name = ?|UPDATE golden_world_c996161_conquest_system SET mob_kills = 1",
              WorldTablesDatabase.Rewrite("SELECT value FROM server_variables WHERE name = ?", "c996161") + "|" +
              WorldTablesDatabase.Rewrite("UPDATE conquest_system SET mob_kills = 1", "c996161"));
        Check("  other tables, a longer name and no tag are left alone",
              "SELECT * FROM char_vars|SELECT * FROM my_server_variables_x|SELECT * FROM server_variables",
              WorldTablesDatabase.Rewrite("SELECT * FROM char_vars", "c1") + "|" + WorldTablesDatabase.Rewrite("SELECT * FROM my_server_variables_x", "c1") + "|" +
              WorldTablesDatabase.Rewrite("SELECT * FROM server_variables", null));
        Check("  a private name is not rewritten again", "golden_world_c1_server_variables",
              WorldTablesDatabase.Rewrite(WorldTablesDatabase.PrivateName("server_variables", "c1"), "c1"));
        Check("  a battlefield's record goes to the run's copy too",
              "SELECT name, fastestName, fastestTime, fastestPartySize FROM golden_world_c1_bcnm_records WHERE bcnmId = ?",
              WorldTablesDatabase.Rewrite("SELECT name, fastestName, fastestTime, fastestPartySize FROM bcnm_records WHERE bcnmId = ?", "c1"));
        Check("  copied: the hashed tables and bcnm_records; a header hashes the first two only",
              "server_variables,conquest_system,bcnm_records/server_variables,conquest_system",
              $"{string.Join(",", WorldTables)}/{string.Join(",", HashedWorldTables)}");
        var tables = new List<WorldTable>
        {
            new("server_variables", new List<string> { "name", "value", "expiry" }, new List<string[]> { new[] { "Davoi_Jar_Move_Time", "1800004534", "0" }, new[] { "a\tb\\c", "1", "0" } }),
            new("conquest_system", new List<string> { "region_id", "mob_kills" }, new List<string[]> { new[] { "0", "5" } }),
            new("bcnm_records", new List<string> { "bcnmId", "fastestName" }, new List<string[]> { new[] { "737", "nobody" } }),
        };
        string text = WorldSnapshotText(tables);
        var back = ReadWorldSnapshot(text, "w.txt");
        Check("  the snapshot reads back as written (a tab and a backslash escaped)", text, WorldSnapshotText(back));
        Check("  ... the escaped value", "a\tb\\c", back[0].Rows[1][0]);
        void Bad(string what, string file, string message)
        {
            try
            {
                ReadWorldSnapshot(file, "w.txt");
                Fail(what, "read");
            }
            catch (FormatException e)
            {
                Check(what, message, e.Message);
            }
        }
        Bad("  a row with too few values", "table server_variables\ncolumns name value expiry\nx\t1\n", "w.txt:3: 2 values for 3 columns");
        Bad("  a table missing", "table server_variables\ncolumns name value expiry\n", "w.txt: no table conquest_system");
        Bad("  a row before its table", "x\t1\t0\n", "w.txt:1: a row before a table line");
        try
        {
            var checkedIn = ReadWorldSnapshot(File.ReadAllText(Path.Combine(GoldenDir, WorldFile)), WorldFile);
            Check("  the checked-in snapshot reads: the three tables", "server_variables,conquest_system,bcnm_records", string.Join(",", checkedIn.Select(t => t.Name)));
            // bcnm_records is sql/bcnm_info.sql's seed, row for row (bcnmId zoneId name fastestName fastestPartySize fastestTime)
            var seed = new List<string>();
            foreach (var line in File.ReadLines(Path.Combine(Root, "sql", SeedFiles["bcnm_records"])))
            {
                var m = Regex.Match(line, @"^INSERT INTO `bcnm_records` VALUES \((\d+),(\d+),'([^']*)','([^']*)',(\d+),(\d+)\);");
                if (m.Success) seed.Add(string.Join("\t", Enumerable.Range(1, 6).Select(i => m.Groups[i].Value)));
            }
            // world-snapshot's seed reader (SeedInserts): each statement up to its own ';', over lines, past quoted
            // ';' and ')' and comments, its tuples counted; another table's left; the file's own 247 single-row ones
            var inserts = SeedInserts("-- INSERT INTO `t` VALUES (9);\nINSERT INTO `t` VALUES (1,'a;b'),\n" +
                                      "  (2,'c)'); -- a; comment\n" +
                                      "  INSERT INTO `t` VALUES (3,\"d\"\"e\") /* ; */, -- c; d\n" +
                                      "  (4,(5));\nINSERT INTO `u` VALUES (6);\n", "t");
            Check("  SeedInserts: a statement over lines, quoted ';' and ')', comments; tuples counted",
                  "VALUES (1,'a;b'),\n  (2,'c)')#2|VALUES (3,\"d\"\"e\") /* ; */, -- c; d\n  (4,(5))#2",
                  string.Join("|", inserts.Select(x => x.Values + "#" + x.Rows)));
            // a column list before VALUES is no row (nor a `values` column); a /* */ block between statements is
            // skipped whole, an INSERT in it not read
            var listed = SeedInserts("/*\nINSERT INTO `t` VALUES (8);\n*/\nINSERT INTO `t` (`a`, `values`) " +
                                     "VALUES (1,2), (3,4);\n", "t");
            Check("  ... a column list before VALUES is not a row; an INSERT in a /* */ block is not read",
                  "(`a`, `values`) VALUES (1,2), (3,4)#2",
                  string.Join("|", listed.Select(x => x.Values + "#" + x.Rows)));
            Check("  ... VALUES is a whole word in any case (IsKeywordAt)", "True/True/False/False",
                  $"{IsKeywordAt("values (", 0, "VALUES")}/{IsKeywordAt("a VaLuEs", 2, "VALUES")}/" +
                  $"{IsKeywordAt("xVALUES", 1, "VALUES")}/{IsKeywordAt("VALUESx", 0, "VALUES")}");
            string seedSql = File.ReadAllText(Path.Combine(Root, "sql", SeedFiles["bcnm_records"]));
            var fileInserts = SeedInserts(seedSql, "bcnm_records");
            Check("  ... sql/bcnm_info.sql: one statement, one row each", "247/247",
                  $"{fileInserts.Count}/{fileInserts.Sum(x => x.Rows)}");
            try
            {
                SeedInserts("INSERT INTO `t` VALUES (1)\n", "t");
                Check("  ... a statement with no ';' is refused", "refused", "read");
            }
            catch (FormatException e)
            {
                Check("  ... a statement with no ';' is refused", "an INSERT INTO `t` statement with no ';'",
                      e.Message);
            }
            var records = checkedIn.Single(t => t.Name == "bcnm_records");
            Check("  ... its bcnm_records is sql/bcnm_info.sql's seed (every row, 'nobody' the holder)",
                  string.Join("\n", seed.OrderBy(r => r, StringComparer.Ordinal)),
                  string.Join("\n", records.Rows.Select(r => string.Join("\t", r)).OrderBy(r => r, StringComparer.Ordinal)));
            Check("  ... Return to the Depths' and Achieving True Power's rows", "737\t13\treturn_to_the_depths\tnobody\t0\t1800|1123\t64\tachieving_true_power\tnobody\t0\t600",
                  string.Join("|", records.Rows.Where(r => r[0] is "737" or "1123").Select(r => string.Join("\t", r))));
            // a run that wins a battlefield: the zone host's record UPDATE goes to the run's copy, and the next world
            // (NewWorld's RestoreWorldTables) empties that copy and fills it with the seed's rows again
            var savedWorld = world_;
            var savedSnapshot = worldSnapshot_;
            try
            {
                var log = new LoggingDatabase();
                world_ = new WorldTablesDatabase(log) { Tag = "c1" };
                worldSnapshot_ = checkedIn;
                world_.Execute("UPDATE bcnm_records SET fastestName = ?, fastestTime = ?, fastestPartySize = ? WHERE bcnmId = ? AND zoneid = ?",
                               new[] { BoundValue.Of("Qgolden169"), BoundValue.Of(12), BoundValue.Of(1), BoundValue.Of(737), BoundValue.Of(13) });
                Check("  a win's record UPDATE writes the run's copy, not bcnm_records",
                      "UPDATE golden_world_c1_bcnm_records SET fastestName = ?, fastestTime = ?, fastestPartySize = ? WHERE bcnmId = ? AND zoneid = ?",
                      log.Queries[0].Query);
                log.Queries.Clear();
                RestoreWorldTables();
                var restore = log.Queries.Where(q => q.Query.Contains("golden_world_c1_bcnm_records", StringComparison.Ordinal)).ToList();
                string row737 = restore.Count == 2 ? string.Join("\t", restore[1].Values.SkipWhile(v => v != "737").Take(6)) : "";
                Check("  the next world: the copy emptied, then the seed's 247 rows put back (737's holder 'nobody' again)",
                      "DELETE FROM `golden_world_c1_bcnm_records`|INSERT INTO `golden_world_c1_bcnm_records` (`bcnmId`, `zoneId`, `name`, `fastestName`, `fastestPartySize`, `fastestTime`) VALUES/" +
                      $"{seed.Count * 6}/737\t13\treturn_to_the_depths\tnobody\t0\t1800",
                      restore.Count == 2
                          ? $"{restore[0].Query}|{restore[1].Query.Substring(0, restore[1].Query.IndexOf(" VALUES", StringComparison.Ordinal) + 7)}/{restore[1].Values.Count}/{row737}"
                          : $"{restore.Count} queries: {string.Join(" | ", restore.Select(q => q.Query))}");
            }
            finally
            {
                world_ = savedWorld;
                worldSnapshot_ = savedSnapshot;
            }
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            Fail("  the checked-in snapshot reads", e.Message);
        }
    }

    // the char id bit-packed in a 0x028's targets: substituted like the byte-aligned <CHARID>
    private static void SelfTestCharBits()
    {
        Console.WriteLine("the char id in a 0x028's targets");
        // a mob (17633281) hitting two targets, the second the char: the first target's result carries an added effect
        // and the second's first result spikes, so the char's id field sits off the first target's fixed offset
        XI.Map.BasicPacket Attack(uint charId, uint actor = 17633281, bool third = false)
        {
            var a = new XI.Zone.ActionPacket { ActorId = actor, ActionType = 1, ActionId = 7 };
            var t1 = a.AddTarget(17633282);
            var r1 = t1.AddResult();
            r1.Param = 12;
            r1.AdditionalEffect = 5;
            r1.AddEffectParam = 3;
            var t2 = a.AddTarget(charId);
            var r2 = t2.AddResult();
            r2.Param = 40;
            r2.SpikesEffect = 2;
            r2.SpikesParam = 9;
            t2.AddResult().Param = 41;
            if (third) a.AddTarget(charId).AddResult().Param = 1;
            return XI.Zone.Battle2Packets.Battle2(a);
        }
        uint saved = CharId;
        try
        {
            var none = new List<Mask>();
            var p1 = Attack(996170);
            int size = (p1.Buffer[1] >> 1) * 4;
            var copy = (byte[])p1.Buffer.Clone();
            var bits = CharTargetBits(copy, size, 996170);
            // 40 + 32 + 6 + 4 + 4 + 32 + 32 = 150 the first target; + 36 + 85 + 1 + 37 + 1 the second; + 36 + (85 + 1 + 1 + 34) + (85 + 1 + 1) the third
            Check("  the char's target field found past a result with an added effect", "310", string.Join(",", bits));
            Check("  its 32 bits zeroed, the rest kept", "0/17633282",
                  $"{XI.Zone.Battle2Packets.Unpack(copy, 310, 32)}/{XI.Zone.Battle2Packets.Unpack(copy, 150, 32)}");
            CharId = 996170;
            string a = Normalise(p1, none);
            CharId = 996180;
            string b = Normalise(Attack(996180), none);
            Check("  two slots' char ids normalise alike (the token after the hex)", true, a == b && a.EndsWith("<CHARID@310>", StringComparison.Ordinal));
            Check("  another target id stays as it is", false, Normalise(Attack(996181), none) == b);
            Check("  every target slot: a third target the char too", true, Normalise(Attack(996180, third: true), none).EndsWith("<CHARID@310><CHARID@554>", StringComparison.Ordinal));
            Check("  the char as the actor: the byte-aligned placeholder, no target token", true,
                  Normalise(Attack(17633290, actor: 996180), none) is var actor && actor.StartsWith("028:", StringComparison.Ordinal) &&
                  actor.Substring(4, 10 + 8) == Convert.ToHexString(Attack(17633290, actor: 996180).Buffer, 0, 5) + "<CHARID>" && !actor.Contains("<CHARID@"));
            Check("  show names it", "[action, target $char at bit 310]", DecodePacket(b));
            Check("  a packet cut short: no walk past its size", "", string.Join(",", CharTargetBits((byte[])p1.Buffer.Clone(), 20, 996170)));
        }
        finally
        {
            CharId = saved;
        }
    }

    // --db / --port and the lock names (the lanes)
    private static void SelfTestLanes()
    {
        Console.WriteLine("the lanes");
        string Lane(string db, string port) =>
            LaneError(db, port, out var name, out int p) is { } error ? "error: " + error : $"{name}:{p}";
        Check("  no --db / --port: xidb on 3307 (today's lane)", "xidb:3307", Lane(null, null));
        Check("  --db xidb_lane2", "xidb_lane2:3307", Lane("xidb_lane2", null));
        Check("  --db and --port", "xidb_lane2:3308", Lane("xidb_lane2", "3308"));
        Check("  --port alone", "xidb:3308", Lane(null, "3308"));
        Check("  --db <port> (its meaning before the lanes) refused", "error: --db takes a database name, not a number (3307); the port is --port", Lane("3307", null));
        Check("  --db with a quote refused", "error: --db takes a database name (letters, digits, _; at most 48), not x'y", Lane("x'y", null));
        Check("  --db too long refused", true, Lane(new string('a', 49), null).StartsWith("error: --db takes a database name", StringComparison.Ordinal));
        Check("  --db 48 long", new string('a', 48) + ":3307", Lane(new string('a', 48), null));
        Check("  --port not a number refused", "error: --port takes a port number, not x", Lane(null, "x"));
        Check("  --port 0 refused", "error: --port takes a port number, not 0", Lane(null, "0"));
        Check("  the lock name carries the database", "golden-xidb-996161|golden-xidb_lane2-996161", $"{LockName("xidb", 996161)}|{LockName("xidb_lane2", 996161)}");
        Check("  the locks: xidb also the name from before the lanes, another lane its own only", "golden-xidb-996161,golden-996161|golden-xidb_lane2-996161",
              $"{string.Join(",", LockNames("xidb", 996161))}|{string.Join(",", LockNames("xidb_lane2", 996161))}");
        Check("  startup --against: a build from before the lanes opens the lane's database", "a SqlDatabase = \"xidb_lane2\" b|null",
              (PatchLaneDb("a SqlDatabase = \"xidb\" b", "xidb_lane2") ?? "null") + "|" +
              (PatchLaneDb("SqlDatabase = \"xidb\" SqlDatabase = \"xidb\"", "xidb_lane2") ?? "null"));
        string saved = dbName_;
        int savedPort = dbPort_;
        try
        {
            LaneError("xidb_lane2", "3307", out dbName_, out dbPort_);
            Check("  a child (--jobs, stable, startup) gets the lane", "--db xidb_lane2 --port 3307", string.Join(" ", LaneArgs()));
        }
        finally
        {
            dbName_ = saved;
            dbPort_ = savedPort;
        }
    }

    private static void SelfTest()
    {
        SelfTestShow();
        SelfTestLanes();
        SelfTestCharBits();
        SelfTestWorld();
        SelfTestPlatformP1();
        SelfTestPlatformP3();
        SelfTestPlatformP4();
        SelfTestPlatformP5();
        SelfTestPlatformP6();
        SelfTestPlatformP7();
        SelfTestPlatformP8();
        SelfTestPlatformP9();
        SelfTestPlatformP10();
        SelfTestCarryHarness();
        SelfTestCarryHarness2();
        SelfTestCarryHarness3();
        SelfTestC2();
        Console.WriteLine("the flow parser");
        const string every = @"# every statement
lists BastokMarkets/Zon-Fobun/onTrigger BastokMarkets/onEventFinish/251
hide scripts/quests/bastok/The_Curse_Collector
char zone=BastokMarkets pos=-239.5,-2,64 job=WAR lvl=75 nation=1 gil=1000
fame Bastok 224
item CopperIngot x2
item BronzeIngot
keyitem Cursepaper
charvar Quest[1][34]Prog 0
quest Bastok.OutOfTheDepths completed
login                            # a comment after a statement
trigger Zon-Fobun
trade Zon-Fobun CopperIngot x1 BronzeIngot x1
finish
finish option=1
finish-if-event
finish-if-event option=2
update 251 option=2
equip CopperIngot Sub
seekeyitem Cursepaper
fill-inventory
free-inventory
tick 1000
wait vana=2
wait jst=1
fame Bastok 225
item BronzeIngot x3
keyitem Cursepaper
charvar Quest[1][34]Prog 1
quest Bastok.AltanasSorrow accepted
walk 0,0,-300
zoneline BastokMines
route BastokMarkets SouthGustaberg NorthGustaberg OldtonMovalpolos
claim BUGALLUG
kill BUGALLUG
warp -300.5,10,20 Beadeaux
trigger The_Mute
logout
login
";
        Flow f = null;
        try
        {
            f = Flow.Parse(every, "every.flow", "bastok.every", null);
        }
        catch (FormatException e)
        {
            Fail("every statement parses", e.Message);
        }
        if (f != null)
        {
            Pass("every statement parses");
            Check("  the setup statements (before login)", "fame,item,item,keyitem,charvar,quest", string.Join(",", f.Setup.Select(st => st.Verb)));
            Check("  the steps", "login,trigger,trade,finish,finish,finish-if-event,finish-if-event,update,equip,seekeyitem,fill-inventory,free-inventory," +
                                 "tick,wait,wait,fame,item,keyitem,charvar,quest,walk,zoneline,route,claim,kill,warp,trigger,logout,login",
                  string.Join(",", f.Steps.Select(st => st.Verb)));
            Check("  the lists", "235/Zon-Fobun/onTrigger,235/onEventFinish/251", string.Join(",", f.Lists.Select(l => $"{l.Zone}/{l.Second}/{l.Third}")));
            Check("  hide", "scripts/quests/bastok/The_Curse_Collector", string.Join(",", f.Hidden));
            Check("  char", "235 -239.5 -2 64 1 75 1 1000",
                  string.Join(" ", f.Char.Zone, f.Char.X.ToString(CultureInfo.InvariantCulture), f.Char.Y, f.Char.Z, f.Char.Job, f.Char.Level, f.Char.Nation, f.Char.Gil));
            Check("  item x2 / no count", $"{Item.CopperIngot}x2,{Item.BronzeIngot}x1",
                  string.Join(",", f.Setup.Where(st => st.Verb == "item").Select(st => $"{st.Value}x{st.Amount}")));
            Check("  quest key", "bastok.out_of_the_depths completed", $"{f.Setup[5].Name} {f.Setup[5].Status}");
            Check("  trade", $"Zon-Fobun {Item.CopperIngot}x1 {Item.BronzeIngot}x1",
                  $"{f.Steps[2].Name} " + string.Join(" ", f.Steps[2].Items.Select(i => $"{i.Item}x{i.Qty}")));
            Check("  options", "-/1/-/2/251:2",
                  $"{(f.Steps[3].Option == null ? "-" : "?")}/{f.Steps[4].Option}/{(f.Steps[5].Option == null ? "-" : "?")}/{f.Steps[6].Option}/{f.Steps[7].Value}:{f.Steps[7].Option}");
            Check("  equip", $"{Item.CopperIngot}/{Slot.Sub}", $"{f.Steps[8].Value}/{f.Steps[8].Amount}");
            Check("  route", $"{Zone.BastokMarkets},{Zone.SouthGustaberg},{Zone.NorthGustaberg},{Zone.OldtonMovalpolos}",
                  string.Join(",", f.Steps.First(st => st.Verb == "route").Zones));
            Check("  warp", $"-300.5,10,20 in {Zone.Beadeaux}", string.Create(CultureInfo.InvariantCulture, $"{f.Steps[25].X},{f.Steps[25].Y},{f.Steps[25].Z} in {f.Steps[25].ZoneId}"));
            Check("  wait", "vana 2, jst 1", $"{f.Steps[13].Name} {f.Steps[13].Value}, {f.Steps[14].Name} {f.Steps[14].Value}");
            Check("  kill: the mob table key", "BUGALLUG", f.Steps[24].Name);
            Check("  labels are the statements, words single-spaced, comments gone", "login", f.Steps[0].Label);
            Check("  the hash ignores CRLF", f.Sha256, Flow.Hash(every.Replace("\n", "\r\n")));
        }

        try
        {
            var g = Flow.Parse("char zone=BastokMarkets pos=0,0,0\nlogin\ntrade Zon-Fobun CopperIngot x1 gil=100\ntrade Zon-Fobun gil=5\nrezone\n", "g.flow", "g", null);
            Check("  trade gil=<n> (after the items, as the gil item) and rezone", $"{Item.CopperIngot}x1 {Flow.GilItem}x100/{Flow.GilItem}x5/rezone",
                  string.Join(" ", g.Steps[1].Items.Select(i => $"{i.Item}x{i.Qty}")) + "/" + string.Join(" ", g.Steps[2].Items.Select(i => $"{i.Item}x{i.Qty}")) + "/" + g.Steps[3].Verb);
        }
        catch (FormatException e)
        {
            Fail("  trade gil=<n> and rezone", e.Message);
        }
        try
        {
            var g = Flow.Parse("char zone=BastokMarkets pos=0,0,0\nlogin\ntrade Zon-Fobun gil=100 CopperIngot x1\n", "g.flow", "g", null);
            Check("  trade gil=<n> first (trade slot 0, as the client offers it)", $"{Flow.GilItem}x100 {Item.CopperIngot}x1",
                  string.Join(" ", g.Steps[1].Items.Select(i => $"{i.Item}x{i.Qty}")));
        }
        catch (FormatException e)
        {
            Fail("  trade gil=<n> first", e.Message);
        }

        try
        {
            var z = Flow.Parse("char zone=BastokMarkets pos=0,0,0\nlogin\nrezone option=1\nwarp 1,2,3 Metalworks option=2\nwarp 1,2,3 BastokMarkets\nrezone\n",
                               "z.flow", "z", null);
            Check("  the zone-in's option on rezone and warp (none when not given)", "1/2/-/-",
                  string.Join("/", z.Steps.Skip(1).Select(st => st.Option?.ToString() ?? "-")));
        }
        catch (FormatException e)
        {
            Fail("  the zone-in's option on rezone and warp", e.Message);
        }

        void Refused(string what, string text, string message)
        {
            try
            {
                Flow.Parse(text, "t.flow", "t", null);
                Fail(what, "parsed");
            }
            catch (FormatException e)
            {
                Check(what, message, e.Message);
            }
        }
        const string head = "char zone=BastokMarkets pos=0,0,0\n";
        Refused("an unknown NPC", head + "login\ntrigger Nobody_Here\n", "t.flow:3: unknown npc Nobody_Here");
        Refused("an NPC of another zone", head + "login\ntrigger The_Mute\n", "t.flow:3: unknown npc The_Mute");
        Refused("a step before login", head + "trigger Zon-Fobun\nlogin\n", "t.flow:2: step before login");
        Refused("an unknown item", head + "item CopperIngott\nlogin\n", "t.flow:2: unknown item CopperIngott");
        Refused("an unknown key item", head + "keyitem Nothing\nlogin\n", "t.flow:2: unknown key item Nothing");
        Refused("an unknown zone", "char zone=Bastok pos=0,0,0\nlogin\n", "t.flow:1: unknown zone Bastok");
        Refused("an unknown mob", head + "login\nkill BUGALLUG\n", "t.flow:3: unknown mob BUGALLUG");
        Refused("an unknown quest", head + "quest Bastok.Nothing completed\nlogin\n", "t.flow:2: unknown quest Bastok.Nothing");
        Refused("an unknown statement", head + "login\ndance\n", "t.flow:3: unknown statement dance");
        Refused("hide after login", head + "login\nhide scripts/quests/bastok/Groceries\n", "t.flow:3: hide after login");
        Refused("a derived fame area", head + "fame Jeuno 1\nlogin\n", "t.flow:2: fame area Jeuno is derived, not stored");
        Refused("no login", head, "t.flow: no login");
        Refused("a trade with nothing", head + "login\ntrade Zon-Fobun\n", "t.flow:3: trade takes 2 to 999 arguments");
        Refused("gil=0", head + "login\ntrade Zon-Fobun gil=0\n", "t.flow:3: gil=<n> takes a positive amount: gil=0");
        Refused("trigger at= not a distance", head + "login\ntrigger Zon-Fobun at=-1\n", "t.flow:3: trigger takes <Npc> [at=<yalms>|here]: at=-1");
        Refused("trigger with another word", head + "login\ntrigger Zon-Fobun near\n", "t.flow:3: trigger takes <Npc> [at=<yalms>|here]: near");
        Refused("kill <Mob>+0", "char zone=BehemothsDominion pos=0,0,0\nlogin\nkill TALEKEEPERS_GIFT_OFFSET+0\n",
                "t.flow:3: kill takes <Mob>, <Mob>+<n> or <Mob>-<n>, n from 1: TALEKEEPERS_GIFT_OFFSET+0");
        Refused("kill <Mob>-0", "char zone=BehemothsDominion pos=0,0,0\nlogin\nkill TALEKEEPERS_GIFT_OFFSET-0\n",
                "t.flow:3: kill takes <Mob>, <Mob>+<n> or <Mob>-<n>, n from 1: TALEKEEPERS_GIFT_OFFSET-0");
        Refused("claim <Mob>+-<n>", "char zone=BehemothsDominion pos=0,0,0\nlogin\nclaim TALEKEEPERS_GIFT_OFFSET+-3\n",
                "t.flow:3: claim takes <Mob>, <Mob>+<n> or <Mob>-<n>, n from 1: TALEKEEPERS_GIFT_OFFSET+-3");
        Refused("claim <Mob>-<n> of an unknown key", "char zone=BehemothsDominion pos=0,0,0\nlogin\nclaim NOBODY-1\n", "t.flow:3: unknown mob NOBODY");
        Refused("claim <Mob>+<n> of an unknown key", "char zone=BehemothsDominion pos=0,0,0\nlogin\nclaim NOBODY+1\n", "t.flow:3: unknown mob NOBODY");
        try
        {
            var t = Flow.Parse("char zone=BastokMarkets pos=0,0,0\nlogin\ntrigger Zon-Fobun\ntrigger Zon-Fobun at=0.5\ntrigger Zon-Fobun at=0\n" +
                               "warp 0,0,0 BehemothsDominion\nkill TALEKEEPERS_GIFT_OFFSET+2\nclaim TALEKEEPERS_GIFT_OFFSET+1\nkill TALEKEEPERS_GIFT_OFFSET\n",
                               "t.flow", "t", null);
            Check("  trigger at=<yalms> (none when not given: the default)", "-/0.5/0",
                  string.Join("/", t.Steps.Skip(1).Take(3).Select(st => st.At?.ToString(CultureInfo.InvariantCulture) ?? "-")));
            Check("  trigger: the default distance is 1.5 yalms", "1.5", Flow.TriggerDistance.ToString(CultureInfo.InvariantCulture));
            Check("  trigger at=: the label keeps it", "trigger Zon-Fobun at=0.5", t.Steps[2].Label);
            Check("  kill / claim <Mob>+<n>: the key and the offset", "kill TALEKEEPERS_GIFT_OFFSET 2/claim TALEKEEPERS_GIFT_OFFSET 1/kill TALEKEEPERS_GIFT_OFFSET 0",
                  string.Join("/", t.Steps.Skip(5).Select(st => $"{st.Verb} {st.Name} {st.Offset}")));
        }
        catch (FormatException e)
        {
            Fail("  trigger at= and kill <Mob>+<n>", e.Message);
        }
        Check("  <Mob>+<n>: the key's id plus n, when a mob has it", "103", MobAtOffset("K", 101, 2, id => id == 103).ToString());
        Check("  <Mob>-<n>: the key's id minus n, when a mob has it", "98", MobAtOffset("K", 101, -3, id => id == 98).ToString());
        try
        {
            MobAtOffset("K", 101, -2, id => id == 98);
            Fail("  <Mob>-<n> naming no mob: refused", "accepted");
        }
        catch (InvalidOperationException e)
        {
            Check("  <Mob>-<n> naming no mob: refused", true, e.Message.StartsWith("K-2 (99) names no mob of zone", StringComparison.Ordinal));
        }
        try
        {
            MobAtOffset("K", 1, -2, id => true);
            Fail("  <Mob>-<n> below id 0: refused", "accepted");
        }
        catch (InvalidOperationException e)
        {
            Check("  <Mob>-<n> below id 0: refused", true, e.Message.StartsWith("K-2 (-1) names no mob", StringComparison.Ordinal));
        }
        try
        {
            var neg = Flow.Parse("char zone=CloisterOfTremors pos=0,0,0\nlogin\nkill GALGALIM-3\nclaim GALGALIM-1\n", "n.flow", "n", null);
            Check("  kill / claim <Mob>-<n>: the key and the negative offset, the label as written", "kill GALGALIM -3 kill GALGALIM-3/claim GALGALIM -1 claim GALGALIM-1",
                  string.Join("/", neg.Steps.Skip(1).Select(st => $"{st.Verb} {st.Name} {st.Offset} {st.Label}")));
        }
        catch (FormatException e)
        {
            Fail("  kill <Mob>-<n>", e.Message);
        }
        try
        {
            MobAtOffset("K", 101, 3, id => id == 103);
            Fail("  <Mob>+<n> naming no mob: refused", "accepted");
        }
        catch (InvalidOperationException e)
        {
            Check("  <Mob>+<n> naming no mob: refused", true, e.Message.StartsWith("K+3 (104) names no mob of zone", StringComparison.Ordinal));
        }
        try
        {
            var m = Flow.Parse("char zone=BastokMarkets pos=0,0,0 nation=1\nmission Bastok TheZeruhnReport current\nmission Windurst TheHeartOfTheMatter completed\nrank 3 points=250\nrank 2\n" +
                               "title FodderchiefFlayer\nsetting EXP_RATE 2\nsetting ENABLE_IMMUNOBREAK true\nlogin\nmission Bastok TheZeruhnReport completed\nrank 4\ntitle FodderchiefFlayer\nsetting ENABLE_IMMUNOBREAK false\n",
                               "m.flow", "m", null);
            Check("  mission / rank / title / setting: setup and steps", "mission Bastok TheZeruhnReport current|mission Windurst TheHeartOfTheMatter completed|rank 3 points=250|rank 2|title FodderchiefFlayer|" +
                  "setting EXP_RATE 2|setting ENABLE_IMMUNOBREAK true/login|mission Bastok TheZeruhnReport completed|rank 4|title FodderchiefFlayer|setting ENABLE_IMMUNOBREAK false",
                  string.Join("|", m.Setup.Select(x => x.Label)) + "/" + string.Join("|", m.Steps.Select(x => x.Label)));
            Check("  mission: log, id and status", $"{MissionLog.Bastok} {Mission.Bastok.TheZeruhnReport} current", $"{m.Setup[0].Value} {m.Setup[0].Amount} {m.Setup[0].Status}");
            Check("  rank: rank and points (none: -1)", "3 250|2 -1", $"{m.Setup[2].Amount} {m.Setup[2].Value}|{m.Setup[3].Amount} {m.Setup[3].Value}");
            Check("  title: the id", Title.FodderchiefFlayer, m.Setup[4].Value);
            Check("  setting: number and boolean", "number 2|bool 1", $"{m.Setup[5].Status} {m.Setup[5].Amount}|{m.Setup[6].Status} {m.Setup[6].Amount}");
        }
        catch (FormatException e)
        {
            Fail("  mission / rank / title / setting", e.Message);
        }
        const string nationHead = "char zone=BastokMarkets pos=0,0,0 nation=1\n";
        Refused("an unknown mission log", head + "mission Nowhere TheZeruhnReport current\nlogin\n", "t.flow:2: unknown mission log Nowhere");
        Refused("an unknown mission", head + "mission Bastok Nothing current\nlogin\n", "t.flow:2: unknown mission Nothing");
        Refused("a mission of another log", head + "mission Bastok SmashTheOrcishScouts current\nlogin\n", "t.flow:2: unknown mission SmashTheOrcishScouts");
        Refused("a mission status", head + "mission Bastok TheZeruhnReport done\nlogin\n", "t.flow:2: a mission status is current or completed: done");
        Refused("rank without nation=", head + "rank 2\nlogin\n", "t.flow:2: rank needs nation= on the char line");
        Refused("rank points not a key", nationHead + "rank 2 250\nlogin\n", "t.flow:2: expected points=<n>: 250");
        Refused("rank out of range", nationHead + "rank 256\nlogin\n","t.flow:2: rank 256 is out of range");
        Refused("rank points out of range", nationHead + "rank 2 points=70000\nlogin\n","t.flow:2: rank points 70000 are out of range");
        {
            // pre-login completion follows the engine's flag rule: CoP and an id >= 64 write no flag (and never the next log's bytes)
            var blob = new byte[XI.Zone.CharData.MaxMissionArea * XI.Zone.CharData.MissionLogSize];
            ApplyMission(blob, 6, 5, true);
            ApplyMission(blob, 1, 64, true);
            ApplyMission(blob, 1, 63, true);
            int ls = XI.Zone.CharData.MissionLogSize;
            Check("  completed CoP / id >= 64: no flag, next log's current untouched", "0|00|0|1", $"{blob[6 * ls + 6 + 5]}|{blob[2 * ls]}{blob[2 * ls + 1]}|{blob[7 * ls]}|{blob[ls + 6 + 63]}");
            Check("  ... the log's current reset", "FFFF", $"{blob[ls + 1]:X2}{blob[ls]:X2}");
        }
        Refused("an unknown title", head + "title Nothing\nlogin\n", "t.flow:2: unknown title Nothing");
        try
        {
            var sk = Flow.Parse(head + "skill GreatAxe 2400\nlogin\nskill HandToHand 0\n", "s.flow", "s", null);
            Check("  skill <Skill> <n>: setup and step, the xi.skill id and the stored value", $"setup skill {Skill.GreatAxe} 2400|step skill {Skill.HandToHand} 0",
                  $"setup {sk.Setup[0].Verb} {sk.Setup[0].Value} {sk.Setup[0].Amount}|step {sk.Steps[1].Verb} {sk.Steps[1].Value} {sk.Steps[1].Amount}");
        }
        catch (FormatException e)
        {
            Fail("  skill <Skill> <n>", e.Message);
        }
        Refused("an unknown skill", head + "skill GreatAxes 2400\nlogin\n", "t.flow:2: unknown skill GreatAxes");
        Refused("a skill value out of range", head + "skill GreatAxe 70000\nlogin\n", "t.flow:2: a skill value is 0 to 65535 (10x the level): 70000");
        Refused("skill without a value", head + "skill GreatAxe\nlogin\n", "t.flow:2: skill takes 2 arguments");
        try
        {
            var cap = Flow.Parse(head + "levelcap 70\nlogin\nlevelcap 75\n", "c.flow", "c", null);
            Check("  levelcap <n>: setup and step", "setup levelcap 70|step levelcap 75",
                  $"setup {cap.Setup[0].Verb} {cap.Setup[0].Amount}|step {cap.Steps[1].Verb} {cap.Steps[1].Amount}");
        }
        catch (FormatException e)
        {
            Fail("  levelcap <n>", e.Message);
        }
        try
        {
            var ws = Flow.Parse(head + "item AxeOfTrials wsp=300\nitem CopperIngot x2\nitem AxeOfTrials x1 wsp=0\nlogin\nitem AxeOfTrials wsp=299\nitem CopperIngot\n", "w.flow", "w", null);
            Check("  item wsp=<n>: one weapon with its points, setup and step (none without)", $"{Item.AxeOfTrials}x1:300|{Item.CopperIngot}x2:-|{Item.AxeOfTrials}x1:0|{Item.AxeOfTrials}x1:299|{Item.CopperIngot}x1:-",
                  string.Join("|", ws.Setup.Concat(ws.Steps.Skip(1)).Select(st => $"{st.Value}x{st.Amount}:{st.WsPoints?.ToString() ?? "-"}")));
        }
        catch (FormatException e)
        {
            Fail("  item wsp=<n>", e.Message);
        }
        try
        {
            Flow.Parse(head + "item CopperIngot wsp=300\nlogin\n", "t.flow", "t", null, i => i == Item.AxeOfTrials);
            Fail("item wsp= on an item that is no weapon", "parsed");
        }
        catch (FormatException e)
        {
            Check("item wsp= on an item that is no weapon", "t.flow:2: wsp= needs a weapon: CopperIngot is not one", e.Message);
        }
        try
        {
            var wsOk = Flow.Parse(head + "item AxeOfTrials wsp=300\nlogin\n", "t.flow", "t", null, i => i == Item.AxeOfTrials);
            Check("  item wsp= on a weapon (the resolver's)", "300", wsOk.Setup[0].WsPoints);
        }
        catch (FormatException e)
        {
            Fail("  item wsp= on a weapon (the resolver's)", e.Message);
        }
        Refused("item wsp= with a count", head + "item AxeOfTrials x2 wsp=300\nlogin\n", "t.flow:2: wsp= gives one weapon: no count, or x1");
        Refused("item wsp= out of range", head + "item AxeOfTrials wsp=70000\nlogin\n", "t.flow:2: wsp=<n> takes 0 to 65535: wsp=70000");
        Refused("item with another word", head + "item AxeOfTrials points=300\nlogin\n", "t.flow:2: a count is x<n>: points=300");
        Refused("item with a count, then another word", head + "item AxeOfTrials x1 points=300\nlogin\n", "t.flow:2: expected wsp=<n>: points=300");
        Refused("item with a word after wsp=", head + "item AxeOfTrials wsp=300 x1\nlogin\n",
                "t.flow:2: item takes <Item> [x<n>] [wsp=<n>] [held]: x1");
        Refused("a level cap out of range", head + "levelcap 100\nlogin\n", "t.flow:2: a level cap is 1 to 99: 100");
        Refused("levelcap not a number", head + "login\nlevelcap high\n", "t.flow:3: not a number: high");
        Refused("a setting value", nationHead + "setting EXP_RATE lots\nlogin\n", "t.flow:2: not a number: lots");
        Refused("a setting name", nationHead + "setting 1X 2\nlogin\n", "t.flow:2: a setting name is an identifier, or map.<identifier>: 1X");
        try
        {
            var npcs = Flow.Parse(head + "login\ntrigger Michea\n", "t.flow", "t", (z, n) => z == Zone.BastokMarkets && n == "Michea");
            Check("an NPC with no script, known to the resolver (npcs.yaml)", "Michea", npcs.Steps[1].Name);
        }
        catch (FormatException e)
        {
            Fail("an NPC with no script, known to the resolver (npcs.yaml)", e.Message);
        }

        Console.WriteLine("the record's call-site check (Lua code gone for an NPC-call quest)");
        const string key = "outlands.personal_hygiene";
        Check("  xi.cs.quest with the key: gone", true, CallsQuest("    if xi.cs.quest(player, npc, 'outlands.personal_hygiene', 'onTrigger') then", key));
        Check("  xi.cs.questEvent, double quotes: gone", true, CallsQuest("    xi.cs.questEvent(player, npc, \"outlands.personal_hygiene\", csid, option)", key));
        Check("  a status read of xi.quest.id: not gone", false,
              CallsQuest("    local status = player:getQuestStatus(xi.questLog.OUTLANDS, xi.quest.id.outlands.PERSONAL_HYGIENE)", key));
        Check("  a call in a comment: not gone", false, CallsQuest("    -- xi.cs.quest(player, npc, 'outlands.personal_hygiene', 'onTrigger')", key));
        Check("  another key: not gone", false, CallsQuest("    xi.cs.quest(player, npc, 'outlands.personal_hygiene2', 'onTrigger')", key));
        Check("  the keys a script calls (comments skipped)", "outlands.indomitable_spirit,outlands.the_immortal_lu_shang",
              string.Join(",", CalledKeys("    if xi.cs.quest(player, npc, 'outlands.the_immortal_lu_shang', 'onTrade', trade) then\n" +
                                          "    xi.cs.questEvent(player, npc, \"outlands.indomitable_spirit\", csid, option)\n" +
                                          "    -- xi.cs.quest(player, npc, 'outlands.personal_hygiene', 'onTrigger')\n")));

        Check("  xi.cs.questZoneIn with the key: gone", true, CallsQuest("        cs = xi.cs.questZoneIn(player, 'outlands.personal_hygiene', prevZone) or cs", key));
        Check("  the key a zone-in calls", "otherAreas.a_moral_manifest",
              string.Join(",", CalledKeys("        cs = xi.cs.questZoneIn(player, 'otherAreas.a_moral_manifest', prevZone) or cs\n")));

        FlowHashTest();
        LuaCommitTest();
        CoverageTest();
        TraceFormatTest();
        StartupMathTest();
    }

    // the startup mode's pure parts: the registered line, another build's runs, the per-quest cost and the projection
    private static void StartupMathTest()
    {
        Console.WriteLine("the startup projection");
        Check("  the registered line: its count and ms", "13/12",
              RegisteredLine("quests: 13 C# quests registered in 12 ms") is { } r ? $"{r.Count}/{r.Ms}" : "(no match)");
        Check("  a failure line is not the registered line", "(no match)",
              RegisteredLine("quests: bastok.groceries not registered: no NPC or mob named Tami in zone 234")?.ToString() ?? "(no match)");
        Check("  the line without its time is not either", "(no match)",
              RegisteredLine("quests: 13 C# quests registered")?.ToString() ?? "(no match)");
        Check("  benign: the registered line", true, BenignQuestLines.Any(re => re.IsMatch("quests: 13 C# quests registered in 12 ms")));
        Check("  not benign: a failure line", false,
              BenignQuestLines.Any(re => re.IsMatch("quests: C# quest bastok.groceries failed to load and has no Lua fallback: x")));
        Check("  a progress failure line is a problem", "True/True/True",
              $"{IsProgressFailure("progress: char 996161: writing its rows failed; the legacy state is used")}" +
              $"/{IsProgressFailure("progress: char 996161: its rows could not be read; the legacy state is used")}" +
              $"/{IsProgressFailure("progress: char 996161: writing its rows failed; its rows are off until its next load")}");
        Check("  the other progress lines are not", "False/False/False/False",
              $"{IsProgressFailure("progress: char 996161 imported")}/{IsProgressFailure("progress: char 996161 re-imported (its rows could not be read: x)")}" +
              $"/{IsProgressFailure("progress: char 996161 re-keyed 2 quests")}" +
              $"/{IsProgressFailure("progress: char 996161 retried its rows write (attempt 2) after a lock conflict (MySQL 1213)")}");

        var probe = ParseProbe("startup runs 905 920 1500 900 910 ms\nstartup median 910 ms\n");
        Check("  a build with no cold line: its slowest run is the cold one, the others' median the warm", "1500/907.5",
              $"{probe.Cold}/{probe.Warm}");
        probe = ParseProbe("startup first 920 ms\nstartup runs 900 905 910 920 1500 ms\nstartup median 910 ms\n");
        Check("  a build patched to print its first run: that one is the cold run, the others' median the warm", "920/907.5",
              $"{probe.Cold}/{probe.Warm}");
        const string old = "        runs.Sort();\n        Console.WriteLine(\"startup runs \" + string.Join(\" \", runs));\n";
        Check("  the patch prints the first run before the sort", "        Console.WriteLine($\"startup first {runs[0]:F0} ms\");\n" + old,
              PatchFirstRun(old));
        Check("  no sort to patch: unchanged (null)", "(null)", PatchFirstRun("        runs.Add(1);\n") ?? "(null)");
        probe = ParseProbe("startup runs 900 905 910 920 930 ms\nstartup cold 1400 ms\nstartup median 910 ms\n");
        Check("  a build with a cold line: its cold line and its median", "1400/910", $"{probe.Cold}/{probe.Warm}");

        Check("  per-quest cost: (812 - 12) ms over 1000 quests", "800", PerQuestUs(812, 12, 1000).ToString("F0"));
        Check("  projected: (1000 ms + 75 x 800 us) / 1000 ms x 1.045", "1.108", ProjectedRatio(1000, 800, 1000, 75, 1.045).ToString("F3"));
        Check("  projected: (1000 ms + 1000 x 800 us) / 1000 ms x 1.045", "1.881", ProjectedRatio(1000, 800, 1000, 1000, 1.045).ToString("F3"));
        Check("  projected against its own baseline (no scaling)", "1.060", ProjectedRatio(1000, 800, 1000, 75, 1.0).ToString("F3"));
        Check("  the gate: a warm ratio over 1.10 fails", "1.110 is over 1.10", StartupGateFailure(1.11, 1.0) ?? "(passes)");
        Check("  the gate: a cold ratio over 1.10 does not fail (info only)", "(passes)", StartupGateFailure(1.05, 1.41) ?? "(passes)");
        Check("  the gate: 1.10 warm passes", "(passes)", StartupGateFailure(1.10, 2.0) ?? "(passes)");
        Check("  synthetic cold estimate: 1437 + (1852 - 1157) ms", "2132", SyntheticColdEstimate(1437, 1157, 1852).ToString("F0"));
        Check("  ... its ratio: 2132 / 1511 x 1.045", "1.474", (SyntheticColdEstimate(1437, 1157, 1852) / 1511 * 1.045).ToString("F3"));
        Check("  a probe's registration line: cold, median and count", "171/79/13",
              string.Join("/", new[] { 1, 2, 3 }.Select(g => Figure("startup registration cold 171 ms, median 79 ms (13 C# quests)\n", "startup registration cold", g))));
        Check("  a probe's hidden Lua files: its own count's line", "498",
              HiddenCount("synthetic 100 quests: 7 Lua quest files hidden\n" +
                          "synthetic 1000 quests: 498 Lua quest files hidden\nsynthetic 1000 quests: 1080 ms registration, 998 us per quest\n", 1000).ToString());
    }

    // a quest with one handler of each label form, for the label test
    private sealed class LabelQuest : XI.Quests.Authoring.QuestScript
    {
        public LabelQuest() : base(XI.Quests.QuestLog.Bastok, "GROCERIES") { }

        protected override void Build(XI.Quests.Authoring.QuestScriptBuilder b)
        {
            b.State(XI.Quests.QuestStatus.Available)
                .Npc(Zone.BastokMines, "Tami").OnTrigger(XI.Quests.Authoring.QuestReply.ProgressEvent(110))
                .OnEventFinish(Zone.BastokMines, 110, _ => { });
            b.State(XI.Quests.QuestStatus.Accepted)
                .Npc(Zone.BastokMines, "Tami").OnTrade(_ => null).OnTrigger(_ => null)
                .OnZoneIn(Zone.BastokMines, _ => null)
                .OnTriggerAreaEnter(Zone.BastokMines, 3, _ => { })
                .OnEventUpdate(Zone.BastokMines, 5, _ => { });
            b.State(XI.Quests.QuestStatus.Completed)
                .Npc(Zone.BastokMines, "Mob").OnMobDeath(_ => { });
        }
    }

    // the coverage gate: the handler labels, the waivers in a flow, the gate over (declared, calls, waivers), the counts files
    private static void CoverageTest()
    {
        Console.WriteLine("handler coverage");
        Check("  the labels: <n>/<entity>/<trigger>, states numbered from 1 in declaration order, '-' for a state-level handler",
              "1/Tami/onTrigger,1/-/onEventFinish:110,2/Tami/onTrade,2/Tami/onTrigger,2/-/onZoneIn,2/-/onTriggerAreaEnter:3,2/-/onEventUpdate:5,3/Mob/onMobDeath",
              string.Join(",", XI.Zone.ZoneHost.HandlerLabels(new LabelQuest().Compile())));

        const string head = "char zone=BastokMarkets pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(head + "uncovered: 2/Michea/onTrade -- needs a battlefield win\nuncovered: 3/-/onEventFinish:216 -- needs a mob the harness cannot spawn\nlogin\n",
                               "w.flow", "w", null);
            Check("  waivers: line, label and reason each", "2 2/Michea/onTrade needs a battlefield win|3 3/-/onEventFinish:216 needs a mob the harness cannot spawn",
                  string.Join("|", f.Waivers.Select(w => $"{w.Line} {w.Label} {w.Reason}")));
            Check("  ... not setup statements", 0, f.Setup.Count);
        }
        catch (FormatException e)
        {
            Fail("  waivers parse", e.Message);
        }
        void Refused(string what, string text, string message)
        {
            try
            {
                Flow.Parse(text, "t.flow", "t", null);
                Fail(what, "parsed");
            }
            catch (FormatException e)
            {
                Check(what, message, e.Message);
            }
        }
        Refused("  a waiver with no reason", head + "uncovered: 2/Michea/onTrade\nlogin\n", "t.flow:2: uncovered needs a reason");
        Refused("  ... an empty reason", head + "uncovered: 2/Michea/onTrade --   \nlogin\n", "t.flow:2: uncovered needs a reason");
        Refused("  a label that is not <n>/<entity>/<trigger>", head + "uncovered: Michea/onTrade -- x\nlogin\n",
                "t.flow:2: a handler label is <n>/<entity>/<trigger>: Michea/onTrade");
        Refused("  ... an unknown trigger", head + "uncovered: 2/Michea/onPoke -- x\nlogin\n", "t.flow:2: a handler label is <n>/<entity>/<trigger>: 2/Michea/onPoke");
        Refused("  ... an event with no csid", head + "uncovered: 3/-/onEventFinish -- x\nlogin\n",
                "t.flow:2: a handler label is <n>/<entity>/<trigger>: 3/-/onEventFinish");
        Refused("  ... state 0", head + "uncovered: 0/Michea/onTrade -- x\nlogin\n", "t.flow:2: a handler label is <n>/<entity>/<trigger>: 0/Michea/onTrade");
        Refused("  a waiver after login", head + "login\nuncovered: 2/Michea/onTrade -- x\n", "t.flow:3: uncovered: after login");
        Refused("  a waiver whose -- has no space before the reason is named so, not as a bad label",
                head + "uncovered: 2/Michea/onTrade --needs a win\nlogin\n",
                "t.flow:2: uncovered: a space each side of -- before the reason: 2/Michea/onTrade -- needs a win");
        Refused("  ... or none after the label", head + "uncovered: 2/Michea/onTrade-- needs a win\nlogin\n",
                "t.flow:2: uncovered: a space each side of -- before the reason: 2/Michea/onTrade -- needs a win");
        const string plain = "char zone=BastokMarkets pos=0,0,0\nlogin\ntrigger Zon-Fobun\n";
        Check("  the flow hash ignores waivers (they change nothing in the run)", Flow.Hash(plain),
              Flow.Hash("char zone=BastokMarkets pos=0,0,0\nuncovered: 2/Michea/onTrade -- needs a battlefield win\nlogin\ntrigger Zon-Fobun\n"));

        var declared = new[] { "1/Tami/onTrigger", "1/-/onEventFinish:110", "2/Tami/onTrade" };
        LabelCalls Calls(int n, params string[] traces)
        {
            var c = new LabelCalls { Count = n };
            c.Traces.UnionWith(traces);
            return c;
        }
        var calls = new Dictionary<string, LabelCalls>
        {
            ["1/Tami/onTrigger"] = Calls(3, "bastok.groceries#b", "bastok.groceries"),
            ["1/-/onEventFinish:110"] = Calls(1, "bastok.groceries"),
        };
        Waiver W(string label, int line = 2) => new Waiver("bastok.groceries.flow", line, label, "a reason");
        Check("  the gate: every label reached or waived: clean", "",
              string.Join("|", CoverageFailures("bastok.groceries", declared, calls, new[] { W("2/Tami/onTrade") })));
        Check("  ... a label with no calls and no waiver", "coverage bastok.groceries: 2/Tami/onTrade reached by no trace",
              string.Join("|", CoverageFailures("bastok.groceries", declared, calls, Array.Empty<Waiver>())));
        Check("  ... a waived label with calls: named by its first trace", "coverage bastok.groceries: waiver 1/Tami/onTrigger is reached by bastok.groceries",
              string.Join("|", CoverageFailures("bastok.groceries", declared, calls, new[] { W("2/Tami/onTrade"), W("1/Tami/onTrigger", 3) })));
        Check("  ... a waiver naming no declared handler", "bastok.groceries.flow:4: unknown handler 9/Tami/onTrigger",
              string.Join("|", CoverageFailures("bastok.groceries", declared, calls, new[] { W("2/Tami/onTrade"), W("9/Tami/onTrigger", 4) })));
        Check("  ... a label with no entry at all counts as no calls", "coverage bastok.groceries: 1/-/onEventFinish:110 reached by no trace",
              string.Join("|", CoverageFailures("bastok.groceries", declared, new Dictionary<string, LabelCalls> { ["1/Tami/onTrigger"] = Calls(1, "x") },
                                                new[] { W("2/Tami/onTrade") })));

        var a = new Coverage { Pass = 5, Fail = 1 };
        a.Ran.Add("bastok.groceries");
        a.Declared["bastok.groceries"] = declared.ToList();
        a.Calls["bastok.groceries"] = new Dictionary<string, LabelCalls> { ["1/Tami/onTrigger"] = Calls(2, "bastok.groceries"), ["2/Tami/onTrade"] = Calls(0) };
        var b = new Coverage { Pass = 3, Fail = 0 };
        b.Ran.Add("bastok.groceries#b");
        b.Declared["bastok.groceries"] = declared.ToList();
        b.Calls["bastok.groceries"] = new Dictionary<string, LabelCalls> { ["1/Tami/onTrigger"] = Calls(1, "bastok.groceries#b"), ["1/-/onEventFinish:110"] = Calls(1, "bastok.groceries#b") };
        string text = a.Write();
        Check("  a counts file: key<TAB>label<TAB>count<TAB>traces lines, the totals and the flows run", true,
              text.Contains("bastok.groceries\t1/Tami/onTrigger\t2\tbastok.groceries\n") && text.Contains("bastok.groceries\t2/Tami/onTrade\t0\t\n") &&
              text.Contains("pass\t5\n") && text.Contains("fail\t1\n") && text.Contains("ran\tbastok.groceries\n"));
        Check("  ... every declared label written, a zero for one not reached", true, text.Contains("bastok.groceries\t1/-/onEventFinish:110\t0\t\n"));
        var merged = Coverage.Parse(text, "1.txt");
        merged.Merge(Coverage.Parse(b.Write(), "2.txt"));
        Check("  merged: the counts summed, the traces joined, the totals summed, the flows run joined",
              "3:bastok.groceries,bastok.groceries#b|1:bastok.groceries#b|0:|8/1|bastok.groceries,bastok.groceries#b",
              string.Join("|", declared.Select(l => merged.Calls["bastok.groceries"].TryGetValue(l, out var c) ? $"{c.Count}:{string.Join(",", c.Traces)}" : "none")) +
              $"|{merged.Pass}/{merged.Fail}|{string.Join(",", merged.Ran)}");
        Check("  ... the declared labels kept", string.Join(",", declared), string.Join(",", merged.Declared["bastok.groceries"]));
        try
        {
            Coverage.Parse("bastok.groceries\t1/Tami/onTrigger\tmany\t\n", "bad.txt");
            Fail("  a counts line whose count is not a number", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a counts line whose count is not a number", "bad.txt:1: a counts line is key<TAB>label<TAB>count<TAB>traces: bastok.groceries\t1/Tami/onTrigger\tmany\t", e.Message);
        }
        CollisionTest();
        NoHandlersTest();
    }

    // a quest whose handlers would share labels across zones: two onZoneIn, two onEventFinish 10, Tami's onTrigger twice
    private sealed class CollidingQuest : XI.Quests.Authoring.QuestScript
    {
        public CollidingQuest() : base(XI.Quests.QuestLog.Bastok, "GROCERIES") { }

        protected override void Build(XI.Quests.Authoring.QuestScriptBuilder b)
        {
            b.State(XI.Quests.QuestStatus.Available)
                .Npc(Zone.BastokMines, "Tami").OnTrigger(_ => null)
                .Npc(Zone.BastokMarkets, "Tami").OnTrigger(_ => null)
                .Npc(Zone.BastokMines, "Tami").OnTrade(_ => null)
                .OnZoneIn(Zone.BastokMines, _ => null)
                .OnZoneIn(Zone.PortBastok, _ => null)
                .OnEventFinish(Zone.BastokMines, 10, _ => { })
                .OnEventFinish(Zone.ZeruhnMines, 10, _ => { })
                .OnEventFinish(Zone.ZeruhnMines, 11, _ => { });
            b.State(XI.Quests.QuestStatus.Accepted)
                .OnZoneIn(Zone.BastokMines, _ => null);
        }
    }

    // labels that would collide get @<Zone> (every one of them), the others keep the plain form; the hook's label (the
    // binding's) is the same; reaching one of a pair leaves the other unreached
    private static void CollisionTest()
    {
        Console.WriteLine("zone-qualified labels on collision");
        var def = new CollidingQuest().Compile();
        string want = "1/Tami/onTrigger@BastokMines,1/Tami/onTrigger@BastokMarkets,1/Tami/onTrade,1/-/onZoneIn@BastokMines,1/-/onZoneIn@PortBastok," +
                      "1/-/onEventFinish:10@BastokMines,1/-/onEventFinish:10@ZeruhnMines,1/-/onEventFinish:11,2/-/onZoneIn";
        var labels = XI.Zone.ZoneHost.HandlerLabels(def);
        Check("  every colliding label zone-qualified, the rest plain, none merged", want, string.Join(",", labels));
        var binding = new XI.Zone.QuestScriptBinding("bastok.groceries", def, "bastok", 37);
        Check("  ... the hook's label of each handler is the same", want,
              string.Join(",", def.States.SelectMany(st => st.Handlers).Select(binding.Label)));
        var calls = new Dictionary<string, LabelCalls>();
        foreach (var l in labels.Where(l => l != "1/-/onEventFinish:10@ZeruhnMines" && l != "1/-/onZoneIn@PortBastok"))
        {
            var c = new LabelCalls { Count = 1 };
            c.Traces.Add("bastok.groceries");
            calls[l] = c;
        }
        Check("  reaching one of a pair leaves the other reached by no trace",
              "coverage bastok.groceries: 1/-/onZoneIn@PortBastok reached by no trace|coverage bastok.groceries: 1/-/onEventFinish:10@ZeruhnMines reached by no trace",
              string.Join("|", CoverageFailures("bastok.groceries", labels, calls, Array.Empty<Waiver>())));
        Check("  ... a waiver names just the one", "",
              string.Join("|", CoverageFailures("bastok.groceries", labels, calls, new[]
              {
                  new Waiver("w.flow", 2, "1/-/onZoneIn@PortBastok", "x"), new Waiver("w.flow", 3, "1/-/onEventFinish:10@ZeruhnMines", "x"),
              })));
        Check("  ... the bare label of a qualified pair is no handler", "w.flow:2: unknown handler 1/-/onZoneIn",
              string.Join("|", CoverageFailures("bastok.groceries", labels, calls, new[]
              {
                  new Waiver("w.flow", 2, "1/-/onZoneIn", "x"), new Waiver("w.flow", 3, "1/-/onZoneIn@PortBastok", "x"),
                  new Waiver("w.flow", 4, "1/-/onEventFinish:10@ZeruhnMines", "x"),
              })));

        const string head = "char zone=BastokMarkets pos=0,0,0\n";
        try
        {
            var f = Flow.Parse(head + "uncovered: 1/-/onZoneIn@PortBastok -- a zone-qualified one\nuncovered: 2/Gloom Phantom/onTrigger -- an entity name with a space\nlogin\n",
                               "w.flow", "w", null);
            Check("  a waiver with @<Zone>, and one whose entity name has a space", "1/-/onZoneIn@PortBastok:a zone-qualified one|2/Gloom Phantom/onTrigger:an entity name with a space",
                  string.Join("|", f.Waivers.Select(w => $"{w.Label}:{w.Reason}")));
        }
        catch (FormatException e)
        {
            Fail("  a waiver with @<Zone>, and one whose entity name has a space", e.Message);
        }
        try
        {
            Flow.Parse(head + "uncovered: 1/-/onZoneIn@Nowhere -- x\nlogin\n", "t.flow", "t", null);
            Fail("  a waiver's unknown zone", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a waiver's unknown zone", "t.flow:2: unknown zone Nowhere", e.Message);
        }
        try
        {
            Flow.Parse(head + "uncovered: 2/Gloom Phantom/onTrigger\nlogin\n", "t.flow", "t", null);
            Fail("  a spaced label with no reason", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a spaced label with no reason", "t.flow:2: uncovered needs a reason", e.Message);
        }
    }

    // a replayed quest whose handlers are not known (no C# quest compiled for its key) fails coverage, merged too
    private static void NoHandlersTest()
    {
        Console.WriteLine("a replayed quest with no known handlers");
        var flows = new List<Flow>
        {
            Flow.Parse("char zone=BastokMarkets pos=0,0,0\nlogin\n", "bastok.groceries.flow", "bastok.groceries", null),
            Flow.Parse("char zone=BastokMarkets pos=0,0,0\nlogin\n", "bastok.groceries#b.flow", "bastok.groceries#b", null),
        };
        var cov = new Coverage();
        cov.Ran.Add("bastok.groceries");
        cov.NoHandlers.Add("bastok.groceries");
        Check("  one flow ran, its quest's handlers unknown: a failure", "FAIL coverage bastok.groceries: no C# handlers known",
              string.Join("|", CoverageResults(cov, flows)));
        var merged = Coverage.Parse(cov.Write(), "1.txt");
        var other = new Coverage();
        other.Ran.Add("bastok.groceries#b");
        other.Declared["bastok.groceries"] = new List<string> { "1/Tami/onTrigger" };
        merged.Merge(Coverage.Parse(other.Write(), "2.txt"));
        Check("  ... written, read back and merged with a shard that knew them: still a failure", "FAIL coverage bastok.groceries: no C# handlers known",
              string.Join("|", CoverageResults(merged, flows)));
        var known = new Coverage();
        known.Ran.UnionWith(new[] { "bastok.groceries", "bastok.groceries#b" });
        known.Declared["bastok.groceries"] = new List<string> { "1/Tami/onTrigger" };
        known.Add("bastok.groceries", "1/Tami/onTrigger", "bastok.groceries");
        Check("  ... known and reached: a pass", "PASS coverage bastok.groceries: 1 of 1 handlers reached, 0 waived", string.Join("|", CoverageResults(known, flows)));
        Check("  a C# quest with no flow: a failure", "FAIL coverage bastok.mine: no flows|PASS coverage bastok.groceries: 1 of 1 handlers reached, 0 waived",
              string.Join("|", CoverageResults(known, flows, new[] { "bastok.groceries", "bastok.mine" })));
        Check("  ... keys not checked (single key, shard): no failure", "PASS coverage bastok.groceries: 1 of 1 handlers reached, 0 waived", string.Join("|", CoverageResults(known, flows, null)));
    }

    // the flow hash: blind to comments, trailing whitespace, blank lines and CRLF; not to a statement
    private static void FlowHashTest()
    {
        Console.WriteLine("the flow hash");
        const string plain = "char zone=BastokMarkets pos=0,0,0\nlogin\ntrigger Zon-Fobun\nfinish option=1\n";
        const string noisy = "# the groceries\n\nchar zone=BastokMarkets pos=0,0,0   \n   \nlogin\t# a comment\r\ntrigger Zon-Fobun\n\n# the end\nfinish option=1  \n\n";
        Check("  comments, trailing spaces, blank lines and CRLF: the same hash", Flow.Hash(plain), Flow.Hash(noisy));
        Check("  a changed statement: another hash", false, Flow.Hash(plain) == Flow.Hash(plain.Replace("option=1", "option=2")));
        Check("  a '#' inside a word is not a comment", false, Flow.Hash("trigger HomePoint#1\n") == Flow.Hash("trigger HomePoint#2\n"));
        try
        {
            Check("  a parsed flow's sha is the hash", Flow.Hash(plain), Flow.Parse(noisy, "h.flow", "h", null).Sha256);
        }
        catch (FormatException e)
        {
            Fail("  a parsed flow's sha is the hash", e.Message);
        }
    }

    // record's choice of the commit whose Lua it runs, the quest's Lua id constant and the overlay's token match
    private static void LuaCommitTest()
    {
        Console.WriteLine("the record's Lua commit");
        const string at = "0fad61802ae0f41a1c67076a91618d5070685aae", header = "156ff1e6156ff1e6156ff1e6156ff1e6156ff1e6";
        string Choice(string luaAt, bool present, string trace)
        {
            var (commit, error) = LuaCommit("bastok.groceries", luaAt, present, trace);
            return commit ?? error;
        }
        Check("  --lua-at given: that commit", at, Choice(at, true, header));
        Check("  else the Lua present at HEAD: HEAD", "HEAD", Choice(null, true, header));
        Check("  else the trace's lua-at", header, Choice(null, false, header));
        Check("  else refused", "cannot record bastok.groceries: its Lua code is gone; use --lua-at <commit>", Choice(null, false, null));
        Check("  the quest's Lua id constant", "xi.quest.id.bastok.GROCERIES", LuaIdConstant("bastok.groceries"));
        Check("  ... an area in camel case", "xi.quest.id.ahtUrhgan.TWO_HORN_THE_SAVAGE", LuaIdConstant("ahtUrhgan.two_horn_the_savage"));
        Check("  the constant as a whole token", true, RefersTo("    local q = Quest:new(xi.questLog.BASTOK, xi.quest.id.bastok.GROCERIES)\n", "xi.quest.id.bastok.GROCERIES"));
        Check("  ... not a longer constant", false, RefersTo("    xi.quest.id.bastok.GROCERIES_2\n", "xi.quest.id.bastok.GROCERIES"));

        // the record's own CSHARP_QUESTS_OFF lines are not problems; the same lines naming another key are
        string[] off = { "bastok.too_many_chefs", "outlands.personal_hygiene" };
        const string m6 = "quests: call-only quests not registered (CSHARP_QUESTS / CSHARP_QUESTS_OFF): ";
        Check("  record: M6 naming only the keys it switched off is expected", true, RecordOffLine(m6 + "bastok.too_many_chefs, outlands.personal_hygiene", off));
        Check("  ... naming another key is a problem", false, RecordOffLine(m6 + "bastok.too_many_chefs, outlands.indomitable_spirit", off));
        Check("  ... a switched-off key with no C# quest yet is expected", true, RecordOffLine("quests: bastok.too_many_chefs in CSHARP_QUESTS_OFF is not a C# quest", off));
        Check("  ... any of it on the C# side (nothing off) is a problem", false, RecordOffLine(m6 + "bastok.too_many_chefs", Array.Empty<string>()));
    }

    private static void TraceFormatTest()
    {
        Console.WriteLine("the trace format");
        var t = new Trace { Name = "bastok.every", Seed = 7, FlowSha = "ab", Recorder = "lua", LuaAt = "0fad61802ae0f41a1c67076a91618d5070685aae" };
        t.World.Add(("server_variables", "0123456789abcdef"));
        t.World.Add(("conquest_system", "fedcba9876543210"));
        t.Masks.Add(new Mask(0x00D, 4, 4, "an example"));
        var s1 = new TraceStep { Label = "trigger Michea", Ret = "onTrigger=number:1" };
        s1.Packets.Add("00A:" + string.Concat(Enumerable.Repeat("0123456789ABCDEF", 9)) + "<CHARID>12");
        s1.Packets.Add("05B:");
        s1.Set("chars").Add("quests=00FF000000");
        s1.Set("char_profile").Add("fame_bastok=0");
        s1.Set("char_vars").Add("varname=a|value=1|expiry=0");
        s1.Orders.Add(("235/Michea/onTrigger", "Quest[1][13],default"));
        s1.Orders.Add(("235/onEventFinish/215", "Quest[1][13]"));
        t.Steps.Add(s1);
        var s2 = s1.Carry("finish option=1");
        s2.Packets.Add("05B:");
        s2.Set("chars").Remove("quests=00FF000000");
        s2.Set("chars").Add("quests=01FF020300");
        s2.Set("char_profile").Remove("fame_bastok=0");
        s2.Set("char_profile").Add("fame_bastok=10");
        s2.Set("char_points").Add("sandoria_cp=1");
        s2.Orders[0] = ("235/Michea/onTrigger", "default,Quest[1][13]");
        t.Steps.Add(s2);
        t.Steps.Add(s2.Carry("trigger Michea"));

        var store = new PacketStore();
        string text = t.Write(store);
        var lines = text.Split('\n');
        Check("  LF only", false, text.Contains('\r'));
        Check("  the world hashes in the header", "world server_variables 0123456789abcdef|world conquest_system fedcba9876543210",
              string.Join("|", lines.TakeWhile(l => !l.StartsWith("step ")).Where(l => l.StartsWith("world "))));
        Check("  a packet by its hash in the store", $"  00A @{PacketStore.Hash(s1.Packets[0])}", lines.First(l => l.StartsWith("  00A")));
        Check("  ... the same packet twice: one store entry", "05B:", store.Get(PacketStore.Hash("05B:")));
        Check("  the first step: every table as one store entry of its rows, tables in name order",
              $"char_profile @{PacketStore.Hash("fame_bastok=0")} (1)|char_vars @{PacketStore.Hash("varname=a|value=1|expiry=0")} (1)|chars @{PacketStore.Hash("quests=00FF000000")} (1)",
              string.Join("|", lines.SkipWhile(l => l != "step trigger Michea").TakeWhile(l => l != "step finish option=1").Where(l => l.StartsWith("char"))));
        Check("  ... every order", 2, lines.TakeWhile(l => l != "step finish option=1").Count(l => l.StartsWith("order ")));
        var second = lines.SkipWhile(l => l != "step finish option=1").Skip(1).TakeWhile(l => !l.StartsWith("step ")).ToList();
        Check("  a later step: a changed table as - and + rows, a new table as + rows",
              "char_points + sandoria_cp=1|char_profile - fame_bastok=0|char_profile + fame_bastok=10|chars - quests=00FF000000|chars + quests=01FF020300",
              string.Join("|", second.Where(l => l.StartsWith("char"))));
        Check("  ... only the order that moved", "order 235/Michea/onTrigger=default,Quest[1][13]", string.Join("|", second.Where(l => l.StartsWith("order "))));
        Check("  ... nothing unchanged (no char_vars)", 0, second.Count(l => l.StartsWith("char_vars ")));
        var third = lines.SkipWhile(l => l != "step trigger Michea").Skip(1).SkipWhile(l => l != "step trigger Michea").Skip(1).ToList();
        Check("  an unchanged step: no state lines", "packets|", string.Join("|", third));

        var back = Trace.Parse(text, "t.trace", store);
        Check("  written, read, written: the same text", text, back.Write(store));
        Check("  the state rebuilt for each step", "quests=01FF020300/fame_bastok=10/varname=a|value=1|expiry=0/sandoria_cp=1",
              $"{string.Join(",", back.Steps[2].Rows("chars"))}/{string.Join(",", back.Steps[2].Rows("char_profile"))}/{string.Join(",", back.Steps[2].Rows("char_vars"))}/" +
              string.Join(",", back.Steps[2].Rows("char_points")));
        Check("  the world hashes read back", "server_variables=0123456789abcdef,conquest_system=fedcba9876543210",
              string.Join(",", back.World.Select(w => $"{w.Table}={w.Hash}")));
        Check("  the mask read back", "mask 0x00D 4 4 an example", back.Masks.Single().Line);
        Check("  the lua-at header line", "lua-at 0fad61802ae0f41a1c67076a91618d5070685aae", lines.FirstOrDefault(l => l.StartsWith("lua-at ")));
        Check("  ... read back (the header alone too)", "0fad61802ae0f41a1c67076a91618d5070685aae/0fad61802ae0f41a1c67076a91618d5070685aae",
              $"{back.LuaAt}/{Trace.Parse(text, "t.trace", null).LuaAt}");
        Check("  ... none when not set", "", string.Join("|", new Trace { Name = "x", Seed = 7, FlowSha = "ab", Recorder = "lua" }.Write(store).Split('\n').Where(l => l.StartsWith("lua-at"))));
        try
        {
            Trace.Parse("seed 7\nlua-at HEAD\n", "bad.trace", null);
            Fail("  a lua-at that is not a 40-hex commit", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a lua-at that is not a 40-hex commit", "bad.trace:2: lua-at is a 40-hex commit: HEAD", e.Message);
        }
        try
        {
            Trace.Parse("seed 7\nworld server_variables HEAD\n", "bad.trace", null);
            Fail("  a world line that is not <table> <16-hex>", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a world line that is not <table> <16-hex>", "bad.trace:2: world is <table> <16-hex>: server_variables HEAD", e.Message);
        }
        Check("  equal traces compare equal", "", string.Join("|", Trace.Compare(t, back)));
        try
        {
            Trace.Parse(text, "t.trace", new PacketStore());
            Fail("  a packet the store lacks", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a packet the store lacks", true, e.Message.Contains("is not in the store"));
        }

        // the low nibble of byte 3 ("67" -> "6F")
        back.Steps[0].Packets[0] = back.Steps[0].Packets[0].Remove(11, 1).Insert(11, "F");
        var diff = Trace.Compare(t, back);
        Check("  a changed packet byte: one difference, at the step, packet, opcode and byte", "step 1 [trigger Michea]: packet #0 (0x00A) differs at byte 3",
              diff.Count == 1 ? diff[0].Split('\n')[0] : string.Join("|", diff));
        var dl = diff.Count == 1 ? diff[0].Split('\n') : new[] { "", "", "", "" };
        Check("  ... with a ^ under the byte's first character", dl[1].IndexOf("00A:") + 4 + 6, dl[3].IndexOf('^'));
        // past a placeholder: 72 bytes, the char id (4), then "12" -> "13"
        back = Trace.Parse(text, "t.trace", store);
        back.Steps[0].Packets[0] = back.Steps[0].Packets[0][..^1] + "3";
        diff = Trace.Compare(t, back);
        Check("  a byte past <CHARID>: its packet offset", "step 1 [trigger Michea]: packet #0 (0x00A) differs at byte 76",
              diff.Count == 1 ? diff[0].Split('\n')[0] : string.Join("|", diff));
        dl = diff.Count == 1 ? diff[0].Split('\n') : new[] { "", "", "", "" };
        Check("  ... with a ^ under it", dl[1].Length - 2, dl[3].IndexOf('^'));
        back = Trace.Parse(text, "t.trace", store);
        back.Steps[2].Set("char_profile").Clear();
        back.Steps[2].Set("char_profile").Add("fame_bastok=11");
        Check("  a table's row: the table and both rows", "step 3 [trigger Michea]: char_profile: recorded only [fame_bastok=10], now only [fame_bastok=11]",
              string.Join("|", Trace.Compare(t, back)));
        back = Trace.Parse(text, "t.trace", store);
        back.Steps[1].Sets.Remove("char_points");
        back.Steps[2].Set("char_merit").Add("meritid=64|upgrades=1");
        Check("  a table only one side has rows in: named", "step 2 [finish option=1]: char_points: recorded only [sandoria_cp=1], now only []|" +
                                                           "step 3 [trigger Michea]: char_merit: recorded only [], now only [meritid=64|upgrades=1]",
              string.Join("|", Trace.Compare(t, back)));
        back = Trace.Parse(text, "t.trace", store);
        back.Steps[1].Set("char_jobs");
        Check("  ... a table with no rows equals one not there", "", string.Join("|", Trace.Compare(t, back)));

        back = Trace.Parse(text, "t.trace", store);
        back.Steps[1].Label = "finish";
        Check("  a changed step label", "the steps differ at step 2: recorded [finish option=1], now [finish]", string.Join("|", Trace.Compare(t, back)));

        // the store keeps what the traces in its folder reference
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"questgolden-selftest-{Environment.ProcessId}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            store.Add("0FF:DEAD");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "t.trace"), text);
            var missing = store.Save(dir);
            var saved = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, PacketStore.FileName));
            Check("  the store saved: referenced packets and table entries only, by hash, LF", $"5/{false}/{false}/0",
                  $"{saved.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length}/{saved.Contains("DEAD")}/{saved.Contains('\r')}/{missing.Count}");
            var loaded = PacketStore.Load(dir);
            Check("  ... and loaded back", "05B:", loaded.Get(PacketStore.Hash("05B:")));
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }

        ValueRenderingTest();
        LoginBundleTest();
        BlobRunsTest();
        TableEntryTest();
        ExplainTest();
    }

    // a DB value as a trace renders it, the tables a step records and their elements
    private static void ValueRenderingTest()
    {
        Console.WriteLine("the DB state");
        Check("  NULL", "null", RenderValue(null, 996161));
        Check("  a blob as hex", "01AB00", RenderValue(new byte[] { 1, 0xAB, 0 }, 996161));
        Check("  ... an empty blob: nothing (not null)", "", RenderValue(Array.Empty<byte>(), 996161));
        Check("  any other value as its text", "-239.5", RenderValue("-239.5", 996161));
        Check("  the char's id as a whole value: $char", "$char", RenderValue("996161", 996161));
        Check("  ... not inside another value", "9961610/1996161", $"{RenderValue("9961610", 996161)}/{RenderValue("1996161", 996161)}");
        Check("  the char's name as a whole value: $charname", "$charname/xQgolden161", $"{RenderValue("Qgolden161", 996161)}/{RenderValue("xQgolden161", 996161)}");
        Check("  char_effects.subid: the char's id cut to 16 bits as $char16, elsewhere as is", "$char16/13121/$char/7",
              $"{RenderCell("char_effects", "subid", "13121", 996161)}/{RenderCell("char_inventory", "itemId", "13121", 996161)}/" +
              $"{RenderCell("char_effects", "subid", "996161", 996161)}/{RenderCell("char_effects", "subid", "7", 996161)}");
        Check("  no char (the world hash): no placeholders", "996161/0/Qgolden161", $"{RenderValue("996161", null)}/{RenderValue("0", null)}/{RenderValue("Qgolden161", null)}");
        Check("  newline, return, tab and backslash escaped", @"a\nb\rc\td\\n", RenderValue("a\nb\rc\td\\n", 996161));
        var esc = new Trace { Name = "x", Seed = 7, FlowSha = "ab", Recorder = "lua" };
        var e1 = new TraceStep { Label = "trigger Michea" };
        esc.Steps.Add(e1);
        var e2 = e1.Carry("finish");
        e2.Set("char_vars").Add("varname=" + RenderValue("two\nlines", 996161) + "|value=1");
        esc.Steps.Add(e2);
        var escStore = new PacketStore();
        string escText = esc.Write(escStore);
        Check("  ... a trace of an escaped value: one line, read back the same", @"char_vars + varname=two\nlines|value=1/True",
              $"{escText.Split('\n').FirstOrDefault(l => l.StartsWith("char_vars"))}/{Trace.Compare(esc, Trace.Parse(escText, "x.trace", escStore)).Count == 0}");
        Check("  a single-row table: one col=value element per column", "a=1,b=null",
              string.Join(",", StateElements(new[] { "a", "b" }, true, new[] { new[] { "1", "null" } })));
        Check("  a multi-row table: one element per row, col=value by '|'", "a=2|b=x,a=1|b=y",
              string.Join(",", StateElements(new[] { "a", "b" }, false, new[] { new[] { "2", "x" }, new[] { "1", "y" } })));

        var schema = new (string, string, string, string)[]
        {
            ("chars", "charid", "int", "PRI"), ("chars", "accid", "int", ""), ("chars", "quests", "blob", ""), ("chars", "lastupdate", "timestamp", ""),
            ("chars", "playtime", "int", ""),
            ("char_inventory", "charid", "int", "PRI"), ("char_inventory", "location", "tinyint", "PRI"), ("char_inventory", "slot", "tinyint", "PRI"),
            ("char_inventory", "extra", "tinyblob", ""),
            ("char_effects", "charid", "int", "MUL"), ("char_effects", "effectid", "smallint", ""), ("char_effects", "added", "datetime", ""),
            ("char_points", "charid", "int", "PRI"), ("char_points", "sandoria_cp", "int", ""), ("char_points", "birthday", "date", ""),
            ("char_noisy", "charid", "int", "PRI"), ("char_noisy", "x", "int", ""),
            ("server_variables", "name", "varchar", "PRI"), ("server_variables", "value", "int", ""),
        };
        var tables = StateTablesFrom(schema, new[] { ("chars.playtime", "a test"), ("char_noisy", "a test") });
        Check("  the tables: every one with a charid column, and chars, in name order; an ignored table gone",
              "char_effects,char_inventory,char_points,chars", string.Join(",", tables.Select(st => st.Name)));
        Check("  ... their columns: no charid, no date / time stamp, no ignored column; blobs marked",
              "effectid|location,slot,extra*|sandoria_cp|accid,quests*",
              string.Join("|", tables.Select(st => string.Join(",", st.Columns.Select(c => c.Name + (c.Blob ? "*" : ""))))));
        Check("  ... single-row: charid the only primary-key column", "False,False,True,True", string.Join(",", tables.Select(st => st.Single)));
        var withRows = schema.Concat(new (string Table, string Column, string Type, string Key)[]
        {
            ("char_assault_state", "charid", "int", "PRI"), ("char_campaign_state", "charid", "int", "PRI"), ("char_eminence_state", "charid", "int", "PRI"),
        });
        Check("  the assault, campaign and eminence rows are not recorded", "char_effects,char_inventory,char_noisy,char_points,chars",
              string.Join(",", StateTablesFrom(withRows, NotRecorded).Select(st => st.Name)));
    }

    // the login step's packets: one bundle in the store, expanded on read
    private static void LoginBundleTest()
    {
        Console.WriteLine("the login bundle");
        var t = new Trace { Name = "b", Seed = 7, FlowSha = "ab", Recorder = "lua" };
        var login = new TraceStep { Label = "login" };
        login.Packets.AddRange(new[] { "00A:01", "05B:", "063:02", "05B:" });
        t.Steps.Add(login);
        var next = login.Carry("trigger Michea");
        next.Packets.Add("05B:");
        t.Steps.Add(next);
        var store = new PacketStore();
        string text = t.Write(store);
        string joined = string.Join(",", login.Packets.Select(PacketStore.Hash));
        string bundle = PacketStore.Hash(joined);
        var lines = text.Split('\n');
        Check("  the login step: one line, the bundle's hash and the packet count", $"packets @{bundle} (4)",
              string.Join("|", lines.SkipWhile(l => l != "step login").Skip(1).TakeWhile(l => !l.StartsWith("step "))));
        Check("  ... another step: a line per packet", "packets|  05B @" + PacketStore.Hash("05B:"),
              string.Join("|", lines.SkipWhile(l => l != "step trigger Michea").Skip(1).Where(l => l.Length > 0)));
        Check("  ... the bundle in the store: the packets' hashes by ','", joined, string.Join(",", store.GetBundle(bundle) ?? Array.Empty<string>()));
        var back = Trace.Parse(text, "b.trace", store);
        Check("  read back: the packets expanded", "00A:01,05B:,063:02,05B:", string.Join(",", back.Steps[0].Packets));
        Check("  ... written again: the same text", text, back.Write(store));

        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"questgolden-bundle-{Environment.ProcessId}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "b.trace"), text);
            store.Add("0FF:DEAD");
            var missing = store.Save(dir);
            var saved = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, PacketStore.FileName)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Check("  saved: the bundle a line of its own, marked '@', with its packets (each once), nothing stale", $"{bundle} @{joined}/4/0",
                  $"{saved.FirstOrDefault(l => l.StartsWith(bundle + " ")) ?? "(none)"}/{saved.Length}/{missing.Count}");
            var loaded = PacketStore.Load(dir);
            Check("  loaded back: the round trip", "00A:01,05B:,063:02,05B:", string.Join(",", Trace.Parse(text, "b.trace", loaded).Steps[0].Packets));
            Check("  ... a packet hash is no bundle, a bundle hash no packet", "True/True",
                  $"{loaded.GetBundle(PacketStore.Hash("05B:")) == null}/{loaded.Get(bundle) == null}");
            // an old-format line (no '@') whose text looks like hashes by ',' is a packet, never a bundle
            string fake = string.Join(",", PacketStore.Hash("05B:"), PacketStore.Hash("063:02"));
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, PacketStore.FileName), $"{PacketStore.Hash(fake)} {fake}\n");
            loaded = PacketStore.Load(dir);
            Check("  a line not marked '@' is a packet, never a bundle", $"True/{fake}",
                  $"{loaded.GetBundle(PacketStore.Hash(fake)) == null}/{loaded.Get(PacketStore.Hash(fake))}");
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, PacketStore.FileName), $"{(bundle[0] == '0' ? '1' : '0')}{bundle.Substring(1)} @{joined}\n");
            try
            {
                PacketStore.Load(dir);
                Fail("  a bundle line that does not hash to its key", "loaded");
            }
            catch (FormatException e)
            {
                Check("  a bundle line that does not hash to its key", true, e.Message.Contains("does not hash to its key"));
            }
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
        try
        {
            Trace.Parse(text.Replace("(4)", "(5)"), "b.trace", store);
            Fail("  a bundle whose count is wrong", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a bundle whose count is wrong", $"b.trace:6: bundle {bundle} holds 4 packets, not 5", e.Message);
        }
    }

    // rerecord --explain: the differences grouped by packet opcode and byte, by field, packets added / missing
    private static void ExplainTest()
    {
        Console.WriteLine("rerecord --explain");
        Trace Make(params TraceStep[] steps)
        {
            var tr = new Trace { Name = "e", Seed = 7, FlowSha = "ab", Recorder = "lua" };
            tr.World.Add(("server_variables", "0123456789abcdef"));
            tr.Steps.AddRange(steps);
            return tr;
        }
        TraceStep Step(string label, string ret, string[] packets, params (string Table, string Row)[] rows)
        {
            var st = new TraceStep { Label = label, Ret = ret };
            st.Packets.AddRange(packets);
            foreach (var (table, row) in rows) st.Set(table).Add(row);
            return st;
        }
        var want = Make(
            Step("login", "", new[] { "00A:0011", "063:AA00", "063:AB00", "05B:" }, ("chars", "x=1")),
            Step("trigger Michea", "onTrigger=number:1", new[] { "032:0000", "063:AA00" }, ("chars", "x=1"), ("char_points", "a=1")),
            Step("finish", "onEventFinish=", new[] { "052:00" }, ("chars", "x=1")));
        var got = Make(
            Step("login", "", new[] { "00A:0011", "063:AA01", "063:AB01", "05B:" }, ("chars", "x=1")),
            Step("trigger Michea", "onTrigger=number:2", new[] { "032:0000", "063:AA01", "05B:" }, ("chars", "x=2"), ("char_points", "a=2")),
            Step("finish", "onEventFinish=", new[] { "052:00" }, ("chars", "x=2")));
        got.World[0] = ("server_variables", "fedcba9876543210");
        want.Steps[1].Orders.Add(("235/Michea/onTrigger", "Quest[1][13]"));
        got.Steps[1].Orders.Add(("235/Michea/onTrigger", "default"));
        Check("  grouped", "world server_variables: recorded 0123456789abcdef, now fedcba9876543210|" +
                           "0x063 +1: 3 packets, first at step 1 [login]|" +
                           "char_points: 1 steps, first at step 2 [trigger Michea]|" +
                           "chars: 2 steps, first at step 2 [trigger Michea]|" +
                           "ret: 1 steps, first at step 2 [trigger Michea]|" +
                           "order 235/Michea/onTrigger: 1 steps, first at step 2 [trigger Michea]|" +
                           "0x05B added: 1",
              string.Join("|", Trace.Explain(want, got)));
        Check("  ... missing the other way round", true, Trace.Explain(got, want).Contains("0x05B missing: 1"));
        Check("  equal traces: nothing", "", string.Join("|", Trace.Explain(want, want)));
        var rec = Make(want.Steps.ToArray());
        rec.Recorder = "csharp-rerecord";
        Check("  ... the recorder line alone: nothing", "", string.Join("|", Trace.Explain(want, rec)));
        rec.LuaAt = "0fad61802ae0f41a1c67076a91618d5070685aae";
        rec.Masks.Add(new Mask(0x00D, 4, 4, "an example"));
        Check("  ... another header line: named", "header lua-at: recorded (none), now 0fad61802ae0f41a1c67076a91618d5070685aae|" +
                                                  "header mask: recorded (none), now mask 0x00D 4 4 an example",
              string.Join("|", Trace.Explain(want, rec)));
    }

    // a single-row table's blob column of the same length as before: written as its changed byte runs
    private static void BlobRunsTest()
    {
        Console.WriteLine("blob runs");
        var t = new Trace { Name = "r", Seed = 7, FlowSha = "ab", Recorder = "lua" };
        t.BlobColumns.Add("chars.keyitems");
        t.BlobColumns.Add("chars.quests");
        var s1 = new TraceStep { Label = "trigger Michea" };
        foreach (var e in new[] { "keyitems=00000000", "quests=null", "playtime=5", "eminence=0000" }) s1.Set("chars").Add(e);
        s1.Set("char_inventory").Add("location=0|extra=0000");
        t.Steps.Add(s1);
        var s2 = s1.Carry("finish");
        s2.Sets["chars"] = new SortedSet<string>(new[] { "keyitems=00010002", "quests=0000", "playtime=6", "eminence=0100" }, StringComparer.Ordinal);
        s2.Sets["char_inventory"] = new SortedSet<string>(new[] { "location=0|extra=0001" }, StringComparer.Ordinal);
        t.Steps.Add(s2);
        var s3 = s2.Carry("trigger Michea");
        s3.Sets["chars"] = new SortedSet<string>(new[] { "keyitems=0001000200", "quests=0100", "playtime=6", "eminence=0100" }, StringComparer.Ordinal);
        t.Steps.Add(s3);
        var store = new PacketStore();
        string text = t.Write(store);
        var lines = text.Split('\n');
        Check("  the header names the blob columns", "blobs chars.keyitems chars.quests", lines.FirstOrDefault(l => l.StartsWith("blobs ")));
        List<string> StepLines(string after, int skip) =>
            lines.SkipWhile(l => l != after).Skip(1).Skip(skip).TakeWhile(l => !l.StartsWith("step ")).Where(l => l.StartsWith("char")).ToList();
        Check("  same length: the changed byte runs; NULL to bytes, another column, a multi-row blob or an unlisted column: - / +",
              "char_inventory - location=0|extra=0000|char_inventory + location=0|extra=0001|" +
              "chars - eminence=0000|chars - playtime=5|chars - quests=null|chars + eminence=0100|chars + playtime=6|chars + quests=0000|" +
              "chars keyitems +1:01 +3:02",
              string.Join("|", StepLines("step finish", 0)));
        Check("  another length: the whole value", "chars - keyitems=00010002|chars + keyitems=0001000200|chars quests +0:01",
              string.Join("|", lines.SkipWhile(l => l != "step finish").Skip(1).SkipWhile(l => l != "step trigger Michea").Skip(1).Where(l => l.StartsWith("char"))));
        var back = Trace.Parse(text, "r.trace", store);
        Check("  read back: the state rebuilt", "eminence=0100,keyitems=0001000200,playtime=6,quests=0100", string.Join(",", back.Steps[2].Rows("chars")));
        Check("  ... compares equal, written again the same", "0/True", $"{Trace.Compare(t, back).Count}/{back.Write(store) == text}");
        try
        {
            Trace.Parse(text.Replace("chars keyitems +1:01", "chars nothere +1:01"), "r.trace", store);
            Fail("  runs of a column the table lacks", "parsed");
        }
        catch (FormatException e)
        {
            Check("  runs of a column the table lacks", true, e.Message.EndsWith(": chars has no nothere to change"));
        }
    }

    // a first step's table: one content-addressed store entry, shared by every trace with the same rows
    private static void TableEntryTest()
    {
        Console.WriteLine("first-step table entries");
        var store = new PacketStore();
        Trace Make(string name, string cp)
        {
            var t = new Trace { Name = name, Seed = 7, FlowSha = "ab", Recorder = "lua" };
            var s1 = new TraceStep { Label = "trigger Michea" };
            s1.Set("char_points").Add("sandoria_cp=0");
            s1.Set("char_points").Add("bastok_cp=" + cp);
            s1.Set("char_vars").Add(@"varname=a\tb|value=1");
            t.Steps.Add(s1);
            return t;
        }
        var a = Make("a", "0");
        var b = Make("b", "0");
        var c = Make("c", "1");
        string ta = a.Write(store), tb = b.Write(store), tc = c.Write(store);
        string joined = "bastok_cp=0\tsandoria_cp=0";
        string h = PacketStore.Hash(joined);
        Check("  the first step: <table> @<hash> (<rows>)", $"char_points @{h} (2)", ta.Split('\n').First(l => l.StartsWith("char_points")));
        Check("  ... the same rows in another trace: the same entry", true, tb.Contains($"char_points @{h} (2)"));
        Check("  ... other rows: another entry", false, tc.Contains($"char_points @{h} (2)"));
        Check("  the entry: the rows by tab", joined.Replace('\t', ','), string.Join(",", store.GetTable(h) ?? Array.Empty<string>()));
        Check("  read back: the rows", "bastok_cp=0,sandoria_cp=0/varname=a\\tb|value=1",
              $"{string.Join(",", Trace.Parse(ta, "a.trace", store).Steps[0].Rows("char_points"))}/{string.Join(",", Trace.Parse(ta, "a.trace", store).Steps[0].Rows("char_vars"))}");

        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"questgolden-tables-{Environment.ProcessId}");
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "a.trace"), ta);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "b.trace"), tb);
            var missing = store.Save(dir);
            var saved = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, PacketStore.FileName)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Check("  saved: an entry a line of its own, marked '#'; c's entry stale, gone", $"{h} #{joined}/2/0",
                  $"{saved.FirstOrDefault(l => l.StartsWith(h + " ")) ?? "(none)"}/{saved.Length}/{missing.Count}");
            var loaded = PacketStore.Load(dir);
            Check("  loaded back: the round trip", true, Trace.Compare(a, Trace.Parse(ta, "a.trace", loaded)).Count == 0);
            Check("  ... a table entry is no packet and no bundle", "True/True", $"{loaded.Get(h) == null}/{loaded.GetBundle(h) == null}");
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, PacketStore.FileName), $"{(h[0] == '0' ? '1' : '0')}{h.Substring(1)} #{joined}\n");
            try
            {
                PacketStore.Load(dir);
                Fail("  a table entry that does not hash to its key", "loaded");
            }
            catch (FormatException e)
            {
                Check("  a table entry that does not hash to its key", true, e.Message.Contains("does not hash to its key"));
            }
        }
        finally
        {
            System.IO.Directory.Delete(dir, true);
        }
        try
        {
            Trace.Parse(ta.Replace($"@{h} (2)", $"@{h} (3)"), "a.trace", store);
            Fail("  a table entry whose count is wrong", "parsed");
        }
        catch (FormatException e)
        {
            Check("  a table entry whose count is wrong", $"a.trace:7: char_points entry {h} holds 2 rows, not 3", e.Message);
        }
    }

    // the trace viewer (show) on a checked-in trace: every step finds its flow line, the event and message packets are
    // decoded, the char's id shows as $char, and --all adds the login bundle's packets and the first step's rows
    private static void SelfTestShow()
    {
        Console.WriteLine("the trace viewer (show)");
        const string key = "bastok.minesweeper";
        string trace = File.ReadAllText(Path.Combine(GoldenDir, key + ".trace"), Encoding.UTF8);
        string flow = File.ReadAllText(Path.Combine(FlowsDir, key + ".flow"), Encoding.UTF8);
        var store = PacketStore.Load(GoldenDir);
        string text = ShowText(trace, flow, store, false);
        var steps = text.Split((char)10).Where(l => l.StartsWith("step ")).ToList();
        Check($"show {key}: one block a step", Regex.Matches(trace, "^step ", RegexOptions.Multiline).Count, steps.Count);
        Check("  each step names its flow line", "", string.Join(" | ", steps.Where(l => l.Contains("flow:?"))));
        // the offer's line found in the flow file (its number and text as the file has them now)
        var flowLines = flow.Split((char)10);
        int offer = Array.FindIndex(flowLines, l => l.Contains("# the offer, 108", StringComparison.Ordinal)) + 1;
        string offerLine = offer > 0 ? $"  flow:{offer}  {flowLines[offer - 1].TrimEnd()}" : "(no offer line)";
        Check("  the offer's step: its flow line with the comment, the event decoded", "True",
              (offer > 0 && steps.Any(l => l.EndsWith(offerLine, StringComparison.Ordinal)) &&
               text.Contains("032 [event 108, entity ")).ToString());
        Check("  the completion's gil message: Bastok Mines' GIL_OBTAINED (6396) to $char with 150", "True",
              text.Contains("02A [message 6396, entity $char params [150]]").ToString());
        Check("  the DB changes and returns as written", "True",
              (text.Contains("  char_profile + fame_bastok=10") && text.Contains("  ret onTrade=number:0") &&
               text.Contains("  order 234/Gerbaum/onTrade=Quest[1][39]")).ToString());
        string all = ShowText(trace, flow, store, true);
        Check("  --all: the login bundle's packets and the first step's rows too", "True",
              (all.Length > text.Length && all.Contains("    quest_key=")).ToString());
        Check("  DecodePacket: an event 0x034 with its params", "[event 42, entity 7, zone 235 params [1,2]]",
              DecodePacket("034:" + Convert.ToHexString(EventNum34(7, 235, 42, 1, 2))));
    }

    // a 0x034 as Events.EventNum lays it out
    private static byte[] EventNum34(uint entity, ushort zone, ushort ev, params int[] nums)
    {
        var b = new byte[4 + XI.Map.S2cEventNum.SizeOf];
        BitConverter.GetBytes(entity).CopyTo(b, 4);
        for (int i = 0; i < nums.Length; i++) BitConverter.GetBytes(nums[i]).CopyTo(b, 4 + XI.Map.S2cEventNum.OffNum + i * 4);
        BitConverter.GetBytes(zone).CopyTo(b, 4 + XI.Map.S2cEventNum.OffEventNum);
        BitConverter.GetBytes(ev).CopyTo(b, 4 + XI.Map.S2cEventNum.OffEventPara);
        return b;
    }
}

// a database that answers nothing and keeps each query with its values (as text): the world tables' self-test
internal sealed class LoggingDatabase : IDatabase
{
    public readonly List<(string Query, List<string> Values)> Queries = new();

    public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters)
    {
        Queries.Add((query, parameters.Select(p => Convert.ToString(p.Value, CultureInfo.InvariantCulture)).ToList()));
        return null;
    }

    public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters) => Execute(query, parameters);
    public string Schema => "golden_selftest";
    public string Version => "0";
    public string DriverVersion => "0";
    public bool Transaction(Fn transactionFn)
    {
        transactionFn();
        return true;
    }
}
