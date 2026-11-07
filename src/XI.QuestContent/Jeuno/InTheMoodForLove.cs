// In the Mood for Love (Log ID 3, Quest ID 69), a line-for-line port of scripts/quests/jeuno/In_the_Mood_for_Love.lua
// (quest rework B3b). Odasel !pos -58 -6 -111 245; Matoaka !pos -37 -6 -122 245. Available once the CoP mission
// is past A Vessel Without a Captain: Odasel's 10035 (0, Chameleon Diamond) begins it. Accepted: Odasel's 10037 (0,
// Chameleon Diamond); the diamond traded alone: 10036 (0, Chameleon Diamond), whose finish completes it (4800 gil, Pick-up
// Artist), fame +7 in the three nations, takes the diamond and sets Option 1. Completed: Odasel's 10038 (replacing his
// default); Matoaka at Option 1: 10039 (Option 0), else her shop (her script).
// Kept Lua quirks: Option is set after quest:complete has cleared the quest's vars, so it survives the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class InTheMoodForLove : QuestScript
    {
        public InTheMoodForLove() : base(QuestLog.Jeuno, "IN_THE_MOOD_FOR_LOVE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/In_the_Mood_for_Love";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 4800, Title = Title.PickUpArtist });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.AVesselWithoutACaptain)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Odasel).OnTrigger(QuestReply.ProgressEvent(10035, 0, Item.ChameleonDiamond))
                .OnEventFinish(Zone.LowerJeuno, 10035, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Odasel)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(10037, 0, Item.ChameleonDiamond);
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.ChameleonDiamond))
                        {
                            return QuestReply.ProgressEvent(10036, 0, Item.ChameleonDiamond);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.LowerJeuno, 10036, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Confirm();
                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Odasel).OnTrigger(QuestReply.Event(10038).ReplaceDefault())
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Matoaka).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        // Additional Cutscene
                        return QuestReply.ProgressEvent(10039);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 10039, q =>
                {
                    q.Quest.SetVar("Option", 0);
                });
        }
    }
}
