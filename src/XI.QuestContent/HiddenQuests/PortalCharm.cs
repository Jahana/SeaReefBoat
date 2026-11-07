// Portal Charm (hidden quest portalCharm), ported from scripts/quests/hiddenQuests/Portal_Charm.lua (quest rework B3b
// hidden quests; the kind and this port: quest rework P9). Kupipi : !pos 2 0.1 30 242. Reward: the Portal Charm key item.
// One section (a Windurst citizen of rank 3 or more). Kupipi's trade, exactly a Rolanberry and no Portal Charm: the Windurst
// mission Written in the Stars (id 11) completed: 291; else 292. Her trigger, Written in the Stars completed and
// Prog 1: 293. 291's finish: quest:complete (the charm; the vars cleared) and, when it gave it, the trade confirmed. 292's
// finish: the trade confirmed, Prog 1. 293's finish: quest:complete.
// Kept as the Lua has it: 292 takes the Rolanberry before the mission and 293 completes after it with no second trade;
// 291 confirms the trade after the completion (the completion cannot fail: no item in the reward); the trigger answers
// nil otherwise (her script and her other quests).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class PortalCharm : HiddenQuestScript
    {
        public PortalCharm() : base("portalCharm") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Portal_Charm";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.PortalCharm,
            });
            b.Steps(1);

            b.AnyState(when: q =>
                    q.Player.Nation == Nation.Windurst &&
                    q.Player.Rank(Nation.Windurst) >= 3)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi)
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.Rolanberry) &&
                        !q.Player.HasKeyItem(KeyItem.PortalCharm))
                    {
                        if (q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.WrittenInTheStars))
                        {
                            return QuestReply.ProgressEvent(291);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(292);
                        }
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.WrittenInTheStars) &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(293);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 291, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.HeavensTower, 292, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.HeavensTower, 293, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
