// Her Memories: Carnelian Footfalls (Log ID 7, Quest ID 69), a line-for-line port of
// scripts/quests/crystalWar/Her_Memories_Carnelian_Footfalls.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria [S]: Mainchelite !pos -16 1 -30 (no script; default 15). East Ronfaure [S]: qm3 !pos 312.821 -30.495 -67.15,
// qm4 !pos 541.425 -49.83 178.563, qm5 !pos 380.015 -26.5 -22.525 (no scripts).
// Available, the WotG mission Her Memories current and the San d'Oria campaign allegiance (1): the Southern San d'Oria
// [S] zone-in from East Ronfaure [S] at Prog 0: 170 (-> Prog 1). Mainchelite: Prog 1: 171 (80, 90), whose finish begins
// it. Accepted: Mainchelite: Option 7 (the three bits): 172 (1, 23, 1749), whose finish completes it (the Large Memory
// Fragment) and runs xi.wotg.helpers.checkMemoryFragments; else 173 (80, 3, 2964; an Event, oncePerZone). The East
// Ronfaure [S] zone-in from Southern San d'Oria [S] at Prog 1: 12 (-> Prog 2). qm3 / qm4 / qm5: their Option bit (0 / 1
// / 2) not set: 13 / 14 / 15 (8 args), whose finish sets it; set: 4 / 5 / 6 (Events).
// Kept as the Lua has it: Prog 1 written while available and kept after the begin, so the East Ronfaure [S] zone-in
// plays 12 once (Prog 2 is read by nothing); 171 begins on any option; the qms answer at any Prog; no completed
// section.
// Rival containers: Steamed Rams at Mainchelite (accepted: 13 / 14 / 624, Progress; completed: 5, replaceDefault) and
// at the three qms (accepted: 1 / 2 / 3, Progress): golden flow #rivals (Steamed Rams accepted again by a GM).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HerMemoriesCarnelianFootfalls : QuestScript
    {
        public HerMemoriesCarnelianFootfalls() : base(QuestLog.CrystalWar, "HER_MEMORIES_CARNELIAN_FOOTFALLS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Her_Memories_Carnelian_Footfalls";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.LargeMemoryFragment4,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Wotg) == Mission.Wotg.HerMemories &&
                    q.Player.CampaignAllegiance == 1)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Mainchelite).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(171, 80, 90);
                    }
                    return null;
                })
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                {
                    if (q.Player.PreviousZone == Zone.EastRonfaureS &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.Event(170);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 170, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 171, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Mainchelite).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 7)
                    {
                        return QuestReply.ProgressEvent(172, 1, 23, 1749);
                    }
                    else
                    {
                        return QuestReply.Event(173, 80, 3, 2964).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 172, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.CheckMemoryFragments();
                    }
                })
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.qm3).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 0))
                    {
                        return QuestReply.ProgressEvent(13, 81, 0, 1756, 0, 0, 0, 3, 4095);
                    }
                    else
                    {
                        return QuestReply.Event(4);
                    }
                })
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.qm4).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 1))
                    {
                        return QuestReply.ProgressEvent(14, 81, 1, 1756, 0, 0, 0, 3, 4095);
                    }
                    else
                    {
                        return QuestReply.Event(5);
                    }
                })
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.qm5).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Option", 2))
                    {
                        return QuestReply.ProgressEvent(15, 81, 0, 1756, 0, 0, 0, 3, 4095);
                    }
                    else
                    {
                        return QuestReply.Event(6);
                    }
                })
                .OnZoneIn(Zone.EastRonfaureS, q =>
                {
                    if (q.Player.PreviousZone == Zone.SouthernSanDoriaS &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.Event(12);
                    }
                    return null;
                })
                .OnEventFinish(Zone.EastRonfaureS, 12, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.EastRonfaureS, 13, q =>
                {
                    q.Quest.SetVarBit("Option", 0);
                })
                .OnEventFinish(Zone.EastRonfaureS, 14, q =>
                {
                    q.Quest.SetVarBit("Option", 1);
                })
                .OnEventFinish(Zone.EastRonfaureS, 15, q =>
                {
                    q.Quest.SetVarBit("Option", 2);
                });
        }
    }
}
