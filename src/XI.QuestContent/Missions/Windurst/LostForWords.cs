// Lost for Words (Windurst 2-1, Log ID 2, Mission ID 3), a line-for-line port of
// scripts/missions/windurst/2_1_Lost_for_Words.lua (quest rework C3 N1c-2). Section 1 (no Windurst mission current, a
// Windurst citizen who has not completed this one): the finish of the gate guards' events (Port Windurst 78, Windurst
// Walls 93, Windurst Waters 111, Windurst Woods 114) with option 3 begins the mission, sets the status to 1 and says
// YOU_ACCEPT_THE_MISSION. Section 2 (current): the other NPCs of the four cities play their own events; Tih Pikeh
// alternates progress events 163 and 164 by a local var that their finishes toggle. Status 1: Tosuka-Porika plays
// progress event 160, whose finish sets status 2. Status 2: Nanaa Mihgo plays progress event 165 with the Lapis Coral
// and Lapis Monocle, whose finish with option 1 gives the Lapis Monocle, picks the rock (a random 0 to 7) and sets
// status 3 (any other option does nothing); Tosuka-Porika plays 161. Status 3: Bopa Greso, Cha Lebagta and Nanaa Mihgo
// play over their own events. Status 3 and 4: the fossil rocks of Maze of Shakhrami; the rock whose index in the
// Fossil_Rock list is the picked one gives the Lapis Coral at status 3 (status 4) and says it needs no more
// investigation at status 4; any other of the first eight says it is just a rock, and the other six say nothing is
// embedded. Status 4: Nanaa Mihgo plays progress event 169, whose finish takes both key items, clears the rock, gives
// the Hideout Key and sets status 5. Status 5: the same three NPCs play 171, 172 and 170; the door of Inner Horutoto
// Ruins plays progress cutscene 46, whose finish takes the Hideout Key and sets status 6. Status 6: the same three
// NPCs play 174, 175 and 173; the door of the House of the Hero says it is unlocked and plays progress event 337,
// whose finish sets status 7. Status 7: Tosuka-Porika plays progress event 168, whose finish completes the mission
// (300 rank points). Last section (completed, A Testing Time not completed): the post-mission events of the four
// cities.
// Kept as the Lua has them: the sections of the earlier missions' last sections and Mihgo's Amigo answer before this
// mission's plain events (the order of the sections, not a bug of this file); a correct rock at a status other than 3
// or 4 returns nothing (unreachable, the section is gated on the status); the rock var is 0 until Nanaa picks it, so
// the first rock is the right one for a mission put at status 3 without the pick.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class LostForWords : MissionScript
    {
        public LostForWords() : base(XI.Quests.MissionLog.Windurst, "LOST_FOR_WORDS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_1_Lost_for_Words";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.LostForWords;

        // handleAcceptMission
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 3)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 1);
                q.Text.Special(accepted);
            }
        }

        // examineRock
        static QuestReply ExamineRock(IQuestContext q)
        {
            int rockOffset = q.Npc.Id - q.Player.NpcIdOf("FOSSIL_ROCK_OFFSET");
            int correctRock = q.Mission.Var("Rock");
            int missionStatus = q.Player.MissionStatus(AreaId);

            if (rockOffset == correctRock)
            {
                if (missionStatus == 3)
                {
                    q.Player.SetMissionStatus(AreaId, 4);

                    return QuestReply.KeyItem(KeyItem.LapisCoral);
                }
                else if (missionStatus == 4)
                {
                    return QuestReply.MessageSpecial(Text.MazeOfShakhrami.NO_NEED_INVESTIGATE);
                }
            }
            // Only the first eight rocks belong to the dig.
            else if (rockOffset > 7)
            {
                return QuestReply.MessageSpecial(Text.MazeOfShakhrami.NOTHING_FOSSIL);
            }
            else
            {
                return QuestReply.MessageSpecial(Text.MazeOfShakhrami.JUST_A_ROCK);
            }

            return null;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 300 });

            // Player is offered mission from a gate guard
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .OnEventFinish(Zone.PortWindurst, 78,
                    q => HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93,
                    q => HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111,
                    q => HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114,
                    q => HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            // Mission has been accepted, these NPCs dialogue does not change during mission
            b.State(current: true)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(120))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(121))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(123))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(122))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(125))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(123))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(124))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(122))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.AoraUora).OnTrigger(QuestReply.Event(167))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(157))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).OnTrigger(QuestReply.Event(165))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KotanMoritan).OnTrigger(QuestReply.Event(166))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(156))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(158))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(159))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(161))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(160))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(162))
                // Tih Pikeh alternates two lines. The toggle advances in onEventFinish.
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(q =>
                {
                    if (q.Player.LocalVar("TihPikehRepeatLine") == 0)
                    {
                        return QuestReply.ProgressEvent(163);
                    }

                    return QuestReply.ProgressEvent(164);
                })
                .OnEventFinish(Zone.WindurstWoods, 163, q => q.Player.SetLocalVar("TihPikehRepeatLine", 1))
                .OnEventFinish(Zone.WindurstWoods, 164, q => q.Player.SetLocalVar("TihPikehRepeatLine", 0));

            // Head for the Optistery on the northern map of Windurst Waters (near Home Point #1).
            // Speak to Tosuka-Porika at (G-8).
            b.State(current: true, status: 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(QuestReply.ProgressEvent(160))
                .OnEventFinish(Zone.WindurstWaters, 160, q => q.Player.SetMissionStatus(AreaId, 2));

            // Speak to Nanaa Mihgo in Windurst Woods (neat Home Point #1).
            b.State(current: true, status: 2)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo)
                .OnTrigger(QuestReply.ProgressEvent(165, 0, KeyItem.LapisCoral, KeyItem.LapisMonocle))
                .OnEventFinish(Zone.WindurstWoods, 165, q =>
                {
                    // Listening sends 1. Refusing sends 2 and grants nothing.
                    if (q.Option != 1)
                    {
                        return;
                    }

                    q.Player.GiveKeyItem(KeyItem.LapisMonocle);
                    q.Mission.SetVar("Rock", q.Player.RandomInt(0, 7));
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(QuestReply.Event(161));

            // Head to Maze of Shakhrami.
            b.State(current: true, status: 3)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(QuestReply.Event(167))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(QuestReply.Event(168))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo)
                .OnTrigger(QuestReply.Event(166, 0, KeyItem.LapisCoral, KeyItem.LapisMonocle));

            // Examine Fossil Rocks until you get LAPIS_CORAL.
            b.State(current: true, when: q => q.Mission.Status == 3 || q.Mission.Status == 4)
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.FossilRock).OnTrigger(ExamineRock);

            // Return to Nanaa Mihgo.
            b.State(current: true, status: 4)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.ProgressEvent(169))
                .OnEventFinish(Zone.WindurstWoods, 169, q =>
                {
                    q.Player.DelKeyItem(KeyItem.LapisCoral);
                    q.Player.DelKeyItem(KeyItem.LapisMonocle);
                    q.Mission.SetVar("Rock", 0);
                    q.Player.GiveKeyItem(KeyItem.HideoutKey);
                    q.Player.SetMissionStatus(AreaId, 5);
                });

            // Head to Inner Horutoto Ruins entering from the tower at (J-7) in East Sarutabaruta.
            b.State(current: true, status: 5)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(QuestReply.Event(171))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(QuestReply.Event(172))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.Event(170))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5ca)
                .OnTrigger(QuestReply.ProgressCutscene(46))
                .OnEventFinish(Zone.InnerHorutotoRuins, 46, q =>
                {
                    q.Player.DelKeyItem(KeyItem.HideoutKey);
                    q.Player.SetMissionStatus(AreaId, 6);
                });

            // Go to the House of the Hero in Windurst Walls (G-3) (Home Point #1).
            b.State(current: true, status: 6)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(QuestReply.Event(174))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(QuestReply.Event(175))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.Event(173))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(q =>
                {
                    q.Text.MessageText(Text.WindurstWalls.DOOR_UNLOCKED, false, 6);

                    return QuestReply.ProgressEvent(337);
                })
                .OnEventFinish(Zone.WindurstWalls, 337, q => q.Player.SetMissionStatus(AreaId, 7));

            // Head back to Tosuka-Porika to complete the mission.
            b.State(current: true, status: 7)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(QuestReply.ProgressEvent(168))
                .OnEventFinish(Zone.WindurstWaters, 168, q => q.Mission.Complete());

            // All of the optional post-mission dialogue
            b.State(current: null, when: q =>
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.ATestingTime))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(74))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(81))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(80))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(329))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(88))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(94))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.AoraUora).OnTrigger(QuestReply.Event(175))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(115))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).OnTrigger(QuestReply.Event(173))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KotanMoritan).OnTrigger(QuestReply.Event(174))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(116))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(114))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(QuestReply.Event(169))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(118))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(117))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(119));
        }
    }
}
