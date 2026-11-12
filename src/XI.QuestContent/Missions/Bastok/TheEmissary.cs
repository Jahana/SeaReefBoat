// The Emissary (Bastok 2-3, Log ID 1, Mission ID 5), a line-for-line port of
// scripts/missions/bastok/2_3_0_The_Emissary.lua (quest rework C3 N2). Cleades !pos -358 -10 -168 235; Rashid !pos
// -8.444 -2 -123.575 234; Malduc !pos 66.200 -14.999 4.426 237; Naji !pos 64 -14 -4 237; Argus !pos 132.157 7.496
// -2.187 236; Baraka !pos 36 -2 -2 231; Helaku !pos 49 -2 -12 231; Melek !pos -80.6 -5.5 157.3 240. Section 1 (no
// mission current, a Bastokan): the four gate guards keep their script's 1001; its finish on option 5 begins the
// mission and sends YOU_ACCEPT_THE_MISSION of the char's zone. Section 2 (2-3 current): Cleades, Rashid, Malduc and
// Argus play their zone's ORIGINAL_MISSION_OFFSET + 23. Naji: with the Kindred Report, 714 (its finish completes the
// mission and takes the report); with no letter to the consuls and status 0, 713 (its finish gives the letter and
// moves to status 1; the eighth parameter is 1 when neither Journey Abroad nor the Three Kingdoms is completed); else
// GOOD_LUCK. Northern San d'Oria: Baraka at status 1, 581 (to The Emissary Sandoria, status 2); Helaku at status 1,
// 676, at 7, 537 (option 0: to The Emissary Sandoria 2, the letter taken, status 8), at 11 with The Emissary Sandoria
// 2 completed, 557; Shakir at status 1 or 7, event 556. Port Windurst: Melek at status 1, 48 (to The Emissary
// Windurst, status 2), at 6, 61 (to The Emissary Windurst 2, the letter taken, status 7), at 7 event 56; Ada, Gold
// Skull and Josef at 7 play 58, 57, 59 (events); with the Kindred Report and The Emissary Windurst 2 completed Ada,
// Gold Skull and Josef play 69, 68 and 70 (events) and Melek 67 (progress). Kept as the Lua has it: the nation is
// compared with the log id.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheEmissary : MissionScript
    {
        public TheEmissary() : base(XI.Quests.MissionLog.Bastok, "THE_EMISSARY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_3_0_The_Emissary";

        // handleAcceptMission: option 5 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 5)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // mission:messageSpecial(zone.text.ORIGINAL_MISSION_OFFSET + 23)
        static QuestReply OffsetPlus23(IQuestContext q, ZoneText offset) =>
            QuestReply.MessageSpecial(q.Text.Id(offset) + 23);

        // Both paths completed, Windurst last: the Kindred Report and The Emissary Windurst 2 completed
        static bool WindurstLast(IQuestContext q) =>
            q.Player.HasKeyItem(KeyItem.KindredReport) &&
            q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheEmissaryWindurst2);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Gil = 3000,
                KeyItem = KeyItem.AdventurersCertificate,
                Rank = 3,
                Title = Title.CertifiedAdventurer,
            });

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == MissionLog.Bastok)
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
                .OnTrigger(q => OffsetPlus23(q, Text.BastokMarkets.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.BastokMines, Npc.BastokMines.Rashid)
                .OnTrigger(q => OffsetPlus23(q, Text.BastokMines.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Malduc)
                .OnTrigger(q => OffsetPlus23(q, Text.Metalworks.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.KindredReport))
                    {
                        return QuestReply.ProgressEvent(714);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.LetterToTheConsulsBastok) &&
                             q.Player.MissionStatus(MissionLog.Bastok) == 0)
                    {
                        int isFirst23 =
                            !q.Player.HasCompletedMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad) &&
                            !q.Player.HasCompletedMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms)
                                ? 1 : 0;

                        // Contains variation for Lion mention.
                        return QuestReply.ProgressEvent(713, 0, 0, 0, 0, 0, 0, 0, isFirst23);
                    }

                    return QuestReply.MessageText(Text.Metalworks.GOOD_LUCK);
                })
                .OnEventFinish(Zone.Metalworks, 713, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterToTheConsulsBastok);
                    q.Player.SetMissionStatus(MissionLog.Bastok, 1);
                })
                .OnEventFinish(Zone.Metalworks, 714, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.KindredReport);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Argus)
                .OnTrigger(q => OffsetPlus23(q, Text.PortBastok.ORIGINAL_MISSION_OFFSET))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Baraka).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 1 ? QuestReply.ProgressEvent(581) : null)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Helaku).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(676);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(537);
                    }
                    else if (missionStatus == 11 &&
                             q.Player.HasCompletedMission(MissionLog.Bastok, Mission.Bastok.TheEmissarySandoria2))
                    {
                        return QuestReply.ProgressEvent(557);
                    }

                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Shakir).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    return missionStatus == 1 || missionStatus == 7 ? QuestReply.Event(556) : null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 537, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                        q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissarySandoria2);
                        q.Player.DelKeyItem(KeyItem.LetterToTheConsulsBastok);
                        q.Player.SetMissionStatus(MissionLog.Bastok, 8);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 581, q =>
                {
                    q.Player.DelMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                    q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissarySandoria);
                    q.Player.SetMissionStatus(MissionLog.Bastok, 2);
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ada).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 7 ? QuestReply.Event(58) :
                    WindurstLast(q) ? QuestReply.Event(69) : null)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GoldSkull).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 7 ? QuestReply.Event(57) :
                    WindurstLast(q) ? QuestReply.Event(68) : null)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Josef).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 7 ? QuestReply.Event(59) :
                    WindurstLast(q) ? QuestReply.Event(70) : null)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Melek).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(48);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(61);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.Event(56);
                    }
                    else if (WindurstLast(q))
                    {
                        return QuestReply.ProgressEvent(67);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 48, q =>
                {
                    q.Player.DelMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                    q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissaryWindurst);
                    q.Player.SetMissionStatus(MissionLog.Bastok, 2);
                })
                .OnEventFinish(Zone.PortWindurst, 61, q =>
                {
                    q.Player.DelMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                    q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissaryWindurst2);
                    q.Player.DelKeyItem(KeyItem.LetterToTheConsulsBastok);
                    q.Player.SetMissionStatus(MissionLog.Bastok, 7);
                });
        }
    }
}
