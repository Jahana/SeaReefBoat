// The Sixth Ministry (Windurst 7-1, Log ID 2, Mission ID 18), a line-for-line port of
// scripts/missions/windurst/7_1_The_Sixth_Ministry.lua (quest rework C3 N5). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo
// !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Tosuka-Porika !pos -26
// -6 103 238; _4pc !pos 132 12 -19 169. Reward: the Blank Book of the Gods and 700 rank points. Section 1 (no Windurst
// mission current, a Windurstian): the gate guards' finishes (Port Windurst 78, Windurst Walls 93, Windurst Waters
// 111, Windurst Woods 114) with option 18 (handleAcceptMission) begin the mission and say the zone's
// YOU_ACCEPT_THE_MISSION. Section 2 (current): Tosuka-Porika at status 0 plays 715 (progress, with the Optistery
// Ring), at status 1 716 (event, with the ring) and at status 2 724 (progress). 715's finish gives the ring and sets
// status 1; 724's completes the mission and then takes the ring. The town NPCs play their own events: Port Windurst
// Janshura-Rashura 469, Nine of Clubs 470, Puo Rhen 472, Ten of Clubs 471; Windurst Walls Chawo Shipeynyo 364,
// Keo-Koruo 363, Pakke-Pokke 362, Zokima-Rokima 361; Windurst Waters Dagoza-Beruza 713, Mokyokyo 711, Panna-Donna 712,
// Ten of Hearts 714; Windurst Woods Miiri-Wohri 570, Rakoh Buuma 569, Sola Jaab 571, Tih Pikeh 572. Section 3 (current
// or completed): Toraimarai Canal's _4pc plays 70 (event, 0, 0, 0, 1) while one of the four Hinge Oils
// (HINGE_OILS_OFFSET + 0 to 3) is not dead, else the cutscene 70 (progress, 0, 0, 0, 2); the fifth Tome of Magic
// (TOME_OF_MAGIC_OFFSET + 4), with the mission current at status 1, plays 69 (progress, with the ring), whose finish
// sets status 2. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 2, which is
// xi.nation.WINDURST) and has no rank check and no completed check; _4pc has no finish for 70.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheSixthMinistry : MissionScript
    {
        public TheSixthMinistry() : base(XI.Quests.MissionLog.Windurst, "THE_SIXTH_MINISTRY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/7_1_The_Sixth_Ministry";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.TheSixthMinistry;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 18)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { KeyItem = KeyItem.BlankBookOfTheGods, RankPoints = 700 });

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
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(469))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(470))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(472))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(471))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(364))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(363))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(362))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(361))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(713))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(711))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(712))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(714))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(715, 0, KeyItem.OptisteryRing);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(716, 0, KeyItem.OptisteryRing);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(724);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 715, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.OptisteryRing);
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.WindurstWaters, 724, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OptisteryRing);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(570))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(569))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(571))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(572));

            // Mission is current, or has been completed
            b.State(current: null, when: q =>
                    q.Mission.IsCurrent ||
                    q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.ToraimaraiCanal, Npc.ToraimaraiCanal._4pc).OnTrigger(q =>
                {
                    for (int i = 0; i <= 3; i++)
                    {
                        if (!q.Player.MobIsDead(Mob.ToraimaraiCanal.HINGE_OILS_OFFSET, i))
                        {
                            // At least one Hinge Oil is alive
                            return QuestReply.Event(70, 0, 0, 0, 1);
                        }
                    }

                    // All four Hinge Oils are dead
                    return QuestReply.ProgressCutscene(70, 0, 0, 0, 2);
                })
                .Npc(Zone.ToraimaraiCanal, Npc.ToraimaraiCanal.TomeofMagic).OnTrigger(q =>
                {
                    int tomeOffset = q.Npc.Id - q.Player.NpcIdOf("TOME_OF_MAGIC_OFFSET");

                    if (tomeOffset == 4 &&
                        q.Player.CurrentMission(AreaId) == MissionId &&
                        q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(69, 0, KeyItem.OptisteryRing);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ToraimaraiCanal, 69, q => q.Player.SetMissionStatus(AreaId, 2));
        }
    }
}
