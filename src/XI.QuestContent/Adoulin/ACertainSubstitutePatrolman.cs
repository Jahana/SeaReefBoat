// A Certain Substitute Patrolman (Log ID 9, Quest ID 78), a line-for-line port of
// scripts/quests/adoulin/A_Certain_Substitute_Patrolman.lua (quest rework B3b Adoulin group 2).
// Western Adoulin: Rising Solstice !pos -154 4 -29 256 (his script: 150 while SoA is from Beauty and the Beast to
// Salvation; default 502). The patrol: Zaoso !pos -94 3 -11 256 (script: 574 from Life on the Frontier, else 506),
// Clemmar !pos -12 0 12 256 (no script), Kongramm !pos 61 32 138 256 (script: A Curse from the Past; default 558),
// Virsaint !pos 32 0 -5 256 (no script), Shipilolo !pos 84 0 -60 256 (script: three Adoulin quests),
// Dangueubert !pos 5 0 -136 256 (no script), Nylene !pos 12 0 -82 256 (no script). Pioneer Registration's completed
// section answers at Rising Solstice, Clemmar, Shipilolo, Dangueubert and Nylene (replaceDefault).
// Available, SoA at Life on the Frontier or later: Rising Solstice: 2550, whose finish (any option) begins it and gives
// the Western Adoulin Patrol Route. Accepted: Rising Solstice: 2552 at Prog 7, else 2551 (an Event); each patrol NPC:
// 2553 + its offset (Zaoso 0 .. Nylene 6) when Prog is its offset, else nothing; that event's finish adds 1 to Prog
// when Prog is still the offset of the event's NPC. 2552's finish completes it (500 bayld, 1000 exp, the Adoulin fame
// area), then takes the key item with KEYITEM_LOST.
// Kept as the Lua has it: the offer begins on any option; the finishes check Prog again; the file header's
// "!addquest 9 76" (76 is Hunger Strikes).
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class ACertainSubstitutePatrolman : QuestScript
    {
        public ACertainSubstitutePatrolman() : base(QuestLog.Adoulin, "A_CERTAIN_SUBSTITUTE_PATROLMAN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/A_Certain_Substitute_Patrolman";

        // patrolData: each patrol NPC's offset (the Prog it answers at; its event is 2553 + offset)
        private static readonly Dictionary<string, int> PatrolData = new Dictionary<string, int>
        {
            [Npc.WesternAdoulin.Zaoso] = 0,
            [Npc.WesternAdoulin.Clemmar] = 1,
            [Npc.WesternAdoulin.Kongramm] = 2,
            [Npc.WesternAdoulin.Virsaint] = 3,
            [Npc.WesternAdoulin.Shipilolo] = 4,
            [Npc.WesternAdoulin.Dangueubert] = 5,
            [Npc.WesternAdoulin.Nylene] = 6,
        };

        // patrolData[npc:getName()]: the NPC's offset; null for a name not in the table (the Lua's nil, which equals no
        // Prog). With no npc the Lua's npc:getName() raises; the C# reads that as null too (its own choice: every
        // handler that calls this is an NPC's, so it always has one).
        private static int? NpcOffset(IQuestContext q) =>
            q.Npc != null && PatrolData.TryGetValue(q.Npc.Name, out var offset) ? offset : (int?)null;

        private static QuestReply PatrolOnTrigger(IQuestContext q)
        {
            int? npcOffset = NpcOffset(q);

            if (q.Quest.Step == npcOffset)
            {
                return QuestReply.ProgressEvent(2553 + npcOffset.Value);
            }

            return null;
        }

        private static void PatrolOnEventFinish(IQuestContext q)
        {
            int? npcOffset = NpcOffset(q);

            if (q.Quest.Step == npcOffset)
            {
                q.Quest.IncrementVar("Prog", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
                Exp = 1000,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Soa) >= Mission.Soa.LifeOnTheFrontier)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.RisingSolstice).OnTrigger(QuestReply.ProgressEvent(2550))
                .OnEventFinish(Zone.WesternAdoulin, 2550, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.WesternAdoulinPatrolRoute);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.RisingSolstice).OnTrigger(q =>
                {
                    if (q.Quest.Step == 7)
                    {
                        return QuestReply.ProgressEvent(2552);
                    }
                    else
                    {
                        return QuestReply.Event(2551);
                    }
                })
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Zaoso).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Clemmar).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Kongramm).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Virsaint).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Shipilolo).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Dangueubert).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Nylene).OnTrigger(PatrolOnTrigger)
                .OnEventFinish(Zone.WesternAdoulin, 2552, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.WesternAdoulinPatrolRoute);
                        q.Text.Special(Text.WesternAdoulin.KEYITEM_LOST, KeyItem.WesternAdoulinPatrolRoute);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 2553, PatrolOnEventFinish)
                .OnEventFinish(Zone.WesternAdoulin, 2554, PatrolOnEventFinish)
                .OnEventFinish(Zone.WesternAdoulin, 2555, PatrolOnEventFinish)
                .OnEventFinish(Zone.WesternAdoulin, 2556, PatrolOnEventFinish)
                .OnEventFinish(Zone.WesternAdoulin, 2557, PatrolOnEventFinish)
                .OnEventFinish(Zone.WesternAdoulin, 2558, PatrolOnEventFinish)
                .OnEventFinish(Zone.WesternAdoulin, 2559, PatrolOnEventFinish);
        }
    }
}
