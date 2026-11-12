// The Queen of the Dance (WotG 4, Log ID 5, Mission ID 3), a line-for-line port of
// scripts/missions/wotg/04_The_Queen_of_the_Dance.lua (quest rework C3 T5). Lion Springs Door !pos 96 0 106 80;
// Turlough !pos -58.697 0 103.553 244. Section 0 (status 0 and xi.wotg.helpers.meetsMission4Reqs, which stays Lua):
// Southern San d'Oria [S]'s Lion Springs plays the progress event 68, whose finish sets status 1. Section 1 (status 1):
// Lion Springs plays 69; Upper Jeuno's Turlough plays the progress event 10172, whose finish gives the Mayakov Show
// Ticket and sets status 2. Section 2 (status 2 or more): Lion Springs plays the progress event 70, whose finish sets
// status 3 and sets the char down at 100.801 1 103.211 rot 31 in the same zone; the zone-in plays 152 at status 3 and
// 153 at status 4; 152's update (option 1) updates the event with 1 0 1756 0 66453367 8366690 4095 131140 and its
// finish sets status 4 and sets the char down again; 153's update (option 1) updates with 1 0 1756 0 66451386 11124900
// 4095 131140 and its finish completes the mission (While the Cat Is Away current) and takes the ticket. Kept as the
// Lua has it: section 2 has no meetsMission4Reqs check (only section 0 does); the ticket is taken only when the
// completion takes (a char who has no ticket still completes).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class TheQueenOfTheDance : MissionScript
    {
        public TheQueenOfTheDance() : base(XI.Quests.MissionLog.WoTG, "THE_QUEEN_OF_THE_DANCE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/04_The_Queen_of_the_Dance";

        const int AreaId = MissionLog.Wotg;

        // utils.MAX_UINT32 - n
        static int MaxUint32Minus(uint n) => unchecked((int)(uint.MaxValue - n));

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.WhileTheCatIsAway,
            });

            // 0: Try to enter without a ticket
            b.State(current: true, status: 0, when: q => q.Player.WotgMeetsMissionReqs(4))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings).OnTrigger(
                    QuestReply.ProgressEvent(68, 80, 4224267, 1756, MaxUint32Minus(1540096),
                        MaxUint32Minus(1239549952), 427798150, 3, 4095))
                .OnEventFinish(Zone.SouthernSanDoriaS, 68, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                });

            // 1: Go to the future to get a ticket
            b.State(current: true, status: 1)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings).OnTrigger(QuestReply.Event(69))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Turlough).OnTrigger(QuestReply.ProgressEvent(10172))
                .OnEventFinish(Zone.UpperJeuno, 10172, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MayakovShowTicket);
                    q.Player.SetMissionStatus(AreaId, 2);
                });

            // 2+: Come back and use your ticket
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) >= 2)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings).OnTrigger(
                    QuestReply.ProgressEvent(70, 1, 0, 2964, 0, 66453367, 8366690, 4095, 131140))
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.Event(152);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.Event(153);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.SouthernSanDoriaS, 152, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(1, 0, 1756, 0, 66453367, 8366690, 4095, 131140);
                    }
                })
                .OnEventUpdate(Zone.SouthernSanDoriaS, 153, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(1, 0, 1756, 0, 66451386, 11124900, 4095, 131140);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 70, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.SetPos(100.801, 1, 103.211, 31, Zone.SouthernSanDoriaS);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 152, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                    q.Player.SetPos(100.801, 1, 103.211, 31, Zone.SouthernSanDoriaS);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 153, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MayakovShowTicket);
                    }
                });
        }
    }
}
