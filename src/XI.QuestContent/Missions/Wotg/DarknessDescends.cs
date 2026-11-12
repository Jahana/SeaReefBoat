// Darkness Descends (WotG 37, Log ID 5, Mission ID 36), a line-for-line port of
// scripts/missions/wotg/37_Darkness_Descends.lua (quest rework C3 T7). One section (the mission current), in the
// Throne Room [S]: the zone-in plays the plain event 8 at Status 0 and 10 at Status 2. 8's update with option 5 sends
// the update (1, 23, 1756, 470, 470, 848, 0, 0) and its finish sets Status 1; 10's update with option 5 sends the
// update (156, the char's face, the head, body, hands, main and sub model ids, 0) and its finish completes the mission
// (Adieu, Lilisette current). The 32001 finish, when the battlefieldWin local var is
// xi.battlefield.id.DARKNESS_DESCENDS (353) at Status 1, sets Status 2 and sets the char down at -90.854 -5.75 -0.009
// rot 129 in the Throne Room [S]. Kept as the Lua has it: no battlefield file holds 353, so no fight opens that 32001
// and nothing sets Status 2 (the Lua's TODO); the 32001 finish is waived in the flows and pinned by fake checks.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class DarknessDescends : MissionScript
    {
        public DarknessDescends() : base(XI.Quests.MissionLog.WoTG, "DARKNESS_DESCENDS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/37_Darkness_Descends";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.AdieuLilisette,
            });

            b.State(current: true)
                .OnZoneIn(Zone.ThroneRoomS, q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(8);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(10);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.ThroneRoomS, 8, q =>
                {
                    if (q.Option == 5)
                    {
                        // the Lua's TODO: check the first parameter for a char of the Bastok or Windurst allegiance
                        q.Player.UpdateEvent(1, 23, 1756, 470, 470, 848, 0, 0);
                    }
                })
                .OnEventUpdate(Zone.ThroneRoomS, 10, q =>
                {
                    if (q.Option == 5)
                    {
                        var equip = q.Player.EquipmentModelIds;
                        q.Player.UpdateEvent(156, q.Player.Face, equip.Head, equip.Body, equip.Hands, equip.Main,
                            equip.Sub, 0);
                    }
                })
                .OnEventFinish(Zone.ThroneRoomS, 8, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.ThroneRoomS, 10, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.ThroneRoomS, 32001, q =>
                {
                    // the Lua's TODO: the battlefield's win should set battlefieldWin to its id
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.DarknessDescends &&
                        q.Mission.Var("Status") == 1)
                    {
                        q.Mission.SetVar("Status", 2);
                        q.Player.SetPos(-90.854, -5.75, -0.009, 129, Zone.ThroneRoomS);
                    }
                });
        }
    }
}
