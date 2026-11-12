// Saintly Invitation (Windurst 6-2, Log ID 2, Mission ID 17), a line-for-line port of
// scripts/missions/windurst/6_2_Saintly_Invitation.lua (quest rework C3 N5). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo
// !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Vestal Chamber (_6q2)
// !pos 0.1 -49 37 242; Kaa Toru the Just !pos -100.188 -62.125 145.422 151. Reward: 40000 gil and rank 7. Section 1
// (no Windurst mission current, a Windurstian): the gate guards' finishes (Port Windurst 78, Windurst Walls 93,
// Windurst Waters 111, Windurst Woods 114) with option 17 (handleAcceptMission) begin the mission and say the zone's
// YOU_ACCEPT_THE_MISSION. Section 2 (current): Heavens Tower's _6q2 at status 0 plays 310 and at status 3 312
// (progress); Foo Beibo 304, Shaz Norem 306, Vahn Paineesha 305 and Zubaba 301 (events); Kupipi at status 0 300, at 1
// or 2 311, else 333; Rhy Epocan at 0 or 3 302, else 307; Ufu Koromoa at 0 303, else 308 (events). 310's finish sets
// status 1, adds the title Hero on Behalf of Windurst and gives the Holy One's Invitation; 312's completes the mission
// and then takes the Holy One's Oath. Balgas Dais' 32001 finish at status 1 with battlefieldWin SAINTLY_INVITATION (99)
// adds the title Victor of the Balga Contest, gives the Balga Champion Certificate, takes the invitation and sets
// status 2. Kaa Toru the Just at status 2 plays the cutscene 45 (progress, with the oath), at status 3 46 (event); 45's
// finish, when the Ashura Necklace is given, gives the oath, takes the certificate and sets status 3. The town NPCs
// play their own events: Port Windurst Janshura-Rashura 464, Nine of Clubs 465, Puo Rhen 467, Ten of Clubs 466;
// Windurst Walls Chawo Shipeynyo 359, Keo-Koruo 357, Pakke-Pokke 358, Zokima-Rokima 356; Windurst Waters
// Dagoza-Beruza 708, Mokyokyo 706, Panna-Donna 707, Ten of Hearts 709; Windurst Woods Miiri-Wohri 564, Rakoh Buuma
// 563, Sola Jaab 565, Tih Pikeh 566. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the
// log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed check.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class SaintlyInvitation : MissionScript
    {
        public SaintlyInvitation() : base(XI.Quests.MissionLog.Windurst, "SAINTLY_INVITATION") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/6_2_Saintly_Invitation";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.SaintlyInvitation;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 17)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 40000, Rank = 7 });

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
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(310);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(312);
                    }

                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.FooBeibo).OnTrigger(QuestReply.Event(304))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(300);
                    }
                    else if (missionStatus == 1 || missionStatus == 2)
                    {
                        return QuestReply.Event(311);
                    }
                    else
                    {
                        return QuestReply.Event(333);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.RhyEpocan).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0 || missionStatus == 3)
                    {
                        return QuestReply.Event(302);
                    }
                    else
                    {
                        return QuestReply.Event(307);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.ShazNorem).OnTrigger(QuestReply.Event(306))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.UfuKoromoa).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(303);
                    }
                    else
                    {
                        return QuestReply.Event(308);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.VahnPaineesha).OnTrigger(QuestReply.Event(305))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Zubaba).OnTrigger(QuestReply.Event(301))
                .OnEventFinish(Zone.HeavensTower, 310, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.AddTitle(Title.HeroOnBehalfOfWindurst);
                    q.Player.GiveKeyItem(KeyItem.HolyOnesInvitation);
                })
                .OnEventFinish(Zone.HeavensTower, 312, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.HolyOnesOath);
                    }
                })
                .OnEventFinish(Zone.BalgasDais, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.SaintlyInvitation)
                    {
                        q.Player.AddTitle(Title.VictorOfTheBalgaContest);
                        q.Player.GiveKeyItem(KeyItem.BalgaChampionCertificate);
                        q.Player.DelKeyItem(KeyItem.HolyOnesInvitation);
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.CastleOztroja, Npc.CastleOztroja.KaaTorutheJust).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressCutscene(45, 0, KeyItem.HolyOnesOath);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(46);
                    }

                    return null;
                })
                .OnEventFinish(Zone.CastleOztroja, 45, q =>
                {
                    if (q.Player.GiveItem(Item.AshuraNecklace))
                    {
                        q.Player.GiveKeyItem(KeyItem.HolyOnesOath);
                        q.Player.DelKeyItem(KeyItem.BalgaChampionCertificate);
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(464))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(465))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(467))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(466))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(359))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(357))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(358))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(356))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(708))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(706))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(707))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(709))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(564))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(563))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(565))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(566));
        }
    }
}
