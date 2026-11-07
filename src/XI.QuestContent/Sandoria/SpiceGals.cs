// Spice Gals (Log ID 0, Quest ID 110), a line-for-line port of scripts/quests/sandoria/Spice_Gals.lua (quest rework B3b,
// San d'Oria group 8). Rouva : !pos -16.194 2.101 11.805 230; Riverne - Site #A01 : !pos -514.717 -6.684 -407.314; Riverne -
// Site #B01 : !pos -517.137 0.094 689.199. After CoP 2-5 (Ancient Vows) is completed, at Prog 0 with the Wait before the next conquest
// tally (any status): Rouva's 724 while available (begun, Prog 1), 726 after (Prog 1). Prog 1 (any status): the qm in
// either Riverne site gives Rivernewort when not held; Rouva's 725 (accepted, the herb: completed, Miratete's Memoirs) or
// 727 (completed, the herb: completed again), each taking the herb and setting Wait to the next tally; else 728 once per
// zone. Secrets of Ovens Lost reads its completion (a status read).
// Kept as the Lua has it: 725 / 727 take the herb only when complete succeeds; complete's cleanup clears Prog (back to 0)
// and the Wait is set after it; the Memoirs are rare, so 727 fails while the first ones are still held.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class SpiceGals : QuestScript
    {
        public SpiceGals() : base(QuestLog.Sandoria, "SPICE_GALS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Spice_Gals";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.MiratetesMemoirs, Fame = 0 });
            // Prog 1: 724 / 726's finish
            b.Steps(1);

            b.AnyState(when: q =>
                    q.Quest.Step == 0 &&
                    q.Quest.Var("Wait") < q.Player.NextConquestTally &&
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.AncientVows))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rouva).OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Available)
                    {
                        return QuestReply.ProgressEvent(724);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(726); // Repeat after conquest tally
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 724, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 726, q => q.Quest.SetStep(1));

            b.AnyState(step: 1)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rouva).OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.HasKeyItem(KeyItem.Rivernewort))
                    {
                        return QuestReply.ProgressEvent(725); // First time completion
                    }
                    else if (q.Quest.Status == QuestStatus.Completed &&
                        q.Player.HasKeyItem(KeyItem.Rivernewort))
                    {
                        return QuestReply.ProgressEvent(727); // Repeat completion
                    }
                    else
                    {
                        return QuestReply.Event(728).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 725, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Rivernewort);
                        q.Quest.SetVar("Wait", q.Player.NextConquestTally);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 727, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Rivernewort);
                        q.Quest.SetVar("Wait", q.Player.NextConquestTally);
                    }
                })
                .Npc(Zone.RiverneSiteA01, Npc.RiverneSiteA01.qmrivernewort).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.Rivernewort))
                    {
                        return QuestReply.KeyItem(KeyItem.Rivernewort);
                    }
                    return null;
                })
                .Npc(Zone.RiverneSiteB01, Npc.RiverneSiteB01.qmrivernewort).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.Rivernewort))
                    {
                        return QuestReply.KeyItem(KeyItem.Rivernewort);
                    }
                    return null;
                });
        }
    }
}
