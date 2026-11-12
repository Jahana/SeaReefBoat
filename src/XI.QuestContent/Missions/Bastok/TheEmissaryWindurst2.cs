// The Emissary Windurst, part 2 (Bastok 2-3, Log ID 1, Mission ID 9), a line-for-line port of
// scripts/missions/bastok/2_3_4_The_Emissary_Windurst2.lua (quest rework C3 N2). Kupipi !pos 2 0.1 30 242; Melek !pos
// -80.6 -5.5 157.3 240. One section (this mission current), no reward table. Heaven's Tower: Kupipi at status 7 plays
// 242 (progress, parameters 1 to 4 = 1 and 5 = Bastok's nation; its finish gives the Dark Key and moves to status 8),
// at 8 243 (an event), at 9 244 (progress; its finish moves to status 10), at 10 245 (an event). Balga's Dais: the
// 32001 finish, at status 8 after the Rank 2 Mission battlefield (id 96), gives the Kindred Crest, takes the Dark Key
// and moves to status 9. Port Windurst: at status 7 Ada 63, Gold Skull 62, Josef 65 and Melek 64 (all progress); Gold
// Skull at 8 says GOLD_SKULL_DIALOG + 27 (a message), at 9 plays 57 (progress); Melek at 8 says MELEK_DIALOG_A (a
// message), and otherwise with the Kindred Crest plays 66 (progress; its finish completes the mission, begins The
// Emissary, takes the crest, moves to status 10 and gives the Kindred Report).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheEmissaryWindurst2 : MissionScript
    {
        public TheEmissaryWindurst2() : base(XI.Quests.MissionLog.Bastok, "THE_EMISSARY_WINDURST2") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_3_4_The_Emissary_Windurst2";

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .OnEventFinish(Zone.BalgasDais, 32001, q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Bastok) == 8 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank2Mission)
                    {
                        q.Player.GiveKeyItem(KeyItem.KindredCrest);
                        q.Player.DelKeyItem(KeyItem.DarkKey);
                        q.Player.SetMissionStatus(MissionLog.Bastok, 9);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(242, 1, 1, 1, 1, Nation.Bastok);
                    }
                    else if (missionStatus == 8)
                    {
                        return QuestReply.Event(243);
                    }
                    else if (missionStatus == 9)
                    {
                        return QuestReply.ProgressEvent(244);
                    }
                    else if (missionStatus == 10)
                    {
                        return QuestReply.Event(245);
                    }

                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 242, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.DarkKey);
                    q.Player.SetMissionStatus(MissionLog.Bastok, 8);
                })
                .OnEventFinish(Zone.HeavensTower, 244, q =>
                    q.Player.SetMissionStatus(MissionLog.Bastok, 10))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ada).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 7 ? QuestReply.ProgressEvent(63) : null)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GoldSkull).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(62);
                    }
                    else if (missionStatus == 8)
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.PortWindurst.GOLD_SKULL_DIALOG) + 27);
                    }
                    else if (missionStatus == 9)
                    {
                        return QuestReply.ProgressEvent(57);
                    }

                    return null;
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Josef).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 7 ? QuestReply.ProgressEvent(65) : null)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Melek).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 7)
                    {
                        return QuestReply.ProgressEvent(64);
                    }
                    else if (missionStatus == 8)
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.PortWindurst.MELEK_DIALOG_A));
                    }
                    else if (q.Player.HasKeyItem(KeyItem.KindredCrest))
                    {
                        return QuestReply.ProgressEvent(66);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 66, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                        q.Player.DelKeyItem(KeyItem.KindredCrest);
                        q.Player.SetMissionStatus(MissionLog.Bastok, 10);
                        q.Player.GiveKeyItem(KeyItem.KindredReport);
                    }
                });
        }
    }
}
