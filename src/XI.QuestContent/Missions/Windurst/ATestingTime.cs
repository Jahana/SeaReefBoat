// A Testing Time (Windurst 2-2, Log ID 2, Mission ID 4), a line-for-line port of
// scripts/missions/windurst/2_2_A_Testing_Time.lua (quest rework C3 N1c-2). Section 1 (no Windurst mission current, a
// Windurst citizen; the Lua does not ask whether 2-2 is done): the finish of the gate guards' events (Port Windurst 78,
// Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 4 begins the mission, sets the status to 1,
// sets AurasteryMissionTaken and says YOU_ACCEPT_THE_MISSION. Section 2 (current): the other NPCs of the four cities
// play their own plain events. Status 1: Moreno-Toeno plays progress event 687 on a repeat (2-2 done), 181 after a
// failed run (the Failed var is 1), else 182; the finish of 181 and 182 with option 2 sets status 2, gives the
// Creature Counter Magic Doll, sets EndTime to a Vana'diel day ahead and clears Failed; the finish of 687 with option
// 2 does the same without clearing Failed and with two days. Status 2: every death of a listed mob of Tahrongi Canyon
// (first run) or of Buburimu Peninsula (a repeat) adds one to KillCount. Moreno-Toeno assesses: after EndTime it plays
// 202; more than an hour before EndTime it plays 183 (204 on a repeat) with the hours left; else by KillCount 35 and
// up 201, 30 and up 200, 19 and up 199 (first run only), otherwise 198 (208 on a repeat; 206 and 209 for 35 and 30).
// The finish of 198, 199, 202 and 208 fails the run (clears EndTime and KillCount, sets Failed and must-zone, takes
// the doll, status 0, then completes the mission again on a repeat or deletes it); 200, 201, 206 and 209 complete the
// mission (300 rank points) and take the doll. Parting sections (no Windurst mission current, Failed 1): Moreno-Toeno
// plays plain event 203 while must-zone holds and 438 after.
// Kept as the Lua has them: the earlier missions' last sections answer before this mission's plain events (the 2-1
// last section shadows the city citizens), and Teacher's Pet's progress events 437 and 438 answer Moreno-Toeno before
// his parting words 203 and 438 of the last sections until Teacher's Pet is done; a Moreno finish of 687 leaves Failed
// as it was; the kill counters have no cap and count a death in any state of status 2. The Lua also binds Goblin
// Archaeologist of Tahrongi Canyon; its spawn in mobs.yaml has no placement, so it never spawns (nothing in the
// scripts spawns it either) and the registration refuses a handler for it: that handler is left out.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class ATestingTime : MissionScript
    {
        public ATestingTime() : base(XI.Quests.MissionLog.Windurst, "A_TESTING_TIME") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_2_A_Testing_Time";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.ATestingTime;

        // xi.vanaTime.HOUR and xi.vanaTime.DAY, in earth seconds
        const int Hour = 144;
        const int Day = 3456;

        // handleAcceptMission
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 4)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 1);
                q.Player.SetCharVar("AurasteryMissionTaken", 1);
                q.Text.Special(accepted);
            }
        }

        // killCounter
        static void KillCounter(IQuestContext q)
        {
            q.Mission.SetVar("KillCount", q.Mission.Var("KillCount") + 1);
        }

        // assessment
        static QuestReply Assessment(IQuestContext q)
        {
            int currentTime = q.Player.VanadielTime;
            int endTime = q.Mission.Var("EndTime");
            int killCount = q.Mission.Var("KillCount");
            bool completed = q.Player.HasCompletedMission(AreaId, MissionId);

            // player took too long to speak under requirements in time, so they fail mission
            if (currentTime > endTime)
            {
                return QuestReply.ProgressEvent(202);
            }

            // player hasn't waited long enough to be assessed
            if (currentTime < endTime - Hour)
            {
                int hoursRemaining = (endTime - currentTime + Hour - 1) / Hour;

                if (completed)
                {
                    return QuestReply.ProgressEvent(204, 0, 0, 0, 0, 0, q.Player.VanadielHour, hoursRemaining, 0);
                }

                return QuestReply.ProgressEvent(183, 0, q.Player.VanadielHour, hoursRemaining);
            }

            int csid = completed ? 208 : 198;

            if (killCount >= 35)
            {
                csid = completed ? 206 : 201;
            }
            else if (killCount >= 30)
            {
                csid = completed ? 209 : 200;
            }
            else if (!completed && killCount >= 19)
            {
                csid = 199;
            }

            return QuestReply.ProgressEvent(csid, 0, q.Player.VanadielHour, 1, killCount);
        }

        // failMission
        static void FailMission(IQuestContext q)
        {
            bool completed = q.Player.HasCompletedMission(AreaId, MissionId);

            q.Mission.SetVar("EndTime", 0);
            q.Mission.SetVar("KillCount", 0);
            q.Mission.SetVar("Failed", 1);
            q.Mission.SetMustZone();
            q.Player.DelKeyItem(KeyItem.CreatureCounterMagicDoll);
            q.Player.SetMissionStatus(AreaId, 0);

            // delMission also wipes the completion bit. A repeat run is dropped with completeMission.
            if (completed)
            {
                q.Mission.CompleteRaw();
            }
            else
            {
                q.Player.DelMission(AreaId, MissionId);
            }
        }

        // acceptAssessment
        static void AcceptAssessment(IQuestContext q)
        {
            if (q.Option != 2)
            {
                return;
            }

            q.Player.SetMissionStatus(AreaId, 2);
            q.Player.GiveKeyItem(KeyItem.CreatureCounterMagicDoll);
            q.Mission.SetVar("EndTime", q.Player.VanadielTime + Day);

            // The retry briefing has played.
            q.Mission.SetVar("Failed", 0);
        }

        // clearMission
        static void ClearMission(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.DelKeyItem(KeyItem.CreatureCounterMagicDoll);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 300 });

            // Player is offered mission from a gate guard. Note that retail will have the
            // player be presented with the mission selection menu, choose, accept, and then
            // start the events below, where you accept it again.
            // Choosing to keep the existing flow we had already.
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId)
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
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(133))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(134))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(136))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(135))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(130))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(128))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(129))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(127))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Akkeke).OnTrigger(QuestReply.Event(190))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.ChomoroKyotoro).OnTrigger(QuestReply.Event(195))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(178))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FoiMui).OnTrigger(QuestReply.Event(193))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe).OnTrigger(QuestReply.Event(186))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kirarara).OnTrigger(QuestReply.Event(188))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KokoLihzeh).OnTrigger(QuestReply.Event(191))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MashuuAjuu).OnTrigger(QuestReply.Event(192))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mimomo).OnTrigger(QuestReply.Event(196))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(177))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PakuNakku).OnTrigger(QuestReply.Event(194))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(179))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PechiruMashiru).OnTrigger(QuestReply.Event(185))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Rukuku).OnTrigger(QuestReply.Event(189))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Tauwawa).OnTrigger(QuestReply.Event(187))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(180))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(474))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(473))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(475))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(476));

            // Talk to Moreno-Toeno (inside the Aurastery) at Windurst Waters, North (L-6)
            b.State(current: true, status: 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(q =>
                {
                    // Repeat run.
                    if (q.Player.HasCompletedMission(AreaId, MissionId))
                    {
                        return QuestReply.ProgressEvent(687);
                    }

                    // Retry after a failed attempt.
                    if (q.Mission.Var("Failed") == 1)
                    {
                        return QuestReply.ProgressEvent(181);
                    }

                    return QuestReply.ProgressEvent(182);
                })
                .OnEventFinish(Zone.WindurstWaters, 181, AcceptAssessment)
                .OnEventFinish(Zone.WindurstWaters, 182, AcceptAssessment)
                .OnEventFinish(Zone.WindurstWaters, 687, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                        q.Player.GiveKeyItem(KeyItem.CreatureCounterMagicDoll);
                        q.Mission.SetVar("EndTime", q.Player.VanadielTime + 2 * Day);
                    }
                });

            // Kill Tahrongi Canyon mobs if first time doing mission
            b.State(current: true, when: q =>
                    q.Mission.Status == 2 && !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.AirElemental).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Akbaba).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Barghest).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.CanyonCrawler).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.CanyonRarab).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.EarthElemental).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Ghost).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.GoblinAmbusher).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.GoblinButcher).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.GoblinDigger).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.GoblinThug).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.GoblinTinkerer).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.GoblinWeaver).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Grenade).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Habrok).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.HerbageHunter).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.KillerBee).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Poltergeist).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.Pygmaioi).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.SerpopardIshtar).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.SkeletonSorcerer).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.SkeletonWarrior).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.StrollingSapling).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.WildDhalmel).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YagudoAcolyte).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YagudoInitiate).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YagudoMendicant).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YagudoPersecutor).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YagudoPiper).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YagudoScribe).OnMobDeath(KillCounter)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.YaraMaYhaWho).OnMobDeath(KillCounter);

            // Kill Buburimu Peninsula mobs if repeating mission
            b.State(current: true, when: q =>
                    q.Mission.Status == 2 && q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.AirElemental).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Backoo).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Bogy).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Buburimboo).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.BullDhalmel).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.CarnivorousCrawler).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Ghoul).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinAmbusher).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinBountyHunter).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinButcher).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinDigger).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinGambler).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinLeecher).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinMugger).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.GoblinTinkerer).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Helldiver).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.MightyRarab).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.PoisonLeech).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.ShoalPugil).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Snipper).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Sylvestre).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.WakeWarderWanda).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.WaterElemental).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.WillotheWisp).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Zombie).OnMobDeath(KillCounter)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.Zu).OnMobDeath(KillCounter);

            // Speaking to Moreno-Toeno afterwards to be assessed.
            b.State(current: true, status: 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(Assessment)
                .OnEventFinish(Zone.WindurstWaters, 198, FailMission)
                .OnEventFinish(Zone.WindurstWaters, 199, FailMission)
                .OnEventFinish(Zone.WindurstWaters, 202, FailMission)
                .OnEventFinish(Zone.WindurstWaters, 208, FailMission)
                .OnEventFinish(Zone.WindurstWaters, 200, ClearMission)
                .OnEventFinish(Zone.WindurstWaters, 201, ClearMission)
                .OnEventFinish(Zone.WindurstWaters, 206, ClearMission)
                .OnEventFinish(Zone.WindurstWaters, 209, ClearMission);

            // Parting words after a failed assessment. They stop once the player zones.
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Mission.Var("Failed") == 1 &&
                    q.Mission.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(QuestReply.Event(203));

            // His usual line returns once the player has zoned.
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Mission.Var("Failed") == 1 &&
                    !q.Mission.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(QuestReply.Event(438));
        }
    }
}
