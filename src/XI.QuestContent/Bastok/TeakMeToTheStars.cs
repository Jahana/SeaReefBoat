// Teak Me to the Stars (Log ID 1, Quest ID 79), a line-for-line port of scripts/quests/bastok/Teak_Me_to_the_Stars.lua
// (quest rework B3b). Raibaht : !gotoid 17748012. Its completion sets the mustZone of Hyper Active, a quest no script
// implements. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TeakMeToTheStars : QuestScript
    {
        public TeakMeToTheStars() : base(QuestLog.Bastok, "TEAK_ME_TO_THE_STARS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Teak_Me_to_the_Stars";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Bastok, Gil = 2100 });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.Metalworks, Npc.Metalworks.Raibaht).OnTrigger(q =>
                {
                    int option = 0;
                    if (q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheCallOfTheWyrmking))
                    {
                        // additional dialogue regarding the Crystal propulsion unit and the hooded scientists of CoP's
                        // The Call of the Wyrmking
                        option = 2;
                    }

                    return QuestReply.ProgressEvent(864, 0, Item.GarhadaTeakLumber, 0, 0, 0, 0, option);
                })
                .OnEventFinish(Zone.Metalworks, 864, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Raibaht)
                    // cycles between 871 and his default 501
                    .OnTrigger(q => QuestReply.Event(871, 0, Item.GarhadaTeakLumber).SetPriority(10))
                    .OnTrade(q => q.Trade.Matches((Item.GarhadaTeakLumber, 1)) ? QuestReply.Event(865) : null)
                .OnEventFinish(Zone.Metalworks, 865, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetQuestMustZone(QuestLog.Bastok, "HYPER_ACTIVE");
                    }
                });
        }
    }
}
