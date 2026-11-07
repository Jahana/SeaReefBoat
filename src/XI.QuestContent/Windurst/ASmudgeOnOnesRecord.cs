// A Smudge on One's Record (Log ID 2, Quest ID 12), a line-for-line port of
// scripts/quests/windurst/A_Smudge_on_Ones_Record.lua (quest rework B3b). Hariga-Origa : !pos -62 -6 105 238. Offered
// after Chasing Tales at fame level 4: a Vial of Slime Oil and a Frost Turnip, traded together, complete it (Map of
// Feiyin, 5000 gil, fame +40, exp 2000) and set its mustZone; until the char zones Hariga and Serukoko play their
// completed lines once each. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ASmudgeOnOnesRecord : QuestScript
    {
        const int RewardGil = 5000;

        public ASmudgeOnOnesRecord() : base(QuestLog.Windurst, "A_SMUDGE_ON_ONES_RECORD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/A_Smudge_on_Ones_Record";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.MapOfFeiyin,
                FameArea = FameArea.Windurst,
                Fame = 40,
                Gil = RewardGil,
                Exp = 2000,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "CHASING_TALES") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 4 &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HarigaOriga)
                    .OnTrigger(QuestReply.ProgressEvent(413, 0, Item.VialOfSlimeOil, Item.FrostTurnip))
                .OnEventFinish(Zone.WindurstWaters, 413, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HarigaOriga)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.VialOfSlimeOil, Item.FrostTurnip))
                        {
                            // quest.reward.gil
                            return QuestReply.ProgressEvent(417, RewardGil);
                        }

                        return null;
                    })
                    .OnTrigger(QuestReply.ProgressEvent(414, 0, Item.VialOfSlimeOil, Item.FrostTurnip))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Serukoko)
                    .OnTrigger(QuestReply.ProgressEvent(415, 0, Item.VialOfSlimeOil, Item.FrostTurnip))
                .OnEventFinish(Zone.WindurstWaters, 417, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HarigaOriga).OnTrigger(QuestReply.Event(418).ImportantOnce())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Serukoko).OnTrigger(QuestReply.Event(419).ImportantOnce());
        }
    }
}
