// XI.Verify.Quests -- quest rework B3b Jeuno group 16 (the fifteen Borghertz's Hands quests, the template
// BorghertzQuests). The golden flows take every arm; these checks also pin each subclass's params row against its Lua
// file, and the reply kinds. An Event and a ProgressEvent send the same 0x032. At an NPC with a script (Yin Pocanakhu,
// Deadly Minnow) the kind shows through the fallback: an action at Event priority makes the next talk run the NPC's own
// script first (interaction_lookup.lua), which the Warring Hands' #yin flow takes. Guslam and qm1 have no NPC script,
// no rival quest or mission line (Lua or C#), no importantEvent, and only Guslam's DefaultActions 154 at Default
// priority, so no talk can show the kind: structurally equivalent in golden, the fake pins all nine. The helpers.lua
// lines cited are the file at 77e0890a (its BorghertzQuests template is gone since carry-quests-1).
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static readonly QuestScript[] JG16Quests =
    {
        new QC.Jeuno.BorghertzsWarringHands(), new QC.Jeuno.BorghertzsStrikingHands(), new QC.Jeuno.BorghertzsHealingHands(),
        new QC.Jeuno.BorghertzsSorcerousHands(), new QC.Jeuno.BorghertzsVermillionHands(), new QC.Jeuno.BorghertzsSneakyHands(),
        new QC.Jeuno.BorghertzsStalwartHands(), new QC.Jeuno.BorghertzsShadowyHands(), new QC.Jeuno.BorghertzsWildHands(),
        new QC.Jeuno.BorghertzsHarmoniousHands(), new QC.Jeuno.BorghertzsChasingHands(), new QC.Jeuno.BorghertzsLoyalHands(),
        new QC.Jeuno.BorghertzsLurkingHands(), new QC.Jeuno.BorghertzsDragonHands(), new QC.Jeuno.BorghertzsCallingHands(),
    };

    static void RunJeunoGroup16()
    {
        System.Console.WriteLine("== quest rework B3b Jeuno group 16: Borghertz's Hands ==");
        Check("jg16b:the fifteen clean", string.Concat(Enumerable.Repeat("/", 14)),
              string.Join("/", JG16Quests.Select(q => string.Join(" | ", QuestScriptValidator.Validate(q.Compile())))));

        // each subclass: its quest constant, its Lua file and its params row (helpers.lua's params, as each of the fifteen
        // Borghertzs_<Name>_Hands.lua files has them: handAFId, requiredLogId / requiredQuestId, requiredJobId,
        // oldGauntletZoneId, optionalZoneId1 / 2, optionalArtifact1 / 2)
        Check("jg16b:the fifteen name their quest constant, Lua file and params",
              "BORGHERTZS_WARRING_HANDS:Borghertzs_Warring_Hands:13961:Bastok/THE_TALEKEEPERS_TRUTH:1:195:161:197:14214:12511," +
              "BORGHERTZS_STRIKING_HANDS:Borghertzs_Striking_Hands:13962:Bastok/THE_FIRST_MEETING:2:197:147:200:12639:12512," +
              "BORGHERTZS_HEALING_HANDS:Borghertzs_Healing_Hands:13963:Sandoria/PRELUDE_OF_BLACK_AND_WHITE:3:147:197:200:14216:13855," +
              "BORGHERTZS_SORCEROUS_HANDS:Borghertzs_Sorcerous_Hands:13964:Windurst/RECOLLECTIONS:4:200:150:195:12641:14217," +
              "BORGHERTZS_VERMILLION_HANDS:Borghertzs_Vermillion_Hands:13965:Sandoria/ENVELOPED_IN_DARKNESS:5:195:151:200:12642:14218," +
              "BORGHERTZS_SNEAKY_HANDS:Borghertzs_Sneaky_Hands:13966:Windurst/AS_THICK_AS_THIEVES:6:150:151:161:14219:12643," +
              "BORGHERTZS_STALWART_HANDS:Borghertzs_Stalwart_Hands:13967:Sandoria/A_BOYS_DREAM:7:195:147:200:14220:12515," +
              "BORGHERTZS_SHADOWY_HANDS:Borghertzs_Shadowy_Hands:13968:Bastok/DARK_PUPPET:8:195:151:150:12645:14221," +
              "BORGHERTZS_WILD_HANDS:Borghertzs_Wild_Hands:13969:Jeuno/SCATTERED_INTO_SHADOW:9:197:147:200:12646:12517," +
              "BORGHERTZS_HARMONIOUS_HANDS:Borghertzs_Harmonious_Hands:13970:Jeuno/THE_REQUIEM:10:161:151:197:14223:13857," +
              "BORGHERTZS_CHASING_HANDS:Borghertzs_Chasing_Hands:13971:Windurst/FIRE_AND_BRIMSTONE:11:200:197:150:14224:12648," +
              "BORGHERTZS_LOYAL_HANDS:Borghertzs_Loyal_Hands:13972:Outlands/YOMI_OKURI:12:174:208:159:14225:13781," +
              "BORGHERTZS_LURKING_HANDS:Borghertzs_Lurking_Hands:13973:Outlands/I_LL_TAKE_THE_BIG_BOX:13:205:176:153:14101:13869," +
              "BORGHERTZS_DRAGON_HANDS:Borghertzs_Dragon_Hands:13974:Sandoria/CHASING_QUOTAS:14:153:205:208:12649:14102," +
              "BORGHERTZS_CALLING_HANDS:Borghertzs_Calling_Hands:13975:Windurst/CLASS_REUNION:15:176:159:169:12650:14103",
              string.Join(",", JG16Quests.Select((q, i) =>
              {
                  var p = QC.Jeuno.BorghertzQuests.Quests[i];
                  return $"{q.ConstName}:{q.LegacyPath.Substring("scripts/quests/jeuno/".Length)}:{p.HandAfId}:" +
                         $"{p.RequiredLogId}/{p.RequiredQuestId}:{p.RequiredJobId}:{p.OldGauntletZoneId}:{p.OptionalZoneId1}:" +
                         $"{p.OptionalZoneId2}:{p.OptionalArtifact1}:{p.OptionalArtifact2}";
              })));

        // the trigger replies and their kinds, as helpers.lua has them: Guslam 155 (progress, :436), 43 (event, :458), 26
        // (progress, :589); Deadly Minnow 24 (progress, :585); Yin Pocanakhu 220 (progress, :518), 221 (event, :520);
        // qm1 20 (progress, :548), 49 (event, :550), 48 (progress, :552)
        var war = JG16Quests[0].Compile();
        static string R(QuestStateDecl s, string entity, Fake f)
        {
            var h = s.Handlers.Single(x => x.Kind == QuestHandlerKind.OnTrigger && x.Entity == entity);
            var r = h.Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        Fake At(QuestStatus status, int prog, bool gauntlets, bool flames)
        {
            var f = new Fake { Status = status, MainJob = QC.Job.War, MainLevel = 50 };
            f.Step = prog;
            f.KeyItems[QC.KeyItem.OldGauntlets] = gauntlets;
            f.KeyItems[QC.KeyItem.ShadowFlames] = flames;
            f.QuestStatuses["Bastok/THE_TALEKEEPERS_TRUTH"] = QuestStatus.Completed;
            return f;
        }
        Check("jg16b:the trigger replies keep the Lua's kinds: Guslam 155 / 43 / 26, Minnow 24, Yin 220 / 221, qm1 20 / 49 / 48",
              "ProgressEvent(155)|Event(43)|ProgressEvent(26)|ProgressEvent(24)|ProgressEvent(220)|Event(221)|" +
              "ProgressEvent(20)|Event(49)|ProgressEvent(48)",
              string.Join("|",
                  R(war.States[0], QC.Npc.UpperJeuno.Guslam, At(QuestStatus.Available, 0, false, false)),
                  R(war.States[1], QC.Npc.UpperJeuno.Guslam, At(QuestStatus.Accepted, 0, false, false)),
                  R(war.States[2], QC.Npc.UpperJeuno.Guslam, At(QuestStatus.Accepted, 0, true, false)),
                  R(war.States[2], QC.Npc.UpperJeuno.DeadlyMinnow, At(QuestStatus.Accepted, 1, true, false)),
                  R(war.States[2], QC.Npc.LowerJeuno.YinPocanakhu, At(QuestStatus.Accepted, 2, true, false)),
                  R(war.States[2], QC.Npc.LowerJeuno.YinPocanakhu, At(QuestStatus.Accepted, 3, true, false)),
                  R(war.States[2], QC.Npc.PortJeuno.qm1, At(QuestStatus.Accepted, 3, true, false)),
                  R(war.States[2], QC.Npc.PortJeuno.qm1, At(QuestStatus.Accepted, 4, true, false)),
                  R(war.States[2], QC.Npc.PortJeuno.qm1, At(QuestStatus.Accepted, 5, true, true))));

        // the loops and the guards at every value (round-1 mutant survivors, since also taken by golden flows where noted):
        // the flows hold one other Borghertz quest accepted or completed at a time (canStartQuest's entries 0 and 1 in
        // #start, isFirstBorghertzQuest's 0-14 across the mains and #yin); Yin Pocanakhu's nil at Prog 5 (#yin, talked
        // twice); the Torch's nil falls to its default, the same NOTHING_OUT_OF_ORDINARY (equivalent in game, pinned
        // here); the second optional coffer with its bit set (#coffer_edges).
        // canStartQuest (helpers.lua:398-403): any of the fifteen accepted stops the Striking Hands' 155
        var striking = JG16Quests[1].Compile();
        Fake StrikingAvailable()
        {
            var f = new Fake { Status = QuestStatus.Available, MainJob = QC.Job.Mnk, MainLevel = 50 };
            f.QuestStatuses["Bastok/THE_FIRST_MEETING"] = QuestStatus.Completed;
            return f;
        }
        Check("jg16b:canStartQuest reads each Borghertz quest's accepted status (the Striking Hands' 155 with each other one accepted)",
              string.Join("|", Enumerable.Repeat("null", 14)) + "|ProgressEvent(155)",
              string.Join("|", JG16Quests.Select((q, j) => (q, j)).Where(x => x.j != 1).Select(x =>
              {
                  var f = StrikingAvailable();
                  f.QuestStatuses["Jeuno/" + x.q.ConstName] = QuestStatus.Accepted;
                  return R(striking.States[0], QC.Npc.UpperJeuno.Guslam, f);
              })) + "|" + R(striking.States[0], QC.Npc.UpperJeuno.Guslam, StrikingAvailable()));

        // isFirstBorghertzQuest (helpers.lua:408-416): any of the fifteen completed makes 26's finish skip to Prog 3
        var finish26 = war.States[2].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 26);
        int Finish26(string completed)
        {
            var f = At(QuestStatus.Accepted, 0, true, false);
            if (completed != null) f.QuestStatuses["Jeuno/" + completed] = QuestStatus.Completed;
            finish26.Run(f);
            return f.Step;
        }
        Check("jg16b:26's finish reads each Borghertz quest's completed status (the Warring Hands at Prog 0 with each other one completed)",
              string.Join("|", Enumerable.Repeat("3", 14)) + "|1",
              string.Join("|", JG16Quests.Skip(1).Select(q => Finish26(q.ConstName).ToString())) + "|" + Finish26(null));

        // Yin Pocanakhu (helpers.lua:514-524) and the Torch (:491-508) by Prog: nil outside their arms
        Check("jg16b:Yin Pocanakhu at Prog 0-6: nil / nil / 220 / 221 / 221 / nil / nil",
              "null|null|ProgressEvent(220)|Event(221)|Event(221)|null|null",
              string.Join("|", Enumerable.Range(0, 7).Select(p => R(war.States[2], QC.Npc.LowerJeuno.YinPocanakhu, At(QuestStatus.Accepted, p, true, false)))));
        Check("jg16b:the Torch at Prog 3, Prog 5 with the flames, Prog 6: nil",
              "null|null|null",
              string.Join("|", R(war.States[2], QC.Npc.CastleZvahlBaileys.Torch, At(QuestStatus.Accepted, 3, true, false)),
                               R(war.States[2], QC.Npc.CastleZvahlBaileys.Torch, At(QuestStatus.Accepted, 5, true, true)),
                               R(war.States[2], QC.Npc.CastleZvahlBaileys.Torch, At(QuestStatus.Accepted, 6, true, false))));

        // the optional coffers' sections (helpers.lua:613-617, :637-641): started or done, on the job, the bit unset
        static bool Holds(QuestStateDecl s, Fake f) =>
            s.IsMatch(f.Status, f.Step, f);
        string Coffers(QuestStatus status, int job, int bits)
        {
            var f = new Fake { Status = status, MainJob = job };
            f.CharVars["[AF]ZilartCoffer"] = bits;
            return $"{(Holds(war.States[3], f) ? 1 : 0)}{(Holds(war.States[4], f) ? 1 : 0)}";
        }
        Check("jg16b:the optional coffers' sections: available / accepted / completed; a Monk; bits 1, 2, both (the Warring Hands)",
              "00|11|11|00|01|10|00",
              string.Join("|", Coffers(QuestStatus.Available, QC.Job.War, 0), Coffers(QuestStatus.Accepted, QC.Job.War, 0),
                               Coffers(QuestStatus.Completed, QC.Job.War, 0), Coffers(QuestStatus.Completed, QC.Job.Mnk, 0),
                               Coffers(QuestStatus.Completed, QC.Job.War, 2), Coffers(QuestStatus.Completed, QC.Job.War, 4),
                               Coffers(QuestStatus.Completed, QC.Job.War, 6)));
    }
}
