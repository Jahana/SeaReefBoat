// XI.Verify.Quests -- quest rework B3b Jeuno group 15 (the twenty Unlocking a Myth quests). The golden flows take every
// arm at the global's 250 points; the era module modules/era/lua/globals/equipment/unlocking_a_myth.lua overrides the Lua
// global xi.equipment.vigilWeaponRequiredWsPoints, which no golden flow can load on the C# side (the C# server has no Lua
// module loader, and the harness fails a module line no C# module ports). So the trade's thresholds at the era's values
// are shown here on the fake (Player.VigilWeaponRequiredWsPoints set as the era would answer), and XI.Verify.QuestScripts.Live
// shows the live member calling the overridden global. The helpers.lua lines cited are the file at 77e0890a (its
// UnlockingAMyth template is gone since carry-quests-1).
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static readonly QuestScript[] JG15Quests =
    {
        new QC.Jeuno.UnlockingAMythWarrior(), new QC.Jeuno.UnlockingAMythMonk(), new QC.Jeuno.UnlockingAMythWhiteMage(),
        new QC.Jeuno.UnlockingAMythBlackMage(), new QC.Jeuno.UnlockingAMythRedMage(), new QC.Jeuno.UnlockingAMythThief(),
        new QC.Jeuno.UnlockingAMythPaladin(), new QC.Jeuno.UnlockingAMythDarkKnight(), new QC.Jeuno.UnlockingAMythBeastmaster(),
        new QC.Jeuno.UnlockingAMythBard(), new QC.Jeuno.UnlockingAMythRanger(), new QC.Jeuno.UnlockingAMythSamurai(),
        new QC.Jeuno.UnlockingAMythNinja(), new QC.Jeuno.UnlockingAMythDragoon(), new QC.Jeuno.UnlockingAMythSummoner(),
        new QC.Jeuno.UnlockingAMythBlueMage(), new QC.Jeuno.UnlockingAMythCorsair(), new QC.Jeuno.UnlockingAMythPuppetmaster(),
        new QC.Jeuno.UnlockingAMythDancer(), new QC.Jeuno.UnlockingAMythScholar(),
    };

    // the Warrior's accepted trade of a Sturdy Axe at the points, the global answering required
    static string JG15Trade(QuestScriptDefinition def, int required, int points)
    {
        var f = new Fake { Status = QuestStatus.Accepted, MainJob = QC.Job.War, VigilWsPoints = required };
        f.TradeItems[QC.Item.SturdyAxe] = 1;
        f.TradeWeaponskillPoints[0] = points;
        var trade = def.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrade);
        var r = trade.Reply(f);
        return r == null ? "null" : QuestContextFake.Describe(r);
    }

    static void RunJeunoGroup15()
    {
        System.Console.WriteLine("== quest rework B3b Jeuno group 15: Unlocking a Myth ==");
        Check("jg15m:the twenty clean", string.Concat(Enumerable.Repeat("/", 19)),
              string.Join("/", JG15Quests.Select(q => string.Join(" | ", QuestScriptValidator.Validate(q.Compile())))));

        // each subclass's job: its quest constant (UNLOCKING_A_MYTH_WARRIOR - 1 + jobId), its Lua file, its Vigil weapon and
        // its xi.wsUnlock, as vigilWeaponsData (helpers.lua:125-246) and the Lua files have them
        Check("jg15m:the twenty name their job, quest constant, Lua file, weapon and weapon skill",
              "1:UNLOCKING_A_MYTH_WARRIOR:Unlocking_A_Myth_WAR:18492:24,2:UNLOCKING_A_MYTH_MONK:Unlocking_A_Myth_MNK:18753:15," +
              "3:UNLOCKING_A_MYTH_WHITE_MAGE:Unlocking_A_Myth_WHM:18851:29,4:UNLOCKING_A_MYTH_BLACK_MAGE:Unlocking_A_Myth_BLM:18589:30," +
              "5:UNLOCKING_A_MYTH_RED_MAGE:Unlocking_A_Myth_RDM:17742:20,6:UNLOCKING_A_MYTH_THIEF:Unlocking_A_Myth_THF:18003:17," +
              "7:UNLOCKING_A_MYTH_PALADIN:Unlocking_A_Myth_PLD:17744:21,8:UNLOCKING_A_MYTH_DARK_KNIGHT:Unlocking_A_Myth_DRK:18944:25," +
              "9:UNLOCKING_A_MYTH_BEASTMASTER:Unlocking_A_Myth_BST:17956:23,10:UNLOCKING_A_MYTH_BARD:Unlocking_A_Myth_BRD:18034:18," +
              "11:UNLOCKING_A_MYTH_RANGER:Unlocking_A_Myth_RNG:18719:33,12:UNLOCKING_A_MYTH_SAMURAI:Unlocking_A_Myth_SAM:18443:28," +
              "13:UNLOCKING_A_MYTH_NINJA:Unlocking_A_Myth_NIN:18426:27,14:UNLOCKING_A_MYTH_DRAGOON:Unlocking_A_Myth_DRG:18120:26," +
              "15:UNLOCKING_A_MYTH_SUMMONER:Unlocking_A_Myth_SMN:18590:31,16:UNLOCKING_A_MYTH_BLUE_MAGE:Unlocking_A_Myth_BLU:17743:22," +
              "17:UNLOCKING_A_MYTH_CORSAIR:Unlocking_A_Myth_COR:18720:34,18:UNLOCKING_A_MYTH_PUPPETMASTER:Unlocking_A_Myth_PUP:18754:16," +
              "19:UNLOCKING_A_MYTH_DANCER:Unlocking_A_Myth_DNC:19102:19,20:UNLOCKING_A_MYTH_SCHOLAR:Unlocking_A_Myth_SCH:18592:32",
              string.Join(",", JG15Quests.Select((q, i) =>
              {
                  var j = QC.Jeuno.UnlockingAMyth.Jobs[i];
                  return $"{j.Job}:{q.ConstName}:{q.LegacyPath.Substring("scripts/quests/jeuno/".Length)}:{j.ItemId}:{j.WsUnlockId}";
              })));

        // the fake's member: the global's 250 unless a test sets the era's value
        Check("jg15m:the fake answers VigilWeaponRequiredWsPoints: 250, or the value a test sets", "250/880",
              $"{new Fake().Player.VigilWeaponRequiredWsPoints}/{new Fake { VigilWsPoints = 880 }.Player.VigilWeaponRequiredWsPoints}");

        // the era's values (NyzulFloorProgress 0: 16000; 80: 880; 100: 250): the trade reads the member, not a constant 250
        var war = JG15Quests[0].Compile();
        Check("jg15m:the trade at the era's 16000 / 880 / 250 required, a 250-point axe: 10091 / 10092 / 10088 (1)",
              "Event(10091)|Event(10092)|ProgressEvent(10088,1)",
              $"{JG15Trade(war, 16000, 250)}|{JG15Trade(war, 880, 250)}|{JG15Trade(war, 250, 250)}");
        // 880 required: / 5 = 176, * 4 / 5 = 704
        Check("jg15m:the trade's edges at 880 required: 176 / 177 / 704 / 705 / 879 / 880 points",
              "Event(10091)|Event(10092)|Event(10092)|Event(10093)|Event(10093)|ProgressEvent(10088,1)",
              string.Join("|", new[] { 176, 177, 704, 705, 879, 880 }.Select(p => JG15Trade(war, 880, p))));

        // Zalsuhm's trigger replies and their kinds, as helpers.lua:274-284 and :330-332 have them. The golden flows can't
        // tell these kinds apart: Zalsuhm has no script (nothing to alternate with), his default 10085 ranks below an
        // Event, and only the main job's quest has a section; a cutscene's lock (no spell lands on a char in one) shows to
        // no flow statement. So they are pinned here: upset with the zoning flag: progress 10090; worn: progress 10086 (1);
        // not worn: progress 10085; accepted: the cutscene 10087
        var trigAvailable = war.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger);
        var trigAccepted = war.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger);
        static string R(QuestHandlerDecl h, Fake f) { var r = h.Reply(f); return r == null ? "null" : QuestContextFake.Describe(r); }
        var upset = new Fake { Status = QuestStatus.Available, MainJob = QC.Job.War, ZoningFlag = true };
        upset.Vars["Upset_Zalsuhm"] = 1;
        var worn = new Fake { Status = QuestStatus.Available, MainJob = QC.Job.War };
        worn.Equipment[QC.Slot.Main] = QC.Item.SturdyAxe;
        Check("jg15m:Zalsuhm's trigger replies keep the Lua's kinds: progress 10090 / 10086 (1) / 10085, the cutscene 10087",
              "ProgressEvent(10090)|ProgressEvent(10086,1)|ProgressEvent(10085)|Cutscene(10087)",
              $"{R(trigAvailable, upset)}|{R(trigAvailable, worn)}|" +
              $"{R(trigAvailable, new Fake { Status = QuestStatus.Available, MainJob = QC.Job.War })}|" +
              $"{R(trigAccepted, new Fake { Status = QuestStatus.Accepted, MainJob = QC.Job.War })}");

        // 10088's finish when quest:complete answers false (helpers.lua:338-343): no MYTHIC_LEARNED, no weapon skill. No
        // flow can take it: the quest has no reward table, and npcUtil.completeQuest fails only on an item reward (a full
        // bag changes nothing), so the arm is waived in the golden flows and shown here on the fake
        var finish = war.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 10088);
        string Finish(bool completes)
        {
            var f = new Fake { Status = QuestStatus.Accepted, MainJob = QC.Job.War, CompleteResult = completes };
            finish.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("jg15m:10088's finish: complete true: MYTHIC_LEARNED (1), King's Justice (24); complete false: nothing more",
              $"Quest.Complete();Text.Special({QC.Zone.LowerJeuno}:MYTHIC_LEARNED,1);Player.AddLearnedWeaponskill(24)|Quest.Complete()",
              $"{Finish(true)}|{Finish(false)}");
    }
}
