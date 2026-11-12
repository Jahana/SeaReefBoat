// The Shadow Awaits (Windurst 5-2, Log ID 2, Mission ID 15), a line-for-line port of
// scripts/missions/windurst/5_2_The_Shadow_Awaits.lua (quest rework C3 N4). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo
// !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Vestal Chamber (_6q2)
// !pos 0.1 -49 37 242; Zubaba !pos 15 -27 18 242; Throne Room door (_4l1) !pos -111 -6 0 165. Reward: 20000 gil and
// rank 6. Section 1 (no Windurst mission current, a Windurstian): the gate guards' finishes (Port Windurst 78,
// Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 15 (handleAcceptMission) begin the mission,
// say the zone's YOU_ACCEPT_THE_MISSION and give the Star-Crested Summons (npcUtil.giveKeyItem). Section 2 (current):
// Heavens Tower's zone-in at status 4 with ReturnCS 0 and the Shadow Fragment plays 215 (text table -1, flags
// RESET_CAMERA | NO_PCS | NO_NPCS), whose finish sets ReturnCS 1. _6q2 with the summons plays 214, else with the
// fragment 216 (progress); Zubaba with the summons plays 157, else with the fragment 194 (events). 214's finish sets
// status 2, adds the title Star-Ordained Warrior and takes the summons; 216's completes the mission and then takes
// the fragment. The Throne Room's _4l1 at status 2 plays 6, whose finish sets status 3; the zone-in at status 3 with
// PostBattle 1 plays 7 (text table -1, flags UNKNOWN_0008), whose finish sets status 4, clears PostBattle and gives
// the Shadow Fragment (npcUtil.giveKeyItem). 32001's finish at status 3 with battlefieldWin SHADOW_LORD_BATTLE (160)
// adds Zilart's The New Frontier unless it is current or completed, sets PostBattle 1 and moves the char to (90.425,
// -5.749, 0.089, 3) in the Throne Room. Kept as the Lua has it: section 1 compares the nation with mission.areaId
// (the log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed check (it reads only the
// current mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheShadowAwaits : MissionScript
    {
        public TheShadowAwaits() : base(XI.Quests.MissionLog.Windurst, "THE_SHADOW_AWAITS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/5_2_The_Shadow_Awaits";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 15)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
                q.Player.GiveKeyItem(KeyItem.StarCrestedSummons1);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 20000, Rank = 6 });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.PortWindurst, 78, q =>
                    HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93, q =>
                    HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111, q =>
                    HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114, q =>
                    HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            // Player has accepted the mission
            b.State(current: true)
                .OnZoneIn(Zone.HeavensTower, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4 &&
                        q.Mission.Var("ReturnCS") == 0 &&
                        q.Player.HasKeyItem(KeyItem.ShadowFragment))
                    {
                        // { 215, -1, bit.bor(RESET_CAMERA, NO_PCS, NO_NPCS) }: 0x01 | 0x02 | 0x10
                        return QuestReply.Event(215).TextTable(-1).Flags(0x13);
                    }

                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.StarCrestedSummons1))
                    {
                        return QuestReply.ProgressEvent(214);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ShadowFragment))
                    {
                        return QuestReply.ProgressEvent(216);
                    }

                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Zubaba).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.StarCrestedSummons1))
                    {
                        return QuestReply.Event(157);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.ShadowFragment))
                    {
                        return QuestReply.Event(194);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 214, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.AddTitle(Title.StarOrdainedWarrior);
                    q.Player.DelKeyItem(KeyItem.StarCrestedSummons1);
                })
                .OnEventFinish(Zone.HeavensTower, 215, q => q.Mission.SetVar("ReturnCS", 1))
                .OnEventFinish(Zone.HeavensTower, 216, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ShadowFragment);
                    }
                })
                .OnZoneIn(Zone.ThroneRoom, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Mission.Var("PostBattle") == 1)
                    {
                        // { 7, -1, xi.cutsceneFlag.UNKNOWN_0008 }
                        return QuestReply.Event(7).TextTable(-1).Flags(0x08);
                    }

                    return null;
                })
                .Npc(Zone.ThroneRoom, Npc.ThroneRoom._4l1).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.ProgressEvent(6);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ThroneRoom, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.ShadowLordBattle)
                    {
                        if (q.Player.CurrentMission(MissionLog.Zilart) != Mission.Zilart.TheNewFrontier &&
                            !q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier))
                        {
                            // Don't add missions we already completed. Players who change nation will hit this.
                            q.Player.AddMission(MissionLog.Zilart, Mission.Zilart.TheNewFrontier);
                        }

                        q.Mission.SetVar("PostBattle", 1);
                        q.Player.SetPos(90.425, -5.749, 0.089, 3, Zone.ThroneRoom);
                    }
                })
                .OnEventFinish(Zone.ThroneRoom, 6, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.ThroneRoom, 7, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Mission.SetVar("PostBattle", 0);
                    q.Player.GiveKeyItem(KeyItem.ShadowFragment);
                });
        }
    }
}
