// Journey to Bastok, part 1 (San d'Oria 2-3, Log ID 0, Mission ID 6), a line-for-line port of
// scripts/missions/sandoria/2_3_1_Journey_to_Bastok.lua (quest rework C3 N2). Grohm !pos -18 -11 -27 237; Pius
// !pos 99 -21 -12 237; Savae E Paleade !pos 23.724 -17.39 -43.360 237. One section (this mission current), no reward
// table, all in Metalworks. Grohm: with the mission's Option var 1 plays 425 (progress), else at status 4 423
// (progress), at status 5 without the Ounce of Mythril Sand 424 (an event), else 422 (an event). Pius: at status 3 355
// (progress), else 356. Savae E Paleade: a trade of one Mythril Sand at status 5 plays 205 (progress). Finishes: 205
// completes the mission, takes the trade, moves to status 6 and begins Journey Abroad; 355 moves to status 4; 423 gives
// three Pickaxes and moves to status 5, or with a full bag sets Option to 1; 425 gives the Pickaxes, moves to status 5
// and sets Option to 0.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class JourneyToBastok : MissionScript
    {
        public JourneyToBastok() : base(XI.Quests.MissionLog.Sandoria, "JOURNEY_TO_BASTOK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_3_1_Journey_to_Bastok";

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Grohm).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(425);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(423);
                    }
                    else if (missionStatus == 5 && !q.Player.HasItem(Item.OnzOfMythrilSand))
                    {
                        return QuestReply.Event(424);
                    }

                    return QuestReply.Event(422);
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Pius).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 3)
                    {
                        return QuestReply.ProgressEvent(355);
                    }

                    return QuestReply.Event(356);
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.SavaeEPaleade).OnTrade(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) == 5 &&
                        q.Trade.Matches((Item.OnzOfMythrilSand, 1)))
                    {
                        return QuestReply.ProgressEvent(205);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 205, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 6);
                        q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 355, q =>
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 4))
                .OnEventFinish(Zone.Metalworks, 423, q =>
                {
                    if (!q.Player.GiveItems((Item.Pickaxe, 3)))
                    {
                        q.Mission.SetVar("Option", 1);
                    }
                    else
                    {
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 5);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 425, q =>
                {
                    if (q.Player.GiveItems((Item.Pickaxe, 3)))
                    {
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 5);
                        q.Mission.SetVar("Option", 0);
                    }
                });
        }
    }
}
