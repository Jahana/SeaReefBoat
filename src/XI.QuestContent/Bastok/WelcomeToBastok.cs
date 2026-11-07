// Welcome to Bastok (Log ID 1, Quest ID 2), a line-for-line port of scripts/quests/bastok/Welcome_to_Bastok.lua (quest
// rework B2). Powhatan : !pos -152.135 -7.48 19.014 236; Bartolomeo : !pos -84.967 1.896 -18.679 236. The Lua file has no
// known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class WelcomeToBastok : QuestScript
    {
        public WelcomeToBastok() : base(QuestLog.Bastok, "WELCOME_TO_BASTOK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Welcome_to_Bastok";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.Spatha, Title = Title.BastokWelcomingCommittee });

            // the offer: option 0 accepts it
            b.State(QuestStatus.Available)
                .Npc(Zone.PortBastok, Npc.PortBastok.Powhatan).OnTrigger(QuestReply.ProgressEvent(50))
                .OnEventFinish(Zone.PortBastok, 50, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // Bartolomeo with a Shell Shield in the off hand, then back to Powhatan
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Powhatan).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(51);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(53);
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Bartolomeo).OnTrigger(q =>
                {
                    if (q.Player.EquipId(Slot.Sub) == Item.ShellShield && q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(52);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 52, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.PortBastok, 53, q => q.Quest.Complete());
        }
    }
}
