// XI.Verify.Quests -- quest rework B3b Outlands group 2 (Kazham: A Question of Taste, Everyone's Grudging, Cloak and
// Dagger, You Call That a Knife, Trial by Fire): what the golden traces cannot show.
// - You Call That a Knife's 28 finish: Chef Nonberry's local var cooldown is an NPC local var, not in a trace's DB rows,
//   and GetSystemTime() reads the harness's wall clock, a constant (1800000000) that only `wait jst=<days>` moves, by whole
//   days, so a flow sees the cooldown only as "ahead" or "not ahead": the fake pins the value (now + 600), that it is
//   set only on a pop, and the pop's arguments.
// - A Question of Taste's completed Angelica reply 773 carries replaceDefault: nothing else answers Angelica at an Event
//   priority while A Question of Taste is completed, so its packets are the same with a plain event.
// - quest:complete's false arm: none of the five rewards has an item, so npcUtil.completeQuest cannot fail and no flow
//   reaches the arm; the fake shows each guarded finish doing nothing more (A Question of Taste's 50 and 57, Cloak and
//   Dagger's 284, You Call That a Knife's 133), and Trial by Fire's 273 going on as its Lua does (the result not read).
// - the two offers' progressCutscene (A Question of Taste's 44, Everyone's Grudging's 107): a cutscene locks the char (no
//   event skip, mobs leave it alone), which no flow step shows; the packets are a progressEvent's.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunOutlandsGroup2()
    {
        Console.WriteLine("== quest rework B3b Outlands group 2: Nonberry's cooldown, 773's replaceDefault, complete false ==");
        OL2Cooldown();
        OL2ReplaceDefault();
        OL2CompleteFalse();
        OL2Cutscenes();
    }

    static void OL2Cutscenes()
    {
        var aqot = new QC.Outlands.AQuestionOfTaste().Compile();
        var eg = new QC.Outlands.EveryonesGrudging().Compile();
        var offer = new Fake();
        Check("outlands g2:the offers are progress cutscenes: A Question of Taste's 44 (Etteh), Everyone's Grudging's 107 (Jakoh)",
              $"ProgressCutscene(44)|ProgressCutscene(107,0,{QC.Item.RancorFlame})",
              $"{OLReply(OLHandler(aqot, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Kazham.EttehSulaej), offer)}|" +
              $"{OLReply(OLHandler(eg, 0, QuestHandlerKind.OnTrigger, entity: QC.Npc.Kazham.JakohWahcondalo), offer)}");
    }

    static void OL2Cooldown()
    {
        var def = new QC.Outlands.YouCallThatAKnife().Compile();
        // section 2 (accepted, no knife): the 28 finish
        var h = OLHandler(def, 1, QuestHandlerKind.OnEventFinish, id: 28);
        string Finish(int cooldown, bool pops)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Csid = 28, Now = 1800000000, PopResult = pops };
            f.Npc = new QuestNpcFake(17428817, f.Calls);
            f.Npc.LocalVars["cooldown"] = cooldown;
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        string pop = $"Player.PopFromQmSeries({QC.Mob.TempleOfUggalepih.COOK_OFFSET},4,0,True,False)";
        string popped = Finish(0, true), ahead = Finish(1800000001, true), atNow = Finish(1800000000, true), up = Finish(1800000000, false);
        Console.WriteLine("  cooldown 0: " + popped);
        Console.WriteLine("  cooldown now + 1: " + ahead);
        Console.WriteLine("  cooldown now, the cooks up: " + up);
        Check("outlands g2:28 takes the trade, pops the four cooks (claimed, not hidden, look false) and sets cooldown to exactly now + 600",
              $"Trade.Complete();{pop};Npc.SetLocalVar(cooldown,1800000600)", popped);
        Check("outlands g2:28 at cooldown == now pops too (now >= cooldown)",
              $"Trade.Complete();{pop};Npc.SetLocalVar(cooldown,1800000600)", atNow);
        Check("outlands g2:28 with the cooldown ahead: the trade taken, no pop, the cooldown not set", "Trade.Complete()", ahead);
        Check("outlands g2:28 when popFromQM pops nothing: the cooldown not set", $"Trade.Complete();{pop}", up);
    }

    static void OL2ReplaceDefault()
    {
        var def = new QC.Outlands.AQuestionOfTaste().Compile();
        // section 3 (completed): Angelica
        var h = OLHandler(def, 2, QuestHandlerKind.OnTrigger, entity: QC.Npc.WindurstWaters.Angelica);
        Check("outlands g2:A Question of Taste's completed Angelica: 773 replaces the default (Prog 0 and 3), 772 at Prog 1 does not",
              "Event(773):replaceDefault()|Event(773):replaceDefault()|Event(772)",
              $"{OLReply(h, new Fake { Status = QuestStatus.Completed })}|{OLReply(h, OL2WithStep(3))}|{OLReply(h, OL2WithStep(1))}");
    }

    static Fake OL2WithStep(int step)
    {
        var f = new Fake { Status = QuestStatus.Completed };
        f.Vars["Prog"] = step;
        return f;
    }

    static void OL2CompleteFalse()
    {
        string Run(QuestHandlerDecl h, int option = 0)
        {
            var f = new Fake { CompleteResult = false, Option = option, JstDay = 1800025200 };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        var aqot = new QC.Outlands.AQuestionOfTaste().Compile();
        var cd = new QC.Outlands.CloakAndDagger().Compile();
        var yk = new QC.Outlands.YouCallThatAKnife().Compile();
        var tbf = new QC.Outlands.TrialByFire().Compile();
        Check("outlands g2:complete false: A Question of Taste's 50 (Kazham) and 57, Cloak and Dagger's 284, You Call That a Knife's 133 do nothing more",
              "Quest.Complete()|Quest.Complete()|Quest.Complete()|Quest.Complete()",
              $"{Run(OLHandler(aqot, 1, QuestHandlerKind.OnEventFinish, id: 50, zone: QC.Zone.Kazham))}|{Run(OLHandler(aqot, 2, QuestHandlerKind.OnEventFinish, id: 57))}|" +
              $"{Run(OLHandler(cd, 1, QuestHandlerKind.OnEventFinish, id: 284))}|{Run(OLHandler(yk, 2, QuestHandlerKind.OnEventFinish, id: 133))}");
        Check("outlands g2:complete false: Trial by Fire's 273 (option 5) goes on, as its Lua does (quest:complete's result not read)",
              "Player.GiveCurrency(gil,10000);Quest.Complete();" +
              $"Player.DelKeyItem({QC.KeyItem.WhisperOfFlames});Quest.SetVar(Timer,1,1800025200)",
              Run(OLHandler(tbf, 1, QuestHandlerKind.OnEventFinish, id: 273), 5));
    }
}
