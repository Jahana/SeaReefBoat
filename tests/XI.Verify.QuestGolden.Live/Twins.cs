using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P5: test-only C# twins of Lua quests, for probe flows that prove a bridge feature against the Lua quest the
// twin copies (P8: probe.zone_in_table, I'll Take the Big Box's zone-in table). Gone once their quests were ported,
// their probes replaying against the port: P5's probe.after_zone_in had One Good Deed?'s afterZoneIn (B3b Windurst
// group 11: OneGoodDeed.cs), probe.zone_in_table had Such Sweet Sorrow's (B3b Aht Urhgan group 10:
// SuchSweetSorrow.cs), P10's probe.player_timer had Embers of His Past's player:timer (B3b Aht Urhgan group 6:
// EmbersOfHisPast.cs), and P9's probe.trigger_area_leave had Dances with Luopans' Ergon Locus leave (B3b Adoulin
// group 4: DancesWithLuopans.cs). A probe's record runs the Lua quest (nothing off); its C# runs (replay, rerecord)
// register the twin (ZoneHost.TestQuestScripts) in each new world, so the bridge answers in place of the Lua
// container: the same key, the same container id. A twin copies every section that holds a
// handler of the probe's lists (the order block counts each section), line for line from the Lua. It is left out once a
// C# quest of XI.QuestContent has its key: the probe then replays against the real port (whose port deletes the Lua
// file, after which the probe can no longer be recorded, only replayed).
internal static partial class Program
{
    // probe flow name -> its twins
    private static readonly Dictionary<string, Func<QuestScript>[]> ProbeTwins = new(StringComparer.Ordinal)
    {
        // quest rework P8
        ["probe.zone_in_table"] = new Func<QuestScript>[] { () => new IllTakeTheBigBoxTwin() },
    };

    // I'll Take the Big Box (scripts/quests/outlands/Ill_Take_the_Big_Box.lua, Log ID 5, Quest ID 144), which
    // probe.zone_in_table reaches (quest rework P8): the accepted section's Selbina table line for line (the only handlers of
    // the probe's lists), on the OnZoneIn table reply. The zone-in at Prog 5: { 1101, -1, RESET_CAMERA | NO_PCS | NO_NPCS }
    // (0x13), whose finish completes the quest (the Ninja Hakama, Norg fame 20) and on that takes the Seance Staff. Not a
    // port: the other sections and zones are not copied
    private sealed class IllTakeTheBigBoxTwin : QuestScript
    {
        // xi.cutsceneFlag.RESET_CAMERA | NO_PCS | NO_NPCS (scripts/enum/cutscene_flag.lua)
        private const uint ZoneInCutsceneFlags = 0x01 | 0x02 | 0x10;

        public IllTakeTheBigBoxTwin() : base(QuestLog.Outlands, "I_LL_TAKE_THE_BIG_BOX") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Norg, Item = Item.NinjaHakama });

            b.State(QuestStatus.Accepted)
                .OnZoneIn(Zone.Selbina, q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        // return { 1101, -1, bit.bor(xi.cutsceneFlag.RESET_CAMERA, xi.cutsceneFlag.NO_PCS, xi.cutsceneFlag.NO_NPCS) }
                        return QuestReply.Event(1101).TextTable(-1).Flags(ZoneInCutsceneFlags);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Selbina, 1101, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SeanceStaff);
                    }
                });
        }
    }

    // the twins the next NewWorld registers (null: none); RunFlow sets them for a C# run of a probe
    private static List<QuestScript> twins_;

    // the probe's twins whose key no C# quest of XI.QuestContent has, made fresh
    internal static List<QuestScript> TwinsOf(string flowName, IReadOnlyCollection<string> shippedKeys)
    {
        if (!ProbeTwins.TryGetValue(flowName, out var makers)) return new List<QuestScript>();
        return makers.Select(m => m()).Where(q => !shippedKeys.Contains(TwinKey(q))).ToList();
    }

    // the guard of a C# probe run: a probe flow whose text names a twin must find them in ProbeTwins (a renamed or copied
    // flow would otherwise run the Lua container against the Lua record and pass), and each twin registered must have answered
    // (its key among the C# quests the run reached)
    internal static List<string> TwinProblems(string flowName, bool mentionsTwin, IReadOnlyCollection<string> twinKeys,
                                              IReadOnlyCollection<string> reachedKeys)
    {
        var problems = new List<string>();
        if (mentionsTwin && !ProbeTwins.ContainsKey(flowName))
            problems.Add($"{flowName}: the flow names a twin but Twins.cs has none under this flow name (renamed?); the C# run would replay Lua against Lua");
        foreach (var k in twinKeys)
            if (!reachedKeys.Contains(k)) problems.Add($"{flowName}: the twin {k} was registered but no handler of it answered");
        return problems;
    }

    // a twin's quest key, as the registry names it (ahtUrhgan.such_sweet_sorrow)
    internal static string TwinKey(QuestScript q) => GoldenKey(q);
}
