// Affairs of State (WotG 12, Log ID 5, Mission ID 11), a line-for-line port of
// scripts/missions/wotg/12_Affairs_of_State.lua (quest rework C3 T5). Velda-Galda (Windurst Waters [S]) and Radford
// (Bastok Markets [S]) are talked to in either order. Section 0 (status 0): Velda-Galda plays the progress event 178
// (3, 2), whose finish sets the mission var WindurstFirst to 1 and status 1; Radford plays the progress event 175 (2,
// 27, 0, 0, 0, 0, 1, 4095), whose finish sets status 1. Section 1 (status 1): Velda-Galda plays the reminder 180 (94,
// 7) when WindurstFirst is 1, else the progress event 179 (the char's Campaign allegiance, 0, 1), whose finish
// completes the mission (Borne by the Wind current, Count Borel's Letter given); Radford plays the reminder 177 (87,
// 55, 0, 0, 0, 0, 1, 4095) when WindurstFirst is 0, else the progress event 176 (3, 75, 1, 0, 0, 0, 4095, 0), whose
// finish completes the mission. As the Lua has it (the intended either-order design): Radford's first visit does not
// set WindurstFirst (it stays 0), so a char who went Bastok first gets Radford's reminder and then
// Velda-Galda's progress event; a char who went Windurst first gets Radford's progress event.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class AffairsOfState : MissionScript
    {
        public AffairsOfState() : base(XI.Quests.MissionLog.WoTG, "AFFAIRS_OF_STATE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/12_Affairs_of_State";

        const int AreaId = MissionLog.Wotg;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.CountBorelsLetter,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.BorneByTheWind,
            });

            // 0: Speak to one of the nations first
            b.State(current: true, status: 0)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.VeldaGalda)
                .OnTrigger(QuestReply.ProgressEvent(178, 3, 2))
                .OnEventFinish(Zone.WindurstWatersS, 178, q =>
                {
                    q.Mission.SetVar("WindurstFirst", 1);
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Radford)
                .OnTrigger(QuestReply.ProgressEvent(175, 2, 27, 0, 0, 0, 0, 1, 4095))
                .OnEventFinish(Zone.BastokMarketsS, 175, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                });

            // 1: Speak to the other one of the nations, ends the mission
            b.State(current: true, status: 1)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.VeldaGalda).OnTrigger(q =>
                {
                    bool windurstFirst = q.Mission.Var("WindurstFirst") == 1;

                    if (windurstFirst)
                    {
                        // Reminder
                        return QuestReply.ProgressEvent(180, 94, 7);
                    }

                    // Progress: the args trigger the "handing over the letter" end part of the cutscene
                    return QuestReply.ProgressEvent(179, q.Player.CampaignAllegiance, 0, 1);
                })
                .OnEventFinish(Zone.WindurstWatersS, 179, q =>
                {
                    q.Mission.Complete();
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Radford).OnTrigger(q =>
                {
                    bool bastokFirst = q.Mission.Var("WindurstFirst") == 0;

                    if (bastokFirst)
                    {
                        // Reminder
                        return QuestReply.ProgressEvent(177, 87, 55, 0, 0, 0, 0, 1, 4095);
                    }

                    // Progress: the args trigger the "handing over the letter" end part of the cutscene
                    return QuestReply.ProgressEvent(176, 3, 75, 1, 0, 0, 0, 4095, 0);
                })
                .OnEventFinish(Zone.BastokMarketsS, 176, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
