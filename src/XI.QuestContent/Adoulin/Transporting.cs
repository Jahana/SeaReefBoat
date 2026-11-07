// Transporting (Log ID 9, Quest ID 82), a line-for-line port of scripts/quests/adoulin/Transporting.lua (quest rework
// B3b Adoulin group 2). Western Adoulin: Vaulois !pos 20 0 85 256 (no script; default 520), Kongramm !pos 61 32 138 256
// (script: A Curse from the Past; default 558); Rala Waterways: qm_sluice_gate_6 !pos -563 -5.768 61.5 258 (no script).
// Available, Adoulin fame level 2 or more: Vaulois: 2590, whose finish (any option) begins it and gives the
// Misdelivered Parcel. Accepted: Kongramm at Prog 0: 2592, whose finish sets Prog 1; qm_sluice_gate_6 at Prog 1: 2802,
// whose finish takes the parcel (KEYITEM_LOST) and sets Prog 2; Vaulois at Prog 2: 2591, whose finish completes it
// (300 bayld, 1000 exp, the Adoulin fame area). Each answers nothing at another Prog.
// Kept as the Lua has it: the offer begins on any option; the completion's result is not read; with
// A Certain Substitute Patrolman at Prog 2, its 2555 and this 2592 (both progressEvents on Kongramm) tie and rotate.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class Transporting : QuestScript
    {
        public Transporting() : base(QuestLog.Adoulin, "TRANSPORTING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Transporting";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 300,
                Exp = 1000,
            });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Adoulin) >= 2)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Vaulois).OnTrigger(QuestReply.ProgressEvent(2590))
                .OnEventFinish(Zone.WesternAdoulin, 2590, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.MisdeliveredParcel);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Kongramm).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(2592);
                    }

                    return null;
                })
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Vaulois).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(2591);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WesternAdoulin, 2591, q =>
                {
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.WesternAdoulin, 2592, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.qmsluicegate6).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(2802);
                    }

                    return null;
                })
                .OnEventFinish(Zone.RalaWaterways, 2802, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MisdeliveredParcel);
                    q.Text.Special(Text.RalaWaterways.KEYITEM_LOST, KeyItem.MisdeliveredParcel);

                    q.Quest.SetStep(2);
                });
        }
    }
}
