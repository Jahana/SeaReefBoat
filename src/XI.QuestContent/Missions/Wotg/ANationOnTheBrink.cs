// A Nation on the Brink (WotG 14, Log ID 5, Mission ID 13), a line-for-line port of
// scripts/missions/wotg/14_A_Nation_on_the_Brink.lua (quest rework C3 T6). Underpass_Hatch !pos 314.083 -1.160
// -181.455 84. Section 0 (the mission current): Sauromugue Champaign [S]'s Bulwark Gate plays the progress event 6
// (98, 23, 1756) without the Underpass Hatch Key (its finish gives the key), else sends SURRENDER_CEREMONY_HASTE once
// per zone; Batallia Downs [S]'s Underpass Hatch plays the progress event 3 (24, 5, 0, 120), whose finish sets status
// 1. The Batallia Downs [S] zone-in plays the plain event 4 at status 2, 20 at status 3, 21 at status 4 and 22 at
// status 5; 4's update (option 1) updates the event with the campaign allegiance, 0, 1756; 4's finish sets status 3,
// 20's status 4 and 21's status 5, each setting the char down at 399.999 8.3 -269.999 rot 159 in the same zone; 22's
// finish completes the mission (Crossroads of Time current, the Battle of Jeuno Veteran title) and gives the Jeunoan
// Flag, else sets the mission var Unclaimed to 1. Everbloom Hollow's event 10000 sets status 2 and sets the char down
// at 302.747 -1 -174.367 rot 31 in Batallia Downs [S] (the Lua's TODO assumes the battlefield's clear sends it; nothing
// in Everbloom Hollow does). Section 1 (the mission completed): the Batallia Downs [S] _qm5 plays the progress event 5
// (84) while Unclaimed is 1; its finish with option 1 gives the flag and clears Unclaimed. Kept as the Lua has it:
// status 1 has no zone-in event. The main flow's trace shows finish 4's set-down at the next tick (399.999 8.3 -269.999
// rot 159); 20 and 21 set the char down on that same spot (the trace shows their zone-outs only), and the 10000
// set-down no flow reaches; a fake check pins all four.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ANationOnTheBrink : MissionScript
    {
        public ANationOnTheBrink() : base(XI.Quests.MissionLog.WoTG, "A_NATION_ON_THE_BRINK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/14_A_Nation_on_the_Brink";

        const int AreaId = MissionLog.Wotg;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.BattleOfJeunoVeteran,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.CrossroadsOfTime,
            });

            // 0. Go to Batallia Downs (S) and check the Underpass Hatch for a cutscene.
            b.State(current: true)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.UnderpassHatchKey))
                    {
                        return QuestReply.ProgressEvent(6, 98, 23, 1756);
                    }

                    return QuestReply.MessageSpecial(Text.SauromugueChampaignS.SURRENDER_CEREMONY_HASTE)
                        .OncePerZone();
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 6, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.UnderpassHatchKey);
                })
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.UnderpassHatch)
                .OnTrigger(QuestReply.ProgressEvent(3, 24, 5, 0, 120, 0, 0, 0, 0))
                .OnZoneIn(Zone.BatalliaDownsS, q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2)
                    {
                        return QuestReply.Event(4);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(20);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(21);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.Event(22);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.BatalliaDownsS, 4, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.CampaignAllegiance, 0, 1756, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.BatalliaDownsS, 3, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 4, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.SetPos(399.999, 8.3, -269.999, 159, Zone.BatalliaDownsS);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 20, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Player.SetPos(399.999, 8.3, -269.999, 159, Zone.BatalliaDownsS);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 21, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 5);
                    q.Player.SetPos(399.999, 8.3, -269.999, 159, Zone.BatalliaDownsS);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 22, q =>
                {
                    q.Mission.Complete();

                    if (!q.Player.GiveItem(Item.JeunoanFlag))
                    {
                        q.Mission.SetVar("Unclaimed", 1);
                    }
                })
                .OnEventFinish(Zone.EverbloomHollow, 10000, q =>
                {
                    // the battlefield-cleared event the Lua only assumes (its own TODO)
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.SetPos(302.747, -1, -174.367, 31, Zone.BatalliaDownsS);
                });

            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, Mission.Wotg.ANationOnTheBrink))
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.qm5).OnTrigger(q =>
                {
                    if (q.Mission.Var("Unclaimed") == 1)
                    {
                        return QuestReply.ProgressEvent(5, 84);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BatalliaDownsS, 5, q =>
                {
                    if (q.Option == 1 && q.Player.GiveItem(Item.JeunoanFlag))
                    {
                        q.Mission.SetVar("Unclaimed", 0);
                    }
                });
        }
    }
}
