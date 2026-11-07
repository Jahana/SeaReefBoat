// XI.Verify.Quests -- quest rework B3b Other Areas group 8 (Koblakiq's Moblin chain and the CoP fields: Missionary
// Moblin, For the Birds, Better the Demon You Know, Bombs Away!, Confessions of a Bellmaker): what no trace can tell
// apart or reach.
// - Kinds: where no other quest and no NPC script answers the NPC (Daa Bola the Seer, GeFhu Yagudoeye, Castle Zvahl
//   Baileys' qm2, Riverne - Site #A01's Stone Monument and Buffalostalker Dodzbraz have no script and no other quest's
//   handler), a Progress and an Event reply play alike over the default, so no trace tells them apart: `oa g8 kinds`
//   checks each reply as the Lua writes it. Koblakiq's replies (three quests share him, no script) and the guild
//   masters' (Reinberta, Mevreauche: their scripts take a repeat talk from an Event reply) do show in traces
//   (missionary_moblin#kinds, for_the_birds#kinds, better_the_demon_you_know#kinds, confessions_of_a_bellmaker#edges);
//   the checks keep them as a second guard.
// - Complete: Missionary Moblin's 9 reads quest:complete, whose reward is gil only, so it cannot fail on Lua: `oa g8
//   complete` takes the false arm on the fake (no confirmTrade, no mustZone).
// - Spawns: Better the Demon's qm2 reads MARQUIS_ANDREALPUS + 0..4, but the Marquis' mob script despawns the four demons
//   at his death and despawn, so no demon is ever up without him and no trace reaches a demon's operand: `oa g8 spawns`
//   puts each one up alone on the fake (nil), then none (the five spawned and claimed).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup8()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 8: the reply kinds no trace records, the unreachable arms ==");
        RunOtherAreasGroup8Kinds();
        RunOtherAreasGroup8Complete();
        RunOtherAreasGroup8Spawns();
    }

    static Fake OA8At(QuestStatus status, int step = 0)
    {
        return new Fake { Status = status, Step = step };
    }

    static Fake OA8Traded(QuestStatus status, int step, params int[] items)
    {
        var f = OA8At(status, step);
        foreach (int i in items)
        {
            f.TradeItems[i] = f.TradeItems.TryGetValue(i, out var n) ? n + 1 : 1;
            f.TradeSlots.Add(i);
        }
        return f;
    }

    static void RunOtherAreasGroup8Kinds()
    {
        var mm = new QC.OtherAreas.MissionaryMoblin().Compile();
        var ftb = new QC.OtherAreas.ForTheBirds().Compile();
        var btd = new QC.OtherAreas.BetterTheDemonYouKnow().Compile();
        var ba = new QC.OtherAreas.BombsAway().Compile();
        var cob = new QC.OtherAreas.ConfessionsOfABellmaker().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        var avail = QuestStatus.Available;
        var acc = QuestStatus.Accepted;
        var done = QuestStatus.Completed;
        string kob = QC.Npc.OldtonMovalpolos.Koblakiq;

        // For the Birds' Daa Bola (86 Event, 87 Progress, LETTING_YOU_GO) and GeFhu (124 Progress, the Prog 2 arm's
        // QUADAV_ARE_ATTACKING, the Prog 3 arm's noAction)
        var mustZone = OA8At(acc, 1);
        mustZone.MustZone = true;
        var option4 = OA8At(acc, 3);
        option4.Vars["Option"] = 4;
        string daa = QC.Npc.CastleOztroja.DaaBolatheSeer, gefhu = QC.Npc.Beadeaux.GeFhuYagudoeye;
        string ftbKinds = string.Join("|",
            G2Reply(G2Handler(ftb, 1, trig, daa), OA8At(acc, 0)),
            G2Reply(G2Handler(ftb, 1, trade, daa), OA8Traded(acc, 0, QC.Item.ArnicaRoot)),
            G2Reply(G2Handler(ftb, 1, trig, daa), mustZone),
            G2Reply(G2Handler(ftb, 1, trig, gefhu), option4),
            G2Reply(G2Handler(ftb, 1, trig, gefhu), OA8At(acc, 2)),
            G2Reply(G2Handler(ftb, 1, trig, gefhu), OA8At(acc, 3)));
        Check("oa g8 kinds:For the Birds' Daa Bola 86 Event, 87 Progress, LETTING_YOU_GO messageSpecial; GeFhu 124 Progress, QUADAV_ARE_ATTACKING messageSpecial, noAction",
              "Event(86)|ProgressCutscene(87)|MessageSpecial(151:LETTING_YOU_GO)|ProgressCutscene(124)|MessageSpecial(147:QUADAV_ARE_ATTACKING)|NoAction()",
              ftbKinds);

        // Better the Demon You Know's qm2: KEYITEM_OBTAINED (the earplug), MARQUIS_ATTACKS
        var nmKilled = OA8At(acc, 2);
        nmKilled.PlayerLocalVars["NMKilled"] = 1;
        string qm2 = QC.Npc.CastleZvahlBaileys.qm2;
        string btdQm2 = string.Join("|",
            G2Reply(G2Handler(btd, 1, trig, qm2), nmKilled),
            G2Reply(G2Handler(btd, 1, trig, qm2), OA8At(acc, 2)));
        Check("oa g8 kinds:Better the Demon You Know's qm2 KEYITEM_OBTAINED (the earplug) and MARQUIS_ATTACKS messageSpecial",
              $"MessageSpecial(161:KEYITEM_OBTAINED,{QC.KeyItem.ZeelozoksEarplug})|MessageSpecial(161:MARQUIS_ATTACKS)",
              btdQm2);

        // Confessions of a Bellmaker's Stone Monument (101 / 102 / 103 Progress, the Prog 3 noAction); the guild masters'
        // 402 / 74 Progress (a second guard: confessions_of_a_bellmaker#edges' repeat talks show it)
        string mon = QC.Npc.RiverneSiteA01.StoneMonument;
        string cobKinds = string.Join("|",
            G2Reply(G2Handler(cob, 0, trig, mon), OA8At(avail)),
            G2Reply(G2Handler(cob, 1, trig, mon), OA8At(acc, 2)),
            G2Reply(G2Handler(cob, 1, trig, mon), OA8At(acc, 3)),
            G2Reply(G2Handler(cob, 1, trig, mon), OA8At(acc, 4)),
            G2Reply(G2Handler(cob, 1, trig, QC.Npc.BastokMarkets.Reinberta), OA8At(acc, 0)),
            G2Reply(G2Handler(cob, 1, trig, QC.Npc.NorthernSanDoria.Mevreauche), OA8At(acc, 1)));
        Check("oa g8 kinds:Confessions of a Bellmaker's Stone Monument 101 / 102 / 103 Progress and noAction; Reinberta 402 and Mevreauche 74 Progress",
              "ProgressCutscene(101)|ProgressCutscene(102)|NoAction()|ProgressCutscene(103)|ProgressEvent(402)|ProgressEvent(74)",
              cobKinds);

        // Bombs Away!'s Buffalostalker Dodzbraz: 6 Progress, 7 Event, 8 Progress
        string buf = QC.Npc.UleguerandRange.BuffalostalkerDodzbraz;
        var cores = new Fake { Status = acc };
        cores.TradeItems[QC.Item.ClusterCore] = 2;
        cores.TradeSlots.Add(QC.Item.ClusterCore);
        string baKinds = string.Join("|",
            G2Reply(G2Handler(ba, 0, trig, buf), OA8At(avail)),
            G2Reply(G2Handler(ba, 1, trig, buf), OA8At(acc)),
            G2Reply(G2Handler(ba, 1, trade, buf), cores));
        Check("oa g8 kinds:Bombs Away!'s Buffalostalker Dodzbraz 6 Progress, 7 Event, 8 Progress",
              "ProgressEvent(6)|Event(7)|ProgressEvent(8)",
              baKinds);

        // Koblakiq (a second guard: the three #kinds flows show each kind against another quest's reply)
        var waiting = OA8At(acc, 1);
        waiting.Vars["Wait"] = 1800000060;
        waiting.Now = 1800000000;
        var waited = OA8At(acc, 1);
        waited.Vars["Wait"] = 1800000000;
        waited.Now = 1800000000;
        string kobKinds = string.Join("|",
            G2Reply(G2Handler(mm, 0, trig, kob), OA8At(avail)),
            G2Reply(G2Handler(mm, 1, trig, kob), OA8At(acc)),
            G2Reply(G2Handler(mm, 1, trade, kob), OA8Traded(acc, 0, QC.Item.SoiledLetter)),
            G2Reply(G2Handler(mm, 2, trig, kob), OA8At(done)),
            G2Reply(G2Handler(ftb, 0, trig, kob), OA8At(avail)),
            G2Reply(G2Handler(ftb, 1, trig, kob), OA8At(acc, 0)),
            G2Reply(G2Handler(ftb, 1, trig, kob), OA8At(acc, 1)),
            G2Reply(G2Handler(ftb, 1, trig, kob), OA8At(acc, 2)),
            G2Reply(G2Handler(ftb, 1, trig, kob), OA8At(acc, 4)),
            G2Reply(G2Handler(ftb, 2, trig, kob), OA8At(done)),
            G2Reply(G2Handler(btd, 0, trig, kob), OA8At(avail)),
            G2Reply(G2Handler(btd, 1, trig, kob), OA8At(acc, 0)),
            G2Reply(G2Handler(btd, 1, trade, kob), OA8Traded(acc, 0, QC.Item.DemonPen)),
            G2Reply(G2Handler(btd, 1, trig, kob), waiting),
            G2Reply(G2Handler(btd, 1, trig, kob), waited),
            G2Reply(G2Handler(btd, 1, trig, kob), OA8At(acc, 2)),
            G2Reply(G2Handler(btd, 1, trig, kob), OA8At(acc, 3)),
            G2Reply(G2Handler(btd, 2, trig, kob), OA8At(done)));
        Check("oa g8 kinds:Koblakiq: Missionary Moblin 7 / 9 Progress, 8 / 12 Event; For the Birds 14 / 16 / 18 Progress, 15 / 17 / 19 Event; Better the Demon 20 / 22 / 24 / 26 Progress, 21 / 23 / 25 Event at priority 101, 27 Event replaceDefault",
              $"ProgressCutscene(7)|Event(8)|ProgressCutscene(9)|Event(12)|ProgressCutscene(14,0,{QC.Item.ArnicaRoot})|Event(15)|ProgressEvent(16,0,{QC.KeyItem.GlitteringFragment})|Event(17)|ProgressCutscene(18)|Event(19)|" +
              $"ProgressCutscene(20,0,{QC.Item.DemonPen})|Event(21,0,{QC.Item.DemonPen}):setPriority(101)|ProgressCutscene(22)|Event(23):setPriority(101)|ProgressCutscene(24)|Event(25):setPriority(101)|ProgressCutscene(26)|Event(27):replaceDefault()",
              kobKinds);
    }

    // quest:complete false (the fake's CompleteResult): Missionary Moblin's 9 confirms no trade and sets no mustZone (its
    // reward is gil only: on Lua the completion cannot fail)
    static void RunOtherAreasGroup8Complete()
    {
        var mm = new QC.OtherAreas.MissionaryMoblin().Compile();
        var f9 = new Fake { Status = QuestStatus.Accepted, CompleteResult = false };
        var t9 = new Fake { Status = QuestStatus.Accepted };
        string Run(Fake f)
        {
            G2Handler(mm, 1, QuestHandlerKind.OnEventFinish, null, 9).Run(f);
            return string.Join(";", f.Calls) + "/" + f.MustZone;
        }
        Check("oa g8 complete:Missionary Moblin's 9: quest:complete false: no confirmTrade, no mustZone; true: both",
              "Quest.Complete()/False|Quest.Complete();Trade.Confirm();Quest.SetMustZone()/True",
              $"{Run(f9)}|{Run(t9)}");
    }

    // the qm2 at Prog 2: each of MARQUIS_ANDREALPUS + 1..4 up alone: nil; none up: the five spawned and claimed, the
    // two messages
    static void RunOtherAreasGroup8Spawns()
    {
        var btd = new QC.OtherAreas.BetterTheDemonYouKnow().Compile();
        string marquis = QC.Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS;
        string Qm2(int up)
        {
            var f = OA8At(QuestStatus.Accepted, 2);
            if (up >= 0) f.SpawnedMobs.Add($"{marquis}+{up}");
            var r = G2Reply(G2Handler(btd, 1, QuestHandlerKind.OnTrigger, QC.Npc.CastleZvahlBaileys.qm2), f);
            return $"{r}:{f.Calls.Count(c => c.StartsWith("Player.SpawnMob"))}";
        }
        Check("oa g8 spawns:Better the Demon's qm2 at Prog 2: the Marquis or one demon (+1..+4) up alone: nil; none up: MARQUIS_ATTACKS and five spawned",
              "null:0|null:0|null:0|null:0|null:0|MessageSpecial(161:MARQUIS_ATTACKS):5",
              string.Join("|", Qm2(0), Qm2(1), Qm2(2), Qm2(3), Qm2(4), Qm2(-1)));
    }
}
