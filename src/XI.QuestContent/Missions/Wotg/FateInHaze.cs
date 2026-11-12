// Fate in Haze (WotG 26, Log ID 5, Mission ID 25), a line-for-line port of scripts/missions/wotg/26_Fate_in_Haze.lua
// (quest rework C3 T6). One section (the mission current and xi.wotg.helpers.meetsMission26Reqs, which stays Lua),
// in Southern San d'Oria [S]. Lion Springs plays the progress event 151 (the campaign allegiance, 0, 1, 0), whose
// finish sets Status 1 and sets the char down at 100.801 1 103.211 rot 31 in the same zone. Rholont plays the plain
// event 656 at allegiance 1. The zone-in plays 154 at Status 1 and 155 at Status 2; each one's update (option 1)
// updates the event with the campaign allegiance, 0, 1756; 154's finish sets Status 2 and sets the char down again;
// 155's finish completes the mission (The Scent of Battle current). Kept as the Lua has it: the whole section is
// gated by meetsMission26Reqs (a char with none of the three quests gets none of it); Rholont's event 656 only at
// allegiance 1. The main flow's trace shows the set-downs only as the rot (0 to 31) and the zone-out, since the char
// starts on the spot; a fake check pins x, y and z.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class FateInHaze : MissionScript
    {
        public FateInHaze() : base(XI.Quests.MissionLog.WoTG, "FATE_IN_HAZE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/26_Fate_in_Haze";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TheScentOfBattle,
            });

            b.State(current: true, when: q => q.Player.WotgMeetsMissionReqs(26))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings)
                .OnTrigger(q => QuestReply.ProgressEvent(151, q.Player.CampaignAllegiance, 0, 1, 0))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (q.Player.CampaignAllegiance == 1)
                    {
                        return QuestReply.Event(656);
                    }

                    return null;
                })
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 1)
                    {
                        return QuestReply.Event(154);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(155);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.SouthernSanDoriaS, 154, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.CampaignAllegiance, 0, 1756);
                    }
                })
                .OnEventUpdate(Zone.SouthernSanDoriaS, 155, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.CampaignAllegiance, 0, 1756);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 151, q =>
                {
                    q.Mission.SetVar("Status", 1);
                    q.Player.SetPos(100.801, 1, 103.211, 31, Zone.SouthernSanDoriaS);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 154, q =>
                {
                    q.Mission.SetVar("Status", 2);
                    q.Player.SetPos(100.801, 1, 103.211, 31, Zone.SouthernSanDoriaS);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 155, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
