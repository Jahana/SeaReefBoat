// Till Death Do Us Part (Log ID 1, Quest ID 18), a line-for-line port of scripts/quests/bastok/Till_Death_Do_Us_Part.lua
// (quest rework B3b). Romilda : !pos 5.424 4.898 -18.699 236. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TillDeathDoUsPart : QuestScript
    {
        public TillDeathDoUsPart() : base(QuestLog.Bastok, "TILL_DEATH_DO_US_PART") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Till_Death_Do_Us_Part";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Bastok, Gil = 2000, Title = Title.QijisRival });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Bastok, "FOREVER_TO_HOLD"))
                // the offer at fame level 3, 127 below it
                .Npc(Zone.PortBastok, Npc.PortBastok.Romilda).OnTrigger(q =>
                    q.Player.FameLevel(FameArea.Bastok) >= 3 ? QuestReply.ProgressEvent(128) : QuestReply.Event(127))
                .OnEventFinish(Zone.PortBastok, 128, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Romilda)
                    .OnTrade(q => q.Trade.Matches((Item.CottonGloves, 1)) ? QuestReply.ProgressEvent(129) : null)
                .OnEventFinish(Zone.PortBastok, 129, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
