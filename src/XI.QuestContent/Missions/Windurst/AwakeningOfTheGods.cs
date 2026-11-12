// Awakening of the Gods (Windurst 7-2, Log ID 2, Mission ID 19), a line-for-line port of
// scripts/missions/windurst/7_2_Awakening_of_the_Gods.lua (quest rework C3 N5). Rakoh Buuma !pos 106 -5 -23 241;
// Mokyokyo !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Leepe-Hoppe
// !pos 13 -9 -197 238; Kerutoto !pos 13 -5 -157 238; Jakoh Wahcondalo !pos 101 -16 -115 250; Romaa Mihgo !pos 29
// -13.023 -176.5 250; Vanono !pos -23.140 -5 -23.101 250; Granite Door (_4fx) !pos 340 0.1 329 159. Reward: 60000 gil
// and rank 8. Section 1 (no Windurst mission current, a Windurstian): the gate guards' finishes (Port Windurst 78,
// Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 19 (handleAcceptMission) begin the mission
// and say the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Kerutoto at status 0 plays 737 (event), at 1 736
// (progress), at 2 or more 738 (event); Leepe-Hoppe at 0 plays 734 (progress), at 1 735, at 2 to 4 739 (events), at 5
// with the Book of the Gods 742 (progress). 734's finish sets status 1, 736's status 2, 742's completes the mission.
// Kazham's Jakoh Wahcondalo at status 2 plays 265; Romaa Mihgo at 2 plays 266 (progress), at 3 or 4 267, at 5 or more
// 263; Vanono at 3 plays 264 (progress), above 3 268. 264's finish sets status 4, 266's status 3. Temple of Uggalepih's
// _4fx, traded exactly one Cursed Key below z 332 at status 3 or 4, plays the cutscene 23 (progress, 0, 0, 1, 0, 0, 0,
// 0, 0), whose finish takes the trade, takes the Blank Book of the Gods, sets status 5 and gives the Book of the Gods.
// The town NPCs play their own events: Port Windurst Janshura-Rashura 474, Nine of Clubs 475, Puo Rhen 477, Ten of
// Clubs 476; Windurst Walls Chawo Shipeynyo 369, Keo-Koruo 368, Pakke-Pokke 367, Zokima-Rokima 366; Windurst Waters
// Dagoza-Beruza 731, Kenapa-Keppa 740, Mokyokyo 730, Ohbiru-Dohbiru 741, Panna-Donna 732, Ten of Hearts 733; Windurst
// Woods Miiri-Wohri 575, Rakoh Buuma 574, Sola Jaab 576, Tih Pikeh 577. Kept as the Lua has it: section 1 compares the
// nation with mission.areaId (the log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed
// check; _4fx's cutscene keeps the Lua's TODO (the decompiled event shows a param[7]).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class AwakeningOfTheGods : MissionScript
    {
        public AwakeningOfTheGods() : base(XI.Quests.MissionLog.Windurst, "AWAKENING_OF_THE_GODS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/7_2_Awakening_of_the_Gods";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.AwakeningOfTheGods;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 19)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 60000, Rank = 8 });

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
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(474))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(475))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(477))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(476))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(369))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(368))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(367))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(366))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(731))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(QuestReply.Event(740))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.Event(737);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(736);
                    }
                    else if (missionStatus >= 2)
                    {
                        return QuestReply.Event(738);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(734);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(735);
                    }
                    else if (missionStatus >= 2 && missionStatus <= 4)
                    {
                        return QuestReply.Event(739);
                    }
                    else if (missionStatus == 5 && q.Player.HasKeyItem(KeyItem.BookOfTheGods))
                    {
                        return QuestReply.ProgressEvent(742);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(730))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(QuestReply.Event(741))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(732))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(733))
                .OnEventFinish(Zone.WindurstWaters, 734, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.WindurstWaters, 736, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.WindurstWaters, 742, q => q.Mission.Complete())
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(575))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(574))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(576))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(577))
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.Event(265);
                    }

                    return null;
                })
                .Npc(Zone.Kazham, Npc.Kazham.RomaaMihgo).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(266);
                    }
                    else if (missionStatus == 3 || missionStatus == 4)
                    {
                        return QuestReply.Event(267);
                    }
                    else if (missionStatus >= 5)
                    {
                        return QuestReply.Event(263);
                    }

                    return null;
                })
                .Npc(Zone.Kazham, Npc.Kazham.Vanono).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(264);
                    }
                    else if (missionStatus > 3)
                    {
                        return QuestReply.Event(268);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Kazham, 264, q => q.Player.SetMissionStatus(AreaId, 4))
                .OnEventFinish(Zone.Kazham, 266, q => q.Player.SetMissionStatus(AreaId, 3))
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih._4fx).OnTrade(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (q.Trade.Matches((Item.CursedKey, 1)) &&
                        q.Player.ZPos < 332 &&
                        missionStatus >= 3 &&
                        missionStatus <= 4)
                    {
                        // TODO: The decompiled event shows a param[7]. Might be based on race?
                        return QuestReply.ProgressCutscene(23, 0, 0, 1, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.TempleOfUggalepih, 23, q =>
                {
                    q.Trade.Complete();
                    q.Player.DelKeyItem(KeyItem.BlankBookOfTheGods);
                    q.Player.SetMissionStatus(AreaId, 5);
                    q.Player.GiveKeyItem(KeyItem.BookOfTheGods);
                });
        }
    }
}
