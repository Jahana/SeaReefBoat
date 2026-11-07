// The Setting Sun (Log ID 0, Quest ID 72), a line-for-line port of scripts/quests/sandoria/The_Setting_Sun.lua (quest
// rework B3b, San d'Oria group 7). Vamorcote : !pos -137.070 10.999 161.855 231. Offered at San d'Oria fame level 5 with
// Blackmail completed: Vamorcote's 654 (options [1] and [2] the Engraved Key; option 1: begun). Accepted: his 655 (option
// [2] the key); a trade of exactly the Engraved Key plays 658 (completed: 10000 gil, fame +50; the key taken; needToZone
// set). Completed while needToZone: his 659, which replaces his default 651. No outside reference.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheSettingSun : QuestScript
    {
        public TheSettingSun() : base(QuestLog.Sandoria, "THE_SETTING_SUN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Setting_Sun";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 50, FameArea = FameArea.Sandoria, Gil = 10000 });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Sandoria) >= 5 &&
                    q.Player.HasCompletedQuest(QuestLog.Sandoria, "BLACKMAIL"))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Vamorcote)
                    .OnTrigger(QuestReply.ProgressEvent(654).Option(1, Item.EngravedKey).Option(2, Item.EngravedKey))
                .OnEventFinish(Zone.NorthernSanDoria, 654, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Vamorcote)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.ENGRAVED_KEY)
                        if (q.Trade.HasExactly(Item.EngravedKey))
                        {
                            return QuestReply.ProgressEvent(658);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(655).Option(2, Item.EngravedKey))
                .OnEventFinish(Zone.NorthernSanDoria, 658, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetNeedToZone(true);
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Player.NeedToZone)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Vamorcote).OnTrigger(QuestReply.Event(659).ReplaceDefault());
        }
    }
}
