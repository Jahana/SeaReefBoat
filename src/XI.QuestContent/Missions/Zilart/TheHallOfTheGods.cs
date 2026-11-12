// The Hall of the Gods (Zilart 11, Log ID 3, Mission ID 22), a line-for-line port of
// scripts/missions/rotz/11_The_Hall_of_the_Gods.lua (quest rework C3, C6). _700 (Oaken Door) !pos 97 -7 -12 252.
// Section 1 (current): the Hall of the Gods' _6z0 (Cermet Gate) says DEPRESSION_A_CLUE (player:messageText with the
// gate as the speaker, no name, mode 6) and answers with no action, so the gate's own script does not run; Lower
// Jeuno's Aldo plays 24; Norg's _700 plays 169, whose finish on option 0 completes the mission (The Mithra and the
// Crystal next; no key item, no title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheHallOfTheGods : MissionScript
    {
        public TheHallOfTheGods() : base(XI.Quests.MissionLog.Zilart, "THE_HALL_OF_THE_GODS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/11_The_Hall_of_the_Gods";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheMithraAndTheCrystal,
            });

            b.State(current: true)
                .Npc(Zone.HallOfTheGods, Npc.HallOfTheGods._6z0).OnTrigger(q =>
                {
                    q.Text.MessageText(Text.HallOfTheGods.DEPRESSION_A_CLUE, false, 6);

                    return QuestReply.NoAction();
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(QuestReply.Event(24))
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(QuestReply.ProgressEvent(169))
                .OnEventFinish(Zone.Norg, 169, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
