// The Heart of the Matter (Windurst 1-2, Log ID 2, Mission ID 1), a line-for-line port of
// scripts/missions/windurst/1_2_The_Heart_of_the_Matter.lua (quest rework C3 N1c). Section 1 (no Windurst mission
// current, a Windurst citizen who has not completed this one): the finish of the gate guards' events (Port Windurst
// 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) begins the mission with option 1, sets the status to
// 1 and says YOU_ACCEPT_THE_MISSION; the guards' own events are not this mission's. Section 2 (current): the other
// NPCs of the four cities play their own events. Status 1: Apururu in Windurst Woods plays 137, whose finish gives the
// six Dark Mana Orbs and sets status 2. Status 2: Apururu plays 138; Pore-Ohre of East Sarutabaruta plays cutscene 46,
// whose finish gives the Southeastern Star Charm and sets status 3. Status 3 and 4 (Pore-Ohre plays 47): each of the
// six gizmos of Outer Horutoto Ruins plays the cutscene that places its orb (58 to 63) at status 3, or the one that
// collects a glowing orb (46 to 51) at status 4, once each; the placing finish sets the gizmo's bit, counts the orb,
// says the placing message, takes the next Dark Mana Orb and, for the sixth, says the all-placed message; the
// collecting finish gives the next Glowing Mana Orb and, for the sixth, sets status 5 and clears the counters. A
// gizmo already used says its already-placed (status 3) or already-retrieved (status 4) message. Status 3 with six
// orbs placed: the gate _5e9 says STAR_CHARM_DISAPPEARS and plays cutscene 44, whose finish takes the charm, sets
// status 4, says ALL_G_ORBS_ENERGIZED and clears the gizmo vars. Status 5: the zone-in of East Sarutabaruta plays 48,
// whose finish takes the six glowing orbs and sets status 6; Apururu plays 145, whose finish takes the orbs and
// completes the mission (250 rank points). Status 6: Pore-Ohre and Quh Berhuja say their stolen-orbs text; Apururu
// plays 143, whose finish completes the mission. Last section (completed, The Price of Peace not completed): the
// post-mission events of the four cities.
// KEPT LUA BUG: the Lua's last section is `check = function(player)` with no `currentMission == NONE` test (1-1 and
// 1-3 have one), so while The Price of Peace is not completed it answers for eleven of 1-3's section-2 NPCs
// whatever mission is current; the port keeps that as the Lua has it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheHeartOfTheMatter : MissionScript
    {
        public TheHeartOfTheMatter() : base(XI.Quests.MissionLog.Windurst, "THE_HEART_OF_THE_MATTER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/1_2_The_Heart_of_the_Matter";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.TheHeartOfTheMatter;

        static readonly int[] DarkOrbKI =
        {
            KeyItem.FirstDarkManaOrb, KeyItem.SecondDarkManaOrb, KeyItem.ThirdDarkManaOrb,
            KeyItem.FourthDarkManaOrb, KeyItem.FifthDarkManaOrb, KeyItem.SixthDarkManaOrb,
        };

        static readonly int[] GlowingOrbKI =
        {
            KeyItem.FirstGlowingManaOrb, KeyItem.SecondGlowingManaOrb, KeyItem.ThirdGlowingManaOrb,
            KeyItem.FourthGlowingManaOrb, KeyItem.FifthGlowingManaOrb, KeyItem.SixthGlowingManaOrb,
        };

        // msgBase + offset: the text id of ORB_ALREADY_PLACED plus the offset
        static int MsgBase(IQuestContext q, int offset) =>
            q.Text.Id(Text.OuterHorutotoRuins.ORB_ALREADY_PLACED) + offset;

        // handleAcceptMission
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 1)
            {
                q.Mission.Begin();
                q.Player.SetMissionStatus(AreaId, 1);
                q.Text.Special(accepted);
            }
        }

        // gizmoOnTrigger
        static QuestReply GizmoOnTrigger(IQuestContext q, int gizmoNum, int placeCs, int collectCs)
        {
            // first section handling placing orbs
            if (q.Mission.Status == 3)
            {
                // has orb been placed here?
                if (!q.Mission.IsVarBitsSet("GizmoUsed", gizmoNum))
                {
                    return QuestReply.Cutscene(placeCs);
                }

                q.Text.Special(MsgBase(q, 0));
            }
            // second section collecting orbs
            else if (q.Mission.Status == 4)
            {
                if (!q.Mission.IsVarBitsSet("GizmoEmpty", gizmoNum))
                {
                    return QuestReply.Cutscene(collectCs);
                }

                q.Text.Special(MsgBase(q, 3));
            }

            return null;
        }

        // placeOrb, for the gizmo its cutscene id (58 to 63) names
        static void PlaceOrb(IQuestContext q, int gizmoNum)
        {
            int numberPlaced = q.Mission.Var("OrbsPlaced");
            int ki = DarkOrbKI[numberPlaced];

            q.Mission.SetVarBit("GizmoUsed", gizmoNum);
            q.Mission.SetVar("OrbsPlaced", numberPlaced + 1);
            q.Text.Special(MsgBase(q, 1), 0, 0, ki);
            q.Player.DelKeyItem(ki);

            // final orb placed triggers additional message
            if (q.Mission.Var("OrbsPlaced") == 6)
            {
                q.Text.Special(MsgBase(q, 5));
            }
        }

        // collectOrb, for the gizmo its cutscene id (46 to 51) names
        static void CollectOrb(IQuestContext q, int gizmoNum)
        {
            int numberCollected = q.Mission.Var("OrbsCollected");
            int ki = GlowingOrbKI[numberCollected];

            q.Mission.SetVarBit("GizmoEmpty", gizmoNum);
            q.Mission.SetVar("OrbsCollected", numberCollected + 1);
            q.Player.GiveKeyItem(ki);

            // collected all the orbs
            if (q.Mission.Var("OrbsCollected") == 6)
            {
                q.Text.Special(MsgBase(q, 4));
                q.Player.SetMissionStatus(AreaId, 5);
                q.Mission.SetVar("OrbsCollected", 0);
                q.Mission.SetVar("GizmoEmpty", 0);
            }
        }

        static void DelGlowingOrbs(IQuestContext q)
        {
            for (int i = 0; i < 6; i++)
            {
                q.Player.DelKeyItem(GlowingOrbKI[i]);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 250 });

            // Player is offered mission from a gate guard (the guards' events are those of 1-1's accepting)
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
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(105))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(106))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(108))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(107))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(110))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(108))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(109))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(107))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(132))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(131))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(133))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(134))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(133))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(136))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(134))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(135));

            // Speak with Apururu who gives the six Dark Mana Orbs
            b.State(current: true, status: 1)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(QuestReply.ProgressEvent(137))
                .OnEventFinish(Zone.WindurstWoods, 137, q =>
                {
                    for (int i = 0; i < 6; i++)
                    {
                        q.Player.GiveKeyItem(DarkOrbKI[i]);
                    }

                    q.Player.SetMissionStatus(AreaId, 2);
                });

            // Pore-Ohre gives the Southeastern Star Charm
            b.State(current: true, status: 2)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(QuestReply.ProgressEvent(138))
                .Npc(Zone.EastSarutabaruta, Npc.EastSarutabaruta.PoreOhre).OnTrigger(QuestReply.ProgressCutscene(46))
                .OnEventFinish(Zone.EastSarutabaruta, 46, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SoutheasternStarCharm);
                    q.Player.SetMissionStatus(AreaId, 3);
                });

            // Handle both the placing and later collecting of mana orbs here
            b.State(current: true, when: q => q.Mission.Status == 3 || q.Mission.Status == 4)
                .Npc(Zone.EastSarutabaruta, Npc.EastSarutabaruta.PoreOhre).OnTrigger(QuestReply.Event(47))
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5ee).OnTrigger(q => GizmoOnTrigger(q, 1, 58, 46))
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5ef).OnTrigger(q => GizmoOnTrigger(q, 2, 59, 47))
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5eg).OnTrigger(q => GizmoOnTrigger(q, 3, 60, 48))
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5eh).OnTrigger(q => GizmoOnTrigger(q, 4, 61, 49))
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5ei).OnTrigger(q => GizmoOnTrigger(q, 5, 62, 50))
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5ej).OnTrigger(q => GizmoOnTrigger(q, 6, 63, 51))
                .OnEventFinish(Zone.OuterHorutotoRuins, 58, q => PlaceOrb(q, 1))
                .OnEventFinish(Zone.OuterHorutotoRuins, 46, q => CollectOrb(q, 1))
                .OnEventFinish(Zone.OuterHorutotoRuins, 59, q => PlaceOrb(q, 2))
                .OnEventFinish(Zone.OuterHorutotoRuins, 47, q => CollectOrb(q, 2))
                .OnEventFinish(Zone.OuterHorutotoRuins, 60, q => PlaceOrb(q, 3))
                .OnEventFinish(Zone.OuterHorutotoRuins, 48, q => CollectOrb(q, 3))
                .OnEventFinish(Zone.OuterHorutotoRuins, 61, q => PlaceOrb(q, 4))
                .OnEventFinish(Zone.OuterHorutotoRuins, 49, q => CollectOrb(q, 4))
                .OnEventFinish(Zone.OuterHorutotoRuins, 62, q => PlaceOrb(q, 5))
                .OnEventFinish(Zone.OuterHorutotoRuins, 50, q => CollectOrb(q, 5))
                .OnEventFinish(Zone.OuterHorutotoRuins, 63, q => PlaceOrb(q, 6))
                .OnEventFinish(Zone.OuterHorutotoRuins, 51, q => CollectOrb(q, 6));

            // Once all of the orbs have been placed, examine the gate _5e9
            b.State(current: true, when: q => q.Mission.Status == 3 && q.Mission.Var("OrbsPlaced") == 6)
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5e9).OnTrigger(q =>
                {
                    q.Text.Special(Text.OuterHorutotoRuins.STAR_CHARM_DISAPPEARS, Zone.OuterHorutotoRuins,
                        KeyItem.SoutheasternStarCharm);

                    return QuestReply.Cutscene(44);
                })
                .OnEventFinish(Zone.OuterHorutotoRuins, 44, q =>
                {
                    q.Player.DelKeyItem(KeyItem.SoutheasternStarCharm);
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Text.Special(Text.OuterHorutotoRuins.ALL_G_ORBS_ENERGIZED);
                    q.Mission.SetVar("GizmoUsed", 0);
                    q.Mission.SetVar("OrbsPlaced", 0);
                });

            // Return the orbs to Apururu; a zone-in to East Sarutabaruta loses them to the Cardians
            b.State(current: true, status: 5)
                .OnZoneIn(Zone.EastSarutabaruta, q => QuestReply.Event(48))
                .OnEventFinish(Zone.EastSarutabaruta, 48, q =>
                {
                    DelGlowingOrbs(q);
                    q.Player.SetMissionStatus(AreaId, 6);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(QuestReply.Event(145))
                .OnEventFinish(Zone.WindurstWoods, 145, q =>
                {
                    DelGlowingOrbs(q);
                    q.Mission.Complete();
                });

            // Only occurs if you lost the mana orbs to the Cardians
            b.State(current: true, status: 6)
                .Npc(Zone.EastSarutabaruta, Npc.EastSarutabaruta.PoreOhre)
                .OnTrigger(QuestReply.MessageText(Text.EastSarutabaruta.PORE_OHRE_STOLEN_ORBS))
                .Npc(Zone.EastSarutabaruta, Npc.EastSarutabaruta.QuhBerhuja)
                .OnTrigger(QuestReply.MessageText(Text.EastSarutabaruta.QUH_BERHUJA_STOLEN_ORBS))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(QuestReply.ProgressEvent(143))
                .OnEventFinish(Zone.WindurstWoods, 143, q => q.Mission.Complete());

            // All of the optional post-mission dialogue
            b.State(current: null, when: q =>
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.ThePriceOfPeace))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(74))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(79))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(80))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(105))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(104))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(112))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(113))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(114))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu)
                .OnTrigger(QuestReply.Event(144).ImportantOnce())
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(118))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(117))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(119));
        }
    }
}
