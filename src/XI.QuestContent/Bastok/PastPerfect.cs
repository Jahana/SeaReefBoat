// Past Perfect (Log ID 1, Quest ID 23), a line-for-line port of scripts/quests/bastok/Past_Perfect.lua (quest rework B3b).
// Evi : !pos -4.656 -2.101 1.664 236; qm2 : !pos -201 16 80 108. While it is available (Bastok fame level 2) Evi plays 21
// (Prog 1), then 130, which begins it; Konschtat Highlands' qm2 gives the KI Tattered Mission Orders; Evi's 131 completes
// it (Scale Mail, fame +20) and deletes the orders; without them she plays 104.
// Kept as the Lua file has it: once completed, Evi plays 21 whenever the char's local var EviDialog is 0 (no script sets
// it, only a GM's !setlocalvar, so in play always), and 21's finish sets the completed quest's Prog to 1 again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class PastPerfect : QuestScript
    {
        public PastPerfect() : base(QuestLog.Bastok, "PAST_PERFECT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Past_Perfect";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.ScaleMail, Fame = 20, FameArea = FameArea.Bastok });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Evi).OnTrigger(q =>
                    q.Quest.Step == 0 ? QuestReply.ProgressEvent(21) : QuestReply.ProgressEvent(130))
                .OnEventFinish(Zone.PortBastok, 21, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.PortBastok, 130, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.qm2).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.TatteredMissionOrders)
                        ? QuestReply.KeyItem(KeyItem.TatteredMissionOrders)
                        : null)
                .Npc(Zone.PortBastok, Npc.PortBastok.Evi).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.TatteredMissionOrders) ? QuestReply.ProgressEvent(131) : QuestReply.Event(104))
                .OnEventFinish(Zone.PortBastok, 131, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.TatteredMissionOrders);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Evi).OnTrigger(q =>
                    q.Player.LocalVar("EviDialog") == 0 ? QuestReply.ProgressEvent(21) : QuestReply.Event(104))
                .OnEventFinish(Zone.PortBastok, 21, q => q.Quest.SetStep(1));
        }
    }
}
