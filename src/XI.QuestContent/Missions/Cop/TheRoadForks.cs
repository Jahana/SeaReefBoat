// The Road Forks (Promathia 3-3, Log ID 6, Mission ID 325), a line-for-line port of
// scripts/missions/cop/3_3_The_Road_Forks.lua (quest rework C3). It uses the extended mission status: the 4-bit fields
// of the CoP status word, position 0 the San d'Oria path (xi.mission.status.COP.SANDORIA) and position 1 the Windurst
// path (WINDURST). San d'Oria path (section 1, the mission current and field 0 at most 14): Northern San d'Oria's
// zone-in (0) plays 14 (1), Arnau (1) 51 (2), Chasalvige (2) 38 (5) and event 6 at 5; Carpenter's Landing's Guilloud
// (5) spawns the Overgrown Ivy (claimed for the char) once, then BEGONE_TRESPASSER while it lives; the Ivy's death (5)
// sets the local ivyDefeated, and Guilloud plays cutscene 0 (the nation flag: San d'Orian 1) whose finish sets 9;
// Southern San d'Oria's Hinaree (9) plays 23 (14) and event 24 at 14. Windurst path (section 2, field 1 at most 14):
// Windurst Waters' zone-in (0) 871 (1), Ohbiru-Dohbiru (1) 872 (2) and 877, Yoran-Oran (Windurst Walls) 469 at 2 (3),
// 474 at 3, 470 at 6 (8, the mirror taken), 471 at 8 with the Mimeo Feather (the three Feathers taken, 9) else 476, 477
// at 9, 472 at 12 (14) and 478 at 14, Kyume-Romeh (3) 873 (5) and 878, Honoi-Gomoi (5) 874 (the Cracked Mimeo Mirror,
// 6) and 879, Port Windurst's Yujuju (9) 592 (11) and 593, Tosuka-Porika (11) 875 (12) and 881. Attohwa Chasm: at 8
// Loose Sand (within 0.5 yalms, else MUST_MOVE_CLOSER) spawns and claims the Lioumere once, its death (status 0) sets
// the mission Status 1, and Loose Sand then gives the Mimeo Jewel with a 30 minute timer (jewelTimer: the first message
// is 7340, MIMEO_STONE_PICKUP "shining brilliantly", then one each 6 minutes, the last at expiry); the Cradle of
// Rebirth (with the Jewel) plays 2, whose finish takes the Jewel and gives the three Feathers; the zone-out with the
// Jewel takes it. Section 3 (both fields 14): Metalworks Cid 847 completes the mission. Tending Aged Wounds is next; no
// title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheRoadForks : MissionScript
    {
        public TheRoadForks() : base(XI.Quests.MissionLog.CoP, "THE_ROAD_FORKS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/3_3_The_Road_Forks";

        // xi.mission.status.COP positions: SANDORIA 0, WINDURST 1 (there are no C# constants for the nibbles)
        private const int Sandoria = 0;
        private const int Windurst = 1;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TendingAgedWounds,
            });

            // A 30 minute timer is started once obtaining the Mimeo Jewel for Windurst Path. A looping timer
            // monitors it, supplies periodic messages and checks for breaking conditions
            QuestCallback jewelTimer = null;
            jewelTimer = b.Callback("jewelTimer", q =>
            {
                if (!q.Player.HasKeyItem(KeyItem.MimeoJewel)) return;

                int secondsRemaining = q.Mission.LocalVar("Timer") - q.Player.SystemTime;
                if (secondsRemaining <= 0)
                {
                    // There are some conditions that can instantly break the jewel. Short-circuit out here and
                    // display the final message should that occur
                    q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_JEWEL_OFFSET) + 4, KeyItem.MimeoJewel);
                    q.Player.DelKeyItem(KeyItem.MimeoJewel);
                }
                else
                {
                    int messageOffset = q.Mission.LocalVar("Option");
                    int nextMessageTime = 30 - messageOffset * 6;
                    double minutesRemaining = secondsRemaining / 60.0;

                    if (minutesRemaining <= nextMessageTime)
                    {
                        q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_JEWEL_OFFSET) + messageOffset - 1,
                                       KeyItem.MimeoJewel);
                        q.Mission.SetLocalVar("Option", messageOffset + 1);
                    }

                    q.Player.Timer(3 * 1000, jewelTimer);
                }
            });

            // San d'Oria Path
            b.State(current: true, when: q => q.Mission.StatusAt(Sandoria) <= 14)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Arnau).OnTrigger(q =>
                    q.Mission.StatusAt(Sandoria) == 1 ? QuestReply.ProgressEvent(51) : null)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Chasalvige).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Sandoria);

                    if (missionStatus == 2) return QuestReply.ProgressEvent(38);
                    if (missionStatus == 5) return QuestReply.Event(6).ReplaceDefault();
                    return null;
                })
                .OnZoneIn(Zone.NorthernSanDoria, q => q.Mission.StatusAt(Sandoria) == 0 ? QuestReply.Event(14) : null)
                .OnEventFinish(Zone.NorthernSanDoria, 14, q => q.Mission.SetStatusAt(1, Sandoria))
                .OnEventFinish(Zone.NorthernSanDoria, 38, q => q.Mission.SetStatusAt(5, Sandoria))
                .OnEventFinish(Zone.NorthernSanDoria, 51, q => q.Mission.SetStatusAt(2, Sandoria))
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.Guilloud).OnTrigger(q =>
                {
                    if (q.Mission.StatusAt(Sandoria) != 5) return null;

                    if (q.Mission.LocalVar("ivyDefeated") == 1)
                    {
                        int isSanDorian = q.Player.Nation == 0 ? 1 : 0; // xi.nation.SANDORIA
                        return QuestReply.ProgressCutscene(0, isSanDorian);
                    }

                    if (!q.Player.MobIsSpawned("OVERGROWN_IVY"))
                    {
                        q.Text.MessageText(Text.CarpentersLanding.YOU_WISH_TO_KNOW_MISTALLE, true, 0);
                        q.Text.MessageText(Text.CarpentersLanding.SQUASH_ANOTHER_WORM, true, 0);
                        q.Player.SpawnMob("OVERGROWN_IVY", 0, true);
                        return QuestReply.NoAction();
                    }

                    return QuestReply.MessageName(Text.CarpentersLanding.BEGONE_TRESPASSER).SetPriority(1000);
                })
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.OvergrownIvy).OnMobDeath(q =>
                {
                    if (q.Mission.StatusAt(Sandoria) == 5) q.Mission.SetLocalVar("ivyDefeated", 1);
                })
                .OnEventFinish(Zone.CarpentersLanding, 0, q => q.Mission.SetStatusAt(9, Sandoria))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hinaree).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Sandoria);

                    if (missionStatus == 9) return QuestReply.ProgressEvent(23);
                    if (missionStatus == 14) return QuestReply.Event(24).ReplaceDefault();
                    return null;
                })
                // End of San d'Oria Path
                .OnEventFinish(Zone.SouthernSanDoria, 23, q => q.Mission.SetStatusAt(14, Sandoria));

            // Windurst Path
            b.State(current: true, when: q => q.Mission.StatusAt(Windurst) <= 14)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HonoiGomoi).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Windurst);

                    if (missionStatus == 5) return QuestReply.ProgressEvent(874);
                    if (missionStatus == 6)
                        return QuestReply.Event(879, 0, KeyItem.CrackedMimeoMirror).ImportantEvent();
                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KyumeRomeh).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Windurst);

                    if (missionStatus == 3) return QuestReply.ProgressEvent(873);
                    if (missionStatus == 5) return QuestReply.Event(878).ImportantEvent();
                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Windurst);

                    if (missionStatus == 1) return QuestReply.ProgressEvent(872);
                    if (missionStatus == 2) return QuestReply.Event(877).ImportantEvent();
                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Windurst);

                    if (missionStatus == 11) return QuestReply.ProgressEvent(875);
                    if (missionStatus == 12) return QuestReply.Event(881).ImportantEvent();
                    return null;
                })
                .OnZoneIn(Zone.WindurstWaters, q => q.Mission.StatusAt(Windurst) == 0 ? QuestReply.Event(871) : null)
                .OnEventFinish(Zone.WindurstWaters, 871, q => q.Mission.SetStatusAt(1, Windurst))
                .OnEventFinish(Zone.WindurstWaters, 872, q => q.Mission.SetStatusAt(2, Windurst))
                .OnEventFinish(Zone.WindurstWaters, 873, q => q.Mission.SetStatusAt(5, Windurst))
                .OnEventFinish(Zone.WindurstWaters, 874, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CrackedMimeoMirror);
                    q.Mission.SetStatusAt(6, Windurst);
                })
                .OnEventFinish(Zone.WindurstWaters, 875, q => q.Mission.SetStatusAt(12, Windurst))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(q =>
                {
                    switch (q.Mission.StatusAt(Windurst))
                    {
                        case 2: return QuestReply.ProgressEvent(469);
                        case 3: return QuestReply.Event(474).ImportantEvent();
                        case 6:
                            return QuestReply.ProgressEvent(470, 0, KeyItem.MimeoFeather,
                                KeyItem.CrackedMimeoMirror, KeyItem.MimeoJewel);
                        case 8:
                            if (q.Player.HasKeyItem(KeyItem.MimeoFeather)) return QuestReply.ProgressEvent(471);
                            return QuestReply.Event(476, 0, KeyItem.MimeoFeather, 0, KeyItem.MimeoJewel)
                                .ImportantEvent();
                        case 9: return QuestReply.Event(477).ImportantEvent();
                        case 12: return QuestReply.ProgressEvent(472);
                        case 14: return QuestReply.Event(478).ImportantEvent();
                        default: return null;
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 469, q => q.Mission.SetStatusAt(3, Windurst))
                .OnEventFinish(Zone.WindurstWalls, 470, q =>
                {
                    q.Player.DelKeyItem(KeyItem.CrackedMimeoMirror);
                    q.Mission.SetStatusAt(8, Windurst);
                })
                .OnEventFinish(Zone.WindurstWalls, 471, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MimeoFeather);
                    q.Player.DelKeyItem(KeyItem.SecondMimeoFeather);
                    q.Player.DelKeyItem(KeyItem.ThirdMimeoFeather);

                    q.Mission.SetStatusAt(9, Windurst);
                })
                .OnEventFinish(Zone.WindurstWalls, 472, q => q.Mission.SetStatusAt(14, Windurst))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Yujuju).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Windurst);

                    if (missionStatus == 9) return QuestReply.ProgressEvent(592);
                    if (missionStatus == 11) return QuestReply.Event(593).ImportantEvent();
                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 592, q => q.Mission.SetStatusAt(11, Windurst))
                .Npc(Zone.AttohwaChasm, Npc.AttohwaChasm.CradleofRebirth).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MimeoJewel)) return null;

                    // GetNPCByID(npc:getID() + 1), when there is one
                    var animationNpc = q.Player.NpcById(q.Npc.Id + 1);
                    if (animationNpc != null)
                    {
                        animationNpc.EntityAnimationPacket("krtu");
                    }

                    return QuestReply.ProgressEvent(2);
                })
                .Npc(Zone.AttohwaChasm, Npc.AttohwaChasm.Lioumere).OnMobDeath(q =>
                {
                    if (q.Mission.StatusAt(Windurst) == 8 && q.Mission.Var("Status") == 0)
                    {
                        q.Mission.SetVar("Status", 1);
                    }
                })
                .Npc(Zone.AttohwaChasm, Npc.AttohwaChasm.LooseSand).OnTrigger(q =>
                {
                    if (q.Npc.PlayerDistance >= 0.5)
                    {
                        return QuestReply.MessageSpecial(Text.AttohwaChasm.MUST_MOVE_CLOSER);
                    }

                    if (q.Mission.StatusAt(Windurst) == 8)
                    {
                        int lioumereStatus = q.Mission.Var("Status");

                        if (lioumereStatus == 0 && !q.Player.MobIsSpawned("LIOUMERE"))
                        {
                            q.Player.SpawnMob("LIOUMERE", 0, true);
                            return QuestReply.NoAction();
                        }

                        if (lioumereStatus == 1 && !q.Player.HasKeyItem(KeyItem.MimeoJewel))
                        {
                            q.Player.GiveKeyItem(KeyItem.MimeoJewel);
                            q.Mission.SetLocalVar("Option", 0);
                            q.Mission.SetLocalVar("Timer", q.Player.SystemTime + 30 * 60);
                            jewelTimer.Run(q);
                            return QuestReply.NoAction();
                        }
                    }

                    return null;
                })
                .OnZoneOut(Zone.AttohwaChasm, q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MimeoJewel))
                    {
                        q.Text.Special(q.Text.Id(Text.AttohwaChasm.MIMEO_JEWEL_OFFSET) + 4, KeyItem.MimeoJewel);
                        q.Player.DelKeyItem(KeyItem.MimeoJewel);
                    }
                })
                .OnEventFinish(Zone.AttohwaChasm, 2, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MimeoJewel);

                    q.Player.GiveKeyItem(KeyItem.MimeoFeather);
                    q.Player.GiveKeyItem(KeyItem.SecondMimeoFeather);
                    q.Player.GiveKeyItem(KeyItem.ThirdMimeoFeather);
                });

            // Completion
            b.State(current: true, when: q => q.Mission.StatusAt(Sandoria) == 14 && q.Mission.StatusAt(Windurst) == 14)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.ProgressEvent(847))
                .OnEventFinish(Zone.Metalworks, 847, q => q.Mission.Complete());
        }
    }
}
