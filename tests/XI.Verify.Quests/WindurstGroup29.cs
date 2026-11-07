// XI.Verify.Quests -- quest rework B3b Windurst group 29 (call-only: Fire and Brimstone and Unbridled Passion, RNG
// AF2 / AF3 at Perih Vashai). The golden flows take every arm a char reaches in play, the GM-set states (the vars 1-8
// on an available, accepted or completed char, Sin Hunting available at the AF level), the level gates' boundaries
// (49 / 50 at the default AF levels, 49 at 49 and 49.4 by setting), not RNG, the full inventory and the rare reward
// held (the completed char's replay of 537 / 546), the non-exact earring trade, CoP 5-3's mission events at Perih
// Vashai, and the interleaving of the two quests' arms. These checks add only what no flow can see (fake-only): the
// replies' kinds (in an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
// player:startEvent: ZoneHost.QuestCalls.cs RenderCallReply) and the finishes' calls in order, a refused completion's
// among them.
// W15Handlers is in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup29()
    {
        Console.WriteLine("== quest rework B3b Windurst group 29: the reply kinds and the finishes (call-only) ==");
        var fab = new QC.Windurst.FireAndBrimstone().Compile();
        var up = new QC.Windurst.UnbridledPassion().Compile();
        const string FabVar = "fireAndBrimstone", UpVar = "unbridledPassion";
        const string Sin = "Windurst/SIN_HUNTING", Fab = "Windurst/FIRE_AND_BRIMSTONE";

        string Problems(QuestScriptDefinition def) => string.Join(" | ", QuestScriptValidator.Validate(def));
        Check("windurst g29:Fire and Brimstone clean", "", Problems(fab));
        Check("windurst g29:Unbridled Passion clean", "", Problems(up));

        // a fresh fake: that status, the other quest's status, the var, the job and level, AF2 / AF3_QUEST_LEVEL 50
        Fake F(QuestStatus status, string other, QuestStatus otherStatus, string var, int value, int job, int lvl)
        {
            var f = new Fake { Status = status, MainJob = job, MainLevel = lvl };
            f.MainNumbers["AF2_QUEST_LEVEL"] = 50;
            f.MainNumbers["AF3_QUEST_LEVEL"] = 50;
            f.QuestStatuses[other] = otherStatus;
            f.CharVars[var] = value;
            return f;
        }
        string Say(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);
        string Reply(QuestScriptDefinition def, QuestHandlerKind kind, Fake f) =>
            Say(W15Handlers(def, kind).Single(h => h.Entity == "Perih_Vashai").Reply(f));
        const QuestHandlerKind T = QuestHandlerKind.OnTrigger, Tr = QuestHandlerKind.OnTrade;
        const int Rng = QC.Job.Rng, War = QC.Job.War;
        const QuestStatus Av = QuestStatus.Available, Ac = QuestStatus.Accepted, Co = QuestStatus.Completed;

        // Fire and Brimstone
        string FabT(QuestStatus s, QuestStatus sin, int var, int job = Rng, int lvl = 50) =>
            Reply(fab, T, F(s, Sin, sin, FabVar, var, job, lvl));
        Check("windurst g29:Perih Vashai's Fire and Brimstone replies, as Event",
              "Event(531)|Event(528)|Event(528)|null|null|null|Event(532)|Event(532)|Event(532)|" +
              "Event(535,0,13360,1113)|Event(536,0,13360,1113)|null|Event(532)|Event(536,0,13360,1113)",
              string.Join("|", FabT(Av, Co, 0), FabT(Av, Co, 0, War), FabT(Av, Co, 0, Rng, 49), FabT(Av, Av, 0),
                  FabT(Ac, Co, 0), FabT(Co, Co, 0), FabT(Ac, Co, 1), FabT(Ac, Co, 3), FabT(Av, Av, 2), FabT(Ac, Co, 4),
                  FabT(Ac, Co, 5), FabT(Ac, Co, 6), FabT(Co, Co, 1), FabT(Co, Co, 5)));
        string FabTr(QuestStatus s, int var, params (int Item, int Qty)[] items)
        {
            var f = F(s, Sin, Co, FabVar, var, Rng, 60);
            foreach (var (item, qty) in items) f.TradeItems[item] = qty;
            return Reply(fab, Tr, f);
        }
        var earring = (QC.Item.OldEarring, 1);
        Check("windurst g29:Perih Vashai's trade: the var 5 and the earring (any status, not exact), as Event",
              "Event(537,0,13360)|Event(537,0,13360)|Event(537,0,13360)|Event(537,0,13360)|null|null|null",
              string.Join("|", FabTr(Ac, 5, earring), FabTr(Av, 5, earring), FabTr(Co, 5, earring),
                  FabTr(Ac, 5, earring, (QC.Item.BlockOfAnimalGlue, 1)), FabTr(Ac, 4, earring), FabTr(Ac, 6, earring),
                  FabTr(Ac, 5, (QC.Item.BlockOfAnimalGlue, 1))));

        // Unbridled Passion
        string UpT(QuestStatus s, QuestStatus fabStatus, int var, int job = Rng, int lvl = 50) =>
            Reply(up, T, F(s, Fab, fabStatus, UpVar, var, job, lvl));
        Check("windurst g29:Perih Vashai's Unbridled Passion replies, as Event",
              "Event(541,0,13360)|null|null|null|null|null|null|Event(542)|Event(542)|Event(546,0,14099)|null|" +
              "Event(542)|Event(546,0,14099)",
              string.Join("|", UpT(Av, Co, 0), UpT(Av, Co, 0, War), UpT(Av, Co, 0, Rng, 49), UpT(Av, Ac, 0),
                  UpT(Av, Av, 0), UpT(Ac, Co, 0), UpT(Co, Co, 0), UpT(Ac, Co, 1), UpT(Ac, Co, 6), UpT(Ac, Co, 7),
                  UpT(Ac, Co, 8), UpT(Co, Co, 3), UpT(Av, Co, 7, War)));

        // the finishes' calls, in order, on that option and with npcUtil.giveItem's answer (the reward item, which
        // the written-out npcUtil.completeQuest gives first)
        string Fin(QuestScriptDefinition def, int csid, int option = 0, bool complete = true)
        {
            var h = def.States.SelectMany(s => s.Handlers)
                       .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, GiveResult = complete };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g29:Fire and Brimstone's finishes: 531 (options 0 / 1), 535, 537 given / refused",
              "Quest.Begin();Player.SetCharVar(fireAndBrimstone,1,0)|" +
              "Quest.Begin();Player.SetCharVar(fireAndBrimstone,1,0)|" +
              "Player.SetCharVar(fireAndBrimstone,5,0)|" +
              "Player.GiveItem(12518);Player.AddFame(2,20);Player.SetCharVar(fireAndBrimstone,0,0);" +
              "Player.CompleteQuest(Windurst,FIRE_AND_BRIMSTONE);Trade.Confirm()|Player.GiveItem(12518)",
              string.Join("|", Fin(fab, 531), Fin(fab, 531, 1), Fin(fab, 535), Fin(fab, 537), Fin(fab, 537, 0, false)));
        Check("windurst g29:Unbridled Passion's finishes: 541 (options 0 / 2), 546 given / refused",
              "Quest.Begin();Player.SetCharVar(unbridledPassion,1,0)|" +
              "Quest.Begin();Player.SetCharVar(unbridledPassion,1,0)|" +
              "Player.GiveItem(14099);Player.AddFame(2,20);Player.SetCharVar(unbridledPassion,0,0);" +
              "Player.CompleteQuest(Windurst,UNBRIDLED_PASSION);Player.DelKeyItem(236)|Player.GiveItem(14099)",
              string.Join("|", Fin(up, 541), Fin(up, 541, 2), Fin(up, 546), Fin(up, 546, 0, false)));
    }
}
