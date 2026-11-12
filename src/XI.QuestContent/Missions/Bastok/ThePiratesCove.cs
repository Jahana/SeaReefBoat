// The Pirate's Cove (Bastok 6-2, Log ID 1, Mission ID 17), a line-for-line port of
// scripts/missions/bastok/6_2_The_Pirates_Cove.lua (quest rework C3 N5). Argus !pos 132.157 7.496 -2.187 236; Cleades
// !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Naji !pos 64 -14
// -4 237; qm4 !pos 171 0 -25 205; Gilgamesh !pos 122.452 -9.009 -12.052 252. Reward: 40000 gil and rank 7. Section 1
// (no Bastok mission current, a Bastokan): the four gate guards' 1001 finish with option 17 (handleAcceptMission)
// begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Cleades, Rashid, Malduc and
// Argus say their zone's EXTENDED_MISSION_OFFSET + 3 (messageSpecial); Naji plays 761 at status 0 and 762 at status 3
// (progress); 761's finish sets status 1 and 762's completes the mission. Norg: Gilgamesh at status 1 plays 98
// (progress), whose finish sets status 2; a trade of exactly one Frag Rock at status 2 plays 99 (progress), whose
// finish takes the trade and sets status 3. Ifrit's Cauldron: qm4 takes a trade of exactly one Chunk of Adaman Ore at
// status 2 when the two Pirate's Cove NMs pop (no claim, hide 900): the trade is taken, the Salamander turns to the
// char, the Magma one is claimed and it says BAD_FEELING_ABOUT_PLACE. Kept as the Lua has it: section 1 compares the
// nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK) and has no rank check and no completed check;
// 762's finish ignores mission:complete's answer.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class ThePiratesCove : MissionScript
    {
        public ThePiratesCove() : base(XI.Quests.MissionLog.Bastok, "THE_PIRATES_COVE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/6_2_The_Pirates_Cove";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Bastok;
        const int MissionId = Mission.Bastok.ThePiratesCove;

        // handleAcceptMission: option 17 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 17)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.EXTENDED_MISSION_OFFSET + 3)
        static QuestReply OffsetPlus3(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 3);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Gil = 40000, Rank = 7 });

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
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades)
                .OnTrigger(q => OffsetPlus3(q, Text.BastokMarkets.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus3(q, Text.BastokMines.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus3(q, Text.Metalworks.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(761);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(762);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 761, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.Metalworks, 762, q => q.Mission.Complete())
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus3(q, Text.PortBastok.EXTENDED_MISSION_OFFSET))
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh)
                .OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Trade.Matches((Item.FragRock, 1)))
                    {
                        return QuestReply.ProgressEvent(99);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(98);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Norg, 98, q => q.Player.SetMissionStatus(AreaId, 2))
                .OnEventFinish(Zone.Norg, 99, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                .Npc(Zone.IfritsCauldron, Npc.IfritsCauldron.qm4)
                .OnTrade(q =>
                {
                    // npcUtil.popFromQM(player, npc, { PIRATES_COVE_NMS, PIRATES_COVE_NMS + 1 }, { claim = false,
                    // hide = 900 }): look false is the Lua's absent look, as popFromQM only tests `if params.look`
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Trade.Matches((Item.ChunkOfAdamanOre, 1)) &&
                        q.Player.PopFromQmSeries(Mob.IfritsCauldron.PIRATES_COVE_NMS, 2, hide: 900, claim: false,
                            look: false))
                    {
                        q.Trade.Complete();
                        q.Player.MobLookAtPlayer(Mob.IfritsCauldron.PIRATES_COVE_NMS, 0); // Salamander
                        q.Player.MobUpdateClaim(Mob.IfritsCauldron.PIRATES_COVE_NMS, 1); // Magma
                        return QuestReply.MessageSpecial(Text.IfritsCauldron.BAD_FEELING_ABOUT_PLACE);
                    }

                    return null;
                });
        }
    }
}
