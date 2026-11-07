// The Zeruhn Report (Bastok 1-1, Log ID 1, Mission ID 0), a line-for-line port of
// scripts/missions/bastok/1_1_The_Zeruhn_Report.lua (quest rework C2). Argus !pos 132.157 7.496 -2.187 236; Cleades
// !pos -358 -10 -168 235; Malduc !pos 66.200 -14.999 4.426 237; Rashid !pos -8.444 -2 -123.575 234; Makarim !pos -58 8
// -333 172; Naji !pos 64 -14 -4 237. Section 1 (no Bastok mission current, a Bastokan, 1-1 not completed): Cleades,
// Rashid, Malduc and Argus each play 1000; its finish on option 0 begins the mission and sends YOU_ACCEPT_THE_MISSION
// of the char's zone. Section 2 (1-1 current): the four play their zone's ORIGINAL_MISSION_OFFSET; Makarim plays 121
// (canSkip) without the Zeruhn Report, whose finish gives it, and MAKARIM_DIALOG_I with it; Rasmus plays 120 (canSkip)
// without it; Naji with it plays 710 (1 when the report was never seen, else 0), whose finish completes the mission and
// takes the report. Kept as the Lua has it: the one handleAcceptMission closure of the four zones is four handlers
// here, each its zone's; section 1 compares the nation with mission.areaId (the log id, 1, which is xi.nation.BASTOK);
// no reward table (mission:complete gives nothing, then the status reset and the var cleanup); 710's finish takes the
// report only when mission:complete answers true; Naji without the report and Rasmus with it return nothing (their
// defaults play).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheZeruhnReport : MissionScript
    {
        public TheZeruhnReport() : base(XI.Quests.MissionLog.Bastok, "THE_ZERUHN_REPORT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/1_1_The_Zeruhn_Report";

        // handleAcceptMission: option 0 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION (the Lua's
        // zones[player:getZoneID()]; the finish runs in the zone of its text)
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 0)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == MissionLog.Bastok &&
                    !q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheZeruhnReport))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades).OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(Zone.BastokMarkets, 1000, q =>
                    HandleAcceptMission(q, Text.BastokMarkets.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid).OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(Zone.BastokMines, 1000, q =>
                    HandleAcceptMission(q, Text.BastokMines.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc).OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(Zone.Metalworks, 1000, q =>
                    HandleAcceptMission(q, Text.Metalworks.YOU_ACCEPT_THE_MISSION))
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus).OnTrigger(QuestReply.ProgressEvent(1000))
                .OnEventFinish(Zone.PortBastok, 1000, q =>
                    HandleAcceptMission(q, Text.PortBastok.YOU_ACCEPT_THE_MISSION));

            b.State(current: true)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Cleades)
                .OnTrigger(QuestReply.MessageSpecial(Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(QuestReply.MessageSpecial(Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(QuestReply.MessageSpecial(Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ZeruhnReport))
                    {
                        return QuestReply.ProgressEvent(710, !q.Player.SeenKeyItem(KeyItem.ZeruhnReport) ? 1 : 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 710, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ZeruhnReport);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(QuestReply.MessageSpecial(Text.PortBastok.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.ZeruhnMines, Npc.ZeruhnMines.Makarim).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ZeruhnReport))
                    {
                        return QuestReply.MessageSpecial(Text.ZeruhnMines.MAKARIM_DIALOG_I);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(121).CanSkip();
                    }
                })
                .Npc(Zone.ZeruhnMines, Npc.ZeruhnMines.Rasmus).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.ZeruhnReport))
                    {
                        return QuestReply.ProgressEvent(120).CanSkip();
                    }

                    return null;
                })
                .OnEventFinish(Zone.ZeruhnMines, 121, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.ZeruhnReport);
                });
        }
    }
}
