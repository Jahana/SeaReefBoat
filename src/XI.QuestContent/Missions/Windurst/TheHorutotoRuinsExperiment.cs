// The Horutoto Ruins Experiment (Windurst 1-1, Log ID 2, Mission ID 0), a line-for-line port of
// scripts/missions/windurst/1_1_The_Horutoto_Ruins_Experiment.lua (quest rework C3 N1c). Section 1 (no Windurst
// mission current, a Windurst citizen who has not completed this one): the gate guards of Port Windurst (Janshura-
// Rashura 83), Windurst Walls (Zokima-Rokima 96), Windurst Waters (Mokyokyo 118) and Windurst Woods (Rakoh_Buuma
// 121); option 1 of the finish begins the mission, gives the guard's zone title, sets the status to 1 and says
// YOU_ACCEPT_THE_MISSION. Section 2 (current): the other NPCs of the four cities play their own events. Status 1:
// Hakkuru-Rinkuru plays 90 (status 2). Status 2: Hakkuru-Rinkuru 91, Kuroido-Moido 92 (important once), Sama_Gohjima
// 53, and the gate _5c5 of Inner Horutoto Ruins plays cutscene 42, whose finish sets status 3 and picks the broken
// gizmo (RandomGizmo, 1 to 6). Status 3: each of the six gizmos plays its success cutscene (48, 50, 52, 54, 56, 58)
// when it is the broken one, its failure cutscene (49, 51, ...) the first time it is examined, and
// EXAMINED_RECEPTACLE after; the success finish gives the Cracked Mana Orb, sets status 4 and clears the gizmo vars;
// the failure finish says NOT_BROKEN_ORB. Status 4: Hakkuru-Rinkuru plays 94 with the orb, whose finish completes
// the mission (250 rank points) and takes the orb only when the completion succeeded; Kuroido-Moido 98; Sama_Gohjima
// says SAMA_GOHJIMA_POSTDIALOG at priority 1000. Last section (completed, The Heart of the Matter not completed):
// the post-mission events of the four cities. Port Windurst has two pairs of Hakkuru-Rinkuru and Kuroido-Moido; the
// Lua's !pos -111 -4 101 is the second pair. Kept as the Lua has it: Mokyokyo plays 123 (the official server's 109
// is thought to be an error there).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheHorutotoRuinsExperiment : MissionScript
    {
        public TheHorutotoRuinsExperiment() : base(XI.Quests.MissionLog.Windurst, "THE_HORUTOTO_RUINS_EXPERIMENT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/1_1_The_Horutoto_Ruins_Experiment";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.TheHorutotoRuinsExperiment;

        // handleAcceptMission, for the zone it finishes in: a title and a message of its own
        static System.Action<IQuestContext> HandleAcceptMission(int title, ZoneText accepted)
        {
            return q =>
            {
                if (q.Option == 1)
                {
                    q.Mission.Begin();
                    q.Player.AddTitle(title);
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Text.Special(accepted);
                }
            };
        }

        // examineGizmo
        static QuestReply ExamineGizmo(IQuestContext q, int gizmoIndex, int successCs, int failCs)
        {
            if (q.Mission.Var("RandomGizmo") == gizmoIndex)
            {
                return QuestReply.ProgressCutscene(successCs);
            }
            else if (!q.Mission.IsVarBitsSet("GizmoExamined", gizmoIndex))
            {
                q.Mission.SetVarBit("GizmoExamined", gizmoIndex);
                return QuestReply.ProgressCutscene(failCs);
            }

            return QuestReply.MessageSpecial(Text.InnerHorutotoRuins.EXAMINED_RECEPTACLE);
        }

        // gizmoSuccess
        static void GizmoSuccess(IQuestContext q)
        {
            q.Player.GiveKeyItem(KeyItem.CrackedManaOrb);
            q.Player.SetMissionStatus(AreaId, 4);
            q.Mission.SetVar("GizmoExamined", 0);
            q.Mission.SetVar("RandomGizmo", 0);
        }

        // gizmoFailure
        static void GizmoFailure(IQuestContext q)
        {
            q.Text.Special(Text.InnerHorutotoRuins.NOT_BROKEN_ORB);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 250 });

            // Player is offered mission from a gate guard
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.ProgressEvent(83))
                .OnEventFinish(Zone.PortWindurst, 83,
                    HandleAcceptMission(Title.NewBestOfTheWestRecruit, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.ProgressEvent(96))
                .OnEventFinish(Zone.WindurstWalls, 96,
                    HandleAcceptMission(Title.HeavensTowerGatehouseRecruit, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.ProgressEvent(118))
                .OnEventFinish(Zone.WindurstWaters, 118,
                    HandleAcceptMission(Title.FreshNorthWindsRecruit, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.ProgressEvent(121))
                .OnEventFinish(Zone.WindurstWoods, 121,
                    HandleAcceptMission(Title.NewBuumasBoomersRecruit, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            // Mission has been accepted, these NPCs dialogue does not change during mission
            b.State(current: true)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(89))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(86))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(88))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(87))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(100))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(101))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(99))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(121))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(123))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(122))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(124))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(124))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(127))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(125))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(126));

            // Go to the Orastery in Port Windurst and talk to Hakkuru-Rinkuru for a cutscene
            b.State(current: true, status: 1)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).OnTrigger(QuestReply.ProgressEvent(90))
                .OnEventFinish(Zone.PortWindurst, 90, q => q.Player.SetMissionStatus(AreaId, 2));

            // Head to Inner Horutoto Ruins and examine Gate: Magic Gizmo
            b.State(current: true, status: 2)
                .Npc(Zone.EastSarutabaruta, Npc.EastSarutabaruta.SamaGohjima).OnTrigger(QuestReply.Event(53))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).OnTrigger(QuestReply.Event(91))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).OnTrigger(QuestReply.Event(92).ImportantOnce())
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5c5).OnTrigger(QuestReply.ProgressCutscene(42))
                .OnEventFinish(Zone.InnerHorutotoRuins, 42, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Mission.SetVar("RandomGizmo", q.Player.RandomInt(1, 6));
                });

            // Find the broken Gizmo
            b.State(current: true, status: 3)
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5cp)
                .OnTrigger(q => ExamineGizmo(q, 1, 48, 49))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5cq)
                .OnTrigger(q => ExamineGizmo(q, 2, 50, 51))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5cr)
                .OnTrigger(q => ExamineGizmo(q, 3, 52, 53))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5cs)
                .OnTrigger(q => ExamineGizmo(q, 4, 54, 55))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5ct)
                .OnTrigger(q => ExamineGizmo(q, 5, 56, 57))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5cu)
                .OnTrigger(q => ExamineGizmo(q, 6, 58, 59))
                .OnEventFinish(Zone.InnerHorutotoRuins, 48, GizmoSuccess)
                .OnEventFinish(Zone.InnerHorutotoRuins, 49, GizmoFailure)
                .OnEventFinish(Zone.InnerHorutotoRuins, 50, GizmoSuccess)
                .OnEventFinish(Zone.InnerHorutotoRuins, 51, GizmoFailure)
                .OnEventFinish(Zone.InnerHorutotoRuins, 52, GizmoSuccess)
                .OnEventFinish(Zone.InnerHorutotoRuins, 53, GizmoFailure)
                .OnEventFinish(Zone.InnerHorutotoRuins, 54, GizmoSuccess)
                .OnEventFinish(Zone.InnerHorutotoRuins, 55, GizmoFailure)
                .OnEventFinish(Zone.InnerHorutotoRuins, 56, GizmoSuccess)
                .OnEventFinish(Zone.InnerHorutotoRuins, 57, GizmoFailure)
                .OnEventFinish(Zone.InnerHorutotoRuins, 58, GizmoSuccess)
                .OnEventFinish(Zone.InnerHorutotoRuins, 59, GizmoFailure);

            // Take the Cracked Mana Orb back to Hakkuru-Rinkuru in the Orastery to complete the mission
            b.State(current: true, status: 4)
                .Npc(Zone.EastSarutabaruta, Npc.EastSarutabaruta.SamaGohjima)
                .OnTrigger(QuestReply.MessageText(Text.EastSarutabaruta.SAMA_GOHJIMA_POSTDIALOG).SetPriority(1000))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru)
                .OnTrigger(QuestReply.ProgressEvent(94, 0, KeyItem.CrackedManaOrb))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido).OnTrigger(QuestReply.Event(98))
                .OnEventFinish(Zone.PortWindurst, 94, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.CrackedManaOrb);
                    }
                });

            // All of the optional, very temporary post-mission dialogue
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.TheHeartOfTheMatter))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru)
                .OnTrigger(QuestReply.Event(96).ImportantOnce())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido)
                .OnTrigger(QuestReply.Event(100).ImportantOnce())
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(102))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(101))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(103))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(105))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(104))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(128))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(127))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(129))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(115))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(130))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(131));
        }
    }
}
