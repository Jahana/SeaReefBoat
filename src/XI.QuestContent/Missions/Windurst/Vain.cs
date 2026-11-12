// Vain (Windurst 8-1, Log ID 2, Mission ID 20), a line-for-line port of scripts/missions/windurst/8_1_Vain.lua (quest
// rework C3 N6). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240;
// Zokima-Rokima !pos 0 -16 124 239; Moreno-Toeno !pos 169 -1.25 159 238; Qu'Hau Spring !pos 0 -29 64 122; Sedal-Godjal
// !pos 185 -3 -116 149. Reward: 750 rank points. Section 1 (no Windurst mission current, a Windurstian): the gate
// guards' finishes (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 20
// (handleAcceptMission) begin the mission and say the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): the
// Windurst NPCs' fixed events (Port Windurst: Janshura-Rashura 479, Nine of Clubs 480, Puo Rhen 482, Ten of Clubs 481;
// Windurst Walls: Chawo Shipeynyo 374, Keo-Koruo 373, Pakke-Pokke 372, Zokima-Rokima 371; Windurst Waters:
// Dagoza-Beruza 749, Mimomo 757, Mokyokyo 748, Panna-Donna 750, Ten of Hearts 751; Windurst Woods: Miiri-Wohri 580,
// Rakoh Buuma 579, Sola Jaab 581 (0, 2), Tih Pikeh 582); at status 1 or more Fuepepe 755, Pechiru-Mashiru 754 and
// Tauwawa 756 (events). Moreno-Toeno plays 752 (0, the Aurastery Ring) at status 0 (progress), 753 up to 3 (event)
// and 758 at 4 (progress; in the play-through Teacher's Pet's 437, a progress event, answers him at status 1 over the
// mission's 753); 752's finish sets status 1, gives the Star Seeker (npcUtil.giveKeyItem) and the title
// Fugitive Minister Bounty Hunter; 758's completes the mission and then takes the Magic-drained Star Seeker. At status
// 1 the zone-ins of nineteen zones play their star-gazing scene, whose update picks the sky from the char's position
// or previous zone (updateEvent(0, 0, 0, 0, 0, n)). Davoi's Sedal-Godjal takes a Curse Wand at status 3 with the
// Magic-drained Star Seeker (progressCutscene 120, whose finish completes the trade and sets status 4); at status 2 or
// more it plays 118 with the Star Seeker (progressCutscene; 0, the wand, the seeker), 121 with the Magic-drained one at
// status 4, else 119 (0, the wand, the seeker); 118's finish takes the seeker, gives the Magic-drained one and sets
// status 3. Romaeve's Qu'Hau Spring at status 1 plays 2 (progressCutscene), whose finish sets status 2; its zone-in at
// status 1 plays 3. Kept as the Lua has it: section 1 compares the nation with mission.areaId (the log id, 2, which is
// xi.nation.WINDURST) and has no rank check and no completed check (it reads only the current mission and the nation);
// the update arms that send nothing (a position or previous zone the Lua does not list) send nothing here; Valkurm
// Dunes' update tests getZPos() > 45 and then getZPos() > -301, so its 3 arm is unreachable; the zone-ins return a bare
// csid (a number), so with ASA Burgeoning Dread current an unmounted char entering East Sarutabaruta from Windurst
// Woods at status 1 gets Burgeoning Dread's event action (a table) first and then Vain's bare 52, and
// interaction_lookup.lua:351 raises "attempt to index local 'action' (a number value)": no cutscene plays (the C#
// returns the same bare number at the same slot, its LegacyPath's, so the same Lua lookup fails the same way).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class Vain : MissionScript
    {
        public Vain() : base(XI.Quests.MissionLog.Windurst, "VAIN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/8_1_Vain";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 20)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // a zone-in at status 1 playing the zone's star-gazing scene
        static QuestReply AtStatus1(IQuestContext q, int csid) =>
            q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(csid) : null;

        // player:updateEvent(0, 0, 0, 0, 0, sky)
        static void Sky(IQuestContext q, int sky) => q.Player.UpdateEvent(0, 0, 0, 0, 0, sky);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { RankPoints = 750 });

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
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(QuestReply.Event(479))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.NineofClubs).OnTrigger(QuestReply.Event(480))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PuoRhen).OnTrigger(QuestReply.Event(482))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.TenofClubs).OnTrigger(QuestReply.Event(481))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ChawoShipeynyo).OnTrigger(QuestReply.Event(374))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KeoKoruo).OnTrigger(QuestReply.Event(373))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.PakkePokke).OnTrigger(QuestReply.Event(372))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(QuestReply.Event(371))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(QuestReply.Event(749))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 1)
                    {
                        return QuestReply.Event(755);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mimomo).OnTrigger(QuestReply.Event(757))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(748))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(752, 0, KeyItem.AurasteryRing);
                    }
                    else if (missionStatus <= 3)
                    {
                        return QuestReply.Event(753);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(758);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(QuestReply.Event(750))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PechiruMashiru).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 1)
                    {
                        return QuestReply.Event(754);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Tauwawa).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 1)
                    {
                        return QuestReply.Event(756);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TenofHearts).OnTrigger(QuestReply.Event(751))
                .OnEventFinish(Zone.WindurstWaters, 752, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.StarSeeker);
                    q.Player.AddTitle(Title.FugitiveMinisterBountyHunter);
                })
                .OnEventFinish(Zone.WindurstWaters, 758, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MagicDrainedStarSeeker);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(QuestReply.Event(580))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(579))
                // TODO: Param 3 may differ. Unconfirmed.
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(QuestReply.Event(581, 0, 2))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(QuestReply.Event(582))
                .OnZoneIn(Zone.BatalliaDowns, q => AtStatus1(q, 903))
                .OnEventUpdate(Zone.BatalliaDowns, 903, q =>
                {
                    if (q.Player.ZPos > -331)
                    {
                        Sky(q, 3);
                    }
                    else
                    {
                        Sky(q, 2);
                    }
                })
                .OnZoneIn(Zone.BeaucedineGlacier, q => AtStatus1(q, 116))
                .OnEventUpdate(Zone.BeaucedineGlacier, 116, q => Sky(q, 4))
                .OnZoneIn(Zone.BuburimuPeninsula, q => AtStatus1(q, 5))
                .OnEventUpdate(Zone.BuburimuPeninsula, 5, q =>
                {
                    if (q.Player.PreviousZone == Zone.LabyrinthOfOnzozo ||
                        q.Player.PreviousZone == Zone.Mhaura)
                    {
                        Sky(q, 7);
                    }
                    else if (q.Player.PreviousZone == Zone.MazeOfShakhrami)
                    {
                        Sky(q, 6);
                    }
                })
                .Npc(Zone.Davoi, Npc.Davoi.SedalGodjal).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.CurseWand, 1)) &&
                        q.Player.MissionStatus(AreaId) == 3 &&
                        q.Player.HasKeyItem(KeyItem.MagicDrainedStarSeeker))
                    {
                        return QuestReply.ProgressCutscene(120);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus >= 2)
                    {
                        if (q.Player.HasKeyItem(KeyItem.StarSeeker))
                        {
                            return QuestReply.ProgressCutscene(118, 0, Item.CurseWand, KeyItem.StarSeeker);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MagicDrainedStarSeeker) &&
                                 missionStatus == 4)
                        {
                            return QuestReply.Event(121);
                        }
                        else
                        {
                            return QuestReply.Event(119, 0, Item.CurseWand, KeyItem.StarSeeker);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.Davoi, 118, q =>
                {
                    q.Player.DelKeyItem(KeyItem.StarSeeker);
                    q.Player.GiveKeyItem(KeyItem.MagicDrainedStarSeeker);
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                .OnEventFinish(Zone.Davoi, 120, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                .OnZoneIn(Zone.EastRonfaure, q => AtStatus1(q, 23))
                .OnEventUpdate(Zone.EastRonfaure, 23, q =>
                {
                    if (q.Player.YPos >= -22)
                    {
                        Sky(q, 7);
                    }
                    else
                    {
                        Sky(q, 6);
                    }
                })
                .OnZoneIn(Zone.EastSarutabaruta, q => AtStatus1(q, 52))
                .OnEventUpdate(Zone.EastSarutabaruta, 52, q =>
                {
                    if (q.Player.PreviousZone == Zone.WindurstWoods ||
                        q.Player.PreviousZone == Zone.WestSarutabaruta)
                    {
                        if (q.Player.ZPos < 570)
                        {
                            Sky(q, 1);
                        }
                        else
                        {
                            Sky(q, 2);
                        }
                    }
                    else if (q.Player.PreviousZone == Zone.OuterHorutotoRuins &&
                             q.Player.ZPos > 570)
                    {
                        Sky(q, 2);
                    }
                })
                .OnZoneIn(Zone.KonschtatHighlands, q => AtStatus1(q, 106))
                .OnEventUpdate(Zone.KonschtatHighlands, 106, q =>
                {
                    if (q.Player.ZPos > 855)
                    {
                        Sky(q, 2);
                    }
                    else if (q.Player.XPos > 32 && q.Player.XPos < 370)
                    {
                        Sky(q, 1);
                    }
                })
                .OnZoneIn(Zone.LaTheinePlateau, q => AtStatus1(q, 125))
                .OnEventUpdate(Zone.LaTheinePlateau, 125, q => Sky(q, 2))
                .OnZoneIn(Zone.MeriphataudMountains, q => AtStatus1(q, 34))
                .OnEventUpdate(Zone.MeriphataudMountains, 34, q =>
                {
                    if (q.Player.PreviousZone == Zone.SauromugueChampaign)
                    {
                        Sky(q, 2);
                    }
                    else if (q.Player.PreviousZone == Zone.TahrongiCanyon)
                    {
                        Sky(q, 1);
                    }
                })
                .OnZoneIn(Zone.NorthGustaberg, q => AtStatus1(q, 246))
                .OnEventUpdate(Zone.NorthGustaberg, 246, q =>
                {
                    if (q.Player.ZPos > 461)
                    {
                        Sky(q, 6);
                    }
                    else if (q.Player.XPos > -240)
                    {
                        Sky(q, 7);
                    }
                })
                .OnZoneIn(Zone.PashhowMarshlands, q => AtStatus1(q, 15))
                .OnEventUpdate(Zone.PashhowMarshlands, 15, q =>
                {
                    if (q.Player.XPos < 362)
                    {
                        Sky(q, 2);
                    }
                })
                .OnZoneIn(Zone.RolanberryFields, q => AtStatus1(q, 4))
                .OnEventUpdate(Zone.RolanberryFields, 4, q =>
                {
                    if (q.Player.ZPos < 75)
                    {
                        Sky(q, 1);
                    }
                    else
                    {
                        Sky(q, 2);
                    }
                })
                .Npc(Zone.Romaeve, Npc.Romaeve.QuHauSpring).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressCutscene(2);
                    }

                    return null;
                })
                .OnZoneIn(Zone.Romaeve, q => AtStatus1(q, 3))
                .OnEventFinish(Zone.Romaeve, 2, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnZoneIn(Zone.SauromugueChampaign, q => AtStatus1(q, 5))
                .OnEventUpdate(Zone.SauromugueChampaign, 5, q =>
                {
                    if (q.Player.PreviousZone == Zone.GarlaigeCitadel)
                    {
                        Sky(q, 2);
                    }
                    else if (q.Player.PreviousZone == Zone.MeriphataudMountains)
                    {
                        Sky(q, 4);
                    }
                    else if (q.Player.PreviousZone == Zone.RolanberryFields ||
                             q.Player.PreviousZone == Zone.PortJeuno)
                    {
                        Sky(q, 3);
                    }
                })
                .OnZoneIn(Zone.SouthGustaberg, q => AtStatus1(q, 37))
                .OnEventUpdate(Zone.SouthGustaberg, 37, q =>
                {
                    if (q.Player.XPos > -390)
                    {
                        if (q.Player.ZPos > -301)
                        {
                            Sky(q, 6);
                        }
                        else
                        {
                            Sky(q, 7);
                        }
                    }
                })
                .OnZoneIn(Zone.TahrongiCanyon, q => AtStatus1(q, 37))
                .OnEventUpdate(Zone.TahrongiCanyon, 37, q =>
                {
                    if (q.Player.PreviousZone == Zone.EastSarutabaruta ||
                        q.Player.PreviousZone == Zone.BuburimuPeninsula)
                    {
                        Sky(q, 7);
                    }
                    else if (q.Player.PreviousZone == Zone.MazeOfShakhrami)
                    {
                        Sky(q, 6);
                    }
                })
                .OnZoneIn(Zone.TheSanctuaryOfZitah, q => AtStatus1(q, 4))
                .OnEventUpdate(Zone.TheSanctuaryOfZitah, 4, q =>
                {
                    if (q.Player.PreviousZone == Zone.TheBoyahdaTree)
                    {
                        Sky(q, 7);
                    }
                    else if (q.Player.PreviousZone == Zone.MeriphataudMountains)
                    {
                        Sky(q, 1);
                    }
                })
                .OnZoneIn(Zone.ValkurmDunes, q => AtStatus1(q, 5))
                .OnEventUpdate(Zone.ValkurmDunes, 5, q =>
                {
                    if (q.Player.ZPos > 45)
                    {
                        if (q.Player.ZPos > -301)
                        {
                            Sky(q, 1);
                        }
                        else
                        {
                            Sky(q, 3);
                        }
                    }
                })
                .OnZoneIn(Zone.WestRonfaure, q => AtStatus1(q, 53))
                .OnEventUpdate(Zone.WestRonfaure, 53, q => Sky(q, 5))
                .OnZoneIn(Zone.WestSarutabaruta, q => AtStatus1(q, 50))
                .OnEventUpdate(Zone.WestSarutabaruta, 50, q =>
                {
                    if (q.Player.ZPos > 470)
                    {
                        Sky(q, 2);
                    }
                    else
                    {
                        Sky(q, 1);
                    }
                })
                .OnZoneIn(Zone.Xarcabard, q => AtStatus1(q, 11))
                .OnEventUpdate(Zone.Xarcabard, 11, q =>
                {
                    if (q.Player.PreviousZone == Zone.BeaucedineGlacier)
                    {
                        Sky(q, 2);
                    }
                    else
                    {
                        Sky(q, 3);
                    }
                });
        }
    }
}
