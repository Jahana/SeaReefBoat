// Storm's Crescendo (WotG 32, Log ID 5, Mission ID 31), a line-for-line port of
// scripts/missions/wotg/32_Storms_Crescendo.lua (quest rework C3 T7). Rally Point: Green !pos 54.013 -23.402 -203.103
// 137. One section (the mission current), in Xarcabard [S]: Rally Point: Green shows FEDERATION_COMPLETE (messageName,
// no entity) and plays nothing. Rally Point: Blue plays the progress event 19 (the campaign allegiance, 23, 1756, 400,
// 67108863, 659255957, 4905, 0) at Status 0, whose finish gives the Alchemical Signal Flare and sets Status 1; at
// Status 1 without the flare it plays the progress event 26 (137, 23, 2964) while the Timer var is not past the
// Vana'diel day (its finish gives the flare again) and shows BASTOKAN_OFFICER_GONE after it, with the flare
// HELP_REPUBLIC_PREPARE; at Status 3 COMPLETED_TASKS; at any status it plays nothing (noAction) when no event plays.
// Rally Point: Red at Status 3 plays the progress event 23 (137, 5), whose finish sets Status 4 and sets the char down
// at -105.798 -25.522 -53.499 rot 176 in Xarcabard [S]. The zone-in plays the plain event 20 at Status 2 (its finish
// sets Status 3) and 39 at Status 4 (its finish completes the mission, Into the Beast's Maw current). Ghoyu's Reverie's
// event 10000 sets Status 2 and sets the char down at 138.527 -16.197 -30.414 rot 211 in Xarcabard [S]. Kept as the
// Lua has it: Timer is read but never set in this mission. No flow statement starts 10000 (the harness has no instance
// run); fake checks pin that finish and its set-down.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class StormsCrescendo : MissionScript
    {
        public StormsCrescendo() : base(XI.Quests.MissionLog.WoTG, "STORMS_CRESCENDO") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/32_Storms_Crescendo";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.IntoTheBeastsMaw,
            });

            b.State(current: true)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointGreen).OnTrigger(q =>
                {
                    q.Text.MessageName(Text.XarcabardS.FEDERATION_COMPLETE);

                    return QuestReply.NoAction();
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointBlue).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(19, q.Player.CampaignAllegiance, 23, 1756, 400, 67108863,
                            659255957, 4905, 0);
                    }
                    else if (missionStatus == 1)
                    {
                        if (!q.Player.HasKeyItem(KeyItem.AlchemicalSignalFlare))
                        {
                            if (q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                            {
                                return QuestReply.ProgressEvent(26, 137, 23, 2964);
                            }
                            else
                            {
                                q.Text.MessageName(Text.XarcabardS.BASTOKAN_OFFICER_GONE);
                            }
                        }
                        else
                        {
                            q.Text.MessageName(Text.XarcabardS.HELP_REPUBLIC_PREPARE);
                        }
                    }
                    else if (missionStatus == 3)
                    {
                        q.Text.MessageName(Text.XarcabardS.COMPLETED_TASKS);
                    }

                    return QuestReply.NoAction();
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointRed).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 3)
                    {
                        return QuestReply.ProgressEvent(23, 137, 5);
                    }

                    return null;
                })
                .OnZoneIn(Zone.XarcabardS, q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 2)
                    {
                        return QuestReply.Event(20);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(39);
                    }

                    return null;
                })
                .OnEventFinish(Zone.XarcabardS, 19, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AlchemicalSignalFlare);
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.XarcabardS, 20, q =>
                {
                    q.Mission.SetVar("Status", 3);
                })
                .OnEventFinish(Zone.XarcabardS, 23, q =>
                {
                    q.Mission.SetVar("Status", 4);
                    q.Player.SetPos(-105.798, -25.522, -53.499, 176, Zone.XarcabardS);
                })
                .OnEventFinish(Zone.XarcabardS, 26, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AlchemicalSignalFlare);
                })
                .OnEventFinish(Zone.XarcabardS, 39, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.GhoyusReverie, 10000, q =>
                {
                    // the Lua's TODO: the script assumes the instance's clear sends 10000
                    q.Mission.SetVar("Status", 2);
                    q.Player.SetPos(138.527, -16.197, -30.414, 211, Zone.XarcabardS);
                });
        }
    }
}
