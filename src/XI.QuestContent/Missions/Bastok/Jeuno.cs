// Jeuno (Bastok 3-3, Log ID 1, Mission ID 12), a line-for-line port of scripts/missions/bastok/3_3_Jeuno.lua (quest
// rework C3 N3). Argus !pos 132.157 7.496 -2.187 236; Cleades !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426
// 237; Rashid !pos -8.444 -2 -123.575 234; Lucius !pos 59.959 -17.39 -42.321 237; Goggehn !pos 3 9 -76 243; _542 !pos
// 596 16 -19 184. Reward: 5000 gil and rank 4. Section 1 (no Bastok mission current, a Bastokan): the four gate guards'
// 1001 finish with option 12 begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current):
// Cleades, Rashid, Malduc and Argus play 1002 (progress). Metalworks: Lucius at status 0 plays 322 (progress), whose
// finish sets status 1 and gives the Letter to the Ambassador. Ru'Lude Gardens: the door _6r2 at status 3 plays 38
// (progress), whose finish completes the mission; Goggehn at 1 plays 41 (progress), at 2 66, at 3 139 (events); 41's
// finish sets status 2 and takes the letter. Lower Delkfutt's Tower: the Cermet Door _542 at status 2 takes a trade of
// exactly one Delkfutt Key with cutscene 1 (progress); its trigger at 2 plays 1 with the Delkfutt Key key item, else
// THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY (priority 1000). 1's finish sets status 3 and, without the key item, gives the key
// item and takes the trade. Kept as the Lua has it: section 1 has no completed check and compares the nation with
// mission.areaId (the log id, 1, which is xi.nation.BASTOK).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class Jeuno : MissionScript
    {
        public Jeuno() : base(XI.Quests.MissionLog.Bastok, "JEUNO") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/3_3_Jeuno";

        // mission.areaId
        const int AreaId = MissionLog.Bastok;

        // handleAcceptMission: option 12 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 12)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 5000, Rank = 4 });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.BastokMarkets, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMarkets.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.BastokMines, 1001, q =>
                    HandleAcceptMission(q, Text.BastokMines.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.Metalworks, 1001, q =>
                    HandleAcceptMission(q, Text.Metalworks.YOU_ACCEPT_THE_MISSION))
                .OnEventFinish(Zone.PortBastok, 1001, q =>
                    HandleAcceptMission(q, Text.PortBastok.YOU_ACCEPT_THE_MISSION));

            b.State(current: true)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades).OnTrigger(q => QuestReply.ProgressEvent(1002))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid).OnTrigger(q => QuestReply.ProgressEvent(1002))
                // NOTE: Trust Bastok, annoyingly, takes priority in retail, so priority should be lower. On the other
                // hand, this is a mandatory step.
                .Npc(Zone.Metalworks, Npc.Metalworks.Lucius).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 0 ? QuestReply.ProgressEvent(322) : null)
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc).OnTrigger(q => QuestReply.ProgressEvent(1002))
                .OnEventFinish(Zone.Metalworks, 322, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.LetterToTheAmbassador);
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus).OnTrigger(q => QuestReply.ProgressEvent(1002))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r2).OnTrigger(q =>
                    q.Player.MissionStatus(AreaId) == 3 ? QuestReply.ProgressEvent(38) : null)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Goggehn).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(41);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(66);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(139);
                    }

                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 38, q => q.Mission.Complete())
                .OnEventFinish(Zone.RuludeGardens, 41, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.DelKeyItem(KeyItem.LetterToTheAmbassador);
                })
                .Npc(Zone.LowerDelkfuttsTower, Npc.LowerDelkfuttsTower._542)
                .OnTrade(q =>
                {
                    // As of the April 2009 update, trading the key to any Cermet Door or the elevator will turn the key
                    // into a key item, allowing the player to drop the inventory key for space.
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Trade.Matches((Item.DelkfuttKey, 1)))
                    {
                        return QuestReply.ProgressCutscene(1);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        if (q.Player.HasKeyItem(KeyItem.DelkfuttKey))
                        {
                            return QuestReply.ProgressCutscene(1);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.LowerDelkfuttsTower.THE_DOOR_IS_FIRMLY_SHUT_OPEN_KEY)
                                .SetPriority(1000);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 1, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);

                    if (!q.Player.HasKeyItem(KeyItem.DelkfuttKey))
                    {
                        q.Player.GiveKeyItem(KeyItem.DelkfuttKey);
                        q.Trade.Complete();
                    }
                });
        }
    }
}
