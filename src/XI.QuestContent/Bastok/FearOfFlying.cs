// Fear of Flying (Log ID 1, Quest ID 35), a line-for-line port of scripts/quests/bastok/Fear_of_Flying.lua (quest rework
// B3b). Kurando : !pos -23.887 3.898 0.870 236. The Option var, set after the completion has cleared the quest's vars,
// plays 172 once. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class FearOfFlying : QuestScript
    {
        public FearOfFlying() : base(QuestLog.Bastok, "FEAR_OF_FLYING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Fear_of_Flying";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30,
                FameArea = FameArea.Bastok,
                Item = Item.BlackSilkNeckerchief,
                Title = Title.AirshipDenouncer,
            });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.PortBastok, Npc.PortBastok.Kurando).OnTrigger(QuestReply.ProgressEvent(170))
                .OnEventFinish(Zone.PortBastok, 170, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Kurando)
                    .OnTrade(q => q.Trade.Matches((Item.SilkwormEgg, 1)) ? QuestReply.ProgressEvent(171) : null)
                .OnEventFinish(Zone.PortBastok, 171, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();

                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Kurando).OnTrigger(q =>
                    q.Quest.Var("Option") == 1 ? QuestReply.ProgressEvent(172) : QuestReply.Event(173).ReplaceDefault())
                .OnEventFinish(Zone.PortBastok, 172, q => q.Quest.SetVar("Option", 0));
        }
    }
}
