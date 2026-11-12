// A New Journey (Windurst 3-3, Log ID 2, Mission ID 12), a line-for-line port of
// scripts/missions/windurst/3_3_A_New_Journey.lua (quest rework C3 N3). Rakoh Buuma !pos 106 -5 -23 241; Mokyokyo !pos
// -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Vestal Chambers !pos 0.1 -49
// 37 242; Pakh Jatalfih !pos 34 8 -35 243; Embassy Door !pos 31 9 -22 243; Cermet Door !pos 636 16 59 184. Reward: 5000
// gil and rank 4. Section 1 (no Windurst mission current, a Windurstian who has not completed this mission): the finish
// of the gate guards' events (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with option
// 12 begins the mission, says the zone's YOU_ACCEPT_THE_MISSION and gives the Star-Crested Summons. Section 2
// (current): Heaven's Tower: the Vestal Chambers door _6q2 at status 0 plays 153 (progress); Foo Beibo, Rhy Epocan and
// Ufu Koromoa at status 1 play 162, 160 and 161; Kupipi at 0 plays 155 (events). 153's finish sets status 1, takes the
// summons and gives the Letter to the Ambassador. Lower Delkfutt's Tower: the Cermet Door _540 at status 2 takes a
// trade of exactly one Delkfutt Key with cutscene 2 (progress); its trigger at 2 plays 2 with the Delkfutt Key key
// item, else THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY (priority 1000). 2's finish sets status 3 and, without the key item,
// takes the trade and gives the key item. Ru'Lude Gardens: the Embassy Door _6r8 at status 4 plays 40 (progress), whose
// finish completes the mission; Pakh Jatalfih at 1 plays 43 (progress), at 2 68 (event), at 3 141 (progress). 43's
// finish sets status 2 and takes the letter; 141's sets status 4. Windurst Woods: Miiri-Wohri 196, Rakoh Buuma 193,
// Sola Jaab 195, Tih Pikeh 194 (events). Section 3 (no Windurst mission current, a Windurstian, this mission completed,
// Magicite not completed): Pakh Jatalfih plays 54 over his default (replaceDefault, priority 20; while Magicite's
// section 1 holds, a rank 4 Windurstian with no mission current, Magicite's own reply wins over it: its event 54,
// priority 100, or with the rank points its progress 50). Kept as the Lua has it: sections 1 and 3 compare the nation
// with mission.areaId (the log id, 2, which is xi.nation.WINDURST).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class ANewJourney : MissionScript
    {
        public ANewJourney() : base(XI.Quests.MissionLog.Windurst, "A_NEW_JOURNEY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/3_3_A_New_Journey";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.ANewJourney;

        // handleAcceptMission: option 12 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION, and gives
        // the Star-Crested Summons
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 12)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
                q.Player.GiveKeyItem(KeyItem.StarCrestedSummons1);
            }
        }

        // a line at status 1 (event 153 gives Foo Beibo, Rhy Epocan and Ufu Koromoa new lines)
        static QuestReply StatusOneLine(IQuestContext q, int csid) =>
            q.Player.MissionStatus(AreaId) == 1 ? QuestReply.Event(csid) : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 5000, Rank = 4 });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    !q.Player.HasCompletedMission(AreaId, MissionId))
                .OnEventFinish(Zone.PortWindurst, 78, q =>
                    HandleAcceptMission(q, Text.PortWindurst.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWalls, 93, q =>
                    HandleAcceptMission(q, Text.WindurstWalls.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWaters, 111, q =>
                    HandleAcceptMission(q, Text.WindurstWaters.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.WindurstWoods, 114, q =>
                    HandleAcceptMission(q, Text.WindurstWoods.YOU_ACCEPT_THE_MISSION));

            b.State(current: true)
                // Event 153 gives Foo Beibo, Rhy Epocan and Ufu Koromoa new lines. Shaz Norem and Vahn Paineesha keep
                // their defaults.
                .Npc(Zone.HeavensTower, Npc.HeavensTower._6q2).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 0 ? QuestReply.ProgressEvent(153) : null)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.FooBeibo).OnTrigger(q => StatusOneLine(q, 162))
                // Kupipi points the way until event 153 plays.
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 0 ? QuestReply.Event(155) : null)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.RhyEpocan).OnTrigger(q => StatusOneLine(q, 160))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.UfuKoromoa).OnTrigger(q => StatusOneLine(q, 161))
                .OnEventFinish(Zone.HeavensTower, 153, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.DelKeyItem(KeyItem.StarCrestedSummons1);
                    q.Player.GiveKeyItem(KeyItem.LetterToTheAmbassador);
                })
                .Npc(Zone.LowerDelkfuttsTower, Npc.LowerDelkfuttsTower._540)
                .OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Trade.Matches((Item.DelkfuttKey, 1)))
                    {
                        return QuestReply.ProgressCutscene(2);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        if (q.Player.HasKeyItem(KeyItem.DelkfuttKey))
                        {
                            return QuestReply.ProgressCutscene(2);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.LowerDelkfuttsTower.THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY)
                                .SetPriority(1000);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 2, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);

                    if (!q.Player.HasKeyItem(KeyItem.DelkfuttKey))
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.DelkfuttKey);
                    }
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r8).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 4 ? QuestReply.ProgressEvent(40) : null)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(43);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(68);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(141);
                    }

                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 40, q => q.Mission.Complete())
                .OnEventFinish(Zone.RuludeGardens, 43, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.DelKeyItem(KeyItem.LetterToTheAmbassador);
                })
                .OnEventFinish(Zone.RuludeGardens, 141, q => q.Player.SetMissionStatus(AreaId, 4))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MiiriWohri).OnTrigger(q => QuestReply.Event(196))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(q => QuestReply.Event(193))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrigger(q => QuestReply.Event(195))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.TihPikeh).OnTrigger(q => QuestReply.Event(194));

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    !q.Player.HasCompletedMission(AreaId, Mission.Windurst.Magicite))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih)
                .OnTrigger(q => QuestReply.Event(54).ReplaceDefault());
        }
    }
}
