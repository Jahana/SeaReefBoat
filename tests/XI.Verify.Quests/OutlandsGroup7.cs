// XI.Verify.Quests -- quest rework B3b Outlands group 7 (Don't Forget the Antidote, Gullible's Travels, Even More
// Gullible's Travels, Greetings to the Guardian, Missionary Man; call-only): what the golden traces cannot show.
// - The reply kinds. The five quests are called from their NPCs' scripts (xi.cs.quest), and an NPC call renders an Event
//   and a ProgressEvent alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply): there is no lookup action,
//   so no priority to tell them apart, and every trace replays the same with either kind. The Lua scripts call
//   player:startEvent, so each reply is pinned here as Event (the flowers' FLOWER_BLOOMING as MessageSpecial).
// - The replies that leave the script's own line to it: Magriffon's trigger answers nothing when Gullible's Travels is
//   not completed and Even More is not accepted (the script's 143 follows), and the Altar of Ashes' offering answers
//   NoAction (the script has nothing after the call, so null would look the same).
// - Gullible's Travels' draw bounds (10, 30): the flow's seeded draws come out the same with (10, 29) (mutant g8).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup7()
    {
        Console.WriteLine("== quest rework B3b Outlands group 7: the call-mode reply kinds ==");
        OL7Antidote();
        OL7Gullibles();
        OL7EvenMore();
        OL7Guardian();
        OL7Missionary();
    }

    static void OL7Antidote()
    {
        var def = new QC.Outlands.DontForgetTheAntidote().Compile();
        string edigey = QC.Npc.Rabao.Edigey;
        var offer = new Fake { Status = QuestStatus.Available };
        offer.Fame[QC.FameArea.SelbinaRabao] = 4;
        var vial = new Fake { Status = QuestStatus.Accepted };
        vial.TradeItems[QC.Item.VialOfDesertVenom] = 1;
        int v = QC.Item.VialOfDesertVenom;
        var accepted = new Fake { Status = QuestStatus.Accepted };
        var completed = new Fake { Status = QuestStatus.Completed };
        Check("outlands g7:Don't Forget the Antidote's replies are events (call mode): Edigey's 2 / 3 / 5; his trade's 4",
              $"Event(2,0,{v})|Event(3,0,{v})|Event(5,0,{v})|Event(4,0,{v})",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: edigey), offer)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: edigey), accepted)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: edigey), completed)}|" +
              OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrade, entity: edigey), vial));
    }

    static void OL7Gullibles()
    {
        var def = new QC.Outlands.GulliblesTravels().Compile();
        string mag = QC.Npc.Kazham.Magriffon;
        var asked = new Fake { Status = QuestStatus.Accepted };
        asked.CharVars["MAGRIFFON_GIL_REQUEST"] = 13000;
        Fake Gil(int gil)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.CharVars["MAGRIFFON_GIL_REQUEST"] = 13000;
            f.TradeItems[65535] = gil;
            return f;
        }
        var offer = new Fake { Status = QuestStatus.Available };
        offer.Fame[QC.FameArea.Windurst] = 6;
        offer.Rolls.Enqueue(13);
        Check("outlands g7:Gullible's Travels' replies are events (call mode): Magriffon's 145 / 144; his trade's 146; " +
              "less gil: NoAction (the script's elseif ends the trade)",
              "Event(145,0,13000)|Event(144,0,13000)|Event(146)|NoAction()",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), asked)}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: mag), offer)}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrade, entity: mag), Gil(13000))}|" +
              OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrade, entity: mag), Gil(12999)));
        // math.randomInt(10, 30) * 1000: .NET's seeded NextInt64 draws 5-bit values for both ranges, so (10, 29) differs
        // only on a draw of 30, which the flow never makes (mutant g8); the bounds are pinned here
        var draw = new Fake { Status = QuestStatus.Available };
        draw.Fame[QC.FameArea.Windurst] = 6;
        OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: mag), draw);
        Check("outlands g7:Gullible's Travels' sum is drawn as math.randomInt(10, 30) * 1000",
              "True", $"{draw.Calls.Contains("Player.RandomInt(10,30)")}");
    }

    static void OL7EvenMore()
    {
        var def = new QC.Outlands.EvenMoreGulliblesTravels().Compile();
        string mag = QC.Npc.Kazham.Magriffon;
        string flower = QC.Npc.YuhtungaJungle.BlueRafflesia;
        Fake Progress(int progress)
        {
            var f = new Fake { Status = QuestStatus.Accepted };
            f.CharVars["EVEN_MORE_GULLIBLES_PROGRESS"] = progress;
            return f;
        }
        Fake Done(QuestStatus status, int windurst)
        {
            var f = new Fake { Status = status };
            f.QuestStatuses["Outlands/GULLIBLES_TRAVELS"] = QuestStatus.Completed;
            f.Fame[QC.FameArea.Windurst] = windurst;
            return f;
        }
        var notDone = new Fake { Status = QuestStatus.Available };
        notDone.Fame[QC.FameArea.Windurst] = 7;
        var gil = Progress(0);
        gil.TradeItems[65535] = 35000;
        // the Blue Rafflesias: 17281586 is BLUE_RAFFLESIA_OFFSET (GetFirstID('Blue_Rafflesia')), the next two its +1 / +2
        Fake Flower(int offset, int progress)
        {
            var f = Progress(progress);
            f.NpcIds["BLUE_RAFFLESIA_OFFSET"] = 17281586;
            f.Npc = new QuestNpcFake(17281586 + offset, f.Calls);
            return f;
        }
        Check("outlands g7:Even More Gullible's Travels' replies are events (call mode): Magriffon's 149 / 151 / 152 / 148 / " +
              "147, his trade's 150; nothing (the script's 143) before Gullible's Travels is completed; the flowers' 21 / 22 / " +
              "23 and FLOWER_BLOOMING",
              "Event(149,0,256,0,0,0,35000)|Event(151)|Event(152,0,1144,256)|Event(148,0,256,0,0,35000)|Event(147)|" +
              "Event(150,0,256)|null|Event(21)|Event(22)|Event(23)|MessageSpecial(123:FLOWER_BLOOMING)",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), Progress(0))}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), Progress(1))}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), Progress(2))}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), Done(QuestStatus.Available, 7))}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), Done(QuestStatus.Available, 6))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrade, entity: mag), gil)}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: mag), notDone)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: flower), Flower(0, 1))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: flower), Flower(1, 1))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: flower), Flower(2, 1))}|" +
              OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: flower), Flower(0, 2)));
    }

    static void OL7Guardian()
    {
        var def = new QC.Outlands.GreetingsToTheGuardian().Compile();
        string hari = QC.Npc.Kazham.HariPakhroib;
        string altar = QC.Npc.IfritsCauldron.AltarofAshes;
        Fake Pamamas(QuestStatus status, int pamamas)
        {
            var f = new Fake { Status = status };
            f.CharVars["PamamaVar"] = pamamas;
            return f;
        }
        var offer = new Fake { Status = QuestStatus.Available };
        offer.Fame[QC.FameArea.Windurst] = 7;
        var offering = new Fake { Status = QuestStatus.Accepted };
        offering.TradeItems[QC.Item.BunchOfWildPamamas] = 1;
        int p = QC.Item.BunchOfWildPamamas;
        Check("outlands g7:Greetings to the Guardian's replies are events (call mode): Hari Pakhroib's 71 / 69 / 68 / 71 / 72; " +
              "the altar's offering answers NoAction",
              $"Event(71)|Event(69,0,{p})|Event(68,{p},{p},{p})|Event(71)|Event(72)|NoAction()",
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: hari), Pamamas(QuestStatus.Accepted, 1))}|" +
              $"{OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrigger, entity: hari), Pamamas(QuestStatus.Accepted, 0))}|" +
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: hari), offer)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: hari), Pamamas(QuestStatus.Completed, 2))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: hari), Pamamas(QuestStatus.Completed, 0))}|" +
              OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrade, entity: altar), offering));
    }

    static void OL7Missionary()
    {
        var def = new QC.Outlands.MissionaryMan().Compile();
        string rau = QC.Npc.Kazham.Rauteinot;
        Fake Var(QuestStatus status, int value)
        {
            var f = new Fake { Status = status };
            f.CharVars["MissionaryManVar"] = value;
            return f;
        }
        var offer = new Fake { Status = QuestStatus.Available };
        offer.Fame[QC.FameArea.Windurst] = 3;
        var marble = Var(QuestStatus.Accepted, 1);
        marble.TradeItems[QC.Item.SlabOfElshimoMarble] = 1;
        int s = QC.Item.SlabOfElshimoMarble;
        var done = new Fake { Status = QuestStatus.Completed };
        Check("outlands g7:Missionary Man's replies are events (call mode): Rauteinot's 137 / 138 / 140 / 141 / 142; his trade's 139",
              $"Event(137,0,{s})|Event(138,0,{s})|Event(140)|Event(141)|Event(142)|Event(139)",
              $"{OLReply(OLHandler(def, 1, QuestHandlerKind.OnTrigger, entity: rau), offer)}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: rau), Var(QuestStatus.Accepted, 1))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: rau), Var(QuestStatus.Accepted, 3))}|" +
              $"{OLReply(OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: rau), Var(QuestStatus.Accepted, 4))}|" +
              $"{OLReply(OLHandler(def, 3, QuestHandlerKind.OnTrigger, entity: rau), done)}|" +
              OLReply(OLHandler(def, 0, QuestHandlerKind.OnTrade, entity: rau), marble));
    }
}
