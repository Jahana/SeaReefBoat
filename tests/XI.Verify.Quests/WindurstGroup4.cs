// XI.Verify.Quests -- quest rework B3b Windurst group 4 (Curses, Foiled Again! (1) and (2), Curses, Foiled A-Golem!?,
// Blood and Glory, Scooped!): the facet members the group added, on the fake; the calls and reply modifiers no trace can
// see (setTitle and addTitle are one binding body; a replaceDefault where no other quest's Event line ties with it sends
// the packets a plain event sends: Hiwon-Biwon's 185, Chyuk-Kochak's 682, Naiko-Paneiko's 663).
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup4()
    {
        Console.WriteLine("== quest rework B3b Windurst group 4: facets ==");
        RunWindurstGroup4Facets();
        RunWindurstGroup4Modifiers();
    }

    // player:getCharSkillLevel, player:setTitle, player:addLearnedWeaponskill, player:setPos(x, y, z) and
    // trade:getItem(slot):getWeaponskillPoints()
    static void RunWindurstGroup4Facets()
    {
        var f = new Fake();
        f.Skills[QC.Skill.Staff] = 2300;
        f.TradeWeaponskillPoints[0] = 300;
        string reads = $"{f.Player.CharSkillLevel(QC.Skill.Staff)}/{f.Player.CharSkillLevel(QC.Skill.Sword)}/" +
                       $"{f.Trade.ItemWeaponskillPoints(0)}/{f.Trade.ItemWeaponskillPoints(1)}";
        f.Player.SetTitle(QC.Title.TotalLoser);
        f.Player.AddLearnedWeaponskill(12);
        f.Player.SetPos(121, -3, 111.5);
        Check("windurst g4 facets:the fake answers CharSkillLevel (Skills, 0 when not set) and ItemWeaponskillPoints (by slot, 0 when not set); SetTitle, AddLearnedWeaponskill and SetPos(x, y, z) are recorded",
              "2300/0/300/0|Player.SetTitle(46);Player.AddLearnedWeaponskill(12);Player.SetPos(121,-3,111.5)",
              $"{reads}|{string.Join(";", f.Calls)}");
    }

    // The titles: Curses (2)'s 180 (option 3) and the Golem's 340 refusal use setTitle, the Golem's 342 addTitle (the
    // binding gives both one body, so the trace is the same either way). The completed sections' replaceDefault replies:
    // Curses (2)'s Hiwon-Biwon 185, Scooped!'s Chyuk-Kochak 682 (no script; the default is another event, but nothing
    // else answers) and Naiko-Paneiko 663 (his script's own 663 after Making Headlines) are seen by no trace: no other
    // quest's Event line ties with them there. Shantotto's 184 (Curses (2)) and 343 (the Golem) are also checked here,
    // but a trace sees them: without replaceDefault each ties with another quest's Event line and the two cycle (184 with
    // the Golem's 341 at the Golem's #guard step 11, 343 with Blood and Glory's 445 / 446 at its main step 11).
    static void RunWindurstGroup4Modifiers()
    {
        var c2 = new QC.Windurst.CursesFoiledAgain2().Compile();
        var ag = new QC.Windurst.CursesFoiledAGolem().Compile();
        var sc = new QC.Windurst.Scooped().Compile();
        string Finish(QuestScriptDefinition def, int state, int csid, int option, QuestStatus status)
        {
            var f = new Fake { Status = status, Csid = csid, Option = option };
            G2Handler(def, state, QuestHandlerKind.OnEventFinish, null, csid).Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g4 modifiers:Curses (2)'s 180 option 3 and the Golem's 340 refusal set the title, the Golem's 342 adds it",
              "Quest.Begin();Player.SetTitle(134)|Player.SetTitle(46)|Player.AddTitle(46);Quest.Complete()",
              $"{Finish(c2, 0, 180, 3, QuestStatus.Available)}|{Finish(ag, 0, 340, 0, QuestStatus.Available)}|" +
              $"{Finish(ag, 1, 342, 0, QuestStatus.Accepted)}");

        var done = new Fake { Status = QuestStatus.Completed };
        Check("windurst g4 modifiers:the completed sections' 185, 184, 343, 682 and 663 replace the default",
              "Event(185):replaceDefault()|Event(184):replaceDefault()|Event(343):replaceDefault()|Event(682):replaceDefault()|Event(663):replaceDefault()",
              $"{G3Reply(c2, 2, QC.Npc.WindurstWalls.HiwonBiwon, done)}|{G3Reply(c2, 2, QC.Npc.WindurstWalls.Shantotto, done)}|" +
              $"{G3Reply(ag, 2, QC.Npc.WindurstWalls.Shantotto, done)}|{G3Reply(sc, 2, QC.Npc.WindurstWaters.ChyukKochak, done)}|" +
              $"{G3Reply(sc, 2, QC.Npc.WindurstWaters.NaikoPaneiko, done)}");
    }
}
