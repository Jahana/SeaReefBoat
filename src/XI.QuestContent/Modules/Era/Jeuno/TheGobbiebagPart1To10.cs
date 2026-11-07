// The Gobbiebag Parts I-X's era module, a line-for-line port of
// modules/era/lua/quests/jeuno/the_gobbiebag_part_1_to_10.lua (quest rework B3b Jeuno, P4). It restores the 75-era fame
// levels (lowered on Feb. 18th, 2014: https://wiki.ffo.jp/html/30645.html): Module:new(
// 'era_gobbiebag_fame_requirements', xi.pre(xi.expansion.SOA)). The Lua overrides the template's constructor
// (xi.jeuno.helpers.GobbiebagQuest.new): params.fame becomes the era level before super builds the sections, and a
// third section is inserted (available, the start size, the prerequisite, fame below the era level: Bluffnix's 42 with
// the inventory size + 1, the pre-relaxation line).
// In C# the template's sections are built already, so the module replaces states 1 and 2's checks with the template's
// own (status and GobbiebagQuest.ReqsMet) at the era fame level, and adds the third state. The template's state 1
// trigger recomputes reqsMet for its third event parameter with the base fame level; under the module the Lua's
// recomputes it with the era level. Both read 1 wherever state 1 matches: the era level is above the base level for
// every part, so a char that passes the era check passes the base one. No other handler reads params.fame.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using XI.QuestContent.Jeuno;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class TheGobbiebagPart1To10 : QuestModule
    {
        public TheGobbiebagPart1To10() : base("era_gobbiebag_fame_requirements", "modules/era/lua/quests/jeuno/the_gobbiebag_part_1_to_10.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        // eraFameRequirements, by part (GobbiebagQuest.Parts' order)
        static readonly (string Key, int Fame)[] EraFameRequirements =
        {
            (QuestKey.Jeuno.TheGobbiebagPartI, 3),
            (QuestKey.Jeuno.TheGobbiebagPartIi, 4),
            (QuestKey.Jeuno.TheGobbiebagPartIii, 5),
            (QuestKey.Jeuno.TheGobbiebagPartIv, 5),
            (QuestKey.Jeuno.TheGobbiebagPartV, 6),
            (QuestKey.Jeuno.TheGobbiebagPartVi, 6),
            (QuestKey.Jeuno.TheGobbiebagPartVii, 7),
            (QuestKey.Jeuno.TheGobbiebagPartViii, 7),
            (QuestKey.Jeuno.TheGobbiebagPartIx, 8),
            (QuestKey.Jeuno.TheGobbiebagPartX, 9),
        };

        protected override void Build(QuestModuleBuilder m)
        {
            // the parts named in their Lua files' order (GetContainerFilenamesList: The_Gobbiebag_Part_IX.lua sorts before
            // _V.lua): the patched quests move to the end of the lists in the order the module names them (PlacePatchedLast),
            // where the Lua's constructor override leaves each part in its file's place. At Bluffnix the parts are the last
            // containers anyway (after the default and Lure of the Wildcat (Jeuno)), and the 43 / 73 finishes are theirs
            // alone, so the end of the lists is their place
            var fileOrder = Enumerable.Range(0, EraFameRequirements.Length)
                .OrderBy(i => GobbiebagQuest.Parts[i].LuaFile + ".lua", StringComparer.Ordinal);
            foreach (int i in fileOrder)
            {
                var (key, fame) = EraFameRequirements[i];
                var part = GobbiebagQuest.Parts[i];

                m.Quest(key)
                    // params.fame = eraFameRequirements[params.questId]: the template's two checks at the era level
                    .State(1).ReplaceCheck(q =>
                        q.Quest.Status == QuestStatus.Available &&
                        GobbiebagQuest.ReqsMet(q.Player, part, fame))
                    .State(2).ReplaceCheck(q =>
                        q.Quest.Status == QuestStatus.Accepted &&
                        GobbiebagQuest.ReqsMet(q.Player, part, fame))
                    .Quest(key)
                    // table.insert(quest.sections, { ... }): restores the original lines for the quest before fame
                    // requirements were abolished
                    .AddStates(b => b
                        .State(QuestStatus.Available, when: q =>
                            q.Player.ContainerSize(GobbiebagQuest.Inventory) == part.StartInventorySize &&
                            q.Player.FameLevel(FameArea.Jeuno) < fame &&
                            (part.Prerequisite == null || q.Player.HasCompletedQuest(QuestLog.Jeuno, part.Prerequisite)))
                        .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Bluffnix).OnTrigger(q =>
                        {
                            return QuestReply.Event(42, q.Player.ContainerSize(GobbiebagQuest.Inventory) + 1);
                        }));
            }
        }
    }
}
