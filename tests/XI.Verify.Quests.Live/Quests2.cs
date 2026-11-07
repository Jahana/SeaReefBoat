using System;
using System.Collections.Generic;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Zone;

// Quests 2: quests of every region and log, each touching a different host feature (repeatable quests and their
// cooldowns, item and gil trades, key items, fame tiers, titles, job and level gates, NMs popped and killed, zone-in
// cutscenes, timed quests, the job unlocks), driven as the chains of Quests 1 are. Chars 996011-996019.
internal static partial class Program
{
    private const int LogSandoria = 0, LogWindurst = 2, LogJeuno = 3, LogOther = 4, LogOutlands = 5, LogAhtUrhgan = 6, LogCrystalWar = 7,
                      LogAbyssea = 8, LogAdoulin = 9, LogCoalition = 10;

    // the sections; QUESTS_ONLY=<name>[,<name>] runs only those (and not the chains of Quests 1)
    private static void Quests2(string only = null)
    {
        var sections = new List<(string Name, Action Run)>
        {
            ("Sandoria", Sandoria),
            ("Windurst", Windurst),
            ("Jeuno", Jeuno),
            ("JeunoNight", JeunoNight),
            ("OtherAreas", OtherAreas),
            ("Outlands", Outlands),
            ("AhtUrhgan", AhtUrhgan),
            ("Crafting", Crafting),
            ("CrystalWarAdoulin", CrystalWarAdoulin),
            ("Bastok", Bastok),
        };
        foreach (var (name, run) in sections)
            if (only == null || only.Split(',').Contains(name)) run();
    }

    // a Hume of the nation past its first login (NEW_ADVENTURER) at x / y / z in the zone, the main job at the level
    // (and the rest of the jobs at 1, the jobs unlocked by default), 10000 gil and the items from inventory slot 1 on
    private static void NewChar(uint id, ushort zoneId, float x, float y, float z, byte nation, int job, int level, params (ushort Item, uint Qty)[] items)
    {
        Logout();
        CharId = id;
        log.Clear();
        var titles = new byte[143];
        titles[TitleNewAdventurer >> 3] |= 1 << (TitleNewAdventurer & 7);
        string[] jobCols = { "", "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng", "sam", "nin", "drg", "smn", "blu", "cor", "pup", "dnc", "sch", "geo", "run" };
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of($"questslive{CharId % 100}"), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel,titles) VALUES (?,?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of($"Questlive{CharId % 100}"), BoundValue.Of(zoneId), BoundValue.Of(zoneId),
            BoundValue.Of(x), BoundValue.Of(y), BoundValue.Of(z), BoundValue.Of(nation), BoundValue.Of((byte)0), BoundValue.Of(titles));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 100, ?, ?)", BoundValue.Of(CharId), BoundValue.Of((byte)job), BoundValue.Of((byte)level));
        Sql(db, $"REPLACE INTO char_jobs(charid, unlocked, genkai, {jobCols[job]}) VALUES (?, ?, ?, ?)", BoundValue.Of(CharId),
            BoundValue.Of(126u | 1u << job), BoundValue.Of((byte)Math.Max(50, level)), BoundValue.Of((byte)level));
        Sql(db, "REPLACE INTO char_look(charid, race) VALUES (?, 1)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_profile(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_unlocks(charid) VALUES (?)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,0,65535,10000)", BoundValue.Of(CharId));
        for (int i = 0; i < items.Length; i++)
            Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,0,?,?,?)",
                BoundValue.Of(CharId), BoundValue.Of((byte)(i + 1)), BoundValue.Of(items[i].Item), BoundValue.Of(items[i].Qty));
        Login();
    }

    // ---- San d'Oria ----------------------------------------------------------------------------------------------
    // A RDM 40 San d'Orian: Starting a Flame (repeatable: a 0x034 params table, flint stones for gil and fame twice),
    // The Seamstress (declined, then taken; the sheepskins traded twice for gloves, the title, fame), The Crimson Trial
    // (RDM AF1: the job and level gate, Valderotaux's event update, the NM spawned by the quest's onZoneIn in Davoi
    // killed, the key traded to the Storage Hole for a key item, the Fencing Degen), A Knight's Test (the PLD unlock:
    // three key items in Southern San d'Oria and Davoi, messageName, unlockJob, the Kite Shield, the gesture key item,
    // the title) and Lufet's Lake Salt (Port San d'Oria: three salts traded for 600 gil and a title).
    private const ushort SouthernSandoria = 230, PortSandoria = 232, Davoi = 149;

    private static void Sandoria()
    {
        ushort flint = ItemIdOf("FLINT_STONE"), sheepskin = ItemIdOf("SHEEPSKIN"), gloves = ItemIdOf("LEATHER_GLOVES"), salt = ItemIdOf("CHUNK_OF_LUFET_SALT");
        ushort storageKey = ItemIdOf("DAVOI_STORAGE_KEY"), degen = ItemIdOf("FENCING_DEGEN"), kite = ItemIdOf("KITE_SHIELD");
        NewChar(996011, SouthernSandoria, 82.0f, 0.0f, 114.0f, 0, 5, 40, (flint, 8), (sheepskin, 6), (salt, 3));
        Console.WriteLine("San d'Oria: a RDM 40 in Southern San d'Oria");
        Check("in Southern San d'Oria, out of any event", $"{SouthernSandoria}/5/40/none", $"{c.Zone}/{c.MJob}/{c.MLvl}/{Cur()}");
        NoErrors("no script errors on the login");

        Console.WriteLine("Starting a Flame: Legata's offer, the flint stones traded twice");
        int flame = QuestId("sandoria", "STARTING_A_FLAME");
        uint legata = TalkTo("Legata");
        Check("Legata: event 37", $"032:{legata}:37/37", $"{Ev()}/{Cur()}");
        EventEnd(legata, 37, 0);
        Check("event 37, option 0: not taken", "0/00", QStat(LogSandoria, flame));
        Talk(legata);
        EventEnd(legata, 37, 1);
        Check("event 37, option 1: accepted", "1/10", QStat(LogSandoria, flame));
        Talk(legata);
        Check("Legata while accepted: event 35", $"032:{legata}:35", Ev());
        EventEnd(legata, 35);
        TradeItems(legata, (flint, 3));
        Check("three flint stones: no event", "none", Ev());
        TradeItems(legata, (flint, 4));
        Check("four: event 36", $"032:{legata}:36/36", $"{Ev()}/{Cur()}");
        EventEnd(legata, 36);
        Check("event 36 finished: completed, 4 flint stones gone, 100 gil, 10 San d'Oria fame", "2/01/4/10100/10",
              $"{QStat(LogSandoria, flame)}/{Qty(flint)}/{Gil()}/{FameOf("SANDORIA")}");
        Talk(legata);
        Check("Legata after it: event 37 with the repeat flag in param 7 (a params table)", $"034:{legata}:37:0,0,0,0,0,0,0,1", Ev());
        EventEnd(legata, 37);
        TradeItems(legata, (flint, 4));
        Check("the repeat: event 36", $"032:{legata}:36", Ev());
        EventEnd(legata, 36);
        Check("  still completed, the stones gone, 100 more gil, 10 more fame", "2/01/0/10200/20",
              $"{QStat(LogSandoria, flame)}/{Qty(flint)}/{Gil()}/{FameOf("SANDORIA")}");
        NoErrors();

        Console.WriteLine("The Seamstress: Hanaa Punaa's offer declined then taken, the sheepskins traded twice");
        int seamstress = QuestId("sandoria", "THE_SEAMSTRESS");
        uint hanaa = TalkTo("Hanaa_Punaa");
        Check("Hanaa Punaa: event 528", $"032:{hanaa}:528", Ev());
        EventEnd(hanaa, 528, 1);
        Check("declined (option 1): Prog 1, not accepted", "1/0", $"{Var($"Quest[{LogSandoria}][{seamstress}]Prog")}/{Status(LogSandoria, seamstress)}");
        Talk(hanaa);
        Check("Hanaa Punaa again: event 531", $"032:{hanaa}:531", Ev());
        EventEnd(hanaa, 531, 0);
        Check("taken (option 0): accepted", "1/10", QStat(LogSandoria, seamstress));
        Talk(hanaa);
        Check("Hanaa Punaa while accepted: event 529", $"032:{hanaa}:529", Ev());
        EventEnd(hanaa, 529);
        TradeItems(hanaa, (sheepskin, 4));
        Check("four sheepskins (tradeHasExactly three): no event", "none", Ev());
        TradeItems(hanaa, (sheepskin, 3));
        Check("three: event 530", $"032:{hanaa}:530", Ev());
        EventEnd(hanaa, 530);
        Check("event 530 finished: completed, 3 sheepskins gone, Leather Gloves, 10 more fame", "2/01/3/1/30",
              $"{QStat(LogSandoria, seamstress)}/{Qty(sheepskin)}/{Qty(gloves)}/{FameOf("SANDORIA")}");
        Check("  the Silencer of the Lambs title", "True/True", TitleNow("SILENCER_OF_THE_LAMBS"));
        TradeItems(hanaa, (sheepskin, 3));
        Check("the repeat: event 530", $"032:{hanaa}:530", Ev());
        EventEnd(hanaa, 530);
        Check("  a second pair of gloves, 10 more fame", "0/2/40", $"{Qty(sheepskin)}/{Qty(gloves)}/{FameOf("SANDORIA")}");
        NoErrors();

        Console.WriteLine("The Crimson Trial (RDM AF1): Sharzalion's offer declined, Valderotaux, the offer taken");
        int crimson = QuestId("sandoria", "THE_CRIMSON_TRIAL");
        uint sharzalion = TalkTo("Sharzalion");
        Check("Sharzalion: event 70", $"032:{sharzalion}:70", Ev());
        EventEnd(sharzalion, 70, 0);
        Check("declined: Prog 1", "1/0", $"{Var($"Quest[{LogSandoria}][{crimson}]Prog")}/{Status(LogSandoria, crimson)}");
        uint valderotaux = TalkTo("Valderotaux");
        Check("Valderotaux: event 54 (0x034, the play time its param)", $"034:{valderotaux}:54/54", $"{string.Join(":", Ev().Split(':').Take(3))}/{Cur()}");
        EventEnd(valderotaux, 54, 1, 1);
        var update = Of(0x05C);
        Check("  the update (option 1): updateEvent(54, 0) in a 0x05C", "1", update.Count.ToString());
        EventEnd(valderotaux, 54);
        Talk(sharzalion);
        Check("Sharzalion: event 71", $"032:{sharzalion}:71", Ev());
        EventEnd(sharzalion, 71, 1);
        Check("taken: accepted", "1/10", QStat(LogSandoria, crimson));
        NoErrors();

        Console.WriteLine("The Crimson Trial: Davoi, Purpleflash Brukdok spawned on zone-in and killed, the Storage Hole");
        Warp(-221.0f, 1.0f, -290.0f, Davoi);
        Check("in Davoi", Davoi, c.Zone);
        uint brukdok = uint.Parse(Lua("report(zones[xi.zone.DAVOI].mob.PURPLEFLASH_BRUKDOK)"));
        var nm = Here.MobById[brukdok];
        Check("the quest's onZoneIn spawned Purpleflash Brukdok", "True", nm.IsSpawned);
        KillByChar(brukdok);
        Check("  killed", "True", nm.IsDead || !nm.IsSpawned);
        // the key the NM drops (a drop roll): put in the inventory as the treasure pool would
        if (Qty(storageKey) == "0") Lua($"player:addItem({storageKey})");
        uint hole = TalkTo("Storage_Hole");
        Check("the Storage Hole: its message", $"none/{Text(Davoi, "AN_ORCISH_STORAGE_HOLE")}:0", $"{Ev()}/{Special()}");
        TradeItems(hole, (storageKey, 1));
        Check("the key traded: tradeComplete, the Orcish Dried Food key item", $"0/1/{Text(Davoi, "KEYITEM_OBTAINED")}:{Ki("ORCISH_DRIED_FOOD")}",
              $"{Qty(storageKey)}/{HasKi("ORCISH_DRIED_FOOD")}/{Special()}");
        NoErrors();

        Console.WriteLine("The Crimson Trial: the reward");
        Warp(95.0f, 0.0f, 109.0f, SouthernSandoria);
        Talk(sharzalion);
        Check("Sharzalion with the food: event 75", $"032:{sharzalion}:75", Ev());
        EventEnd(sharzalion, 75);
        Check("event 75 finished: completed, the Fencing Degen, the key item taken, 20 more fame", "2/01/1/0/60",
              $"{QStat(LogSandoria, crimson)}/{Qty(degen)}/{HasKi("ORCISH_DRIED_FOOD")}/{FameOf("SANDORIA")}");
        NoErrors();

        Console.WriteLine("A Knight's Test (the PLD unlock): Balasiel's offer");
        int knight = QuestId("sandoria", "A_KNIGHTS_TEST");
        uint balasiel = TalkTo("Balasiel");
        Check("Balasiel before A Squire's Test II: his default, no quest event", "False", Cur() == "627");
        if (c.IsInEvent) Finish();
        Lua($"player:completeQuest({LogSandoria}, xi.quest.id.sandoria.A_SQUIRES_TEST_II)");
        Talk(balasiel);
        Check("Balasiel: event 627", $"032:{balasiel}:627", Ev());
        EventEnd(balasiel, 627, 0);
        Check("taken: accepted, the Book of Tasks", "1/10/1", $"{QStat(LogSandoria, knight)}/{HasKi("BOOK_OF_TASKS")}");
        Talk(balasiel);
        Check("Balasiel without the soul: event 669", $"032:{balasiel}:669", Ev());
        EventEnd(balasiel, 669);
        uint baunise = TalkTo("Baunise");
        Check("Baunise: event 634", $"032:{baunise}:634", Ev());
        EventEnd(baunise, 634);
        Talk(baunise);
        Check("Baunise with the book: the quest's messageName action (showText from her): DO_NOT_FRET", $"none/1/{Text(SouthernSandoria, "DO_NOT_FRET")}:0",
              $"{Ev()}/{HasKi("BOOK_OF_THE_WEST")}/{Special()}");
        uint cahaurme = TalkTo("Cahaurme");
        Check("Cahaurme: event 633", $"032:{cahaurme}:633", Ev());
        EventEnd(cahaurme, 633);
        Check("  the Book of the East", "1", HasKi("BOOK_OF_THE_EAST"));
        Warp(-221.0f, 2.0f, -290.0f, Davoi);
        TalkTo("Disused_Well");
        Check("Davoi's Disused Well: the Knight's Soul (a keyItem action)", $"1/{Text(Davoi, "KEYITEM_OBTAINED")}:{Ki("KNIGHTS_SOUL")}",
              $"{HasKi("KNIGHTS_SOUL")}/{Special()}");
        TalkTo("Disused_Well");
        Check("  again: YOU_FIND_NOTHING", $"{Text(Davoi, "YOU_FIND_NOTHING")}:0", Special());
        Warp(-136.0f, -11.0f, 62.0f, SouthernSandoria);
        Check("PLD not unlocked yet", "false", Lua("report(tostring(player:hasJob(xi.job.PLD)))"));
        Talk(balasiel);
        Check("Balasiel with the soul: event 628", $"032:{balasiel}:628", Ev());
        EventEnd(balasiel, 628);
        Check("event 628 finished: completed, PLD unlocked (char_jobs.unlocked), at level 1", "2/01/true/True/1",
              $"{QStat(LogSandoria, knight)}/{Lua("report(tostring(player:hasJob(xi.job.PLD)))")}/" +
              $"{(uint.Parse(DbRow(db, "unlocked", "FROM char_jobs WHERE charid = ?")) & 1u << 7) != 0}/{DbRow(db, "pld", "FROM char_jobs WHERE charid = ?")}");
        Check("  UNLOCK_PALADIN, the Kite Shield, the PLD gesture, the books and the soul taken", $"True/1/1/0/0/0/0",
              $"{Special().Contains($"{Text(SouthernSandoria, "UNLOCK_PALADIN")}:")}/{Qty(kite)}/{HasKi("JOB_GESTURE_PALADIN")}/" +
              $"{HasKi("KNIGHTS_SOUL")}/{HasKi("BOOK_OF_TASKS")}/{HasKi("BOOK_OF_THE_WEST")}/{HasKi("BOOK_OF_THE_EAST")}");
        Check("  the Tried and Tested Knight title, 20 more fame", "True/True/80", $"{TitleNow("TRIED_AND_TESTED_KNIGHT")}/{FameOf("SANDORIA")}");
        NoErrors();

        Console.WriteLine("Lufet's Lake Salt: Port San d'Oria");
        int lufet = QuestId("sandoria", "LUFETS_LAKE_SALT");
        Warp(-70.0f, -5.5f, -31.0f, PortSandoria);
        uint nogelle = TalkTo("Nogelle");
        Check("Nogelle: event 12", $"032:{nogelle}:12", Ev());
        EventEnd(nogelle, 12, 1);
        Check("accepted", "1/10", QStat(LogSandoria, lufet));
        string gil = Gil();
        TradeItems(nogelle, (salt, 3));
        Check("three salts: event 11", $"032:{nogelle}:11", Ev());
        EventEnd(nogelle, 11);
        Check("event 11 finished: completed, the salts gone, 600 gil, the Bean Cuisine Salter title", $"2/01/0/{int.Parse(gil) + 600}/True/True",
              $"{QStat(LogSandoria, lufet)}/{Qty(salt)}/{Gil()}/{TitleNow("BEAN_CUISINE_SALTER")}");
        Talk(nogelle);
        Check("Nogelle after it: event 522 (replaceDefault)", $"032:{nogelle}:522", Ev());
        EventEnd(nogelle, 522);
        NoErrors();
    }

    // ---- Windurst -------------------------------------------------------------------------------------------------
    // A WAR 75 Windurstian: The Fanged One (the RNG unlock: Perih Vashai's offer, a Black Tiger Fang traded and taken,
    // Old Sabertooth popped at the Tiger Bones in Sauromugue Champaign: killed by the char (the TAKE_DAMAGE listener;
    // onMobDeath's isKiller sets the Wait), popped again and left to die of its own poison (onMobDeath's noKiller: the
    // Timer, the zone's players in range), the Old Tiger's Fang, the unlock, the necklace, the gesture, the title,
    // mustZone) and I Can Hear a Rainbow (the SMN unlock: the door's offer with Carbuncle's Ruby held, seven zones
    // entered under seven weathers (setWeather; the quest's onZoneIn cutscenes, their event updates), the ruby traded
    // in La Theine: SMN, the Carbuncle spell).
    private const ushort WindurstWoods = 241, WindurstWalls = 239, Sauromugue = 120, LaTheine = 102;

    private static void Windurst()
    {
        ushort fang = ItemIdOf("BLACK_TIGER_FANG"), necklace = ItemIdOf("RANGERS_NECKLACE"), ruby = ItemIdOf("CARBUNCLES_RUBY");
        // a WAR 75: the field's mobs leave it be while it waits on the tiger
        NewChar(996012, WindurstWoods, 117.0f, -3.0f, 90.0f, 2, 1, 75, (fang, 1), (ruby, 1));
        Console.WriteLine("Windurst: a WAR 75 in Windurst Woods");
        Check("in Windurst Woods, out of any event", $"{WindurstWoods}/none", $"{c.Zone}/{Cur()}");
        NoErrors("no script errors on the login");

        Console.WriteLine("The Fanged One (the RNG unlock): Perih Vashai's offer, the fang");
        int fanged = QuestId("windurst", "THE_FANGED_ONE");
        string fangVar(string name) => Var($"Quest[{LogWindurst}][{fanged}]{name}");
        uint perih = TalkTo("Perih_Vashai");
        Check("Perih Vashai: event 351", $"032:{perih}:351", Ev());
        EventEnd(perih, 351);
        Check("accepted", "1/10", QStat(LogWindurst, fanged));
        Talk(perih);
        Check("Perih Vashai, Prog 0: event 352", $"032:{perih}:352", Ev());
        EventEnd(perih, 352);
        uint kapeh = TalkTo("Kapeh_Myohrye");
        Check("Kapeh Myohrye: event 354", $"032:{kapeh}:354", Ev());
        EventEnd(kapeh, 354);
        uint muhk = TalkTo("Muhk_Johldy");
        Check("Muhk Johldy: event 353", $"032:{muhk}:353", Ev());
        EventEnd(muhk, 353);
        Near(perih);
        TradeItems(perih, (fang, 1));
        Check("the Black Tiger Fang traded: event 356", $"032:{perih}:356", Ev());
        EventEnd(perih, 356);
        Check("  tradeComplete: the fang gone, still accepted", "0/1", $"{Qty(fang)}/{Status(LogWindurst, fanged)}");
        NoErrors();

        Console.WriteLine("The Fanged One: Old Sabertooth popped at the Tiger Bones and killed by the char");
        Warp(664.0f, -8.0f, -377.0f, Sauromugue);
        Check("in Sauromugue Champaign", Sauromugue, c.Zone);
        uint tigerId = uint.Parse(Lua("report(zones[xi.zone.SAUROMUGUE_CHAMPAIGN].mob.OLD_SABERTOOTH)"));
        var tiger = Here.MobById[tigerId];
        uint bones = TalkTo("Tiger_Bones");
        Check("the Tiger Bones: Old Sabertooth spawned, OLD_SABERTOOTH_DIALOG_I", $"True/{Text(Sauromugue, "OLD_SABERTOOTH_DIALOG_I")}:0",
              $"{tiger.IsSpawned}/{Special()}");
        Check("  its onMobSpawn: poisoned", "true", Lua($"report(tostring(GetMobByID({tigerId}):hasStatusEffect(xi.effect.POISON)))"));
        long sys = long.Parse(Lua("report(GetSystemTime())"));
        KillByChar(tigerId);
        long wait = long.TryParse(fangVar("Wait"), out var w) ? w : 0;
        Check("killed by the char: onMobDeath's isKiller, the TAKE_DAMAGE listener's tookDamage: Wait about 300 s on, no Timer",
              "True/True/-", $"{tiger.IsDead || !tiger.IsSpawned}/{wait >= sys + 299 && wait <= sys + 330}/{fangVar("Timer")}");
        for (int i = 0; i < 60 && tiger.IsSpawned; i++) Tick(1000);
        Check("  despawned", "False", tiger.IsSpawned);
        Near(bones);
        Talk(bones);
        Check("the Tiger Bones while the Wait runs: no tiger, MANY_TIGER_BONES (its default)", $"False/{Lua("report(zones[xi.zone.SAUROMUGUE_CHAMPAIGN].text.MANY_TIGER_BONES)")}:0",
              $"{tiger.IsSpawned}/{Special()}");
        Lua($"player:setCharVar('Quest[{LogWindurst}][{fanged}]Wait', 0)");
        NoErrors();

        Console.WriteLine("The Fanged One: popped again and left to die of its poison");
        Near(bones);
        Talk(bones);
        Check("the Tiger Bones: spawned again", "True", tiger.IsSpawned);
        int secs = 0;
        for (; secs < 400 && !tiger.IsDead && tiger.IsSpawned; secs++)
        {
            MoveTo(tiger.X + 3.0f, tiger.Y, tiger.Z);
            Tick(1000);
        }
        Tick(1000);
        Tick(1000);
        sys = long.Parse(Lua("report(GetSystemTime())"));
        long timer = long.TryParse(fangVar("Timer"), out var t) ? t : 0;
        Check("dead of the poison (its HP / 64 a 3 s tick, about 192 s), not hurt by the char", "True/True/0",
              $"{tiger.IsDead || !tiger.IsSpawned}/{secs > 150 && secs < 260}/{Lua($"report(GetMobByID({tigerId}):getLocalVar('tookDamage'))")}");
        Check("  onMobDeath's noKiller, the char within 32: Timer about 180 s on", "True", timer >= sys + 150 && timer <= sys + 181);
        Near(bones);
        Talk(bones);
        Check("the Tiger Bones within the Timer: the Old Tiger's Fang, Prog 1", $"1/1/{Text(Sauromugue, "KEYITEM_OBTAINED")}:{Ki("OLD_TIGERS_FANG")}",
              $"{HasKi("OLD_TIGERS_FANG")}/{fangVar("Prog")}/{Special()}");
        NoErrors();

        Console.WriteLine("The Fanged One: the reward");
        Warp(117.0f, -3.0f, 90.0f, WindurstWoods);
        Talk(perih);
        Check("Perih Vashai with the fang: event 357", $"032:{perih}:357", Ev());
        EventEnd(perih, 357);
        Check("event 357 finished: completed, RNG unlocked, the fang key item taken, the gesture", "2/01/true/0/1",
              $"{QStat(LogWindurst, fanged)}/{Lua("report(tostring(player:hasJob(xi.job.RNG)))")}/{HasKi("OLD_TIGERS_FANG")}/{HasKi("JOB_GESTURE_RANGER")}");
        Check("  the Ranger's Necklace, The Fanged One title, 20 Windurst fame, mustZone", $"1/True/True/20/{WindurstWoods}",
              $"{Qty(necklace)}/{TitleNow("THE_FANGED_ONE")}/{FameOf("WINDURST")}/{fangVar("mustZone")}");
        Talk(perih);
        Check("Perih Vashai before a zone change: event 358", $"032:{perih}:358", Ev());
        EventEnd(perih, 358);
        NoErrors();

        Console.WriteLine("I Can Hear a Rainbow (the SMN unlock): the door's offer");
        int rainbow = QuestId("windurst", "I_CAN_HEAR_A_RAINBOW");
        Warp(-20.0f, -2.0f, -120.0f, WindurstWalls);
        uint door = TalkTo("_6n2");
        Check("the door with Carbuncle's Ruby held: event 384 (0x034: 0, the ruby)", $"034:{door}:384:0,{ruby}", Ev());
        EventEnd(door, 384);
        Check("accepted", "1/10", QStat(LogWindurst, rainbow));
        Talk(door);
        Check("the door again: event 385", $"034:{door}:385:0,{ruby}", Ev());
        EventEnd(door, 385);
        Talk(door);
        Check("  once per zone: not again", "False", Cur() == "385");
        if (c.IsInEvent) Finish();
        NoErrors();

        Console.WriteLine("I Can Hear a Rainbow: seven zones entered under seven weathers");
        // (zone, a spot in it, the weather, the quest's zone-in cutscene)
        var lights = new (ushort Zone, float X, float Y, float Z, string Weather, int Cs)[]
        {
            (100, -24.0f, -40.0f, -300.0f, "NONE", 51),       // West Ronfaure
            (101, 248.0f, -40.0f, 0.0f, "HOT_SPELL", 21),     // East Ronfaure
            (106, -560.0f, 40.0f, 60.0f, "DUST_STORM", 244),  // North Gustaberg
            (107, 300.0f, 0.0f, -350.0f, "RAIN", 901),        // South Gustaberg
            (115, -24.0f, -18.0f, -150.0f, "WIND", 48),       // West Sarutabaruta
            (116, 400.0f, -10.0f, -180.0f, "SNOW", 50),       // East Sarutabaruta
            (102, 60.0f, 5.0f, 250.0f, "THUNDER", 123),       // La Theine Plateau
        };
        for (int i = 0; i < lights.Length; i++)
        {
            var (z, x, y, zz, weather, cs) = lights[i];
            // a GM's weather (CLuaBaseEntity::setWeather on an entity of the zone: the zone's weather)
            Lua($"GetZone({z}):getNPCs()[1]:setWeather(xi.weather.{weather})");
            Warp(x, y, zz, z, false);
            Check($"zone {z} under {weather}: the quest's zone-in cutscene {cs}", $"{z}/{cs}", $"{c.Zone}/{Cur()}");
            if (Cur() != cs.ToString())
            {
                if (c.IsInEvent) Finish();
                continue;
            }
            EndEvent(cs, 0, 1);
            var upd = Of(0x05C);
            Check("  its update: updateEvent(0, 1, the weather, the lights before it)",
                  $"1/0,1,{Lua($"report(xi.weather.{weather})")},{i}",
                  $"{upd.Count}/{string.Join(",", Enumerable.Range(0, 4).Select(k => upd.Count == 0 ? -1 : BitConverter.ToInt32(upd[^1].Buffer, 4 + XI.Map.S2cPendingnum.NumOffset(k))))}");
            EndEvent(cs);
        }
        // the seventh light's update sets Prog 1, so its finish no longer matches the Prog 0 section and its bit (5,
        // thunder) is never set: the script's, as in C++
        Check("the seven lights: Prog 1 (Light 95: the last light's bit not set)", "95/1",
              $"{Var($"Quest[{LogWindurst}][{rainbow}]Light")}/{Var($"Quest[{LogWindurst}][{rainbow}]Prog")}");
        NoErrors();

        Console.WriteLine("I Can Hear a Rainbow: the ruby traded in La Theine Plateau");
        uint qm3 = NpcId("qm3");
        Near(qm3);
        TradeItems(qm3, (ruby, 1));
        Check("the ruby traded: event 124", $"032:{qm3}:124", Ev());
        EventEnd(qm3, 124);
        Check("event 124 finished: completed, the ruby gone, SMN unlocked, the Carbuncle spell", "2/01/0/true/true",
              $"{QStat(LogWindurst, rainbow)}/{Qty(ruby)}/{Lua("report(tostring(player:hasJob(xi.job.SMN)))")}/{Lua("report(tostring(player:hasSpell(xi.magic.spell.CARBUNCLE)))")}");
        string keyMsg = Text(LaTheine, "KEYITEM_OBTAINED") + ":";
        Check("  UNLOCK_SUMMONER, UNLOCK_CARBUNCLE, the gesture, the Rainbow Weaver title, the rainbow's setRainbow",
              $"{Text(LaTheine, "UNLOCK_SUMMONER")}:0,{Text(LaTheine, "UNLOCK_CARBUNCLE")}:0/1/True/True/1",
              $"{string.Join(",", Special().Split(',').Where(m => !m.StartsWith(keyMsg)))}/{HasKi("JOB_GESTURE_SUMMONER")}/" +
              $"{TitleNow("RAINBOW_WEAVER")}/{Lua("report(GetNPCByID(zones[xi.zone.LA_THEINE_PLATEAU].npc.RAINBOW):getLocalVar('setRainbow'))")}");
        NoErrors();
    }

    // ---- Jeuno ----------------------------------------------------------------------------------------------------
    // A WAR 50 at its level cap: Chocobo's Wounds (the chocobo license: Brutus' offer declined then taken with its
    // option param, the chocobo fed six times on a 45 s GetSystemTime timer (the wall clock moved on), Gysahl Greens
    // refused, confirmTrade only past the third feed; the license, the title, fame in the three nations), Path of the
    // Beastmaster (the BST unlock on Save My Son), The Gobbiebag Part I (the inventory 30 -> 35: changeContainerSize,
    // the Jeuno fame level), Path of the Bard (the BRD unlock: Mertaire's messageName with its flags, the Song Runes'
    // cutscene in Valkurm Dunes, 3000 gil) and In Defiant Challenge (Maat's first limit break: nine ??? in three
    // dungeons, the key item sets fused into items, the trade, setLevelCap 55).
    private const ushort UpperJeuno = 244, LowerJeuno = 245, RuludeGardens = 243, ValkurmDunes = 103, CrawlersNest = 197, Garlaige = 200, Eldieme = 195;

    private static void Jeuno()
    {
        ushort wildgrass = ItemIdOf("CLUMP_OF_GAUSEBIT_WILDGRASS"), greens = ItemIdOf("BUNCH_OF_GYSAHL_GREENS");
        ushort leather = ItemIdOf("SQUARE_OF_DHALMEL_LEATHER"), ingot = ItemIdOf("STEEL_INGOT"), linen = ItemIdOf("SQUARE_OF_LINEN_CLOTH"), peridot = ItemIdOf("PERIDOT");
        NewChar(996013, UpperJeuno, -55.0f, 8.0f, 93.0f, 1, 1, 50, (wildgrass, 8), (greens, 1), (leather, 1), (ingot, 1), (linen, 1), (peridot, 1));
        Console.WriteLine("Jeuno: a WAR 50 in Upper Jeuno");
        Check("in Upper Jeuno, level cap 50", $"{UpperJeuno}/none/50", $"{c.Zone}/{Cur()}/{Lua("report(player:getLevelCap())")}");
        NoErrors("no script errors on the login");

        Console.WriteLine("Chocobo on the Loose and Chocobo's Wounds: Brutus' two offers");
        int wounds = QuestId("jeuno", "CHOCOBOS_WOUNDS"), loose = QuestId("jeuno", "CHOCOBO_ON_THE_LOOSE");
        string woundsVar(string name) => Var($"Quest[{LogJeuno}][{wounds}]{name}");
        string looseVar(string name) => Var($"Quest[{LogJeuno}][{loose}]{name}");
        uint brutus = TalkTo("Brutus");
        // two progress events on offer: the first container's (the files sorted: Chocobo_on_the_Loose.lua first)
        Check("Brutus: Chocobo on the Loose's event 10093 (the first of the two offers)", $"032:{brutus}:10093", Ev());
        EventEnd(brutus, 10093);
        Check("  Chocobo on the Loose accepted", "1/10", QStat(LogJeuno, loose));
        Talk(brutus);
        Check("Brutus: Chocobo's Wounds' event 71 (0x034, 1: not declined yet; a progress event over the other's)", $"034:{brutus}:71:1", Ev());
        EventEnd(brutus, 71, 0);
        Check("declined: Declined 1", "1/0", $"{woundsVar("Declined")}/{Status(LogJeuno, wounds)}");
        Talk(brutus);
        Check("Brutus again: event 71 with 0", $"034:{brutus}:71", Ev());
        EventEnd(brutus, 71, 1);
        Check("taken: accepted, Prog 1", "1/10/1", $"{QStat(LogJeuno, wounds)}/{woundsVar("Prog")}");
        uint osker = TalkTo("Osker");
        Check("Osker, Prog 1: event 103", $"032:{osker}:103", Ev());
        EventEnd(osker, 103);
        NoErrors();

        Console.WriteLine("Chocobo's Wounds: the chocobo fed");
        uint chocobo = TalkTo("Chocobo");
        Check("the chocobo: event 62", $"032:{chocobo}:62", Ev());
        EventEnd(chocobo, 62);
        TradeItems(chocobo, (greens, 1));
        Check("Gysahl Greens: event 76, kept", $"032:{chocobo}:76/1", $"{Ev()}/{Qty(greens)}");
        EventEnd(chocobo, 76);
        // (the feed, its event, Prog after it, the wildgrass left)
        var feeds = new (int Event, int Prog, int Left)[] { (57, 2, 8), (58, 3, 8), (99, 4, 7), (59, 5, 6), (60, 6, 5) };
        foreach (var (ev, prog, left) in feeds)
        {
            TradeItems(chocobo, (wildgrass, 1));
            Check($"a Gausebit Wildgrass: event {ev}", $"032:{chocobo}:{ev}", Ev());
            long sys = long.Parse(Lua("report(GetSystemTime())"));
            EventEnd(chocobo, ev);
            Check($"  Prog {prog}, the Timer 45 s on, the wildgrass left", $"{prog}/True/{left}",
                  $"{woundsVar("Prog")}/{long.TryParse(woundsVar("Timer"), out var t) && t >= sys + 45 && t <= sys + 46}/{Qty(wildgrass)}");
            TradeItems(chocobo, (wildgrass, 1));
            Check("  fed again within the 45 s: event 73", $"032:{chocobo}:73", Ev());
            EventEnd(chocobo, 73);
            Later(46);
            if (prog == 3)
            {
                Talk(osker);
                Check("  Osker, Prog 3: event 52", $"032:{osker}:52", Ev());
                EventEnd(osker, 52);
            }
        }
        TradeItems(chocobo, (wildgrass, 2));
        Check("two wildgrass past the third feed (tradeHasExactly one): nothing", "none", Ev());
        Warp(-88.0f, -7.65f, -166.0f, LowerJeuno);
        uint door = TalkTo("_6t2");
        Check("Lower Jeuno's _6t2 door, Prog 6: event 63", $"032:{door}:63", Ev());
        EventEnd(door, 63);
        Warp(-58.0f, 8.2f, 93.0f, UpperJeuno);
        Near(chocobo);
        TradeItems(chocobo, (wildgrass, 1));
        Check("the sixth feed: event 64", $"032:{chocobo}:64", Ev());
        EventEnd(chocobo, 64);
        Check("event 64 finished: completed, the wildgrass taken, the Chocobo License, the Chocobo Trainer title", "2/01/4/1/True/True",
              $"{QStat(LogJeuno, wounds)}/{Qty(wildgrass)}/{HasKi("CHOCOBO_LICENSE")}/{TitleNow("CHOCOBO_TRAINER")}");
        Check("  7 fame in each nation", "7/7/7", $"{FameOf("SANDORIA")}/{FameOf("BASTOK")}/{FameOf("WINDURST")}");
        NoErrors();

        Console.WriteLine("Chocobo on the Loose: the tracks, Hantileon, a Vana'diel day's wait");
        ushort egg = ItemIdOf("CHOCOBO_EGG_FAINTLY_WARM");
        Warp(-556.0f, 0.0f, 521.0f, LaTheine);
        uint tracks = TalkTo("Chocobo_Tracks");
        Check("La Theine's Chocobo Tracks: event 209", $"032:{tracks}:209", Ev());
        EventEnd(tracks, 209);
        Check("  Prog 1", "1", looseVar("Prog"));
        Warp(-58.0f, 8.2f, 93.0f, UpperJeuno);
        Talk(brutus);
        Check("Brutus, Prog 1: event 10095", $"032:{brutus}:10095", Ev());
        EventEnd(brutus, 10095);
        Warp(-2.675f, -1.1f, -103.0f, SouthernSandoria);
        uint hantileon = TalkTo("Hantileon");
        Check("Southern San d'Oria's Hantileon, Prog 2: event 821", $"032:{hantileon}:821", Ev());
        EventEnd(hantileon, 821);
        Warp(-58.0f, 8.2f, 93.0f, UpperJeuno);
        Talk(brutus);
        Check("Brutus, Prog 3: event 10100", $"032:{brutus}:10100", Ev());
        long day = long.Parse(Lua("report(VanadielUniqueDay())"));
        EventEnd(brutus, 10100);
        Check("  Prog 4, the Timer the next Vana'diel day, mustZone", $"4/{day + 1}/{UpperJeuno}", $"{looseVar("Prog")}/{looseVar("Timer")}/{looseVar("mustZone")}");
        Talk(brutus);
        Check("Brutus before a zone change: Chocobo's Wounds' event 22", $"032:{brutus}:22", Ev());
        EventEnd(brutus, 22);
        TakeZoneLine(LineTo(LowerJeuno));
        TakeZoneLine(LineTo(UpperJeuno));
        Check("zoned out and back: mustZone cleared", $"{UpperJeuno}/-", $"{c.Zone}/{looseVar("mustZone")}");
        Near(brutus);
        Talk(brutus);
        Check("Brutus the same Vana'diel day: event 22", $"032:{brutus}:22", Ev());
        EventEnd(brutus, 22);
        vanaTicks += VanaTime.DAY;
        Talk(brutus);
        Check("a Vana'diel day on: event 10109", $"032:{brutus}:10109", Ev());
        EventEnd(brutus, 10109);
        Check("event 10109 finished: completed, the egg, 7 more fame each", "2/01/1/14/14/14",
              $"{QStat(LogJeuno, loose)}/{Qty(egg)}/{FameOf("SANDORIA")}/{FameOf("BASTOK")}/{FameOf("WINDURST")}");
        NoErrors();

        Console.WriteLine("Path of the Beastmaster (the BST unlock)");
        int beastmaster = QuestId("jeuno", "PATH_OF_THE_BEASTMASTER");
        Lua($"player:completeQuest({LogJeuno}, xi.quest.id.jeuno.SAVE_MY_SON)");
        Talk(brutus);
        Check("Brutus with Save My Son done: event 70", $"032:{brutus}:70", Ev());
        EventEnd(brutus, 70);
        Check("event 70 finished: completed, BST unlocked, YOU_CAN_NOW_BECOME_A_BEASTMASTER, the gesture, the title, 7 more fame each",
              $"2/01/true/{Text(UpperJeuno, "YOU_CAN_NOW_BECOME_A_BEASTMASTER")}:0/1/True/True/21/21/21",
              $"{QStat(LogJeuno, beastmaster)}/{Lua("report(tostring(player:hasJob(xi.job.BST)))")}/{Special().Split(',')[0]}/{HasKi("JOB_GESTURE_BEASTMASTER")}/" +
              $"{TitleNow("ANIMAL_TRAINER")}/{FameOf("SANDORIA")}/{FameOf("BASTOK")}/{FameOf("WINDURST")}");
        Near(osker);
        Talk(osker);
        Check("Osker after it: event 45 (replaceDefault)", $"032:{osker}:45", Ev());
        EventEnd(osker, 45);
        NoErrors();

        Console.WriteLine("The Gobbiebag Part I: the inventory 30 -> 35");
        int gobbie = QuestId("jeuno", "THE_GOBBIEBAG_PART_I");
        Warp(-43.0f, 6.0f, -113.0f, LowerJeuno);
        uint bluffnix = TalkTo("Bluffnix");
        Check("Bluffnix: event 43 (0x034: the size + 1, available, the requirements met)", $"034:{bluffnix}:43:31,0,1", Ev());
        EventEnd(bluffnix, 43, 0);
        Check("taken (option 0): accepted", "1/10", QStat(LogJeuno, gobbie));
        TradeItems(bluffnix, (leather, 1), (ingot, 1));
        Check("two of the four items: event 43 (accepted)", $"034:{bluffnix}:43:31,1,1", Ev());
        EventEnd(bluffnix, 43);
        TradeItems(bluffnix, (leather, 1), (ingot, 1), (linen, 1), (peridot, 1));
        Check("the four: event 73 with the new size + 1", $"034:{bluffnix}:73:36", Ev());
        EventEnd(bluffnix, 73);
        var sizes = Of(0x01C);
        Check("event 73 finished: completed, the items taken, the inventory 35 (getContainerSize, char_storage, a 0x01C's 1 + 35)",
              $"2/01/0/35/35/True/{Text(LowerJeuno, "INVENTORY_INCREASED")}:0",
              $"{QStat(LogJeuno, gobbie)}/{Qty(peridot)}/{Lua("report(player:getContainerSize(xi.inv.INVENTORY))")}/" +
              $"{DbRow(db, "inventory", "FROM char_storage WHERE charid = ?")}/{sizes.Count > 0 && sizes[^1].Buffer[4] == 36}/{Special()}");
        NoErrors();

        Console.WriteLine("Path of the Bard (the BRD unlock)");
        int bard = QuestId("jeuno", "PATH_OF_THE_BARD");
        Lua($"player:completeQuest({LogJeuno}, xi.quest.id.jeuno.A_MINSTREL_IN_DESPAIR)");
        uint bki = TalkTo("Bki_Tbujhja");
        Check("Bki Tbujhja: event 182", $"032:{bki}:182", Ev());
        EventEnd(bki, 182);
        uint mertaire = TalkTo("Mertaire");
        Check("Mertaire: The Old Monument's event 102 (a progress event over the message)", $"032:{mertaire}:102", Ev());
        EventEnd(mertaire, 102);
        Talk(mertaire);
        var said = Of(0x02A);
        Check("Mertaire again: two messages on offer, the first's: messageName's showText COULD_HE_BE from him", $"none/{Text(LowerJeuno, "COULD_HE_BE")}:0/{mertaire}",
              $"{Ev()}/{Special()}/{(said.Count == 1 ? BitConverter.ToUInt32(said[0].Buffer, 4) : 0)}");
        string gil = Gil();
        Warp(-719.0f, -7.0f, 102.0f, ValkurmDunes);
        uint runes = TalkTo("Song_Runes");
        Check("the Song Runes: cutscene 2, the char locked", $"032:{runes}:2/2/True", $"{Ev()}/{Cur()}/{c.Locked}");
        EventEnd(runes, 2);
        Check("cutscene 2 finished: completed, BRD unlocked, UNLOCK_BARD, 3000 gil, the gesture, the Wandering Minstrel title",
              $"2/01/true/True/{int.Parse(gil) + 3000}/1/True/True/False",
              $"{QStat(LogJeuno, bard)}/{Lua("report(tostring(player:hasJob(xi.job.BRD)))")}/{Special().Contains($"{Text(ValkurmDunes, "UNLOCK_BARD")}:")}/" +
              $"{Gil()}/{HasKi("JOB_GESTURE_BARD")}/{TitleNow("WANDERING_MINSTREL")}/{c.Locked}");
        NoErrors();

        Console.WriteLine("In Defiant Challenge (limit break 1): Maat's offer");
        int defiant = QuestId("jeuno", "IN_DEFIANT_CHALLENGE");
        Warp(8.0f, 3.0f, 116.0f, RuludeGardens);
        uint maat = TalkTo("Maat");
        Check("Maat: event 79", $"032:{maat}:79", Ev());
        EventEnd(maat, 79, 1);
        Check("taken: accepted", "1/10", QStat(LogJeuno, defiant));
        Talk(maat);
        Check("Maat while accepted: event 80", $"032:{maat}:80", Ev());
        EventEnd(maat, 80);
        NoErrors();

        Console.WriteLine("In Defiant Challenge: three ??? in each of three dungeons");
        // (zone, the item its key items fuse into, the fusion text, the three ??? and where)
        var dungeons = new (ushort Zone, string Item, string Text, (string Qm, float X, float Y, float Z)[] Qms)[]
        {
            (CrawlersNest, "CLUMP_OF_EXORAY_MOLD", "COMBINE_INTO_A_CLUMP",
             new[] { ("qm10", -83.391f, -8.222f, 79.065f), ("qm11", 98.081f, -38.75f, -181.198f), ("qm12", 99.326f, -0.126f, -188.869f) }),
            (Garlaige, "CHUNK_OF_BOMB_COAL", "COMBINE_INTO_A_CHUNK",
             new[] { ("qm18", -13.425f, -1.176f, 191.669f), ("qm19", -50.175f, 6.264f, 251.669f), ("qm20", -137.047f, 0.0f, 347.502f) }),
            (Eldieme, "PIECE_OF_ANCIENT_PAPYRUS", "PUT_TOGUETHER_TO_COMPLETE",
             new[] { ("qm7", 252.824f, -32.0f, -64.803f), ("qm8", 105.275f, -32.0f, 92.551f), ("qm9", 92.272f, -32.0f, -64.676f) }),
        };
        var items = new List<ushort>();
        foreach (var (z, item, text, qms) in dungeons)
        {
            ushort itemId = ItemIdOf(item);
            items.Add(itemId);
            Warp(qms[0].X, qms[0].Y, qms[0].Z, z);
            for (int i = 0; i < 3; i++)
            {
                var (qm, x, y, zz) = qms[i];
                MoveTo(x, y, zz);
                Talk(NpcId(qm));
                if (i < 2) Check($"zone {z} {qm}: a key item", $"{Text(z, "KEYITEM_OBTAINED")}:", string.Join(",", Special().Split(':').Take(1)) + ":");
            }
            Check($"zone {z}: the third: the set fused, the item", $"True/True/1",
                  $"{Special().Contains($"{Text(z, text)}:")}/{Special().Contains($"{Text(z, "ITEM_OBTAINED")}:{itemId}")}/{Qty(itemId)}");
        }
        NoErrors();

        Console.WriteLine("In Defiant Challenge: the three items to Maat");
        Warp(8.0f, 3.0f, 116.0f, RuludeGardens);
        Near(maat);
        TradeItems(maat, items.Select(i => (i, 1u)).ToArray());
        Check("the three: event 81", $"032:{maat}:81", Ev());
        EventEnd(maat, 81);
        Check("event 81 finished: completed, the items taken, level cap 55 (char_jobs.genkai), the Horizon Breaker title", "2/01/0/55/55/True/True",
              $"{QStat(LogJeuno, defiant)}/{Qty(items[0])}/{Lua("report(player:getLevelCap())")}/{DbRow(db, "genkai", "FROM char_jobs WHERE charid = ?")}/{TitleNow("HORIZON_BREAKER")}");
        NoErrors();
    }

    // ---- Jeuno at night -------------------------------------------------------------------------------------------
    // The same char: Community Service (a quest of the Vana'diel hours: Zauko's offer at 19:00 taken through an event
    // update, the twelve street lamps lit (setAnimation, their event params from getAnimation and the clock), the
    // reward and the membership card; Lower Jeuno's onGameHour at 05:00 turning the lamps off and clearing the zone's
    // local vars; the card offered back by day).
    private static void JeunoNight()
    {
        CharId = 996013;
        if (c == null || c.Id != CharId) Login();
        Console.WriteLine("Community Service: Lower Jeuno at 19:00");
        int service = QuestId("jeuno", "COMMUNITY_SERVICE");
        string serviceVar(string name) => Var($"Quest[{LogJeuno}][{service}]{name}");
        long day0 = vanaTicks / VanaTime.DAY + 1;
        vanaTicks = day0 * VanaTime.DAY + 19 * VanaTime.HOUR;
        Warp(-15.0f, 0.0f, -10.0f, LowerJeuno);
        Check("in Lower Jeuno at 19:00", $"{LowerJeuno}/19", $"{c.Zone}/{Lua("report(VanadielHour())")}");
        uint zauko = TalkTo("Zauko");
        Check("Zauko between 18:00 and 23:00: event 116 (0x034: 0, not done before)", $"034:{zauko}:116", Ev());
        EventEnd(zauko, 116, 0, 1);
        var upd = Of(0x05C);
        Check("  the update: begun, updateEvent(1, 0), Prog 1, Count 12, the Timer today", $"1/10/1,0/1/12/{day0}",
              $"{Status(LogJeuno, service)}/{QuestBits(LogJeuno, service)}/" +
              $"{(upd.Count == 0 ? "-" : $"{BitConverter.ToInt32(upd[^1].Buffer, 4 + XI.Map.S2cPendingnum.NumOffset(0))},{BitConverter.ToInt32(upd[^1].Buffer, 4 + XI.Map.S2cPendingnum.NumOffset(1))}")}/" +
              $"{serviceVar("Prog")}/{serviceVar("Count")}/{serviceVar("Timer")}");
        EventEnd(zauko, 116);
        Talk(zauko);
        Check("Zauko while on it: event 114", $"032:{zauko}:114", Ev());
        EventEnd(zauko, 114);
        NoErrors();

        Console.WriteLine("Community Service: the twelve lamps lit");
        int closeDoor = int.Parse(Lua("report(xi.animation.CLOSE_DOOR)")), openDoor = int.Parse(Lua("report(xi.animation.OPEN_DOOR)"));
        for (int i = 0; i < 12; i++)
        {
            uint lamp = NpcId($"_l{i:D2}");
            var n = Npc(lamp);
            Near(lamp);
            Talk(lamp);
            Check($"lamp {i}: event {120 + i} (0x034: 1 to light it, the lamp)", $"034:{lamp}:{120 + i}:1" + (i > 0 ? $",{i}" : ""), Ev());
            EventEnd(lamp, 120 + i, 1);
            string msgs = Special();
            Check($"  lit: OPEN_DOOR, {11 - i} left", $"{openDoor}/{11 - i}/True", $"{n.Animation}/{(i == 11 ? "0" : serviceVar("Count"))}/" +
                  (i < 11 ? msgs.Contains($"{int.Parse(Text(LowerJeuno, "LAMP_MSG_OFFSET")) + 1}:{11 - i}") : msgs.Contains($"{Text(LowerJeuno, "LAMP_MSG_OFFSET")}:0")).ToString());
            if (i == 0)
            {
                Talk(lamp);
                Check("  the lit lamp again: event 120 with 2 (lit)", $"034:{lamp}:120:2", Ev());
                EventEnd(lamp, 120, 0);
            }
        }
        Check("all lit: Prog 2, the zone's allLightsLit", "2/1", $"{serviceVar("Prog")}/{Lua("report(player:getZone():getLocalVar('allLightsLit'))")}");
        NoErrors();

        Console.WriteLine("Community Service: the reward");
        Near(zauko);
        Talk(zauko);
        Check("Zauko, Prog 2: event 117 (0x034: 0, 0: the first time, no card)", $"034:{zauko}:117", Ev());
        string fame = FameOf("SANDORIA");
        EventEnd(zauko, 117, 1);
        Check("event 117 finished (option 1): completed, the Lamp Lighter's Membership Card, the Torchbearer title, 7 more fame",
              $"2/01/1/True/True/{int.Parse(fame) + 7}/1",
              $"{QStat(LogJeuno, service)}/{HasKi("LAMP_LIGHTERS_MEMBERSHIP_CARD")}/{TitleNow("TORCHBEARER")}/{FameOf("SANDORIA")}/" +
              $"{Lua("report(player:getZone():getLocalVar('commServiceComp'))")}");
        Talk(zauko);
        Check("Zauko after it, the same night: event 113", $"032:{zauko}:113", Ev());
        EventEnd(zauko, 113);
        NoErrors();

        Console.WriteLine("Community Service: 05:00, the lamps turned off by the zone's onGameHour");
        for (int h = 20; h <= 29; h++)
        {
            vanaTicks = day0 * VanaTime.DAY + h * VanaTime.HOUR;
            Tick(2400);
        }
        Check("05:00 the next day", "5", Lua("report(VanadielHour())"));
        Check("  the lamps CLOSE_DOOR, the zone's vars cleared", $"{closeDoor}/{closeDoor}/0/0",
              $"{Npc(NpcId("_l00")).Animation}/{Npc(NpcId("_l11")).Animation}/{Lua("report(player:getZone():getLocalVar('commServiceComp'))")}/" +
              $"{Lua("report(player:getZone():getLocalVar('allLightsLit'))")}");
        Talk(zauko);
        Check("Zauko by day with the card: event 118 (keep it?)", $"034:{zauko}:118:1", Ev());
        EventEnd(zauko, 118, 0);
        Check("  kept (option 0): the Wait today", $"1/{day0 + 1}", $"{HasKi("LAMP_LIGHTERS_MEMBERSHIP_CARD")}/{serviceVar("Wait")}");
        Talk(zauko);
        Check("  not asked again the same day: his default event 118 (0x032)", $"032:{zauko}:118", Ev());
        EventEnd(zauko, 118);
        NoErrors();
    }

    // ---- Other Areas -------------------------------------------------------------------------------------------------
    // A Bastokan WAR 10: The Old Lady (the subjob unlock: Vera's level gate (event 133 below 18, setLevel), the offer
    // taken with option 40, three trades with the next item in each event, unlockJob(0)) and Give a Moogle a Break (the
    // Mog House of the home nation: entered through Port Bastok's entrance line, a Bronze Bed placed (0x0FA; its
    // onFurniturePlaced sets bedPlacedTime and a mustZone local var), the Moogle's offer a zone change and a minute on,
    // the questSeen local var, the bow and ring traded, another minute: the mog safes 10 bigger).
    private const ushort Mhaura = 249, Selbina = 248;

    private static void OtherAreas()
    {
        ushort tail = ItemIdOf("WILD_RABBIT_TAIL"), saliva = ItemIdOf("CUP_OF_DHALMEL_SALIVA"), robe = ItemIdOf("BLOODY_ROBE");
        ushort bow = ItemIdOf("POWER_BOW"), ring = ItemIdOf("BEETLE_RING"), bed = ItemIdOf("BRONZE_BED");
        NewChar(996014, Mhaura, -51.0f, -4.0f, 17.0f, 1, 1, 10, (tail, 1), (saliva, 1), (robe, 1), (bow, 1), (ring, 1));
        Console.WriteLine("Other Areas: a WAR 10 in Mhaura");
        Check("in Mhaura, no subjob", $"{Mhaura}/none/false", $"{c.Zone}/{Cur()}/{Lua("report(tostring(player:hasJob(0)))")}");
        NoErrors("no script errors on the login");

        Console.WriteLine("The Old Lady (the subjob unlock): Vera");
        int oldLady = QuestId("otherAreas", "THE_OLD_LADY");
        string ladyVar(string name) => Var($"Quest[{LogOther}][{oldLady}]{name}");
        uint vera = TalkTo("Vera");
        Check("Vera below level 18: event 133", $"032:{vera}:133", Ev());
        EventEnd(vera, 133);
        Lua("player:setLevel(18)");
        Check("setLevel 18", "18", Lua("report(player:getMainLvl())"));
        Talk(vera);
        Check("Vera at 18: event 131 with the Wild Rabbit Tail", $"034:{vera}:131:{tail}", Ev());
        EventEnd(vera, 131, 1);
        Check("option 1: not taken", "0", Status(LogOther, oldLady));
        Talk(vera);
        EventEnd(vera, 131, 40);
        Check("option 40: accepted", "1/10", QStat(LogOther, oldLady));
        Talk(vera);
        Check("Vera while accepted: event 132 with the item she wants", $"034:{vera}:132:{tail}", Ev());
        EventEnd(vera, 132);
        TradeItems(vera, (saliva, 1));
        Check("the saliva first: nothing", "none", Ev());
        TradeItems(vera, (tail, 1));
        Check("the tail: event 135 with the next, the saliva", $"034:{vera}:135:{saliva}", Ev());
        EventEnd(vera, 135);
        Check("  tradeComplete, Prog 1", "0/1", $"{Qty(tail)}/{ladyVar("Prog")}");
        TradeItems(vera, (saliva, 1));
        Check("the saliva: event 136 with the robe", $"034:{vera}:136:{robe}", Ev());
        EventEnd(vera, 136);
        TradeItems(vera, (robe, 1));
        Check("the robe: event 137 (no next item)", $"032:{vera}:137", Ev());
        EventEnd(vera, 137);
        Check("event 137 finished: completed, the robe taken, SUBJOB_UNLOCKED, the subjob unlocked (char_jobs.unlocked bit 0)",
              $"2/01/0/{Text(Mhaura, "SUBJOB_UNLOCKED")}:0/true/True",
              $"{QStat(LogOther, oldLady)}/{Qty(robe)}/{Special()}/{Lua("report(tostring(player:hasJob(0)))")}/" +
              $"{(uint.Parse(DbRow(db, "unlocked", "FROM char_jobs WHERE charid = ?")) & 1u) != 0}");
        Talk(vera);
        Check("Vera after it: event 138 (replaceDefault)", $"032:{vera}:138", Ev());
        EventEnd(vera, 138);
        Lua("player:changesJob(xi.job.MNK)");
        Check("a subjob set", "2", Lua("report(player:getSubJob())"));
        Warp(-12.0f, -12.0f, 8.0f, Selbina);
        uint isacio = TalkTo("Isacio");
        Check("Selbina's Isacio after it: event 99", $"032:{isacio}:99", Ev());
        EventEnd(isacio, 99);
        NoErrors();

        Console.WriteLine("Give a Moogle a Break: Port Bastok's Mog House, a Bronze Bed placed");
        int moogle = QuestId("otherAreas", "GIVE_A_MOOGLE_A_BREAK");
        string moogleVar(string name) => Var($"Quest[{LogOther}][{moogle}]{name}");
        Lua("player:setFame(xi.fameArea.BASTOK, 150)");
        Check("Bastok fame level 3", "3", Lua("report(player:getFameLevel(xi.fameArea.BASTOK))"));
        Logout();
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,1,1,?,1)", BoundValue.Of(CharId), BoundValue.Of(bed));
        Sql(db, "UPDATE chars SET pos_zone = ?, pos_prevzone = ?, pos_x = 60, pos_y = 4, pos_z = -246 WHERE charid = ?",
            BoundValue.Of(PortBastok), BoundValue.Of(PortBastok), BoundValue.Of(CharId));
        Login();
        void EnterMogHouse()
        {
            var entrance = zone.ZoneInfoOf(c.Zone).ZoneLines.Values.First(l => ZoneLine.IdText(l.Id).StartsWith("zmr") && ZoneLine.IdText(l.Id) != "zmrq");
            TakeZoneLine(ZoneLine.IdText(entrance.Id));
        }
        // GP_CLI_COMMAND_MAPRECT: the mog house's exit line (zmrq), regular
        void LeaveMogHouse()
        {
            MoveTo(0, 0, 0);
            Send(0x05E, 24, b => BitConverter.GetBytes(ZoneLine.PackId("zmrq")).CopyTo(b, 4));
            zone.Tick();
            Login();
        }
        EnterMogHouse();
        Check("in the own Mog House in Port Bastok", $"{PortBastok}/{CharId}/true", $"{c.Zone}/{c.MoghouseId}/{Lua("report(tostring(player:inMogHouse()))")}");
        long sys = long.Parse(Lua("report(GetSystemTime())"));
        // GP_CLI_COMMAND_MYROOM_LAYOUT: the bed (mog safe slot 1) at 0,0
        s.PacketList.Clear();
        Send(0x0FA, 16, b =>
        {
            BitConverter.GetBytes(bed).CopyTo(b, 4);
            b[6] = 1; b[7] = 1;
        });
        Check("the bed placed: its onFurniturePlaced: bedPlacedTime now, the quest's mustZone local var", "True/1",
              $"{long.TryParse(moogleVar("bedPlacedTime"), out var placed) && placed >= sys && placed <= sys + 2}/{Lua($"report(player:getLocalVar('Quest[{LogOther}][{moogle}]mustZone'))")}");
        uint moogleNpc = NpcId("Moogle");
        void TalkMoogle()
        {
            Near(moogleNpc);
            Talk(moogleNpc);
        }
        TalkMoogle();
        Check("the Moogle at once: not the quest's offer", "False", Cur() == "30005");
        if (c.IsInEvent) Finish();
        LeaveMogHouse();
        EnterMogHouse();
        TalkMoogle();
        Check("a zone change on, within the minute: not yet", "False", Cur() == "30005");
        if (c.IsInEvent) Finish();
        Later(61);
        TalkMoogle();
        Check("a minute on: the Moogle's event 30005 (the bow, the ring)", $"034:{moogleNpc}:30005:0,0,0,5,0,{bow},{ring}", Ev());
        EndEvent(30005, 1);
        Check("taken (option 1): accepted, questSeen", "1/10/1", $"{QStat(LogOther, moogle)}/{Lua($"report(player:getLocalVar('Quest[{LogOther}][{moogle}]questSeen'))")}");
        TalkMoogle();
        Check("the Moogle again with questSeen: not the quest's", "False", Cur() is "30006" or "30005");
        if (c.IsInEvent) Finish();
        LeaveMogHouse();
        EnterMogHouse();
        TalkMoogle();
        Check("a zone change on: event 30006", $"034:{moogleNpc}:30006:0,0,0,0,0,{bow},{ring}", Ev());
        EndEvent(30006);
        TradeItems(moogleNpc, (bow, 1), (ring, 1));
        Check("the bow and the ring: event 30007", $"032:{moogleNpc}:30007", Ev());
        sys = long.Parse(Lua("report(GetSystemTime())"));
        EndEvent(30007);
        Check("  tradeComplete, Prog 1, the Timer a minute on", "0/0/1/True",
              $"{Qty(bow)}/{Qty(ring)}/{moogleVar("Prog")}/{long.TryParse(moogleVar("Timer"), out var tm) && tm >= sys + 60 && tm <= sys + 61}");
        LeaveMogHouse();
        EnterMogHouse();
        TalkMoogle();
        Check("within the minute: not yet", "False", Cur() == "30008");
        if (c.IsInEvent) Finish();
        LeaveMogHouse();
        EnterMogHouse();
        Later(61);
        string safe = Lua("report(player:getContainerSize(xi.inv.MOGSAFE))"), safe2 = Lua("report(player:getContainerSize(xi.inv.MOGSAFE2))");
        TalkMoogle();
        Check("a minute on: event 30008", $"032:{moogleNpc}:30008", Ev());
        EndEvent(30008);
        Check("event 30008 finished: completed, the Mog's Kind Master title, the mog safes 10 bigger", $"2/01/True/True/{int.Parse(safe) + 10}/{int.Parse(safe2) + 10}",
              $"{QStat(LogOther, moogle)}/{TitleNow("MOGS_KIND_MASTER")}/{Lua("report(player:getContainerSize(xi.inv.MOGSAFE))")}/{Lua("report(player:getContainerSize(xi.inv.MOGSAFE2))")}");
        LeaveMogHouse();
        NoErrors();
    }

    // ---- Outlands ---------------------------------------------------------------------------------------------------
    // A WAR 40 in Norg: Forge Your Destiny (the SAM unlock: Jaucribaix's offer with its params, Aeka's and Ranemaud's
    // items (findItem, the Option var bits), the Forger popped at Konschtat's blackened spot (checkDistance 1.6, the
    // mob claimed with updateClaim, its DESPAWN listener setting the ???'s cooldown), Guardian Treant popped in Zi'Tah
    // with a hatchet and killed (the quest's own onMobDeath), the sprig cut for the Sacred Branch, the trade and the
    // three-day GetSystemTime wait (its event param the hours left), Mumeito, SAM, Norg fame).
    private const ushort Norg = 252, Zitah = 121;

    private static void Outlands()
    {
        ushort branch = ItemIdOf("SACRED_BRANCH"), sprig = ItemIdOf("SACRED_SPRIG"), bomb = ItemIdOf("LUMP_OF_BOMB_STEEL"), oriental = ItemIdOf("LUMP_OF_ORIENTAL_STEEL");
        ushort hatchet = ItemIdOf("HATCHET"), mumeito = ItemIdOf("MUMEITO");
        NewChar(996015, Norg, 89.0f, -7.0f, -8.0f, 1, 1, 40, (hatchet, 1));
        Console.WriteLine("Outlands: a WAR 40 in Norg");
        Check("in Norg", $"{Norg}/none", $"{c.Zone}/{Cur()}");
        NoErrors("no script errors on the login");

        Console.WriteLine("Forge Your Destiny (the SAM unlock): the offer, Aeka's and Ranemaud's items");
        int forge = QuestId("outlands", "FORGE_YOUR_DESTINY");
        string forgeVar(string name) => Var($"Quest[{LogOutlands}][{forge}]{name}");
        uint jaucribaix = TalkTo("Jaucribaix");
        Check("Jaucribaix: event 25 (the branch, the bomb steel)", $"034:{jaucribaix}:25:{branch},{bomb}", Ev());
        EventEnd(jaucribaix, 25, 1);
        Check("taken: accepted", "1/10", QStat(LogOutlands, forge));
        Talk(jaucribaix);
        Check("Jaucribaix, no wait yet: event 26", $"032:{jaucribaix}:26", Ev());
        EventEnd(jaucribaix, 26);
        uint aeka = TalkTo("Aeka");
        Check("Aeka: event 44", $"034:{aeka}:44:{bomb},{oriental}", Ev());
        EventEnd(aeka, 44);
        Check("  the Lump of Oriental Steel, the Option bit 0", "1/1", $"{Qty(oriental)}/{forgeVar("Option")}");
        Talk(aeka);
        Check("Aeka with the steel (findItem): event 45", $"034:{aeka}:45:{bomb},{oriental}", Ev());
        EventEnd(aeka, 45);
        uint ranemaud = TalkTo("Ranemaud");
        Check("Ranemaud: event 40", $"034:{ranemaud}:40:{branch},{sprig}", Ev());
        EventEnd(ranemaud, 40);
        Check("  the Sacred Sprig, the Option bits 0 and 1", "1/3", $"{Qty(sprig)}/{forgeVar("Option")}");
        Talk(ranemaud);
        Check("Ranemaud with the sprig: event 41", $"032:{ranemaud}:41", Ev());
        EventEnd(ranemaud, 41);
        NoErrors();

        Console.WriteLine("Forge Your Destiny: the Forger at Konschtat's blackened spot");
        Warp(-707.0f, 2.7f, 102.3f, Konschtat);
        uint qm3 = NpcId("qm3");
        uint forgerId = uint.Parse(Lua("report(zones[xi.zone.KONSCHTAT_HIGHLANDS].mob.FORGER)"));
        var forger = Here.MobById[forgerId];
        Talk(qm3);
        Check("the blackened spot: BLACKENED_SHOULD_PLACE with the steel", $"7462:{oriental}", Special());
        MoveTo(-707.0f, 2.681f, 102.29f);
        TradeItems(qm3, (oriental, 1));
        Check("the steel traded from 2.2 away: BLACKENED_MUST_BE_CLOSER, kept", $"7465:0/1/False", $"{Special()}/{Qty(oriental)}/{forger.IsSpawned}");
        MoveTo(-708.0f, 2.681f, 102.29f);
        TradeItems(qm3, (oriental, 1));
        Check("from 1.2: the Forger spawned and claimed by the char (updateClaim), the steel taken, PLACE_BLACKENED_SPOT",
              $"True/{CharId}/0/7463:{oriental}", $"{forger.IsSpawned}/{forger.OwnerId}/{Qty(oriental)}/{Special()}");
        Talk(qm3);
        Check("the spot while it is up: NOT_THE_TIME_FOR_THAT", "7474:0", Special());
        KillByChar(forgerId);
        for (int i = 0; i < 60 && forger.IsSpawned; i++) Tick(1000);
        long sys = long.Parse(Lua("report(GetSystemTime())"));
        uint next = uint.Parse(Lua($"report(GetNPCByID({qm3}):getLocalVar('forgerNextPopAllowedTime'))"));
        Check("killed and despawned: its DESPAWN listener set the spot's cooldown 120 s on", "False/True", $"{forger.IsSpawned}/{next >= sys + 60 && next <= sys + 121}");
        Near(qm3);
        Talk(qm3);
        Check("the spot on its cooldown: BLACKENED_SPOT_ON_GROUND", "7461:0", Special());
        // the bomb steel the Forger drops (a drop roll): put in the inventory as the treasure pool would
        if (Qty(bomb) == "0") Lua($"player:addItem({bomb})");
        NoErrors();

        Console.WriteLine("Forge Your Destiny: Guardian Treant in Zi'Tah, the Sacred Branch");
        Warp(640.0f, -4.9f, -149.6f, Zitah);
        uint qm2 = NpcId("qm2");
        uint treantId = uint.Parse(Lua("report(zones[xi.zone.THE_SANCTUARY_OF_ZITAH].mob.GUARDIAN_TREANT)"));
        var treant = Here.MobById[treantId];
        Talk(qm2);
        Check("the branch: LOOKS_LIKE_STURDY_BRANCH with the hatchet", $"{Text(Zitah, "LOOKS_LIKE_STURDY_BRANCH")}:{hatchet}", Special());
        TradeItems(qm2, (hatchet, 1));
        Check("the hatchet, the sprig held: Guardian Treant spawned and claimed, the hatchet taken, SENSE_STRONG_EVIL_PRESENCE",
              $"True/{CharId}/0/{Text(Zitah, "SENSE_STRONG_EVIL_PRESENCE")}:0", $"{treant.IsSpawned}/{treant.OwnerId}/{Qty(hatchet)}/{Special()}");
        KillByChar(treantId);
        Check("killed: the quest's onMobDeath, Prog 1", "1", forgeVar("Prog"));
        for (int i = 0; i < 60 && treant.IsSpawned; i++) Tick(1000);
        Near(qm2);
        Talk(qm2);
        Check("the branch, Prog 1: NO_LONGER_SENSE_EVIL", $"{Text(Zitah, "NO_LONGER_SENSE_EVIL")}:0", Special());
        TradeItems(qm2, (sprig, 1));
        Check("the sprig traded: the Sacred Branch, Prog 2, STRANGE_FORCE_VANISHED", $"0/1/2/True",
              $"{Qty(sprig)}/{Qty(branch)}/{forgeVar("Prog")}/{Special().Contains($"{Text(Zitah, "STRANGE_FORCE_VANISHED")}:{branch}")}");
        NoErrors();

        Console.WriteLine("Forge Your Destiny: the forging, three Vana'diel days");
        Warp(89.0f, -7.0f, -8.0f, Norg);
        Near(jaucribaix);
        TradeItems(jaucribaix, (bomb, 1), (branch, 1));
        Check("the bomb steel and the branch: event 27", $"032:{jaucribaix}:27", Ev());
        sys = long.Parse(Lua("report(GetSystemTime())"));
        EventEnd(jaucribaix, 27);
        Check("  taken, the waitTime 10368 s on", "0/0/True", $"{Qty(bomb)}/{Qty(branch)}/{long.TryParse(forgeVar("waitTime"), out var w) && w >= sys + 10368 && w <= sys + 10369}");
        Later(3650);
        Talk(jaucribaix);
        // (10368 - 3650) / 144 = 46.65: the param the Lua number rounded (sol's get<uint32> is llround(lua_tonumber) under LuaJIT)
        Check("3650 s on: event 28 with the Vana'diel hours left, rounded", $"034:{jaucribaix}:28:47", Ev());
        EventEnd(jaucribaix, 28);
        Later(6720);
        Talk(jaucribaix);
        Check("the wait over: event 29 with Mumeito", $"034:{jaucribaix}:29:{mumeito}", Ev());
        EventEnd(jaucribaix, 29);
        Check("event 29 finished: completed, Mumeito, SAM unlocked, YOU_CAN_NOW_BECOME_A_SAMURAI, the gesture, the Bushido Blade title, Norg fame",
              $"2/01/1/true/True/1/True/True/20",
              $"{QStat(LogOutlands, forge)}/{Qty(mumeito)}/{Lua("report(tostring(player:hasJob(xi.job.SAM)))")}/" +
              $"{Special().Contains($"{Text(Norg, "YOU_CAN_NOW_BECOME_A_SAMURAI")}:{mumeito}")}/{HasKi("JOB_GESTURE_SAMURAI")}/{TitleNow("BUSHIDO_BLADE")}/{FameOf("NORG")}");
        NoErrors();
    }

    // ---- Aht Urhgan ----------------------------------------------------------------------------------------------
    // A WAR 40: No Strings Attached (the PUP unlock: Shamarhaan in Bastok Markets, Iruki-Waraki and Ghatsad in
    // Whitegate, the Arrapago Reef cutscene's key item, a Vana'diel day's wait, the automaton named through the event's
    // option (setPetName), the Harlequin attachments (unlockAttachment), the Animator lost and bought back with a gil
    // trade), Luck of the Draw (the COR unlock: Whitegate, Arrapago Reef, Talacca Cove's two NPCs) and An Empty Vessel
    // (the BLU unlock: Waoud's divination answered through event updates, a failed one paid for, the passing one's
    // randomly drawn item (read back from its var), the mustZone and day waits, the item shown, the cutscene from the
    // quest's onTriggerAreaEnter in Aydeewa Subterrane walked into, its setPos back to Whitegate).
    private const ushort Whitegate = 50, Arrapago = 54, Talacca = 57, Aydeewa = 68;

    // a GM's !pos to the NPC of the zone
    private static void WarpTo(ushort zoneId, string npc)
    {
        var n = zone.ZoneInfoOf(zoneId).Npcs.First(x => x.Name == npc);
        Warp(n.X + 1.5f, n.Y, n.Z, zoneId);
    }

    private static void AhtUrhgan()
    {
        ushort animator = ItemIdOf("ANIMATOR"), die = ItemIdOf("CORSAIR_DIE");
        NewChar(996016, BastokMarkets, -283.0f, -13.0f, -84.7f, 1, 1, 40);
        Console.WriteLine("Aht Urhgan: a WAR 40 in Bastok Markets");
        NoErrors("no script errors on the login");

        Console.WriteLine("No Strings Attached (the PUP unlock)");
        int strings = QuestId("ahtUrhgan", "NO_STRINGS_ATTACHED");
        string stringsVar(string name) => Var($"Quest[{LogAhtUrhgan}][{strings}]{name}");
        uint shamarhaan = TalkTo("Shamarhaan");
        Check("Shamarhaan: event 434", $"032:{shamarhaan}:434", Ev());
        EventEnd(shamarhaan, 434);
        Talk(shamarhaan);
        Check("  Option 1; Shamarhaan again: event 435", $"1/032:{shamarhaan}:435", $"{stringsVar("Option")}/{Ev()}");
        EventEnd(shamarhaan, 435);
        WarpTo(Whitegate, "Iruki-Waraki");
        uint iruki = TalkTo("Iruki-Waraki");
        Check("Whitegate's Iruki-Waraki: event 260", $"032:{iruki}:260", Ev());
        EventEnd(iruki, 260);
        Check("accepted", "1/10", QStat(LogAhtUrhgan, strings));
        uint ghatsad = TalkTo("Ghatsad");
        Check("Ghatsad: event 262", $"032:{ghatsad}:262", Ev());
        EventEnd(ghatsad, 262);
        Talk(ghatsad);
        Check("Ghatsad, Prog 1: event 263", $"032:{ghatsad}:263", Ev());
        EventEnd(ghatsad, 263);
        WarpTo(Arrapago, "qm10");
        uint qm10 = TalkTo("qm10");
        Check("Arrapago Reef's qm10: cutscene 214", $"032:{qm10}:214/True", $"{Ev()}/{c.Locked}");
        EventEnd(qm10, 214);
        Check("  the Antique Automaton, Prog 2", "1/2", $"{HasKi("ANTIQUE_AUTOMATON")}/{stringsVar("Prog")}");
        WarpTo(Whitegate, "Ghatsad");
        Talk(ghatsad);
        Check("Ghatsad with it: event 264", $"032:{ghatsad}:264", Ev());
        long day = long.Parse(Lua("report(VanadielUniqueDay())"));
        EventEnd(ghatsad, 264);
        Check("  the key item taken, Prog 3, the Timer tomorrow", $"0/3/{day + 1}", $"{HasKi("ANTIQUE_AUTOMATON")}/{stringsVar("Prog")}/{stringsVar("Timer")}");
        Talk(ghatsad);
        Check("Ghatsad the same day: not event 265", "False", Ev().EndsWith(":265"));
        if (c.IsInEvent) Finish();
        vanaTicks += VanaTime.DAY;
        Talk(ghatsad);
        Check("a day on: event 265", $"032:{ghatsad}:265", Ev());
        EventEnd(ghatsad, 265);
        Near(iruki);
        Talk(iruki);
        Check("Iruki-Waraki, Prog 4: event 266", $"032:{iruki}:266", Ev());
        EventEnd(iruki, 266, 5);
        Check("event 266 finished (option 5): completed, PUP unlocked, YOU_CAN_BECOME_PUP, the Animator, the gesture, the title",
              $"2/01/true/True/1/1/True/True",
              $"{QStat(LogAhtUrhgan, strings)}/{Lua("report(tostring(player:hasJob(xi.job.PUP)))")}/{Special().Contains($"{Text(Whitegate, "YOU_CAN_BECOME_PUP")}:")}/" +
              $"{Qty(animator)}/{HasKi("JOB_GESTURE_PUPPETMASTER")}/{TitleNow("PROUD_AUTOMATON_OWNER")}");
        Check("  the automaton's name 5 + 118 (char_pet.automatonid), the Harlequin frame and head unlocked", "123/true/true",
              $"{DbRow(db, "automatonid", "FROM char_pet WHERE charid = ?")}/{Lua("report(tostring(player:hasAttachment(xi.item.HARLEQUIN_FRAME)))")}/" +
              $"{Lua("report(tostring(player:hasAttachment(xi.item.HARLEQUIN_HEAD)))")}");
        Talk(iruki);
        Check("Iruki-Waraki with the Animator: event 267", $"032:{iruki}:267", Ev());
        EventEnd(iruki, 267);
        Lua($"player:delItem({animator}, 1)");
        Talk(iruki);
        // a low priority action of the quests' alternates with the NPC's own script (the interaction fallback): the
        // last talk was the quests', this one is his script's
        Check("the Animator lost: his own script's event 267 this time (the fallback's turn)", $"032:{iruki}:267", Ev());
        EventEnd(iruki, 267);
        Talk(iruki);
        Check("the Animator lost: messageName YOU_LOST_YOUR_ANIMATOR (showText with the item)", $"none/{Text(Whitegate, "YOU_LOST_YOUR_ANIMATOR")}:{animator}",
              $"{Ev()}/{Special()}");
        string gil = Gil();
        TradeItems(iruki, (65535, 10000));
        Check("10000 gil traded: the gil taken, the Animator, YOU_BETTER_NOT_LOSE_IT_AGAIN", $"{int.Parse(gil) - 10000}/1/True",
              $"{Gil()}/{Qty(animator)}/{Special().Contains($"{Text(Whitegate, "YOU_BETTER_NOT_LOSE_IT_AGAIN")}:0")}");
        NoErrors();

        Console.WriteLine("Luck of the Draw (the COR unlock)");
        int luck = QuestId("ahtUrhgan", "LUCK_OF_THE_DRAW");
        string luckVar(string name) => Var($"Quest[{LogAhtUrhgan}][{luck}]{name}");
        uint ratihb = TalkTo("Ratihb");
        Check("Ratihb: event 547", $"032:{ratihb}:547", Ev());
        EventEnd(ratihb, 547);
        Check("accepted, Prog 1", "1/10/1", $"{QStat(LogAhtUrhgan, luck)}/{luckVar("Prog")}");
        uint mafwahb = TalkTo("Mafwahb");
        Check("Mafwahb: event 548", $"032:{mafwahb}:548", Ev());
        EventEnd(mafwahb, 548);
        WarpTo(Arrapago, "qm6");
        uint qm6 = TalkTo("qm6");
        Check("Arrapago Reef's qm6: cutscene 211", $"032:{qm6}:211", Ev());
        EventEnd(qm6, 211);
        WarpTo(Talacca, "qm1");
        uint tqm1 = TalkTo("qm1");
        Check("Talacca Cove's qm1, Prog 3: event 2", $"032:{tqm1}:2", Ev());
        EventEnd(tqm1, 2);
        Check("  the Forgotten Hexagun, Prog 4", "1/4", $"{HasKi("FORGOTTEN_HEXAGUN")}/{luckVar("Prog")}");
        uint slab = TalkTo("_1l0");
        Check("the rock slab: event 3", $"032:{slab}:3", Ev());
        EventEnd(slab, 3);
        Check("event 3 finished: completed, COR unlocked, the hexagun taken, the Corsair Die, the gesture, the title", "2/01/true/0/1/1/True/True",
              $"{QStat(LogAhtUrhgan, luck)}/{Lua("report(tostring(player:hasJob(xi.job.COR)))")}/{HasKi("FORGOTTEN_HEXAGUN")}/{Qty(die)}/" +
              $"{HasKi("JOB_GESTURE_CORSAIR")}/{TitleNow("SEAGULL_PHRATRIE_CREW_MEMBER")}");
        WarpTo(Whitegate, "Ratihb");
        Talk(ratihb);
        Check("Ratihb after it: event 552", $"032:{ratihb}:552", Ev());
        EventEnd(ratihb, 552);
        Check("  Equipped for All Occasions' Stage 1", "1", Var($"Quest[{LogAhtUrhgan}][{QuestId("ahtUrhgan", "EQUIPPED_FOR_ALL_OCCASIONS")}]Stage"));
        NoErrors();

        Console.WriteLine("An Empty Vessel (the BLU unlock): a failed divination, paid for");
        int vessel = QuestId("ahtUrhgan", "AN_EMPTY_VESSEL");
        string vesselVar(string name) => Var($"Quest[{LogAhtUrhgan}][{vessel}]{name}");
        string UpdateParams()
        {
            var u = Of(0x05C);
            return u.Count == 0 ? "-" : string.Join(",", Enumerable.Range(0, 8).Select(k => BitConverter.ToInt32(u[^1].Buffer, 4 + XI.Map.S2cPendingnum.NumOffset(k))));
        }
        Lua("player:addGil(20000)");
        uint waoud = TalkTo("Waoud");
        gil = Gil();
        Check("Waoud: event 60 with the gil", $"034:{waoud}:60:{gil}", Ev());
        EventEnd(waoud, 60, 1, 1);
        EventEnd(waoud, 60, 40, 1);
        Check("  one wrong answer, then the results (update 40): Spring (10)", $"{gil},0,0,0,0,0,0,10", UpdateParams());
        EventEnd(waoud, 60, 45);
        day = long.Parse(Lua("report(VanadielUniqueDay())"));
        Check("finished with 45: 1000 gil paid, PAY_DIVINATION, mustZone, the Timer tomorrow, not accepted",
              $"{int.Parse(gil) - 1000}/{Text(Whitegate, "PAY_DIVINATION")}:0/{Whitegate}/{day + 1}/0",
              $"{Gil()}/{Special()}/{vesselVar("mustZone")}/{vesselVar("Timer")}/{Status(LogAhtUrhgan, vessel)}");
        Talk(waoud);
        Check("Waoud before a zone and a day: event 63", $"032:{waoud}:63", Ev());
        EventEnd(waoud, 63);
        NoErrors();

        Console.WriteLine("An Empty Vessel: the divination passed");
        vanaTicks += VanaTime.DAY;
        ZoneOutAndBack();
        Near(waoud);
        Talk(waoud);
        gil = Gil();
        Check("a day and a zone on: event 60", $"034:{waoud}:60:{gil}", Ev());
        foreach (uint answer in new uint[] { 2, 6, 9, 12, 13, 18, 21, 24, 26, 30 }) EventEnd(waoud, 60, answer, 1);
        Check("  the ten right answers counted", "10", Lua($"report(player:getLocalVar('Quest[{LogAhtUrhgan}][{vessel}]numCorrect'))"));
        EventEnd(waoud, 60, 40, 1);
        string option = vesselVar("Option");
        Check("  the results: an item drawn (1-3, kept in Option), updateEvent(gil, .., the item, 70)", $"True/{gil},0,0,0,0,0,{option},70",
              $"{option is "1" or "2" or "3"}/{UpdateParams()}");
        EventEnd(waoud, 60, 50);
        day = long.Parse(Lua("report(VanadielUniqueDay())"));
        Check("finished with 50: accepted, mustZone, the Timer tomorrow, numCorrect reset", $"1/10/{Whitegate}/{day + 1}/0",
              $"{QStat(LogAhtUrhgan, vessel)}/{vesselVar("mustZone")}/{vesselVar("Timer")}/{Lua($"report(player:getLocalVar('Quest[{LogAhtUrhgan}][{vessel}]numCorrect'))")}");
        Talk(waoud);
        Check("Waoud on the waits: event 64", $"032:{waoud}:64", Ev());
        EventEnd(waoud, 64);
        vanaTicks += VanaTime.DAY;
        ZoneOutAndBack();
        Near(waoud);
        Talk(waoud);
        Check("a day and a zone on: event 65", $"032:{waoud}:65", Ev());
        EventEnd(waoud, 65);
        Talk(waoud);
        Check("Prog 1: event 66 with the item's index in param 6", $"034:{waoud}:66:0,0,0,0,0,0,{option}", Ev());
        EventEnd(waoud, 66);
        string[] itemNames = { "SIRENS_TEAR", "PINCH_OF_VALKURM_SUNSAND", "DANGRUF_STONE" };
        ushort item = ItemIdOf(itemNames[int.Parse(option) - 1]);
        Lua($"player:addItem({item})");
        TradeItems(waoud, (item, 1));
        Check("the item shown: event 67", $"034:{waoud}:67:0,0,0,0,0,0,{option}", Ev());
        EventEnd(waoud, 67);
        Check("  kept, Prog 2", "1/2", $"{Qty(item)}/{vesselVar("Prog")}");
        NoErrors();

        Console.WriteLine("An Empty Vessel: Aydeewa Subterrane's trigger area");
        Warp(370.0f, 0.0f, 340.0f, Aydeewa);
        Check("in Aydeewa Subterrane, out of the area", $"{Aydeewa}/none", $"{c.Zone}/{Cur()}");
        // xi.player.onGameIn's ZoningIn: no trigger areas for 2.5 s after the zone-in
        for (int i = 0; i < 3; i++) Tick(1000);
        MoveTo(380.0f, 0.0f, 340.0f);
        for (int i = 0; i < 3 && !c.IsInEvent; i++) Tick(400);
        Check("walked into area 1: the quest's onTriggerAreaEnter, cutscene 3 with the item", $"3/034:{CharId}:3:{item}",
              $"{Cur()}/{Ev()}");
        EndEvent(3, 13);
        Rezone();
        Check("cutscene 3 finished (13): completed, BLU unlocked, the Mark of Zahak, the gesture, the title, setPos to Whitegate",
              $"2/01/true/1/1/True/True/{Whitegate}/1",
              $"{QStat(LogAhtUrhgan, vessel)}/{Lua("report(tostring(player:hasJob(xi.job.BLU)))")}/{HasKi("MARK_OF_ZAHAK")}/{HasKi("JOB_GESTURE_BLUE_MAGE")}/" +
              $"{TitleNow("BEARER_OF_THE_MARK_OF_ZAHAK")}/{c.Zone}/{vesselVar("completeEvent")}");
        uint waoudId = NpcId("Waoud");
        Near(waoudId);
        Talk(waoudId);
        Check("Waoud after it: event 69", $"032:{waoudId}:69", Ev());
        EventEnd(waoudId, 69, 1);
        Check("  completeEvent cleared", "-", vesselVar("completeEvent"));
        NoErrors();
    }

    // ---- Crafting guilds ------------------------------------------------------------------------------------------
    // A WAR 10 Bastokan: the Goldsmiths' Guild in Bastok Markets. Reinberta (the guild master: signed up for the
    // crystal and the Guild_Member bit, the rank test item named once the skill is near its cap, the Copper Hairpin
    // traded for the Recruit rank: setSkillRank, char_skills) and Ellard (guild points: the trading contract formed
    // with the event's -1 option, the day's item traded (getCurrentGPItem / addGuildPoints / confirmSlot, the points
    // and the daily cap var to JST midnight), a belt and a key item bought through event updates (delCurrency), the
    // contract moved to another guild barring the day's points).
    private static void Crafting()
    {
        ushort hairpin = ItemIdOf("COPPER_HAIRPIN"), fire = ItemIdOf("FIRE_CRYSTAL"), belt = ItemIdOf("GOLDSMITHS_BELT");
        NewChar(996017, BastokMarkets, -188.0f, -7.8f, -59.4f, 1, 1, 10, (hairpin, 1));
        Console.WriteLine("Crafting: a WAR 10 in Bastok Markets, by the Goldsmiths' Guild");
        int skill = int.Parse(Lua("report(xi.skill.GOLDSMITHING)")), guild = int.Parse(Lua("report(xi.guild.GOLDSMITHING)"));
        NoErrors("no script errors on the login");

        Console.WriteLine("Reinberta: signing up, the rank test");
        uint reinberta = TalkTo("Reinberta");
        string ev = Ev();
        Check("Reinberta: event 300 (the time for no test yet, skill 0, cap 10, no guild joined)", $"034:{reinberta}:300/0,10", $"{string.Join(":", ev.Split(':').Take(3))}/{string.Join(",", ev.Split(':')[3].Split(',').Skip(1).Take(3))}");
        EventEnd(reinberta, 300, 1);
        Check("signed up (option 1): a Fire Crystal, ITEM_OBTAINED, Guild_Member's goldsmithing bit", $"1/{Text(BastokMarkets, "ITEM_OBTAINED")}:{fire}/{1 << guild}",
              $"{Qty(fire)}/{Special()}/{Var("Guild_Member")}");
        Lua($"player:setSkillLevel({skill}, 80)");
        Talk(reinberta);
        Check("skill 8.0 of 10: event 300 naming the Copper Hairpin, the guild joined, no artisan craft (the renounce mask 510)",
              $"034:{reinberta}:300:{hairpin},8,10,{1 << guild},0,0,0,510", Ev());
        EventEnd(reinberta, 300);
        TradeItems(reinberta, (hairpin, 1));
        Check("the hairpin traded: event 301 with the new rank 1", $"034:{reinberta}:301:0,0,0,0,1", Ev());
        EventEnd(reinberta, 301);
        Check("event 301 finished: the hairpin taken, rank 1 (getSkillRank, char_skills.rank)", "0/1/1",
              $"{Qty(hairpin)}/{Lua($"report(player:getSkillRank({skill}))")}/{DbRow(db, "rank", $"FROM char_skills WHERE charid = ? AND skillid = {skill}")}");
        NoErrors();

        Console.WriteLine("Ellard: the guild points contract");
        // a GM's ranks: the contract wants rank 3 (Initiate) or more
        Lua($"player:setSkillRank({skill}, 3) player:setSkillLevel({skill}, 350)");
        uint ellard = TalkTo("Ellard");
        string gp = Lua($"local item, left = player:getCurrentGPItem({guild}) report(item) report(left)");
        ushort gpItem = ushort.Parse(gp.Split(',')[0]);
        int gpLeft = int.Parse(gp.Split(',')[1]);
        Check("Ellard: event 340 (no points, no contract (-1), the day's item and its points, cap 40, the key items on sale)",
              $"034:{ellard}:340:0,{uint.MaxValue},{gpItem},{gpLeft},40,0,31", Ev());
        EventEnd(ellard, 340, uint.MaxValue);
        Check("finished with -1: the contract (currentGuild), GUILD_NEW_CONTRACT", $"{guild + 1}/{Text(BastokMarkets, "GUILD_NEW_CONTRACT")}:{guild}",
              $"{Var("[GUILD]currentGuild")}/{Special()}");
        NoErrors();

        Console.WriteLine("Ellard: the day's item traded for points");
        int pointsPer = int.Parse(DbScalar($"SELECT points FROM guild_item_points WHERE guildid = {guild} AND itemid = {gpItem} AND max_points = {gpLeft} LIMIT 1"));
        // two of it (a stack, or two slots of an item that does not stack)
        Lua($"player:addItem({gpItem}, 2)");
        TradeItems(ellard, (gpItem, 2));
        int earned = Math.Min(2 * pointsPer, gpLeft);
        Check("two traded: event 341 with the points", $"034:{ellard}:341:{earned}", Ev());
        EventEnd(ellard, 341);
        Check("  the items taken, the points (guild_goldsmithing), the day's tally to JST midnight, the points left",
              $"0/{earned}/{earned}/True/{gpLeft - earned}",
              $"{Qty(gpItem)}/{Lua("report(player:getCurrency('guild_goldsmithing'))")}/{Var("[GUILD]daily_points")}/" +
              $"{DbScalar("SELECT expiry FROM char_vars WHERE charid = ? AND varname = '[GUILD]daily_points'") == Lua("report(JstMidnight())")}/" +
              $"{Lua($"local _, left = player:getCurrentGPItem({guild}) report(left)")}");
        NoErrors();

        Console.WriteLine("Ellard: a belt and a key item bought");
        Lua("player:addCurrency('guild_goldsmithing', 60000)");
        int points = int.Parse(Lua("report(player:getCurrency('guild_goldsmithing'))"));
        Talk(ellard);
        // category 1, index 0 (the Goldsmith's Belt), quantity 1: 1 << 9 | 1 << 2
        EventEnd(ellard, 340, 1 << 9 | 1 << 2, 1);
        var upd = Of(0x05C);
        Check("the belt (update 516): ITEM_OBTAINED, 10000 points spent, updateEvent(points, guild, the cost ..)",
              $"1/{Text(BastokMarkets, "ITEM_OBTAINED")}:{belt}/{points - 10000}/{points - 10000},{guild},10000",
              $"{Qty(belt)}/{Special()}/{Lua("report(player:getCurrency('guild_goldsmithing'))")}/" +
              $"{(upd.Count == 0 ? "-" : string.Join(",", Enumerable.Range(0, 3).Select(k => BitConverter.ToInt32(upd[^1].Buffer, 4 + XI.Map.S2cPendingnum.NumOffset(k)))))}");
        // category 3, key item 0 (Gold Purification, Novice, 40000): 3 << 2 | 1 << 5
        EventEnd(ellard, 340, 3 << 2 | 1 << 5, 1);
        Check("Gold Purification (update 44): the key item, 40000 points spent", $"1/{points - 50000}",
              $"{HasKi("GOLD_PURIFICATION")}/{Lua("report(player:getCurrency('guild_goldsmithing'))")}");
        EventEnd(ellard, 340, 3 << 2 | 1 << 5, 1);
        Check("again with too few points: NOT_HAVE_ENOUGH_GP (messageText, 0x036), nothing spent", $"True/{points - 50000}",
              $"{Of(0x036).Exists(p => (BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF) == int.Parse(Text(BastokMarkets, "NOT_HAVE_ENOUGH_GP")))}/{Lua("report(player:getCurrency('guild_goldsmithing'))")}");
        EventEnd(ellard, 340);
        NoErrors();

        Console.WriteLine("Bastok Mines' Hemewmew: the contract moved to the Alchemists, no points for the day");
        int alchemy = int.Parse(Lua("report(xi.guild.ALCHEMY)"));
        Lua("player:setSkillRank(xi.skill.ALCHEMY, 3) player:setSkillLevel(xi.skill.ALCHEMY, 350)");
        TakeZoneLine(LineTo(BastokMines));
        uint hemewmew = TalkTo("Hemewmew");
        Check("Hemewmew: event 206", $"{hemewmew}:206", EvId());
        EventEnd(hemewmew, 206, uint.MaxValue);
        Check("finished with -1: GUILD_TERMINATE_CONTRACT (the new guild, the old), the contract moved, the day's tally 1 to JST midnight",
              $"{Text(BastokMines, "GUILD_TERMINATE_CONTRACT")}:{alchemy}/{alchemy + 1}/1/True",
              $"{Special()}/{Var("[GUILD]currentGuild")}/{Var("[GUILD]daily_points")}/" +
              $"{DbScalar("SELECT expiry FROM char_vars WHERE charid = ? AND varname = '[GUILD]daily_points'") == Lua("report(JstMidnight())")}");
        ushort alchemyItem = ushort.Parse(Lua($"local item, left = player:getCurrentGPItem({alchemy}) report(item)"));
        Check("  the day's alchemy item worth nothing today", "0", Lua($"local item, left = player:getCurrentGPItem({alchemy}) report(left)"));
        Lua($"player:addItem({alchemyItem})");
        TradeItems(hemewmew, (alchemyItem, 1));
        var said = Of(0x036);
        Check("its item traded: no event, NO_MORE_GP_ELIGIBLE (messageText), kept", $"none/True/1",
              $"{Ev()}/{said.Exists(p => (BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF) == int.Parse(Text(BastokMines, "NO_MORE_GP_ELIGIBLE")))}/{Qty(alchemyItem)}");
        NoErrors();
    }

    // ---- Crystal War and Adoulin ----------------------------------------------------------------------------------
    // A Bastokan BLM 40: A Little Knowledge (the SCH unlock in the past: Rolanberries traded by the stack to Tucker
    // (confirmItem of whole stacks, a stack over four handed back), the twelve vellum to Erlene, Manafont used (the
    // 0x01A job ability) to finish; as a SCH, Embrava and Kaustra) and Dances with Luopans (the GEO unlock: Sylvie's
    // offer declined then taken, the homeland soil at Konschtat's Ergon Locus, a Petrified Log for the Luopan, /heal
    // (0x0E8) inside Ceizak's locus trigger area: the healing effect's hook, the quest's timer after a random rest,
    // Indi-Poison and the Matre Bell).
    private const ushort EldiemeS = 175, CrawlersNestS = 171, WesternAdoulin = 256, Ceizak = 261;

    // the time passes on both clocks: the zone's ticks (the timers) and the wall clock (GetSystemTime)
    private static void Wait(int seconds)
    {
        for (int i = 0; i < seconds; i++)
        {
            Later(1);
            Tick(1000);
        }
    }

    private static void CrystalWarAdoulin()
    {
        ushort berry = ItemIdOf("ROLANBERRY"), vellum = ItemIdOf("SHEET_OF_VELLUM"), petrifiedLog = ItemIdOf("PETRIFIED_LOG");
        var items = Enumerable.Repeat((berry, 12u), 13).Append((petrifiedLog, 1u)).ToArray();
        NewChar(996018, EldiemeS, 374.0f, -40.0f, 18.0f, 1, 4, 40, items);
        Console.WriteLine("Crystal War: a BLM 40 in The Eldieme Necropolis [S]");
        Check("in Eldieme [S]", $"{EldiemeS}/4/none", $"{c.Zone}/{c.MJob}/{Cur()}");
        NoErrors("no script errors on the login");

        Console.WriteLine("A Little Knowledge (the SCH unlock): Erlene, Tucker's rolanberries");
        int knowledge = QuestId("crystalWar", "A_LITTLE_KNOWLEDGE");
        string knowVar(string name) => Var($"Quest[{LogCrystalWar}][{knowledge}]{name}");
        uint erlene = TalkTo("Erlene");
        Check("Erlene: event 10 (1)", $"034:{erlene}:10:1", Ev());
        EventEnd(erlene, 10, 0);
        Check("accepted", "1/10", QStat(LogCrystalWar, knowledge));
        Talk(erlene);
        Check("Erlene before the vellum: event 11", $"032:{erlene}:11", Ev());
        EventEnd(erlene, 11);
        WarpTo(CrawlersNestS, "Tucker");
        uint tucker = TalkTo("Tucker");
        Check("Crawler's Nest [S]'s Tucker: event 6", $"032:{tucker}:6", Ev());
        EventEnd(tucker, 6);
        Talk(tucker);
        Check("  Option 1; Tucker again: event 7", $"1/032:{tucker}:7", $"{knowVar("Option")}/{Ev()}");
        EventEnd(tucker, 7);
        byte[] BerrySlots(int n) => c.Items.Where(i => i.Location == Container.Inventory && i.ItemId == berry).OrderBy(i => i.Slot).Take(n).Select(i => i.Slot).ToArray();
        Trade(tucker, (BerrySlots(1)[0], 11));
        Check("eleven rolanberries: event 14 (not a stack)", $"032:{tucker}:14", Ev());
        EventEnd(tucker, 14);
        // (the stacks traded, the event, the vellum after, the berries left)
        var trades = new (int Stacks, int Event, int Vellum, int Berries)[] { (4, 8, 4, 108), (5, 10, 8, 60), (4, 11, 12, 12) };
        int option = 1;
        foreach (var (stacks, ev, vel, left) in trades)
        {
            Trade(tucker, BerrySlots(stacks).Select(sl => (sl, 12u)).ToArray());
            Check($"{stacks} stacks: event {ev}", $"032:{tucker}:{ev}", Ev());
            EventEnd(tucker, ev);
            option++;
            Check($"  four stacks taken at most (confirmItem), {vel} vellum, Option {option}", $"{left}/{vel}/{option}",
                  $"{Qty(berry)}/{Qty(vellum)}/{knowVar("Option")}");
        }
        Talk(tucker);
        Check("Tucker, Option 4: event 12", $"032:{tucker}:12", Ev());
        EventEnd(tucker, 12);
        NoErrors();

        Console.WriteLine("A Little Knowledge: the vellum to Erlene, Manafont");
        WarpTo(EldiemeS, "Erlene");
        TradeItems(erlene, (vellum, 12));
        Check("the twelve vellum: event 12 (a mage job: 1)", $"034:{erlene}:12:1", Ev());
        EventEnd(erlene, 12);
        Check("  taken, Prog 1", "0/1", $"{Qty(vellum)}/{knowVar("Prog")}");
        Talk(erlene);
        Check("Erlene without a two-hour effect: event 13", $"032:{erlene}:13", Ev());
        EventEnd(erlene, 13);
        // GP_CLI_COMMAND_ACTION JobAbility: Manafont on the char itself
        s.PacketList.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(c.Id).CopyTo(b, 4);
            BitConverter.GetBytes(c.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.JobAbility).CopyTo(b, C2sAction.OffActionID);
            C2sAction.SetSkillId(b, ushort.Parse(Lua("report(xi.jobAbility.MANAFONT)")));
        });
        for (int i = 0; i < 3; i++) Tick(500);
        Check("Manafont used: the Manafont effect", "true", Lua("report(tostring(player:hasStatusEffect(xi.effect.MANAFONT)))"));
        Talk(erlene);
        Check("Erlene under Manafont: event 14", $"032:{erlene}:14", Ev());
        EventEnd(erlene, 14);
        Check("event 14 finished: completed, SCH unlocked, YOU_CAN_NOW_BECOME_A_SCHOLAR, the Grimoire, the gesture, the Schultz Scholar title",
              $"2/01/true/True/1/1/True/True",
              $"{QStat(LogCrystalWar, knowledge)}/{Lua("report(tostring(player:hasJob(xi.job.SCH)))")}/" +
              $"{Special().Contains($"{Text(EldiemeS, "YOU_CAN_NOW_BECOME_A_SCHOLAR")}:")}/{HasKi("GRIMOIRE")}/{HasKi("JOB_GESTURE_SCHOLAR")}/{TitleNow("SCHULTZ_SCHOLAR")}");
        Lua("player:changeJob(xi.job.SCH) player:setLevel(5)");
        Talk(erlene);
        Check("as a SCH 5: event 47", $"032:{erlene}:47", Ev());
        EventEnd(erlene, 47);
        Check("  Embrava and Kaustra learned, YOU_LEARN_EMBRAVA_AND_KAUSTRA", $"true/true/{Text(EldiemeS, "YOU_LEARN_EMBRAVA_AND_KAUSTRA")}:0",
              $"{Lua("report(tostring(player:hasSpell(xi.magic.spell.EMBRAVA)))")}/{Lua("report(tostring(player:hasSpell(xi.magic.spell.KAUSTRA)))")}/{Special()}");
        Lua("player:changeJob(xi.job.BLM)");
        NoErrors();

        Console.WriteLine("Dances with Luopans (the GEO unlock): Sylvie's offer");
        int luopans = QuestId("adoulin", "DANCES_WITH_LUOPANS");
        string luoVar(string name) => Var($"Quest[{LogAdoulin}][{luopans}]{name}");
        WarpTo(WesternAdoulin, "Sylvie");
        uint sylvie = TalkTo("Sylvie");
        Check("Western Adoulin's Sylvie: event 31", $"032:{sylvie}:31", Ev());
        EventEnd(sylvie, 31, 0);
        Check("declined: Declined 1", "1/0", $"{luoVar("Declined")}/{Status(LogAdoulin, luopans)}");
        Talk(sylvie);
        Check("Sylvie again: event 32", $"032:{sylvie}:32", Ev());
        EventEnd(sylvie, 32, 1);
        Check("taken: accepted, Declined cleared", "1/10/-", $"{QStat(LogAdoulin, luopans)}/{luoVar("Declined")}");
        Talk(sylvie);
        Check("Sylvie without the luopan: event 33", $"032:{sylvie}:33", Ev());
        EventEnd(sylvie, 33);
        NoErrors();

        Console.WriteLine("Dances with Luopans: the homeland soil, the Luopan");
        WarpTo(Konschtat, "Ergon_Locus");
        TalkTo("Ergon_Locus");
        Check("Konschtat's Ergon Locus, a Bastokan: the Fistful of Homeland Soil", $"1/{Text(Konschtat, "KEYITEM_OBTAINED")}:{Ki("FISTFUL_OF_HOMELAND_SOIL")}",
              $"{HasKi("FISTFUL_OF_HOMELAND_SOIL")}/{Special()}");
        WarpTo(WesternAdoulin, "Sylvie");
        Near(sylvie);
        TradeItems(sylvie, (petrifiedLog, 1));
        Check("the Petrified Log: event 34", $"032:{sylvie}:34", Ev());
        EventEnd(sylvie, 34);
        Check("  the log and the soil taken, the Luopan", "0/0/1", $"{Qty(petrifiedLog)}/{HasKi("FISTFUL_OF_HOMELAND_SOIL")}/{HasKi("LUOPAN")}");
        Talk(sylvie);
        Check("Sylvie with the Luopan: event 35", $"032:{sylvie}:35", Ev());
        EventEnd(sylvie, 35);
        NoErrors();

        Console.WriteLine("Dances with Luopans: /heal at Ceizak's Ergon Locus");
        Warp(330.0f, 0.0f, -250.0f, Ceizak);
        Wait(3);
        float groundY = c.Y;
        MoveTo(357.8f, groundY, -250.2f);
        Tick(400);
        Check("into the locus area (trigger area 1): UNCANNY_SENSATION, LocusArea 1",
              $"{Text(Ceizak, "UNCANNY_SENSATION")}:0/1", $"{Special()}/{Lua($"report(player:getLocalVar('Quest[{LogAdoulin}][{luopans}]LocusArea'))")}");
        // GP_CLI_COMMAND_CAMP: /heal on
        s.PacketList.Clear();
        Send(0x0E8, 8, b => BitConverter.GetBytes((uint)CampMode.On).CopyTo(b, C2sCamp.OffMode));
        for (int i = 0; i < 25 && !Special().Contains(Text(Ceizak, "ENERGIES_COURSE")); i++) Tick(1000);
        long restUntil = long.Parse(Lua($"report(player:getLocalVar('Quest[{LogAdoulin}][{luopans}]RestUntil'))"));
        long sys = long.Parse(Lua("report(GetSystemTime())"));
        Check("resting: the healing effect, its hook: ENERGIES_COURSE, a RestUntil 30 s to 8 min on", "true/True/True",
              $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.HEALING)))")}/{Special().Contains($"{Text(Ceizak, "ENERGIES_COURSE")}:0")}/" +
              $"{restUntil >= sys + 29 && restUntil <= sys + 480}");
        int waited = 0;
        for (; waited < 500 && luoVar("Prog") != "1"; waited++) Wait(1);
        Check("rested until then: the quest's timer, MYSTICAL_WARMTH, Prog 1", $"1/True",
              $"{luoVar("Prog")}/{waited + 2 >= restUntil - sys && waited <= restUntil - sys + 2}");
        Send(0x0E8, 8, b => BitConverter.GetBytes((uint)CampMode.Off).CopyTo(b, C2sCamp.OffMode));
        NoErrors();

        Console.WriteLine("Dances with Luopans: the reward");
        WarpTo(WesternAdoulin, "Sylvie");
        Near(sylvie);
        Talk(sylvie);
        Check("Sylvie, Prog 1: event 36", $"032:{sylvie}:36", Ev());
        EventEnd(sylvie, 36);
        Check("event 36 finished: completed, GEO unlocked, Indi-Poison and the Matre Bell, the gesture", "2/01/true/1/1/1",
              $"{QStat(LogAdoulin, luopans)}/{Lua("report(tostring(player:hasJob(xi.job.GEO)))")}/{Qty(ItemIdOf("PLATE_OF_INDI_POISON"))}/" +
              $"{Qty(ItemIdOf("MATRE_BELL"))}/{HasKi("JOB_GESTURE_GEOMANCER")}");
        NoErrors();
    }

    // ---- Bastok ---------------------------------------------------------------------------------------------------
    // A Bastokan WAR 50: Blade of Darkness (the DRK unlock: Gumbah's offer, the zone-in cutscenes keyed on the zone the
    // char came from (Zeruhn from Palborough's Old Toolbox (its event's setPos), Beadeaux from Pashhow), Chaosbringer thrown away (the recycle bin's
    // onItemDrop, then the bin emptied on the next zone-in: the kills reset) and given again, its onItemEquip ATTACK
    // listener counting real melee kills, none while it is off) and Ayame and Kaede (the NIN unlock: the Port Bastok
    // talks, Korroloka's ??? popping three leeches claimed by the char, the quest's onMobDeath, the ??? hidden for its
    // reset time once the coral is given, Norg's Ryoma, the key item param).
    private const ushort Palborough = 143, Zeruhn = 172, Pashhow = 109, Beadeaux = 147, Korroloka = 173;

    // the zone line from one zone into the next: the char put on the line's origin in the first zone, then across
    private static void Across(ushort from, ushort to, bool endZoneIn = false)
    {
        if (c.Zone != from)
        {
            var line = zone.ZoneInfoOf(from).ZoneLines.Values.First(l => l.DestinationZone == to);
            Warp(line.OriginX, line.OriginY, line.OriginZ, from);
        }
        TakeZoneLine(LineTo(to), endZoneIn);
    }

    // a field mob killed by the char's own swings: one hit point left, the claim, the auto-attack rounds until it dies
    private static bool KillByMelee(MobEntity m)
    {
        // disengaged from the last target and past the engage lockout (CCharEntity::OnDisengage)
        for (int i = 0; i < 15 && (c.AttackTarget != null || now < c.EngageLockedUntilMs); i++) Tick(1000);
        Lua($"GetMobByID({m.Id}):setHP(1)");
        ClaimBySwing(m.Id);
        for (int i = 0; i < 40 && !m.IsDead; i++)
        {
            MoveTo(m.X - 2.0f, m.Y, m.Z);
            Tick(1000);
        }
        bool dead = m.IsDead;
        for (int i = 0; i < 4; i++) Tick(1000);
        return dead;
    }

    // Palborough's Old Toolbox: its event 14, the setPos into Zeruhn Mines
    private static void ToolboxToZeruhn()
    {
        WarpTo(Palborough, "_3z2");
        uint toolbox = TalkTo("_3z2");
        Check("  Palborough's Old Toolbox: event 14", $"{toolbox}:14", EvId());
        EventEnd(toolbox, 14);
        Rezone(false);
    }

    private static MobEntity FieldMob(HashSet<uint> used) => Here.MobById.Values
        .Where(m => m.IsSpawned && !m.IsDead && m.Hp > 0 && !used.Contains(m.Id) && m.MLvl < 30 && m.OwnerId == 0)
        .OrderBy(m => (m.X - c.X) * (m.X - c.X) + (m.Z - c.Z) * (m.Z - c.Z)).FirstOrDefault();

    private static void Bastok()
    {
        ushort chaosbringer = ItemIdOf("CHAOSBRINGER");
        NewChar(996019, BastokMines, 53.5f, 0.0f, -36.0f, 1, 1, 50);
        Console.WriteLine("Bastok: a WAR 50 in Bastok Mines");
        NoErrors("no script errors on the login");

        Console.WriteLine("Blade of Darkness (the DRK unlock): Gumbah, Chaosbringer in Zeruhn Mines");
        int blade = QuestId("bastok", "BLADE_OF_DARKNESS");
        string bladeVar(string name) => Var($"Quest[{LogBastok}][{blade}]{name}");
        uint gumbah = TalkTo("Gumbah");
        Check("Gumbah: event 99", $"032:{gumbah}:99", Ev());
        EventEnd(gumbah, 99);
        Check("event 99 finished: accepted", "1/10", QStat(LogBastok, blade));
        ToolboxToZeruhn();
        Check("into Zeruhn from Palborough, Prog 0: the quest's cutscene 130 (over the zone's 150)", $"{Zeruhn}/130", $"{c.Zone}/{Cur()}");
        EndEvent(130);
        Check("event 130 finished: Chaosbringer, Prog 1", $"1/1/{Text(Zeruhn, "ITEM_OBTAINED")}:{chaosbringer}", $"{Qty(chaosbringer)}/{bladeVar("Prog")}/{Special()}");
        NoErrors();

        Console.WriteLine("Blade of Darkness: Chaosbringer thrown away, the kills reset, the sword again");
        Lua("player:setCharVar('ChaosbringerKills', 5)");
        byte slot = SlotOf(chaosbringer);
        // GP_CLI_COMMAND_ITEM_DUMP: one from the inventory slot
        Send(0x028, 12, b => { BitConverter.GetBytes(1u).CopyTo(b, 4); b[8] = 0; b[9] = slot; });
        Check("thrown away: into the recycle bin (its onItemDrop with recycleBin: the kills kept)", "0/1/5",
              $"{c.Items.Count(i => i.Location == Container.Inventory && i.ItemId == chaosbringer)}/{c.Items.Count(i => i.Location == Container.RecycleBin && i.ItemId == chaosbringer)}/{Lua("report(player:getCharVar('ChaosbringerKills'))")}");
        TakeZoneLine(LineTo(BastokMines));
        Check("into Bastok Mines: the bin emptied (onItemDrop again: the kills reset)", $"{BastokMines}/0/0",
              $"{c.Zone}/{c.Items.Count(i => i.Location == Container.RecycleBin)}/{Lua("report(player:getCharVar('ChaosbringerKills'))")}");
        ToolboxToZeruhn();
        Check("into Zeruhn without the sword: cutscene 131", "131", Cur());
        EndEvent(131);
        Check("event 131 finished: Chaosbringer again", "1", Qty(chaosbringer));
        ToolboxToZeruhn();
        Check("into Zeruhn with it: the zone's own cutscene 150", "150", Cur());
        if (c.IsInEvent) Finish();
        NoErrors();

        Console.WriteLine("Blade of Darkness: the kills under Chaosbringer's ATTACK listener");
        Equip(SlotOf(chaosbringer), 0);
        Check("Chaosbringer worn", $"{chaosbringer}", Worn(0));
        Lua("player:setCharVar('ChaosbringerKills', 98)");
        var used = new HashSet<uint>();
        string kills() => Lua("report(player:getCharVar('ChaosbringerKills'))");
        var mob = FieldMob(used);
        used.Add(mob.Id);
        Check("a field mob killed by the char's swings: ChaosbringerKills 99", "True/99", $"{KillByMelee(mob)}/{kills()}");
        Equip(0, 0);
        Check("Chaosbringer off (onItemUnequip: the listener removed)", "0", Worn(0));
        mob = FieldMob(used);
        used.Add(mob.Id);
        Check("  a kill bare-handed: not counted", "True/99", $"{KillByMelee(mob)}/{kills()}");
        Equip(SlotOf(chaosbringer), 0);
        mob = FieldMob(used);
        used.Add(mob.Id);
        Check("  worn again, a kill: 100", "True/100", $"{KillByMelee(mob)}/{kills()}");
        Lua("player:setHP(player:getMaxHP())");
        NoErrors();

        Console.WriteLine("Blade of Darkness: Beadeaux from Pashhow");
        Across(Pashhow, Beadeaux);
        Check("into Beadeaux from Pashhow with 100 kills: cutscene 121", $"{Beadeaux}/121", $"{c.Zone}/{Cur()}");
        EndEvent(121);
        Check("event 121 finished: completed, DRK unlocked, YOU_CAN_NOW_BECOME_A_DARK_KNIGHT, the gesture, the Dark Sider title, Bastok fame",
              $"2/01/true/True/1/True/True/20",
              $"{QStat(LogBastok, blade)}/{Lua("report(tostring(player:hasJob(xi.job.DRK)))")}/" +
              $"{Special().Contains($"{Text(Beadeaux, "YOU_CAN_NOW_BECOME_A_DARK_KNIGHT")}:0")}/{HasKi("JOB_GESTURE_DARK_KNIGHT")}/{TitleNow("DARK_SIDER")}/{FameOf("BASTOK")}");
        NoErrors();

        Console.WriteLine("Ayame and Kaede (the NIN unlock): Port Bastok");
        int ayame = QuestId("bastok", "AYAME_AND_KAEDE");
        string ayameVar(string name) => Var($"Quest[{LogBastok}][{ayame}]{name}");
        Warp(49.5f, -6.0f, 67.0f, PortBastok);
        uint kaede = TalkTo("Kaede");
        Check("Kaede: event 240", $"032:{kaede}:240", Ev());
        EventEnd(kaede, 240);
        Check("accepted", "1/10", QStat(LogBastok, ayame));
        uint kagetora = TalkTo("Kagetora");
        Check("Kagetora, Prog 0: event 241", $"032:{kagetora}:241", Ev());
        EventEnd(kagetora, 241);
        uint ensetsu = TalkTo("Ensetsu");
        Check("Ensetsu, Prog 1: event 242", $"032:{ensetsu}:242", Ev());
        EventEnd(ensetsu, 242);
        Check("  Prog 2", "2", ayameVar("Prog"));
        NoErrors();

        Console.WriteLine("Ayame and Kaede: Korroloka's ???, three leeches");
        Warp(-206.5f, -9.0f, 176.0f, Korroloka);
        uint leech = uint.Parse(Lua("report(zones[xi.zone.KORROLOKA_TUNNEL].mob.KORROLOKA_LEECH)"));
        var leeches = Enumerable.Range(0, 3).Select(k => Here.MobById[leech + (uint)k]).ToArray();
        // CBattleEntity::isDead: a mob not spawned is Disappear, so dead
        Check("the leeches not spawned: isDead", "true/false", Lua($"local m = GetMobByID({leech}) report(tostring(m:isDead())) report(tostring(m:isSpawned()))").Replace(",", "/"));
        uint qm2 = NpcId("qm2");
        Near(qm2);
        Talk(qm2);
        Check("the ???: the three leeches spawned and claimed by the char, HAIR_STANDS_ON_END (messageText)", $"True/True/True/{CharId}/{Text(Korroloka, "HAIR_STANDS_ON_END")}",
              $"{string.Join("/", leeches.Select(m => m.IsSpawned))}/{string.Join(",", leeches.Select(m => m.OwnerId).Distinct())}/" +
              $"{string.Join(",", Of(0x036).ConvertAll(p => BitConverter.ToUInt16(p.Buffer, 10) & 0x7FFF))}");
        Talk(qm2);
        Check("the ??? while they are up: nothing from the quest", "1", Lua($"report(player:getLocalVar('Quest[{LogBastok}][{ayame}]isActor'))"));
        foreach (var m in leeches) KillByChar(m.Id);
        Check("killed: the quest's onMobDeath, nmDefeated", "1", Lua($"report(player:getLocalVar('Quest[{LogBastok}][{ayame}]nmDefeated'))"));
        for (int i = 0; i < 60 && leeches.Any(m => m.IsSpawned); i++) Tick(1000);
        Near(qm2);
        Talk(qm2);
        Check("the ??? with them gone: Prog 3, the Strangely Shaped Coral, the ??? hidden (hideNPC)", $"3/1/False",
              $"{ayameVar("Prog")}/{HasKi("STRANGELY_SHAPED_CORAL")}/{Npc(qm2).Status == NpcEntity.StatusNormal}");
        Tick(301_000);
        Check("  FORCE_SPAWN_QM_RESET_TIME (300 s) on: the ??? back", "True", $"{Npc(qm2).Status == NpcEntity.StatusNormal}");
        NoErrors();

        Console.WriteLine("Ayame and Kaede: Ensetsu, Kagetora, Norg's Ryoma, the end");
        Warp(34.5f, -6.0f, 67.0f, PortBastok);
        ensetsu = TalkTo("Ensetsu");
        Check("Ensetsu, Prog 3: event 245", $"032:{ensetsu}:245", Ev());
        EventEnd(ensetsu, 245);
        Talk(ensetsu);
        Check("Ensetsu, Prog 4: event 243", $"032:{ensetsu}:243", Ev());
        EventEnd(ensetsu, 243);
        kagetora = TalkTo("Kagetora");
        Check("Kagetora, Prog 4: event 244", $"032:{kagetora}:244", Ev());
        if (c.IsInEvent) Finish();
        Warp(-21.5f, 0.0f, -9.0f, Norg);
        uint ryoma = TalkTo("Ryoma");
        Check("Norg's Ryoma, Prog 4: event 95", $"032:{ryoma}:95", Ev());
        EventEnd(ryoma, 95);
        Check("  the Sealed Dagger for the coral, Prog 5", "1/0/5", $"{HasKi("SEALED_DAGGER")}/{HasKi("STRANGELY_SHAPED_CORAL")}/{ayameVar("Prog")}");
        Warp(34.5f, -6.0f, 67.0f, PortBastok);
        ensetsu = TalkTo("Ensetsu");
        Check("Ensetsu, Prog 5: event 246 with the dagger", $"034:{ensetsu}:246:{Ki("SEALED_DAGGER")}", Ev());
        EventEnd(ensetsu, 246);
        Check("event 246 finished: completed, the dagger taken, NIN unlocked, UNLOCK_NINJA, the gesture, the Shadow Walker title, Bastok fame",
              $"2/01/0/true/True/1/True/True/40",
              $"{QStat(LogBastok, ayame)}/{HasKi("SEALED_DAGGER")}/{Lua("report(tostring(player:hasJob(xi.job.NIN)))")}/" +
              $"{Special().Contains($"{Text(PortBastok, "UNLOCK_NINJA")}:0")}/{HasKi("JOB_GESTURE_NINJA")}/{TitleNow("SHADOW_WALKER")}/{FameOf("BASTOK")}");
        kaede = TalkTo("Kaede");
        Check("Kaede after it: event 248", $"032:{kaede}:248", Ev());
        EventEnd(kaede, 248);
        NoErrors();
    }

    private static ushort ItemIdOf(string name) => ItemId(name);
}
