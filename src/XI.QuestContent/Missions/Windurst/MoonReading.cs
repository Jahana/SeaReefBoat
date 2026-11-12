// Moon Reading (Windurst 9-2, Log ID 2, Mission ID 23), a line-for-line port of
// scripts/missions/windurst/9_2_Moon_Reading.lua (quest rework C3 N6). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo !pos
// -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; _6q2 !pos 0.1 -49 37 242;
// qm_windy_9_2 !pos -239.442 -1.000 -18.870 159; Qu'Hau Spring !pos 0 -29 64 122. Reward: 100000 gil, the Windurstian
// Flag, rank 10 and the title Vestal Chamberlain. Section 1 (no Windurst mission current, a Windurstian): the gate
// guards' finishes (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option 23
// (handleAcceptMission) begin the mission and say the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): _6q2 plays
// 384 at status 0, 385 at 1 with the three ancient verses, 386 (0, 0, the Orastery Ring) at 3 and, at 4, says
// ITEM_CANNOT_BE_OBTAINED (the Windurstian Flag) with no free slot, else plays 407 (all progress); Kupipi at 3 or more
// plays 400 (progress, importantOnce). 384's finish sets status 1, 385's 2, 386's 4; 407's moves the char to (0,
// -16.750, 130, 64) in Windurst Walls. At status 1 the three verses: the Chamber of Oracles' zone-in from the Quicksand
// Caves without the Ancient Verse of Altepa plays 3 (text table -1, flags RESET_CAMERA | NO_PCS | NO_NPCS); Romaeve's
// Qu'Hau Spring without the verse of Ro'Maeve plays 4 and the Temple of Uggalepih's qm_windy_9_2 without the verse of
// Uggalepih 68 (progressCutscene); each finish gives its verse (npcUtil.giveKeyItem). Full Moon Fountain's 32001 finish
// at status 2 with battlefieldWin MOON_READING (225) takes the three verses and sets status 3. Windurst Walls' zone-in
// at status 4 sets the char at (0, -16.750, 130, 64) and plays 443 (text table -1, flags NO_PCS | OPENING_MODE), whose
// finish completes the mission. Section 3 (current at status 3 or more, or completed): the Heavens Tower NPCs' events
// (replaceDefault). Section 4 (completed): Janshura-Rashura 567, Mokyokyo 837 and Rakoh Buuma 632 (oncePerZone);
// Tosuka-Porika 380 (replaceDefault). Kept as the Lua has it: section 1 compares the nation with mission.areaId (the
// log id, 2, which is xi.nation.WINDURST) and has no rank check and no completed check (it reads only the current
// mission and the nation).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class MoonReading : MissionScript
    {
        public MoonReading() : base(XI.Quests.MissionLog.Windurst, "MOON_READING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/9_2_Moon_Reading";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.MoonReading;

        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 23)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Gil = 100000,
                Item = Item.WindurstianFlag,
                Rank = 10,
                Title = Title.VestalChamberlain,
            });

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
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.ChamberOfOracles, q =>
                {
                    if (q.Player.PreviousZone == Zone.QuicksandCaves &&
                        q.Player.MissionStatus(AreaId) == 1 &&
                        !q.Player.HasKeyItem(KeyItem.AncientVerseOfAltepa))
                    {
                        // { 3, -1, bit.bor(RESET_CAMERA, NO_PCS, NO_NPCS) }: 0x01 | 0x02 | 0x10
                        return QuestReply.Event(3).TextTable(-1).Flags(0x13);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChamberOfOracles, 3, q => q.Player.GiveKeyItem(KeyItem.AncientVerseOfAltepa))
                .OnEventFinish(Zone.FullMoonFountain, 32001, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.MoonReading)
                    {
                        q.Player.DelKeyItem(KeyItem.AncientVerseOfRomaeve);
                        q.Player.DelKeyItem(KeyItem.AncientVerseOfAltepa);
                        q.Player.DelKeyItem(KeyItem.AncientVerseOfUggalepih);
                        q.Player.SetMissionStatus(AreaId, 3);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(384);
                    }
                    else if (missionStatus == 1 &&
                             q.Player.HasKeyItem(KeyItem.AncientVerseOfRomaeve) &&
                             q.Player.HasKeyItem(KeyItem.AncientVerseOfAltepa) &&
                             q.Player.HasKeyItem(KeyItem.AncientVerseOfUggalepih))
                    {
                        return QuestReply.ProgressEvent(385);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(386, 0, 0, KeyItem.OrasteryRing);
                    }
                    else if (missionStatus == 4)
                    {
                        // This does not use the npcUtil function, as in both cases we need to return
                        // an appropriate mission function.

                        if (q.Player.FreeSlotCount == 0)
                        {
                            return QuestReply.MessageSpecial(Text.HeavensTower.ITEM_CANNOT_BE_OBTAINED,
                                Item.WindurstianFlag);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(407);
                        }
                    }

                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 3)
                    {
                        return QuestReply.ProgressEvent(400).ImportantOnce();
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 384, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.HeavensTower, 385, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.HeavensTower, 386, q => q.Player.SetMissionStatus(AreaId, 4))
                .OnEventFinish(Zone.HeavensTower, 407, q =>
                    q.Player.SetPos(0, -16.750, 130, 64, Zone.WindurstWalls))
                .Npc(Zone.Romaeve, Npc.Romaeve.QuHauSpring).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        !q.Player.HasKeyItem(KeyItem.AncientVerseOfRomaeve))
                    {
                        return QuestReply.ProgressCutscene(4);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Romaeve, 4, q => q.Player.GiveKeyItem(KeyItem.AncientVerseOfRomaeve))
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qmwindy92).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1 &&
                        !q.Player.HasKeyItem(KeyItem.AncientVerseOfUggalepih))
                    {
                        return QuestReply.ProgressCutscene(68);
                    }

                    return null;
                })
                .OnEventFinish(Zone.TempleOfUggalepih, 68, q =>
                    q.Player.GiveKeyItem(KeyItem.AncientVerseOfUggalepih))
                .OnZoneIn(Zone.WindurstWalls, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4)
                    {
                        q.Player.SetPos(0, -16.750, 130, 64);

                        // { 443, -1, bit.bor(NO_PCS, OPENING_MODE) }: 0x02 | 0x80
                        return QuestReply.Event(443).TextTable(-1).Flags(0x82);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 443, q =>
                {
                    // NOTE: Inventory is checked before this series of events.  This is not
                    // correct behavior, and instead if unable to obtain, it can be obtained
                    // by clicking the blank target in Heaven's Tower
                    // See: https://ffxiclopedia.fandom.com/wiki/Moon_Reading

                    q.Mission.Complete();
                });

            // Optional Dialogue
            b.State(current: null, when: q =>
                    (q.Mission.IsCurrent && q.Mission.Status >= 3) ||
                    q.Player.HasCompletedMission(AreaId, MissionId))
                // NOTE: The replaceDefault behavior is current function from the scripts.  This
                // may be inconsistent with how San d'Oria optional dialogue is handled, and should
                // be verified.  It appears that in a previous iteration, there were unique charVars
                // tracking these.  These might just be a 'Once' event, but can be adapted here if
                // necessary by adding a bitfield to a check, and tabling up the bit values along
                // with a helper function.
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Aeshushu).OnTrigger(QuestReply.Event(391).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Boycoco).OnTrigger(QuestReply.Event(388).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Churara).OnTrigger(QuestReply.Event(390).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Dattata).OnTrigger(QuestReply.Event(392).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.FooBeibo).OnTrigger(QuestReply.Event(403).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Heruru).OnTrigger(QuestReply.Event(393).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Ikucheechee)
                .OnTrigger(QuestReply.Event(394).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kinono).OnTrigger(QuestReply.Event(398).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kiwawa).OnTrigger(QuestReply.Event(389).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Nayutata).OnTrigger(QuestReply.Event(395).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Nebibi).OnTrigger(QuestReply.Event(399).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.RhyEpocan).OnTrigger(QuestReply.Event(405).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.ShazNorem).OnTrigger(QuestReply.Event(401).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Tsuryarya).OnTrigger(QuestReply.Event(396).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.UfuKoromoa)
                .OnTrigger(QuestReply.Event(402).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Utsuitsui).OnTrigger(QuestReply.Event(397).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.VahnPaineesha)
                .OnTrigger(QuestReply.Event(404).ReplaceDefault())
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Zubaba).OnTrigger(QuestReply.Event(387).ReplaceDefault());

            // Optional Dialogue on Completed Mission Only.  This should be verified to confirm that
            // these events are actually played more than once, and also check to see if they are blocking
            // for running future repeatable missions.
            // TODO: Capture events for missing gate guards.
            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, MissionId))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura)
                .OnTrigger(QuestReply.Event(567).OncePerZone())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(QuestReply.Event(837).OncePerZone())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.TosukaPorika)
                .OnTrigger(QuestReply.Event(380).ReplaceDefault())
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(QuestReply.Event(632).OncePerZone());
        }
    }
}
