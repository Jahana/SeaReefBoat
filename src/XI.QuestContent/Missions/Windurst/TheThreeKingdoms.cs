// The Three Kingdoms (Windurst 2-3, Log ID 2, Mission ID 5), a line-for-line port of
// scripts/missions/windurst/2_3_0_The_Three_Kingdoms.lua (quest rework C3 N3). Rakoh Buuma !pos 106 -5 -23 241;
// Mokyokyo !pos -55 -8 227 238; Janshura-Rashura !pos -227 -8 184 240; Zokima-Rokima !pos 0 -16 124 239; Kupipi !pos 2
// 0.1 30 242; Heruze-Moruze !pos -56 -3 36 231; Kasaroro !pos -72 -3 34 231; Patt-Pott !pos 23 -17 42 237. Reward: rank
// 3, 3000 gil, the Adventurer's Certificate and its title. Section 1 (no Windurst mission current, a Windurstian): the
// finish of the gate guards' events (Port Windurst 78, Windurst Walls 93, Windurst Waters 111, Windurst Woods 114) with
// option 5 begins the mission and says the zone's YOU_ACCEPT_THE_MISSION. Section 2 (current): Janshura-Rashura 138,
// Zokima-Rokima 134, Mokyokyo 215, Rakoh Buuma 177 (progress), Dagoza-Beruza 213 and Panna-Donna 212 (events). Kupipi:
// status 0, 95 (progress) with the letter to the consuls in slot 3, and with ENABLE_TRUST_QUESTS 1 an eighth parameter,
// 1 when the char has neither Semih Lafihna's spell nor her cipher; status 11, 101 with the certificate in slot 2; else
// 97 while the local var KupipiRepeatLine is 0, 98 otherwise (97's finish sets it to 1, 98's to 0). 95's finish sets
// status 1, gives the letter and, under the same trust test, the cipher; 101's finish completes the mission and, when
// that succeeds, takes the Kindred Report. Metalworks: Mih Ketto, Moyoyo and Topuru-Kuperu play 268, 265, 262 at status
// 11 and 266, 263, 260 at any other status above 0 (events); Patt-Pott plays 254, 256, 258, 259 at statuses 1, 6, 7, 11
// (progress). 254's finish swaps the mission for The Three Kingdoms Bastok at status 3; 256's for The Three Kingdoms
// Bastok 2 at status 8, the letter taken. Northern San d'Oria: Heruze-Moruze at status 1 plays 582 with slot 7 = 1
// while RoV's The Path Untraveled is current (582's finish sets status 2); Kasaroro at 2 plays 546 (to The Three
// Kingdoms San d'Oria, status 3), at 6 says KASARORO_DIALOG + 7, at 7 plays 547 (to The Three Kingdoms San d'Oria 2 at
// status 8, the letter taken), at 11 shows KASARORO_DIALOG + 20 to the char and answers no action. Section 3 (no
// Windurst mission current, this one completed, a Windurstian): Kupipi 102, Kasaroro 604 over his default. Kept as the
// Lua has it: section 1 has no completed check and compares the nation with the log id, section 3 with
// xi.nation.WINDURST (the same number).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheThreeKingdoms : MissionScript
    {
        public TheThreeKingdoms() : base(XI.Quests.MissionLog.Windurst, "THE_THREE_KINGDOMS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_3_0_The_Three_Kingdoms";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Windurst;
        const int MissionId = Mission.Windurst.TheThreeKingdoms;

        // handleAcceptMission: option 5 begins the mission, with the char's zone's YOU_ACCEPT_THE_MISSION
        static void HandleAcceptMission(IQuestContext q, ZoneText accepted)
        {
            if (q.Option == 5)
            {
                q.Mission.Begin();
                q.Text.Special(accepted);
            }
        }

        // ENABLE_TRUST_QUESTS is 1 and the char has neither Semih Lafihna's spell nor her cipher
        static bool NeedsSemihTrust(IQuestContext q) =>
            q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
            !q.Player.HasSpell(Spell.SemihLafihna) &&
            !q.Player.FindItem(Item.CipherOfSemihsAlterEgo);

        // Mih Ketto, Moyoyo and Topuru-Kuperu: both consuls visited (status 11), or carrying the letter (above 0)
        static QuestReply StaffLine(IQuestContext q, int bothVisited, int carrying)
        {
            int missionStatus = q.Player.MissionStatus(AreaId);

            // Both consuls have been visited.
            if (missionStatus == 11)
            {
                return QuestReply.Event(bothVisited);
            }
            // Carrying the letter of introduction.
            else if (missionStatus > 0)
            {
                return QuestReply.Event(carrying);
            }

            return null;
        }

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
                .Npc(Zone.PortWindurst, Npc.PortWindurst.JanshuraRashura).OnTrigger(q => QuestReply.ProgressEvent(138))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.ZokimaRokima).OnTrigger(q => QuestReply.ProgressEvent(134))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.DagozaBeruza).OnTrigger(q => QuestReply.Event(213))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Mokyokyo).OnTrigger(q => QuestReply.ProgressEvent(215))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PannaDonna).OnTrigger(q => QuestReply.Event(212))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.RakohBuuma).OnTrigger(q => QuestReply.ProgressEvent(177))
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        if (q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1))
                        {
                            int needsSemihTrust =
                                !q.Player.HasSpell(Spell.SemihLafihna) &&
                                !q.Player.FindItem(Item.CipherOfSemihsAlterEgo) ? 1 : 0;

                            return QuestReply.ProgressEvent(95, 0, 0, 0, KeyItem.LetterToTheConsulsWindurst, 0, 0, 0,
                                needsSemihTrust);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(95, 0, 0, 0, KeyItem.LetterToTheConsulsWindurst);
                        }
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(101, 0, 0, KeyItem.AdventurersCertificate);
                    }
                    // Kupipi alternates two lines. The toggle advances in onEventFinish.
                    else if (q.Player.LocalVar("KupipiRepeatLine") == 0)
                    {
                        return QuestReply.ProgressEvent(97);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(98);
                    }
                })
                .OnEventFinish(Zone.HeavensTower, 95, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.LetterToTheConsulsWindurst);

                    if (NeedsSemihTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfSemihsAlterEgo);
                    }
                })
                // onTrigger also runs on clicks where the action is discarded for the NPC's own file.
                .OnEventFinish(Zone.HeavensTower, 97, q => q.Player.SetLocalVar("KupipiRepeatLine", 1))
                .OnEventFinish(Zone.HeavensTower, 98, q => q.Player.SetLocalVar("KupipiRepeatLine", 0))
                .OnEventFinish(Zone.HeavensTower, 101, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.KindredReport);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.MihKetto).OnTrigger(q => StaffLine(q, 268, 266))
                .Npc(Zone.Metalworks, Npc.Metalworks.Moyoyo).OnTrigger(q => StaffLine(q, 265, 263))
                .Npc(Zone.Metalworks, Npc.Metalworks.PattPott).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(254);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(256);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(258);
                    }
                    else if (missionStatus == 11)
                    {
                        return QuestReply.ProgressEvent(259);
                    }

                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.TopuruKuperu).OnTrigger(q => StaffLine(q, 262, 260))
                .OnEventFinish(Zone.Metalworks, 254, q =>
                {
                    q.Player.DelMission(AreaId, MissionId);
                    q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsBastok);
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                // The second consul visited takes the letter.
                .OnEventFinish(Zone.Metalworks, 256, q =>
                {
                    q.Player.DelMission(AreaId, MissionId);
                    q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsBastok2);
                    q.Player.DelKeyItem(KeyItem.LetterToTheConsulsWindurst);
                    q.Player.SetMissionStatus(AreaId, 8);
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.HeruzeMoruze).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        int onPathUntraveled =
                            q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.ThePathUntraveled ? 1 : 0;

                        return QuestReply.ProgressEvent(582).Option(7, onPathUntraveled);
                    }

                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Kasaroro).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(546);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.NorthernSanDoria.KASARORO_DIALOG) + 7);
                    }
                    else if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(547);
                    }
                    else if (missionStatus == 11)
                    {
                        q.Player.ShowText(q.Text.Id(Text.NorthernSanDoria.KASARORO_DIALOG) + 20);
                        return QuestReply.NoAction();
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 546, q =>
                {
                    q.Player.DelMission(AreaId, MissionId);
                    q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsSandoria);
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                // The second consul visited takes the letter.
                .OnEventFinish(Zone.NorthernSanDoria, 547, q =>
                {
                    q.Player.DelMission(AreaId, MissionId);
                    q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdomsSandoria2);
                    q.Player.DelKeyItem(KeyItem.LetterToTheConsulsWindurst);
                    q.Player.SetMissionStatus(AreaId, 8);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 582, q => q.Player.SetMissionStatus(AreaId, 2));

            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Player.Nation == Nation.Windurst)
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q => QuestReply.Event(102))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Kasaroro)
                .OnTrigger(q => QuestReply.Event(604).ReplaceDefault());
        }
    }
}
