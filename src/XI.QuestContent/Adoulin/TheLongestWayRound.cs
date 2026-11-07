// The Longest Way Round... (Log ID 9, Quest ID 91), a line-for-line port of
// scripts/quests/adoulin/The_Longest_Way_Round.lua (quest rework B3b Adoulin group 2).
// Eastern Adoulin: Vastran !pos -118.93 -0.15 -8.813 257 (no script). The patrol:
// Fostaig !pos -84.806 -0.15 14.079 257,
// Lamaron !pos -52.352 -0.65 96.708 257 (script: 590 with the Pioneer's Badge, else 531),
// Irate Destrier !pos -72.986 -0.15 -34.96 257, Nhili Uvolep !pos 24.06 -22.15 34.488 257,
// Ploh Trishbahk !pos 100.58 -40.15 -63.83 257, Cunegonde !pos -55.964 -0.15 -129.833 257,
// Ndah Tolohjin !pos -86.808 0.001 -36.867 257 (no scripts but Lamaron's).
// Available, SoA at Life on the Frontier or later: Vastran: 2510, whose finish (any option) begins it and gives the
// Eastern Adoulin Patrol Route. Accepted: Vastran: 2512 at Prog 7, else 2511 (an Event); each patrol NPC: 2513 + its
// offset (Fostaig 0 .. Ndah Tolohjin 6) when Prog is its offset, else nothing; that event's finish adds 1 to Prog when
// Prog is still the offset of the event's NPC. 2512's finish completes it (500 bayld, 1000 exp, the Adoulin fame area),
// then takes the key item with LOST_KEYITEM (Eastern Adoulin's name for the message).
// Kept as the Lua has it: the offer begins on any option; the finishes check Prog again.
// Ploh Trishbahk's order line holds 23 SoA mission handlers before this quest (Arciela's Promise's progressEvent 1506
// among them): no flow sets one of those missions current.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class TheLongestWayRound : QuestScript
    {
        public TheLongestWayRound() : base(QuestLog.Adoulin, "THE_LONGEST_WAY_ROUND") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/The_Longest_Way_Round";

        // patrolData: each patrol NPC's offset (the Prog it answers at; its event is 2513 + offset)
        private static readonly Dictionary<string, int> PatrolData = new Dictionary<string, int>
        {
            [Npc.EasternAdoulin.Fostaig] = 0,
            [Npc.EasternAdoulin.Lamaron] = 1,
            [Npc.EasternAdoulin.IrateDestrier] = 2,
            [Npc.EasternAdoulin.NhiliUvolep] = 3,
            [Npc.EasternAdoulin.PlohTrishbahk] = 4,
            [Npc.EasternAdoulin.Cunegonde] = 5,
            [Npc.EasternAdoulin.NdahTolohjin] = 6,
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
                return QuestReply.ProgressEvent(2513 + npcOffset.Value);
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
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Vastran).OnTrigger(QuestReply.ProgressEvent(2510))
                .OnEventFinish(Zone.EasternAdoulin, 2510, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.EasternAdoulinPatrolRoute);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Vastran).OnTrigger(q =>
                {
                    if (q.Quest.Step == 7)
                    {
                        return QuestReply.ProgressEvent(2512);
                    }
                    else
                    {
                        return QuestReply.Event(2511);
                    }
                })
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Fostaig).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Lamaron).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.IrateDestrier).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.NhiliUvolep).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.PlohTrishbahk).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Cunegonde).OnTrigger(PatrolOnTrigger)
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.NdahTolohjin).OnTrigger(PatrolOnTrigger)
                .OnEventFinish(Zone.EasternAdoulin, 2512, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.EasternAdoulinPatrolRoute);
                        q.Text.Special(Text.EasternAdoulin.LOST_KEYITEM, KeyItem.EasternAdoulinPatrolRoute);
                    }
                })
                .OnEventFinish(Zone.EasternAdoulin, 2513, PatrolOnEventFinish)
                .OnEventFinish(Zone.EasternAdoulin, 2514, PatrolOnEventFinish)
                .OnEventFinish(Zone.EasternAdoulin, 2515, PatrolOnEventFinish)
                .OnEventFinish(Zone.EasternAdoulin, 2516, PatrolOnEventFinish)
                .OnEventFinish(Zone.EasternAdoulin, 2517, PatrolOnEventFinish)
                .OnEventFinish(Zone.EasternAdoulin, 2518, PatrolOnEventFinish)
                .OnEventFinish(Zone.EasternAdoulin, 2519, PatrolOnEventFinish);
        }
    }
}
