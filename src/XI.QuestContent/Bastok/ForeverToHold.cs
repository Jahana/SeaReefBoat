// Forever to Hold (Log ID 1, Quest ID 17), a line-for-line port of scripts/quests/bastok/Forever_to_Hold.lua (quest rework
// B3b). Qiji : !pos 4.257 4.898 -18.92 236; Romilda : !pos 5.424 4.898 -18.699 236. Till Death Do Us Part reads its
// status and keeps the read. Kept as the Lua file has it: the hairpin shown to Qiji (124) is not taken, and Romilda's
// 125 takes another hairpin each time while the quest is accepted.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class ForeverToHold : QuestScript
    {
        public ForeverToHold() : base(QuestLog.Bastok, "FOREVER_TO_HOLD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Forever_to_Hold";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Gil = 300, Title = Title.QijisFriend });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Qiji).OnTrigger(QuestReply.ProgressEvent(123))
                .OnEventFinish(Zone.PortBastok, 123, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Qiji)
                    // no trade complete
                    .OnTrade(q => q.Trade.Matches((Item.BrassHairpin, 1)) ? QuestReply.Event(124) : null)
                    .OnTrigger(q => q.Quest.Var("Prog") == 1 ? QuestReply.ProgressEvent(126) : null)
                .Npc(Zone.PortBastok, Npc.PortBastok.Romilda)
                    .OnTrade(q => q.Trade.Matches((Item.BrassHairpin, 1)) ? QuestReply.ProgressEvent(125) : null)
                .OnEventFinish(Zone.PortBastok, 125, q =>
                {
                    q.Trade.Complete();

                    q.Quest.SetVar("Prog", 1);
                })
                .OnEventFinish(Zone.PortBastok, 126, q => q.Quest.Complete());
        }
    }
}
