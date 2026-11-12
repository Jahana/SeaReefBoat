// Doll of the Dead (Windurst 9-1, Log ID 2, Mission ID 22), a line-for-line port of
// scripts/missions/windurst/9_1_Doll_of_the_Dead.lua (quest rework C3 N6). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo
// !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Apururu !pos -11 -2 13
// 241; Yoran-Oran !pos -109.987 -14 203.338 239; Mandragora Warden !pos 81.981 7.593 139.556 153. Reward: the title
// Guiding Star and 800 rank points. Section 1 (no Windurst mission current, a Windurstian): the gate guards' finishes
// (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 22 (handleAcceptMission)
// begin the mission and say the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Apururu plays 619 at status 0, 620
// at 3 and 621 at 6 (progress); 619's finish sets status 1, 620's 4, 621's sets 7 and takes the Letter from
// Zonpa-Zippa. Heavens Tower's zone-in at status 1 plays 335, whose finish sets 2; _6q2 at 2 plays 362, whose finish
// sets 3. Yoran-Oran at 4 plays 439 (0, the Jug of Humus, the Clump of Goobbue Humus; progress), whose finish sets 5.
// The Boyahda Tree's Mandragora Warden at status 4 or 5 takes the Clump of Goobbue Humus (progressCutscene 13, whose
// finish completes the trade, sets 6 and gives the letter: npcUtil.giveKeyItem), and when talked to says WARDEN_SPEECH
// (the warden speaking: player:messageText(npc, ...)) and then WARDEN_TRANSLATION (messageSpecial). Full Moon
// Fountain's zone-in at 7 plays 61, whose finish completes the mission. Kept as the Lua has it: section 1 compares the
// nation with mission.areaId (the log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed check
// (it reads only the current mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class DollOfTheDead : MissionScript
    {
        public DollOfTheDead() : base(XI.Quests.MissionLog.Windurst, "DOLL_OF_THE_DEAD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/9_1_Doll_of_the_Dead";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 22)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Title = Title.GuidingStar, RankPoints = 800 });

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

            b.State(current: true)
                .OnZoneIn(Zone.FullMoonFountain, q =>
                    q.Player.MissionStatus(AreaId) == 7 ? QuestReply.Event(61) : null)
                .OnEventFinish(Zone.FullMoonFountain, 61, q => q.Mission.Complete())
                // TODO: Kupipi, Zubaba, and other guards around Star Sybil's Chamber are missing dialogue
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.ProgressEvent(362);
                    }

                    return null;
                })
                .OnZoneIn(Zone.HeavensTower, q =>
                    q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(335) : null)
                .OnEventFinish(Zone.HeavensTower, 335, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.HeavensTower, 362, q => q.Player.SetMissionStatus(AreaId, 3))
                .Npc(Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.MandragoraWarden).OnTrade(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if ((missionStatus == 4 || missionStatus == 5) &&
                        q.Trade.Matches((Item.ClumpOfGoobbueHumus, 1)))
                    {
                        return QuestReply.ProgressCutscene(13);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 4 || missionStatus == 5)
                    {
                        q.Text.MessageTextFromNpc(Text.TheBoyahdaTree.WARDEN_SPEECH);
                        return QuestReply.MessageSpecial(Text.TheBoyahdaTree.WARDEN_TRANSLATION);
                    }

                    return null;
                })
                .OnEventFinish(Zone.TheBoyahdaTree, 13, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetMissionStatus(AreaId, 6);
                    q.Player.GiveKeyItem(KeyItem.LetterFromZonpaZippa);
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4)
                    {
                        return QuestReply.ProgressEvent(439, 0, Item.JugOfHumus, Item.ClumpOfGoobbueHumus);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 439, q => q.Player.SetMissionStatus(AreaId, 5))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(619);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(620);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(621);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 619, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.WindurstWoods, 620, q => q.Player.SetMissionStatus(AreaId, 4))
                .OnEventFinish(Zone.WindurstWoods, 621, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 7);
                    q.Player.DelKeyItem(KeyItem.LetterFromZonpaZippa);
                });
        }
    }
}
