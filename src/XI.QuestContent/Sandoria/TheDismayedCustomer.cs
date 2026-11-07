// The Dismayed Customer (Log ID 0, Quest ID 6), a line-for-line port of scripts/quests/sandoria/The_Dismayed_Customer.lua
// (quest rework B3b, San d'Oria group 6). Gulemont : !pos -69 -5 -38 232; qm1 : !pos -453 -20 -230 100; qm2 : !pos -550 -0
// -542 100; qm3 : !pos -399 -10 -438 100. Available with A Taste for Meat completed: Gulemont's 605 (option 0, not 1,
// begins it and sets the var Stage to math.randomInt(1, 3)). Accepted: Gulemont's 606, or 607 with Gulemont's Document
// (completed: 560 gil, fame +20, Lost & Found Officer; the document deleted). In West Ronfaure the qm whose number is
// Stage sets Stage 0 and gives the document; the others (and that one again) return nothing (their default
// DISMAYED_CUSTOMER). No outside reference.
// Kept as the Lua has it: 605's option 0 accepts.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheDismayedCustomer : QuestScript
    {
        public TheDismayedCustomer() : base(QuestLog.Sandoria, "THE_DISMAYED_CUSTOMER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Dismayed_Customer";

        // the Lua's local handleQm(player, qmNumber)
        static QuestReply HandleQm(IQuestContext q, int qmNumber)
        {
            if (q.Quest.Var("Stage") == qmNumber)
            {
                q.Quest.SetVar("Stage", 0);
                return QuestReply.KeyItem(KeyItem.GulemontsDocument);
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Gil = 560, Title = Title.LostFoundOfficer });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Sandoria, "A_TASTE_FOR_MEAT"))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Gulemont).OnTrigger(QuestReply.ProgressEvent(605))
                .OnEventFinish(Zone.PortSanDoria, 605, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Quest.SetVar("Stage", q.Player.RandomInt(1, 3));
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Gulemont).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.GulemontsDocument))
                    {
                        return QuestReply.ProgressEvent(607);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(606);
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 607, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.GulemontsDocument);
                    }
                })
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.qm1).OnTrigger(q => HandleQm(q, 1))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.qm2).OnTrigger(q => HandleQm(q, 2))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.qm3).OnTrigger(q => HandleQm(q, 3));
        }
    }
}
