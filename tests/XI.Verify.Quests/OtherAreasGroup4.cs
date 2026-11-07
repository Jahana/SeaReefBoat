// XI.Verify.Quests -- quest rework B3b Other Areas group 4 (It's Raining Mannequins!, Behind the Smile, Knocking on
// Forbidden Doors, Recycling Rods, The Rescue): the facet members the group added (Player.AddItemWithExData,
// MannequinPose / SetMannequinPose, DelGilChecked, NpcIdOf), on the fake, and the arms no trace can record.
// - Knocking on Forbidden Doors' delGil false arms (319's trade short of 2000, 321's purchase short of 100000 and pose short
//   of 200): the engine logs `lua: delGil : Not enough Gil in currency slot`, which the golden recorder refuses as a script
//   error, so no trace holds them: `oa g4 kofd` checks the C# takes nothing and gives nothing there, beside the arms the
//   traces hold.
// - Kinds: where no other quest and no NPC script answers the NPC (Enaremand, Keshab-Menjab, Thunder Hawk and the Jail
//   Door, the ???s, Fyi's 319 trade), a Progress and an Event reply play alike, so no trace tells them apart: `oa g4 kinds`
//   checks each reply as the Lua writes it. The traces see the kinds on Fyi (ties with the other quests' replies), Chemioue
//   (In Search of the Truth, Petals for Parelbriaux), Ramona, Cheupirudaux and the Wooden Ladder (their scripts).
// - quest:complete false where no reward item can fail it (Recycling Rods' 317: gil only; The Rescue's 81: no item; It's
//   Raining Mannequins!' 311: no reward table, a free slot checked first): `oa g4 complete` checks nothing follows it.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOtherAreasGroup4()
    {
        Console.WriteLine("== quest rework B3b Other Areas group 4: facets, the arms no trace records ==");
        RunOtherAreasGroup4Facets();
        RunOtherAreasGroup4Knocking();
        RunOtherAreasGroup4Complete();
        RunOtherAreasGroup4Kinds();
        RunOtherAreasGroup4MannequinKinds();
    }

    // player:addItem({ id }) + item:setExData, player:getMannequinPose / setMannequinPose, player:delGil's boolean,
    // zones[zone].npc[key]
    static void RunOtherAreasGroup4Facets()
    {
        var f = new Fake { GilHeld = 150 };
        f.MannequinPoses[QC.Item.GalkaMannequin] = 2;
        f.NpcIds["LADDER_KNOCKING"] = 16888097;
        string reads = $"{f.Player.MannequinPose(QC.Item.GalkaMannequin)}/{f.Player.MannequinPose(QC.Item.HumeMMannequin)}/" +
                       $"{f.Player.NpcIdOf("LADDER_KNOCKING")}";
        bool added = f.Player.AddItemWithExData(QC.Item.HumeMMannequin, "race", 1);
        bool short200 = f.Player.DelGilChecked(200);
        bool exact = f.Player.DelGilChecked(150);
        bool negative = f.Player.DelGilChecked(-1);
        f.Player.SetMannequinPose(QC.Item.HumeMMannequin, 1, 3);
        string unknown;
        try
        {
            unknown = f.Player.NpcIdOf("NO_SUCH_KEY").ToString();
        }
        catch (InvalidOperationException)
        {
            unknown = "throws";
        }
        Check("oa g4 facets:the fake answers MannequinPose (0 when not set) and NpcIdOf (an unknown key throws); DelGilChecked takes the gil only when it is held; AddItemWithExData, DelGilChecked and SetMannequinPose are recorded",
              $"2/0/16888097|True|False|True|False|0|3|throws|Player.AddItemWithExData({QC.Item.HumeMMannequin},race,1);" +
              $"Player.DelGilChecked(200);Player.DelGilChecked(150);Player.DelGilChecked(-1);Player.SetMannequinPose({QC.Item.HumeMMannequin},1,3)",
              $"{reads}|{added}|{short200}|{exact}|{negative}|{f.GilHeld}|{f.Player.MannequinPose(QC.Item.HumeMMannequin)}|{unknown}|{string.Join(";", f.Calls)}");
    }

    static Fake OA4ShopFake(int option, int gil)
    {
        var f = new Fake { Status = QuestStatus.Completed, Option = option, GilHeld = gil };
        f.KeyItems[QC.KeyItem.BetterHumesAndMannequins] = true;
        return f;
    }

    static string OA4Run(QuestHandlerDecl h, Fake f)
    {
        h.Run(f);
        return string.Join(";", f.Calls) + "/" + f.GilHeld;
    }

    // the completed state's 319 and 321 finishes, each with the gil short of its cost (no trace: the engine's delGil log
    // line) and with exactly the cost (the traces' side)
    static void RunOtherAreasGroup4Knocking()
    {
        var kofd = new QC.OtherAreas.KnockingOnForbiddenDoors().Compile();
        var fin = QuestHandlerKind.OnEventFinish;
        int hume = QC.Item.HumeMMannequin, galka = QC.Item.GalkaMannequin;

        Check("oa g4 kofd:319's finish (option 1 to 8) short of 2000 gil takes nothing: no trade confirmed, no mannequin, no pose; with 2000 the trade, the mannequin and its pose reset",
              "Player.DelGilChecked(2000)/1999|Player.DelGilChecked(2000)/1999|" +
              $"Player.DelGilChecked(2000);Trade.Confirm();Player.GiveItem({galka});Player.SetMannequinPose({galka},8,0)/0",
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 319), OA4ShopFake(1, 1999))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 319), OA4ShopFake(8, 1999))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 319), OA4ShopFake(8, 2000))}");

        Check("oa g4 kofd:321's finish with option 1 to 8 short of 100000 gil takes nothing and does not fall to the pose (option < 10); with 100000 the mannequin and its pose reset",
              "Player.DelGilChecked(100000)/99999|Player.DelGilChecked(100000)/99999|" +
              $"Player.DelGilChecked(100000);Text.Special(249:ITEM_OBTAINED,{hume});Player.AddItem({hume},1);Player.SetMannequinPose({hume},1,0)/0",
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(1, 99999))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(8, 99999))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(1, 100000))}");

        Check("oa g4 kofd:321's finish with option 10 or more short of 200 gil poses nothing; with 200 the pose (race ((option - 11) % 8) + 1, floored; pose floor(option / 32))",
              "Player.DelGilChecked(200)/199|Player.DelGilChecked(200)/199|" +
              $"Player.DelGilChecked(200);Player.SetMannequinPose({hume},1,2)/0|Player.DelGilChecked(200);Player.SetMannequinPose({galka},8,0)/0",
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(10, 199))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(82, 199))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(75, 200))}|" +
              $"{OA4Run(G2Handler(kofd, 2, fin, null, 321), OA4ShopFake(10, 200))}");
    }

    // quest:complete false (the fake's CompleteResult): nothing after it
    static void RunOtherAreasGroup4Complete()
    {
        var fin = QuestHandlerKind.OnEventFinish;
        var rods = new QC.OtherAreas.RecyclingRods().Compile();
        var rescue = new QC.OtherAreas.TheRescue().Compile();
        var irm = new QC.OtherAreas.ItsRainingMannequins().Compile();

        Fake Failing(QuestStatus status, int step = 0)
        {
            var f = new Fake { Status = status, Step = step, CompleteResult = false };
            f.RaceId = 1;
            return f;
        }

        Check("oa g4 complete:quest:complete false: Recycling Rods' 317 confirms no trade, The Rescue's 81 adds no fame and keeps the sack, It's Raining Mannequins!' 311 gives no mannequin",
              "Quest.Complete()|Quest.Complete()|Quest.Complete()",
              $"{OA4Run(G2Handler(rods, 1, fin, null, 317), Failing(QuestStatus.Accepted)).Split('/')[0]}|" +
              $"{OA4Run(G2Handler(rescue, 1, fin, null, 81), Failing(QuestStatus.Accepted)).Split('/')[0]}|" +
              $"{OA4Run(G2Handler(irm, 4, fin, null, 311), Failing(QuestStatus.Accepted, 3)).Split('/')[0]}");
    }

    // the reply kinds no trace can tell apart (no competitor, no NPC script)
    static void RunOtherAreasGroup4Kinds()
    {
        var bts = new QC.OtherAreas.BehindTheSmile().Compile();
        var kofd = new QC.OtherAreas.KnockingOnForbiddenDoors().Compile();
        var rods = new QC.OtherAreas.RecyclingRods().Compile();
        var rescue = new QC.OtherAreas.TheRescue().Compile();
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;

        Fake At(QuestStatus status, int step = 0, params int[] keyItems)
        {
            var f = new Fake { Status = status, Step = step };
            foreach (int k in keyItems) f.KeyItems[k] = true;
            return f;
        }
        Fake Trading(QuestStatus status, int item, params int[] keyItems)
        {
            var f = At(status, 0, keyItems);
            f.TradeItems[item] = 1;
            return f;
        }
        int redOil = QC.KeyItem.RedOil, incense = QC.KeyItem.MireIncense, sack = QC.KeyItem.TradersSack;

        string btsKinds = string.Join("|",
            G2Reply(G2Handler(bts, 0, trig, QC.Npc.TavnazianSafehold.Enaremand), At(QuestStatus.Available)),
            G2Reply(G2Handler(bts, 1, trig, QC.Npc.TavnazianSafehold.Enaremand), At(QuestStatus.Accepted, 3, redOil)),
            G2Reply(G2Handler(bts, 1, trig, QC.Npc.TavnazianSafehold.Enaremand), At(QuestStatus.Accepted, 3)),
            G2Reply(G2Handler(bts, 1, trig, QC.Npc.CarpentersLanding.qmbehindthesmile), At(QuestStatus.Accepted, 1)),
            G2Reply(G2Handler(bts, 1, trig, QC.Npc.CarpentersLanding.qmbehindthesmile), At(QuestStatus.Accepted, 2)));
        string kofdKinds = string.Join("|",
            G2Reply(G2Handler(kofd, 0, trig, QC.Npc.TavnazianSafehold.Enaremand), At(QuestStatus.Available)),
            G2Reply(G2Handler(kofd, 1, trig, QC.Npc.MisareauxCoast.qmmireincense), At(QuestStatus.Accepted, 2)),
            G2Reply(G2Handler(kofd, 1, trig, QC.Npc.MisareauxCoast.qmalsha), At(QuestStatus.Accepted, 2, incense)),
            G2Reply(G2Handler(kofd, 1, trig, QC.Npc.MisareauxCoast.qmalsha), At(QuestStatus.Accepted, 3)),
            G2Reply(G2Handler(kofd, 1, trig, QC.Npc.MisareauxCoast.qmalsha), At(QuestStatus.Accepted, 4)),
            G2Reply(G2Handler(kofd, 2, trade, QC.Npc.Mhaura.FyiChalmwoh), Trading(QuestStatus.Completed, QC.Item.GalkaMannequin)));
        string rodsKinds = string.Join("|",
            G2Reply(G2Handler(rods, 0, trig, QC.Npc.Mhaura.KeshabMenjab), At(QuestStatus.Available)),
            G2Reply(G2Handler(rods, 1, trig, QC.Npc.Mhaura.KeshabMenjab), At(QuestStatus.Accepted)),
            G2Reply(G2Handler(rods, 1, trade, QC.Npc.Mhaura.KeshabMenjab), Trading(QuestStatus.Accepted, QC.Item.CleanSnapRod)),
            G2Reply(G2Handler(rods, 1, trade, QC.Npc.Mhaura.KeshabMenjab), Trading(QuestStatus.Accepted, QC.Item.ChocoboFeather)),
            G2Reply(G2Handler(rods, 2, trig, QC.Npc.Mhaura.KeshabMenjab), At(QuestStatus.Completed)));
        string rescueKinds = string.Join("|",
            G2Reply(G2Handler(rescue, 0, trig, QC.Npc.Selbina.ThunderHawk), At(QuestStatus.Available)),
            G2Reply(G2Handler(rescue, 1, trig, QC.Npc.Selbina.ThunderHawk), At(QuestStatus.Accepted, 0, sack)),
            G2Reply(G2Handler(rescue, 1, trig, QC.Npc.Selbina.ThunderHawk), At(QuestStatus.Accepted)),
            G2Reply(G2Handler(rescue, 1, trig, QC.Npc.Beadeaux._43b), At(QuestStatus.Accepted)),
            G2Reply(G2Handler(rescue, 1, trade, QC.Npc.Beadeaux._43b), Trading(QuestStatus.Accepted, QC.Item.QuadavCharm)),
            G2Reply(G2Handler(rescue, 2, trig, QC.Npc.Selbina.ThunderHawk), At(QuestStatus.Completed)));
        Check("oa g4 kinds:Behind the Smile's Enaremand 533 / 534 Progress, 541 Event, the ??? a messageSpecial and noAction; Knocking on Forbidden Doors' 535, 556, 557, 558 and 319 Progress, the incense a keyItem reply; Recycling Rods' 313 / 317 Progress, 315 / 316 / 314 Event; The Rescue's 80 / 81 / 1000 Progress, 83 Event, 82 Event replaceDefault, the door a messageSpecial",
              "ProgressEvent(533)|ProgressEvent(534)|Event(541)|MessageSpecial(2:STENCH_OF_DECAY)|NoAction()||" +
              $"ProgressEvent(535)|KeyItem({QC.KeyItem.MireIncense})|ProgressEvent(556,{{[1]={QC.KeyItem.MireIncense}}})|" +
              $"ProgressEvent(557,{{[1]={QC.KeyItem.MireIncense}}})|ProgressEvent(558)|" +
              "ProgressEvent(319,{[0]=2,[1]=0,[2]=100000,[3]=2000,[4]=1})||" +
              "ProgressEvent(313)|Event(315)|ProgressEvent(317)|Event(316)|Event(314)||" +
              "ProgressEvent(80)|ProgressEvent(81)|Event(83)|MessageSpecial(147:LOCKED_DOOR_QUADAV_HAS_KEY)|ProgressEvent(1000)|Event(82):replaceDefault()",
              $"{btsKinds}||{kofdKinds}||{rodsKinds}||{rescueKinds}");
    }

    // It's Raining Mannequins!: 309 (a trade no other quest at Fyi answers: Knocking's 319 wants one mannequin alone) and the
    // era module's section 5 trigger (#era has no other quest at Fyi; the module's replies are not the base's, so the ties of
    // #kinds do not see them)
    static void RunOtherAreasGroup4MannequinKinds()
    {
        var irm = new QC.OtherAreas.ItsRainingMannequins().Compile();
        var parts = new Fake { Status = QuestStatus.Accepted, Step = 2 };
        foreach (int item in new[] { QC.Item.MannequinHead, QC.Item.MannequinBody, QC.Item.MannequinHands, QC.Item.MannequinLegs, QC.Item.MannequinFeet })
            parts.TradeItems[item] = 1;
        string trade = G2Reply(G2Handler(irm, 3, QuestHandlerKind.OnTrade, QC.Npc.Mhaura.FyiChalmwoh), parts);

        var applied = QuestOverrides.Apply(new QC.OtherAreas.ItsRainingMannequins().Compile(),
            new QC.Modules.Era.OtherAreas.ItsRainingMannequins().Compile(new Fake()), QC.QuestKey.OtherAreas.ItsRainingMannequins);
        var trig = G2Handler(applied.Definition, 4, QuestHandlerKind.OnTrigger, QC.Npc.Mhaura.FyiChalmwoh);
        Fake Waiting(int wait, int now)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 3, Now = now };
            f.Vars["Wait"] = wait;
            return f;
        }
        Check("oa g4 kinds:It's Raining Mannequins!' 309 Progress; the era module's Fyi (Prog 3): now >= Wait 311 Progress, else 310 Event",
              "ProgressEvent(309)|era|ProgressEvent(311)|ProgressEvent(311)|Event(310)",
              $"{trade}|{trig.Module?.Name.Substring(0, 3)}|{G2Reply(trig, Waiting(1000, 1001))}|{G2Reply(trig, Waiting(1000, 1000))}|{G2Reply(trig, Waiting(1000, 999))}");
    }
}
