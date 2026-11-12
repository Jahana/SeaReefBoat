// The Jester Who'd be King (Windurst 8-2, Log ID 2, Mission ID 21), a line-for-line port of
// scripts/missions/windurst/8_2_The_Jester_Whod_be_King.lua (quest rework C3 N6). Rakoh Buuma !pos 106 -5 -23 241;
// Mokyokyo !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Apururu !pos
// -11 -2 13 241; Cermet Door(Ruk) !pos -183 0 190 204; Sedal-Godjal !pos 185 -3 -116 149; Tosuka-Porika !pos -26 -6
// 103 238; Kupipi !pos 2 0.1 30 242; Shantotto !pos 122 -2 112 239; _5e5 (Cr. Wall) !pos -423 0 619.75 194; _5cb
// (Gate. Drk) !pos -228 0 99 192. Reward: 80000 gil and rank 9. Section 1 (no Windurst mission current, a
// Windurstian): the gate guards' finishes (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods
// 114) with option 21 (handleAcceptMission) begin the mission and say the zone's YOU_ACCEPT_THE_MISSION. Section 2
// (current): Apururu plays 588 (0, the Manustery Ring) at status 0, 589 at 1, 601 (0, the Orastery Ring) at 2
// (progress) and 3 (event), 590 at 6, 591 at 7, 592 at 8, 593 at 9 and 609 (0, the Glove of Perpetual Twilight) at
// 10; 588's finish sets status 1 and gives the Manustery Ring, 590's sets 7, 592's takes the glove and sets 9, 601's
// sets 3, 609's completes the mission and then sets Option 1. Rakoh Buuma, Janshura-Rashura, Zokima-Rokima and
// Mokyokyo at status 0 play 584, 484, 376 and 764 (events). At status 1 the three rings: Davoi's Sedal-Godjal without
// the Aurastery Ring plays 122 (0, the ring; progress), Fei'Yin's Cermet Door _no4 without the Rhinostery Ring 22 (0,
// the ring; progressCutscene), and Tosuka-Porika 802 (0, the Optistery Ring; event) with it, else 801 (progress); each
// finish gives its ring (npcUtil.giveKeyItem) and, with the other two held, sets status 2. Kupipi plays 326 (0, the
// Orastery Ring) at 3 (progress), whose finish sets 4, and 327 at 4 (event). The Outer Horutoto Ruins' _5e5 at status 4
// with neither queen (JESTER_WHOD_BE_KING_OFFSET + 0 and + 1) spawned spawns the two and says GUARDIAN_BLOCKING_WAY,
// and at 5 plays 71, whose finish gives the Orastery Ring and sets 6; each queen's death at status 4 with the other
// queen dead sets status 5. Shantotto at 7 plays 397 (0, 0, 0, the glove), whose finish gives the glove, takes the
// five rings and sets 8. The Inner Horutoto Ruins' _5cb at 9 plays 75 (progressCutscene), whose finish takes the Book
// of the Gods and sets 10. Section 3 (completed, Option 1): Shantotto plays 399 (0, 0, 0, the glove; progress), whose
// finish clears Option; Apururu 605 (event). Kept as the Lua has it: section 1 compares the nation with mission.areaId
// (the log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed check (it reads only the current
// mission and the nation); the queens' spawns take neither claim nor enmity; Queen of Coins' death reads
// JESTER_WHOD_BE_KING_OFFSET + 0, which is Queen of Swords (17572201), and Queen of Swords' death reads + 1, Queen of
// Coins (17572202): each reads the other queen, and only isDead.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheJesterWhodBeKing : MissionScript
    {
        public TheJesterWhodBeKing() : base(XI.Quests.MissionLog.Windurst, "THE_JESTER_WHOD_BE_KING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/8_2_The_Jester_Whod_be_King";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.TheJesterWhodBeKing;

        // outerHorutotoID.mob.JESTER_WHOD_BE_KING_OFFSET
        const string Jester = Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 21)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 80000, Rank = 9 });

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
                .Npc(Zone.Davoi, Npc.Davoi.SedalGodjal).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        !q.Player.HasKeyItem(KeyItem.AurasteryRing))
                    {
                        return QuestReply.ProgressEvent(122, 0, KeyItem.AurasteryRing);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Davoi, 122, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AurasteryRing);

                    if (q.Player.HasKeyItem(KeyItem.RhinosteryRing) &&
                        q.Player.HasKeyItem(KeyItem.OptisteryRing))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.Feiyin, Npc.Feiyin.no4).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        !q.Player.HasKeyItem(KeyItem.RhinosteryRing))
                    {
                        return QuestReply.ProgressCutscene(22, 0, KeyItem.RhinosteryRing);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Feiyin, 22, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RhinosteryRing);

                    if (q.Player.HasKeyItem(KeyItem.AurasteryRing) &&
                        q.Player.HasKeyItem(KeyItem.OptisteryRing))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(326, 0, KeyItem.OrasteryRing);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(327, 0, KeyItem.OrasteryRing);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 326, q => q.Player.SetMissionStatus(AreaId, 4))
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5cb).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 9)
                    {
                        return QuestReply.ProgressCutscene(75);
                    }

                    return null;
                })
                .OnEventFinish(Zone.InnerHorutotoRuins, 75, q =>
                {
                    q.Player.DelKeyItem(KeyItem.BookOfTheGods);
                    q.Player.SetMissionStatus(AreaId, 10);
                })
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins._5e5).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 4 &&
                        !q.Player.MobIsSpawned(Jester, 0) &&
                        !q.Player.MobIsSpawned(Jester, 1))
                    {
                        q.Player.SpawnMob(Jester, 0);
                        q.Player.SpawnMob(Jester, 1);

                        return QuestReply.MessageSpecial(Text.OuterHorutotoRuins.GUARDIAN_BLOCKING_WAY);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(71);
                    }

                    return null;
                })
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins.QueenofCoins).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4 &&
                        q.Player.MobIsDead(Jester))
                    {
                        q.Player.SetMissionStatus(AreaId, 5);
                    }
                })
                .Npc(Zone.OuterHorutotoRuins, Npc.OuterHorutotoRuins.QueenofSwords).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4 &&
                        q.Player.MobIsDead(Jester, 1))
                    {
                        q.Player.SetMissionStatus(AreaId, 5);
                    }
                })
                .OnEventFinish(Zone.OuterHorutotoRuins, 71, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.OrasteryRing);
                    q.Player.SetMissionStatus(AreaId, 6);
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(484);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 7)
                    {
                        return QuestReply.ProgressEvent(397, 0, 0, 0, KeyItem.GloveOfPerpetualTwilight);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(376);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 397, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.GloveOfPerpetualTwilight);
                    q.Player.DelKeyItem(KeyItem.ManusteryRing);
                    q.Player.DelKeyItem(KeyItem.OptisteryRing);
                    q.Player.DelKeyItem(KeyItem.AurasteryRing);
                    q.Player.DelKeyItem(KeyItem.RhinosteryRing);
                    q.Player.DelKeyItem(KeyItem.OrasteryRing);
                    q.Player.SetMissionStatus(AreaId, 8);
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(764);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        if (q.Player.HasKeyItem(KeyItem.OptisteryRing))
                        {
                            return QuestReply.Event(802, 0, KeyItem.OptisteryRing);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(801, 0, KeyItem.OptisteryRing);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 801, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.OptisteryRing);
                    if (q.Player.HasKeyItem(KeyItem.RhinosteryRing) &&
                        q.Player.HasKeyItem(KeyItem.AurasteryRing))
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(588, 0, KeyItem.ManusteryRing);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.Event(589);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(601, 0, KeyItem.OrasteryRing);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(601, 0, KeyItem.OrasteryRing);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(590);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.Event(591);
                    }
                    else if (missionStatus == 8)
                    {
                        return QuestReply.ProgressEvent(592);
                    }
                    else if (missionStatus == 9)
                    {
                        return QuestReply.Event(593);
                    }
                    else if (missionStatus == 10)
                    {
                        return QuestReply.ProgressEvent(609, 0, KeyItem.GloveOfPerpetualTwilight);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(584);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 588, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.ManusteryRing);
                })
                .OnEventFinish(Zone.WindurstWoods, 590, q => q.Player.SetMissionStatus(AreaId, 7))
                .OnEventFinish(Zone.WindurstWoods, 592, q =>
                {
                    q.Player.DelKeyItem(KeyItem.GloveOfPerpetualTwilight);
                    q.Player.SetMissionStatus(AreaId, 9);
                })
                .OnEventFinish(Zone.WindurstWoods, 601, q => q.Player.SetMissionStatus(AreaId, 3))
                .OnEventFinish(Zone.WindurstWoods, 609, q =>
                {
                    if (q.Mission.Complete())
                    {
                        // Optional CS with Shantotto
                        q.Mission.SetVar("Option", 1);
                    }
                });

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Mission.Var("Option") == 1)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto)
                .OnTrigger(QuestReply.ProgressEvent(399, 0, 0, 0, KeyItem.GloveOfPerpetualTwilight))
                .OnEventFinish(Zone.WindurstWalls, 399, q => q.Mission.SetVar("Option", 0))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(QuestReply.Event(605));
        }
    }
}
