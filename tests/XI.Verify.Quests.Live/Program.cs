using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.Quests.Live -- live gate for quest chains driven end to end as a client drives them: the 0x00A login (the
// zone-in cutscene ended with 0x05B), 0x015 moves up to the NPCs, 0x01A Talk to them (InteractionGlobal.onTrigger ->
// the quest containers' sections -> startEvent: 0x032 / 0x034, or a DefaultActions event), 0x05B event ends
// (onEventFinish) and updates (onEventUpdate -> updateEvent's 0x05C), 0x036 trades (InteractionGlobal.onTrade with the
// CLuaTradeContainer), 0x050 gear changes, 0x064 key items seen, 0x05E zone lines (the quests' mustZone, the way to
// other zones), and every step's effect: the quest log bits (chars.quests and the 0x056 quest pages), char_vars, items,
// key items (chars.keyitems and 0x055), gil, fame, titles and the NPC messages (0x02A). The real scripts in
// scripts/quests on the real MariaDB (xidb), chars 995871-995873.
//
// Chain 1, Port Bastok: The Quadav's Curse (Corann: the offer, the Quadav Backplate traded, fame and a Bronze Subligar)
// then Out of One's Shell, which opens on it and Bastok fame level 2 (Ronan: the offer, Mine Konte's hint, three
// Shell Bugs traded, the mustZone wait, a zone out to Bastok Markets and back, the Monk's Headgear, fame and the Shell
// Outer title).
//
// Chain 2, Bastok Markets and Bastok Mines (and Konschtat Highlands): Mom, the Adventurer? (Nbu Latteh: the offer and
// a Fire Crystal, Parnika; a zone over to Bastok Mines, a Copper Ring traded to Roh Latteh for his letter (a key
// item); the 0x064 key item seen; back to Nbu Latteh: the letter taken, 100 gil, fame and the Ringbearer title) then
// The Signpost Marks the Spot, which opens on it once the char has zoned and has Bastok fame level 2 (Nbu Latteh's
// offer declined then taken with the 0x05B option; the zone lines through Port Bastok and North Gustaberg to the
// signpost in Konschtat Highlands for the Painting of a Windmill; back to Roh Latteh in Bastok Mines: a Linen Robe,
// fame and the Treasure Scavenger title).
//
// Chain 3, Bastok Markets: Brygid the Stylist (Brygid: the offer, a Robe and a Bronze Subligar worn through 0x050,
// Gloves, fame and the Brygid-Approved title) then Brygid the Stylist Returns, which opens on it with artifact armor
// worn (the requested body and legs worn, the reward picked through a 0x05B event update answered by updateEvent's
// 0x05C, the zodiac subligar traded for it, fame and the Bastok's Second Best Dressed title; then the repeat offer).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static partial class Program
{
    private static int _pass, _fail;

    private static uint CharId = 995871;
    private static readonly uint[] CharIds = { 995871, 995872, 995873, 996011, 996012, 996013, 996014, 996015, 996016, 996017, 996018, 996019 };
    private const ushort PortBastok = 236, BastokMarkets = 235, BastokMines = 234, NorthGustaberg = 106, Konschtat = 108;
    private const uint Corann = 17743921, Ronan = 17743920, MineKonte = 17743926;
    private const ushort QuadavBackplate = 596, ShellBug = 17397, BronzeSubligar = 12832, MonksHeadgear = 12501;
    private const int LogBastok = 1, QuadavsCurse = 4, OutOfOnesShell = 5;
    private const int TitleShellOuter = 82, TitleNewAdventurer = 206, TitleRingbearer = 92, TitleTreasureScavenger = 96;
    private const uint NbuLatteh = 17739826, Parnika = 17739840, RohLatteh = 17735700, Signpost3 = 17220136;
    private const ushort CopperRing = 13454, FireCrystal = 4096, LinenRobe = 12601;
    private const int MomTheAdventurer = 21, TheSignpostMarksTheSpot = 22;
    private const uint Brygid = 17739813;
    private const ushort Robe = 12600, TempleCrown = 12512, AriesSubligar = 15375, Gloves = 12720, DuendeCotehardie = 14401;
    private const int BrygidTheStylist = 44, BrygidTheStylistReturns = 74, TitleBrygidApproved = 103, TitleSecondBestDressed = 371;
    private const byte SlotHead = 4, SlotBody = 5, SlotLegs = 7;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    private static string DbRow(IDatabase db, string cols, string from)
    {
        var r = Sql(db, $"SELECT {cols} {from}", BoundValue.Of(CharId));
        if (r == null || !r.Next()) return "-";
        var parts = new List<string>();
        for (uint i = 0; i < r.ColumnCount(); i++) parts.Add(r.Get<string>(i) ?? "null");
        return string.Join(" ", parts);
    }

    private static byte[] DbBlob(IDatabase db, string col)
    {
        var r = Sql(db, $"SELECT {col} FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        return r != null && r.Next() ? r.GetBlobBytes(col) ?? Array.Empty<byte>() : Array.Empty<byte>();
    }

    // every char table the logins, the scripts and the zone changes write
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
            scripts?.Dispose();
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static int Opcode(BasicPacket p) => p.Buffer[0] | (p.Buffer[1] & 1) << 8;

    // a Hume Bastokan WAR 10 past its first login (NEW_ADVENTURER) at x / y / z in the zone, with the Bastok fame, 1000
    // gil and the items from inventory slot 1 on
    private static void Fixture(IDatabase db, ushort zoneId, float x, float y, float z, int fame, params (ushort Item, uint Qty)[] items)
    {
        var titles = new byte[143];
        titles[TitleNewAdventurer >> 3] |= 1 << (TitleNewAdventurer & 7);
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of($"questslive{CharId % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of($"Questlive{CharId % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of((byte)1), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 300, 50, 1, 10)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 10)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_profile(charid, fame_bastok) VALUES (?, ?)", BoundValue.Of(CharId), BoundValue.Of(fame));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,1000)", BoundValue.Of(CharId));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(CharId), BoundValue.Of((byte)(i + 1)), BoundValue.Of(items[i].Item), BoundValue.Of(items[i].Qty));
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        // chain 1: by Corann in Port Bastok with 30 Bastok fame, a Quadav Backplate (slot 1) and three Shell Bugs (slot 2)
        Fixture(db, PortBastok, 90.0f, -8.772f, 29.0f, 30, (QuadavBackplate, 1), (ShellBug, 3));

        Setup(db, dataDir, grades, stats);
        if (Environment.GetEnvironmentVariable("QUESTS_ONLY") is { Length: > 0 } only)
        {
            Quests2(only);
            return;
        }

        Console.WriteLine("the login in Port Bastok");
        Login();
        Check("in Port Bastok, by Corann, out of any event", $"{PortBastok}/none", $"{c.Zone}/{Cur()}");
        Check("no quest yet, 30 Bastok fame (level 1)", "0,0,30,1",
              Lua($"report(player:getQuestStatus({LogBastok}, {QuadavsCurse})) report(player:getQuestStatus({LogBastok}, {OutOfOnesShell})) " +
                  "report(player:getFame(xi.fameArea.BASTOK)) report(player:getFameLevel(xi.fameArea.BASTOK))"));
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Out of One's Shell is not offered before The Quadav's Curse");
        Near(Ronan);
        Talk(Ronan);
        Check("Ronan: his default action (DefaultActions.lua), event 37", $"032:{Ronan}:37/37", $"{Ev()}/{Cur()}");
        EventEnd(Ronan, 37);

        Console.WriteLine("The Quadav's Curse: Corann's offer");
        Near(Corann);
        Talk(Corann);
        Check("Corann: event 80 (0x032, no params)", $"032:{Corann}:80/80", $"{Ev()}/{Cur()}");
        Check("  a normal event: the char not locked (setLocked only for a CUTSCENE)", $"{EventType.Normal}/False", $"{c.CurrentEvent.Type}/{c.Locked}");
        EventEnd(Corann, 80);
        Check("event 80 finished: the quest accepted", "1/none", $"{Status(LogBastok, QuadavsCurse)}/{Cur()}");
        Check("  chars.quests: the current bit", "10", QuestBits(LogBastok, QuadavsCurse));
        Check("  0x056: the Bastok offer page (port 0x58) with the quest's bit", "1/True", QuestPage(0x58, QuadavsCurse));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("The Quadav's Curse: the Quadav Backplate traded");
        Trade(Corann, (1, 1));
        Check("onTrade: event 81", $"032:{Corann}:81/81", $"{Ev()}/{Cur()}");
        Check("  the backplate still in the inventory until the event ends", "1", Qty(QuadavBackplate));
        EventEnd(Corann, 81);
        Check("event 81 finished: the quest completed", "2/01", $"{Status(LogBastok, QuadavsCurse)}/{QuestBits(LogBastok, QuadavsCurse)}");
        Check("  0x056: the offer page without it, the complete page (port 0x98) with it", "1/False/1/True",
              $"{QuestPage(0x58, QuadavsCurse)}/{QuestPage(0x98, QuadavsCurse)}");
        Check("  tradeComplete: the backplate gone", "0", Qty(QuadavBackplate));
        Check("  the reward: a Bronze Subligar", "1", Qty(BronzeSubligar));
        Check("  the reward: 20 Bastok fame, 50 (level 2)", "50/2", $"{Fame()}/{Lua("report(player:getFameLevel(xi.fameArea.BASTOK))")}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        Talk(Corann);
        Check("Corann after the quest: event 87 (replaceDefault)", $"032:{Corann}:87", Ev());
        EventEnd(Corann, 87);

        Console.WriteLine("Out of One's Shell: Ronan's offer");
        Near(Ronan);
        Talk(Ronan);
        Check("Ronan: event 82", $"032:{Ronan}:82/82", $"{Ev()}/{Cur()}");
        EventEnd(Ronan, 82);
        Check("event 82 finished: Out of One's Shell accepted", "1/10", $"{Status(LogBastok, OutOfOnesShell)}/{QuestBits(LogBastok, OutOfOnesShell)}");
        Talk(Ronan);
        Check("Ronan before the trade: his dialog text, no event", "none/none", $"{Ev()}/{Cur()}");
        var said = Of(0x02A);
        Check("  showText: RONAN_DIALOG_1 in one 0x02A from Ronan", $"{Text(PortBastok, "RONAN_DIALOG_1")}:0/{Ronan}",
              $"{Special()}/{(said.Count == 1 ? BitConverter.ToUInt32(said[0].Buffer, 4) : 0)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Out of One's Shell: Mine Konte's hint");
        Near(MineKonte);
        Talk(MineKonte);
        Check("Mine Konte: event 83", $"032:{MineKonte}:83/83", $"{Ev()}/{Cur()}");
        EventEnd(MineKonte, 83);

        Console.WriteLine("Out of One's Shell: three Shell Bugs traded");
        Near(Ronan);
        Trade(Ronan, (2, 2));
        Check("two Shell Bugs: no event", "none/none", $"{Ev()}/{Cur()}");
        Trade(Ronan, (2, 3));
        Check("three: event 84", $"032:{Ronan}:84/84", $"{Ev()}/{Cur()}");
        EventEnd(Ronan, 84);
        Check("event 84 finished: the bugs gone, Prog 1, mustZone set to Port Bastok", $"0/1/{PortBastok}",
              $"{Qty(ShellBug)}/{Var("Quest[1][5]Prog")}/{Var("Quest[1][5]mustZone")}");
        Check("  still accepted", "1", Status(LogBastok, OutOfOnesShell));
        Talk(Ronan);
        Check("Ronan before zoning: event 85", $"032:{Ronan}:85", Ev());
        EventEnd(Ronan, 85);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("a zone out to Bastok Markets and back");
        TakeZoneLine("z6k2");
        Check("in Bastok Markets: onGameIn cleared the mustZone of another zone", $"{BastokMarkets}/-", $"{c.Zone}/{Var("Quest[1][5]mustZone")}");
        var back = zone.ZoneInfoOf(BastokMarkets).ZoneLines.Values.First(l => l.DestinationZone == PortBastok);
        TakeZoneLine(ZoneLine.IdText(back.Id));
        Check("back in Port Bastok", PortBastok, c.Zone);

        Console.WriteLine("Out of One's Shell: the reward");
        Near(Ronan);
        Talk(Ronan);
        Check("Ronan: event 86", $"032:{Ronan}:86/86", $"{Ev()}/{Cur()}");
        EventEnd(Ronan, 86);
        Check("event 86 finished: the quest completed", "2/01", $"{Status(LogBastok, OutOfOnesShell)}/{QuestBits(LogBastok, OutOfOnesShell)}");
        Check("  the reward: a Monk's Headgear", "1", Qty(MonksHeadgear));
        Check("  the reward: 20 more Bastok fame", "70", Fame());
        Check("  the reward: the Shell Outer title, in chars.titles", $"{TitleShellOuter}/True", $"{Lua("report(player:getTitle())")}/{DbTitle(TitleShellOuter)}");
        Check("  the quest's vars cleared", "-/-", $"{Var("Quest[1][5]Prog")}/{Var("Quest[1][5]mustZone")}");
        Talk(Ronan);
        Check("Ronan after the quest: event 89", $"032:{Ronan}:89", Ev());
        EventEnd(Ronan, 89);
        Near(Corann);
        Talk(Corann);
        Check("Corann after both: event 88", $"032:{Corann}:88", Ev());
        EventEnd(Corann, 88);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- chain 2 ----
        // by Nbu Latteh in Bastok Markets with 40 Bastok fame and a Copper Ring (slot 1)
        Console.WriteLine("chain 2: the login in Bastok Markets");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[1];
        Fixture(db, BastokMarkets, -113.0f, -4.0f, -113.0f, 40, (CopperRing, 1));
        Login();
        Check("in Bastok Markets, out of any event", $"{BastokMarkets}/none", $"{c.Zone}/{Cur()}");
        int letter = int.Parse(Lua("report(xi.keyItem.LETTER_FROM_ROH_LATTEH)"));
        int painting = int.Parse(Lua("report(xi.keyItem.PAINTING_OF_A_WINDMILL)"));
        Check("no script errors on the login", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Mom, the Adventurer?: Nbu Latteh's offer");
        Talk(NbuLatteh);
        Check("Nbu Latteh: event 230", $"032:{NbuLatteh}:230/230", $"{Ev()}/{Cur()}");
        EventEnd(NbuLatteh, 230);
        Check("event 230 finished: the quest accepted", "1/10", $"{Status(LogBastok, MomTheAdventurer)}/{QuestBits(LogBastok, MomTheAdventurer)}");
        Check("  npcUtil.giveItem: a Fire Crystal, the ITEM_OBTAINED message", $"1/{Text(BastokMarkets, "ITEM_OBTAINED")}:{FireCrystal}",
              $"{Qty(FireCrystal)}/{Special()}");
        Talk(NbuLatteh);
        Check("Nbu Latteh without the letter: event 231", $"032:{NbuLatteh}:231", Ev());
        EventEnd(NbuLatteh, 231);
        Near(Parnika);
        Talk(Parnika);
        Check("Parnika: event 232", $"032:{Parnika}:232", Ev());
        EventEnd(Parnika, 232);
        Check("still accepted", "1", Status(LogBastok, MomTheAdventurer));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Mom, the Adventurer?: the Copper Ring to Roh Latteh in Bastok Mines");
        TakeZoneLine("z6j0");
        Check("in Bastok Mines", BastokMines, c.Zone);
        Near(RohLatteh);
        Talk(RohLatteh);
        Check("Roh Latteh talked to: his default action, event 29", $"032:{RohLatteh}:29", Ev());
        EventEnd(RohLatteh, 29);
        Trade(RohLatteh, (2, 1));
        Check("the Fire Crystal traded: no event", "none/none", $"{Ev()}/{Cur()}");
        Trade(RohLatteh, (1, 1));
        Check("the Copper Ring traded: event 95", $"032:{RohLatteh}:95/95", $"{Ev()}/{Cur()}");
        EventEnd(RohLatteh, 95);
        Check("event 95 finished: the ring gone", "0", Qty(CopperRing));
        Check("  npcUtil.giveKeyItem: the letter, unseen, in chars.keyitems", "true,false/10",
              $"{Lua("report(tostring(player:hasKeyItem(xi.keyItem.LETTER_FROM_ROH_LATTEH))) report(tostring(player:seenKeyItem(xi.keyItem.LETTER_FROM_ROH_LATTEH)))")}/{DbKeyItem(letter)}");
        Check("  the KEYITEM_OBTAINED message, the 0x055 key item log", $"{Text(BastokMines, "KEYITEM_OBTAINED")}:{letter}/True",
              $"{Special()}/{Of(0x055).Count > 0}");
        var kiPage = Of(0x055).LastOrDefault();
        Check("  0x055: the letter's table, owned and not seen", $"{letter / 512}/True/False",
              kiPage == null ? "none" : $"{BitConverter.ToUInt16(kiPage.Buffer, 132)}/{(kiPage.Buffer[4 + letter % 512 / 8] & 1 << letter % 8) != 0}/" +
                                        $"{(kiPage.Buffer[68 + letter % 512 / 8] & 1 << letter % 8) != 0}");
        Trade(RohLatteh, (2, 1));
        Check("a second trade with the letter held: no event", "none", Ev());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("the letter looked at in the key item menu (GP_CLI_COMMAND_SCENARIOITEM)");
        Send(0x064, 76, b =>
        {
            BitConverter.GetBytes(CharId).CopyTo(b, 4);
            BitConverter.GetBytes(1u << letter % 32).CopyTo(b, 8 + letter % 512 / 32 * 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, 72);
            BitConverter.GetBytes((ushort)(letter / 512)).CopyTo(b, 74);
        });
        Check("0x064: the letter seen, saved", "true/11", $"{Lua("report(tostring(player:seenKeyItem(xi.keyItem.LETTER_FROM_ROH_LATTEH)))")}/{DbKeyItem(letter)}");

        Console.WriteLine("Mom, the Adventurer?: the letter to Nbu Latteh");
        TakeZoneLine("z6i2");
        Check("back in Bastok Markets", BastokMarkets, c.Zone);
        Near(NbuLatteh);
        Talk(NbuLatteh);
        Check("Nbu Latteh with the letter seen: event 234", $"032:{NbuLatteh}:234/234", $"{Ev()}/{Cur()}");
        EventEnd(NbuLatteh, 234);
        Check("event 234 finished: the quest completed", "2/01", $"{Status(LogBastok, MomTheAdventurer)}/{QuestBits(LogBastok, MomTheAdventurer)}");
        // charutils::delKeyItem clears only the keyList bit: the seen bit stays
        Check("  the letter taken (its seen bit kept, as charutils::delKeyItem)", "false/01",
              $"{Lua("report(tostring(player:hasKeyItem(xi.keyItem.LETTER_FROM_ROH_LATTEH)))")}/{DbKeyItem(letter)}");
        Check("  npcUtil.giveCurrency: 100 gil, the GIL_OBTAINED message", $"1100/{Text(BastokMarkets, "GIL_OBTAINED")}:100", $"{Gil()}/{Special()}");
        Check("  the reward: 10 Bastok fame, 50 (level 2)", "50/2", $"{Fame()}/{Lua("report(player:getFameLevel(xi.fameArea.BASTOK))")}");
        Check("  the reward: the Ringbearer title, in chars.titles", $"{TitleRingbearer}/True", $"{Lua("report(player:getTitle())")}/{DbTitle(TitleRingbearer)}");
        Check("  mustZone set to Bastok Markets", $"{BastokMarkets}", Var("Quest[1][21]mustZone"));
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("The Signpost Marks the Spot: not before a zone change");
        Talk(NbuLatteh);
        Check("Nbu Latteh before zoning: his default action, event 127", $"032:{NbuLatteh}:127", Ev());
        EventEnd(NbuLatteh, 127);
        TakeZoneLine("z6j0");
        Check("in Bastok Mines: the mustZone cleared", $"{BastokMines}/-", $"{c.Zone}/{Var("Quest[1][21]mustZone")}");
        TakeZoneLine("z6i2");

        Console.WriteLine("The Signpost Marks the Spot: Nbu Latteh's offer");
        Near(NbuLatteh);
        Talk(NbuLatteh);
        Check("Nbu Latteh: event 235", $"032:{NbuLatteh}:235/235", $"{Ev()}/{Cur()}");
        EventEnd(NbuLatteh, 235, 1);
        Check("event 235 declined (option 1): not accepted", "0/00", $"{Status(LogBastok, TheSignpostMarksTheSpot)}/{QuestBits(LogBastok, TheSignpostMarksTheSpot)}");
        Talk(NbuLatteh);
        Check("Nbu Latteh again: event 235", $"032:{NbuLatteh}:235", Ev());
        EventEnd(NbuLatteh, 235, 0);
        Check("event 235 taken (option 0): accepted", "1/10", $"{Status(LogBastok, TheSignpostMarksTheSpot)}/{QuestBits(LogBastok, TheSignpostMarksTheSpot)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("The Signpost Marks the Spot: to Konschtat Highlands");
        TakeZoneLine("z6j2");
        Check("in Port Bastok", PortBastok, c.Zone);
        TakeZoneLine("z6k0");
        Check("in North Gustaberg", NorthGustaberg, c.Zone);
        TakeZoneLine("z2y6");
        Check("in Konschtat Highlands", Konschtat, c.Zone);
        Near(Signpost3);
        Talk(Signpost3);
        Check("the signpost: no event, SIGNPOST3_DIALOG_2 then the painting's KEYITEM_OBTAINED",
              $"none/{Text(Konschtat, "SIGNPOST3_DIALOG_2")}:0,{Text(Konschtat, "KEYITEM_OBTAINED")}:{painting}", $"{Ev()}/{Special()}");
        Check("  the Painting of a Windmill held, in chars.keyitems", "true/10",
              $"{Lua("report(tostring(player:hasKeyItem(xi.keyItem.PAINTING_OF_A_WINDMILL)))")}/{DbKeyItem(painting)}");
        Talk(Signpost3);
        Check("the signpost again: its directions only", $"{Text(Konschtat, "SIGNPOST3_DIALOG_1")}:0", Special());
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("The Signpost Marks the Spot: the painting to Roh Latteh");
        TakeZoneLine("z300");
        TakeZoneLine("z2y0");
        TakeZoneLine("z6k2");
        TakeZoneLine("z6j0");
        Check("in Bastok Mines", BastokMines, c.Zone);
        Near(RohLatteh);
        Talk(RohLatteh);
        Check("Roh Latteh: event 96", $"032:{RohLatteh}:96/96", $"{Ev()}/{Cur()}");
        EventEnd(RohLatteh, 96);
        Check("event 96 finished: the quest completed", "2/01", $"{Status(LogBastok, TheSignpostMarksTheSpot)}/{QuestBits(LogBastok, TheSignpostMarksTheSpot)}");
        Check("  the painting taken", "00", DbKeyItem(painting));
        Check("  the reward: a Linen Robe", "1", Qty(LinenRobe));
        Check("  the reward: 20 more Bastok fame", "70", Fame());
        Check("  the reward: the Treasure Scavenger title, in chars.titles", $"{TitleTreasureScavenger}/True",
              $"{Lua("report(player:getTitle())")}/{DbTitle(TitleTreasureScavenger)}");
        Talk(RohLatteh);
        Check("Roh Latteh after the quest: his default action", $"032:{RohLatteh}:29", Ev());
        EventEnd(RohLatteh, 29);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        // ---- chain 3 ----
        // a MNK 75 by Brygid in Bastok Markets with a Robe (slot 1), a Bronze Subligar (2), a Temple Crown (3) and an
        // Aries Subligar (4)
        Console.WriteLine("chain 3: the login in Bastok Markets");
        zone.OnCharTimeout(s, false);
        s = null;
        CharId = CharIds[2];
        Fixture(db, BastokMarkets, -91.5f, -4.302f, -108.5f, 0, (Robe, 1), (BronzeSubligar, 1), (TempleCrown, 1), (AriesSubligar, 1));
        Sql(db, "UPDATE char_stats SET mjob = 2, mlvl = 75 WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "UPDATE char_jobs SET mnk = 75 WHERE charid = ?", BoundValue.Of(CharId));
        Login();
        Check("a MNK 75 in Bastok Markets, out of any event", $"{BastokMarkets}/2/75/none", $"{c.Zone}/{c.MJob}/{c.MLvl}/{Cur()}");

        Console.WriteLine("Brygid the Stylist: the offer");
        Talk(Brygid);
        Check("Brygid: event 310", $"032:{Brygid}:310/310", $"{Ev()}/{Cur()}");
        EventEnd(Brygid, 310);
        Check("event 310 finished: the quest accepted", "1/10", $"{Status(LogBastok, BrygidTheStylist)}/{QuestBits(LogBastok, BrygidTheStylist)}");
        Talk(Brygid);
        Check("Brygid, not dressed for her: event 310 again", $"032:{Brygid}:310", Ev());
        EventEnd(Brygid, 310);
        Check("  no change", "1", Status(LogBastok, BrygidTheStylist));

        Console.WriteLine("Brygid the Stylist: the Robe and the Bronze Subligar worn");
        Equip(1, SlotBody);
        Equip(2, SlotLegs);
        Check("0x050: the Robe and the Bronze Subligar worn, in char_equip", $"{Robe},{BronzeSubligar}/2",
              $"{Worn(SlotBody)},{Worn(SlotLegs)}/{DbRow(db, "COUNT(*)", "FROM char_equip WHERE charid = ? AND equipslotid IN (5, 7)")}");
        Talk(Brygid);
        Check("Brygid: event 311", $"032:{Brygid}:311/311", $"{Ev()}/{Cur()}");
        EventEnd(Brygid, 311);
        Check("event 311 finished: the quest completed", "2/01", $"{Status(LogBastok, BrygidTheStylist)}/{QuestBits(LogBastok, BrygidTheStylist)}");
        Check("  the reward: Gloves, 20 Bastok fame, the Brygid-Approved title", $"1/20/{TitleBrygidApproved}/True",
              $"{Qty(Gloves)}/{Fame()}/{Lua("report(player:getTitle())")}/{DbTitle(TitleBrygidApproved)}");
        Talk(Brygid);
        Check("Brygid without artifact armor: her default action, event 119", $"032:{Brygid}:119", Ev());
        EventEnd(Brygid, 119);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Brygid the Stylist Returns: the Temple Crown (artifact armor) worn, the offer");
        Equip(3, SlotHead);
        Check("the Temple Crown worn", TempleCrown.ToString(), Worn(SlotHead));
        Talk(Brygid);
        string reqBody = Var("Quest[1][74]requestedBody"), reqLegs = Var("Quest[1][74]requestedLegs");
        Check("Brygid: event 380 (0x034) with the robe worn, the requested body and legs (kept in char_vars), the job",
              $"034:{Brygid}:380:1,{reqBody},{reqLegs},2/380", $"{Ev()}/{Cur()}");
        Check("  the requests equippable by a MNK 75", "True/True",
              $"{CanEquip(int.Parse(reqBody), 2, 75)}/{CanEquip(int.Parse(reqLegs), 2, 75)}");
        EventEnd(Brygid, 380);
        Check("event 380 finished: the quest accepted", "1/10",
              $"{Status(LogBastok, BrygidTheStylistReturns)}/{QuestBits(LogBastok, BrygidTheStylistReturns)}");
        Talk(Brygid);
        Check("Brygid, the requests not worn: event 381 with the same", $"034:{Brygid}:381:1,{reqBody},{reqLegs},2", Ev());
        EventEnd(Brygid, 381);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Brygid the Stylist Returns: the requested pieces worn, the reward chosen through an event update");
        // the pieces bought: put in the inventory as a shop would, then worn with 0x050
        Lua($"player:addItem({reqBody}) player:addItem({reqLegs})");
        Equip(SlotOf(int.Parse(reqBody)), SlotBody);
        Equip(SlotOf(int.Parse(reqLegs)), SlotLegs);
        Check("the requests worn", $"{reqBody},{reqLegs}", $"{Worn(SlotBody)},{Worn(SlotLegs)}");
        Talk(Brygid);
        Check("Brygid: event 382", $"032:{Brygid}:382/382", $"{Ev()}/{Cur()}");
        // GP_CLI_COMMAND_EVENTEND mode 1: the Duende Cotehardie (option 1) looked at
        EventEnd(Brygid, 382, 1, 1);
        var pending = Of(0x05C);
        bool canEquipReward = CanEquip(DuendeCotehardie, 2, 75);
        Check("the update: onEventUpdate's updateEvent(0, option - 1, not held, equippable) in a 0x05C, still in the event",
              $"1/0,0,1,{(canEquipReward ? 1 : 0)}/382",
              $"{pending.Count}/{string.Join(",", Enumerable.Range(0, 4).Select(i => pending.Count == 0 ? -1 : BitConverter.ToInt32(pending[^1].Buffer, 4 + S2cPendingnum.NumOffset(i))))}/{Cur()}");
        EventEnd(Brygid, 382, 1);
        Check("event 382 finished with option 1: Option 1", "1/none", $"{Var("Quest[1][74]Option")}/{Cur()}");
        Talk(Brygid);
        Check("Brygid: event 385 with the reward and the subligar she wants", $"034:{Brygid}:385:0,{DuendeCotehardie},{AriesSubligar}", Ev());
        EventEnd(Brygid, 385);
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));

        Console.WriteLine("Brygid the Stylist Returns: the Aries Subligar traded");
        Trade(Brygid, (SlotOf(Robe), 1));
        Check("the Robe traded: no event", "none/none", $"{Ev()}/{Cur()}");
        Trade(Brygid, (SlotOf(AriesSubligar), 1));
        Check("the Aries Subligar traded: event 383", $"032:{Brygid}:383/383", $"{Ev()}/{Cur()}");
        EventEnd(Brygid, 383);
        Check("event 383 finished: the quest completed", "2/01",
              $"{Status(LogBastok, BrygidTheStylistReturns)}/{QuestBits(LogBastok, BrygidTheStylistReturns)}");
        Check("  the subligar gone, a Duende Cotehardie, the ITEM_OBTAINED message", $"0/1/{Text(BastokMarkets, "ITEM_OBTAINED")}:{DuendeCotehardie}",
              $"{Qty(AriesSubligar)}/{Qty(DuendeCotehardie)}/{Special()}");
        Check("  the reward: 10 more Bastok fame, the Bastok's Second Best Dressed title", $"30/{TitleSecondBestDressed}/True",
              $"{Fame()}/{Lua("report(player:getTitle())")}/{DbTitle(TitleSecondBestDressed)}");
        Check("  the quest's vars cleared", "-/-/-", $"{Var("Quest[1][74]Option")}/{Var("Quest[1][74]requestedBody")}/{Var("Quest[1][74]requestedLegs")}");
        // a requested cloak takes the head slot, so the Temple Crown came off: the body off and the crown back on
        if (Worn(SlotHead) != TempleCrown.ToString())
        {
            Equip(0, SlotBody);
            Equip(SlotOf(TempleCrown), SlotHead);
        }
        Check("the Temple Crown worn again", TempleCrown.ToString(), Worn(SlotHead));
        Talk(Brygid);
        Check("Brygid again with artifact armor: the repeat offer, event 380", $"380", Cur());
        EventEnd(Brygid, 380);
        Check("event 380 finished: delQuest then begin, accepted again", "1/10",
              $"{Status(LogBastok, BrygidTheStylistReturns)}/{QuestBits(LogBastok, BrygidTheStylistReturns)}");
        Check("no script errors", "", string.Join(" | ", ScriptErrors()));
        Quests2();
        if (Environment.GetEnvironmentVariable("QUESTS_DUMP_LOG") == "1") foreach (var l in log.Distinct()) Console.WriteLine("  log: " + l);
    }
}
