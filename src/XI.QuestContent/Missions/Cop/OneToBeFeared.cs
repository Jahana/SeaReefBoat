// One to be Feared (Promathia 6-4, Log ID 6), a line-for-line port of scripts/missions/cop/6_4_One_to_be_Feared.lua
// (quest rework C3). The mission current. Metalworks' Cid plays 856 at Status 0 (Status 1). Tavnazian Safehold's
// Despachiaire, Ironclad Gorilla and Justinius play 318, 306 and 267 whatever the status. Sealion's Den: the zone-in
// plays 15 at Status 1 (update with option 0: updateEvent(0, 0, 0, 0, 32, 2, 0, 0); finish: Status 2) and 33 at Status
// 4 (its finish completes the mission and sets the char down in Lufaise Meadows); Sueleen plays 24 and the Iron Gate 31
// at Status 2 (finish 31: Status 3). The 32001 finish with the One to be Feared battlefield won
// (battlefield.one_to_be_feared sets battlefieldWin) at Status 3 sets Status 4 and the char down in Sealion's Den so
// that the zone-in plays 33. Once complete the Ironclad Gorilla plays 306 as the replacing event. Chains and Bonds is
// next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class OneToBeFeared : MissionScript
    {
        public OneToBeFeared() : base(XI.Quests.MissionLog.CoP, "ONE_TO_BE_FEARED") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/6_4_One_to_be_Feared";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.ChainsAndBonds,
            });

            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(856) : null)
                .OnEventFinish(Zone.Metalworks, 856, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(QuestReply.Event(318))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.IroncladGorilla).OnTrigger(QuestReply.Event(306))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(QuestReply.Event(267))
                .Npc(Zone.SealionsDen, Npc.SealionsDen._0w0).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.ProgressEvent(31) : null)
                .Npc(Zone.SealionsDen, Npc.SealionsDen.Sueleen).OnTrigger(q =>
                    q.Mission.Var("Status") == 2 ? QuestReply.Event(24) : null)
                .OnZoneIn(Zone.SealionsDen, q =>
                {
                    int status = q.Mission.Var("Status");

                    if (status == 1)
                    {
                        return QuestReply.Event(15);
                    }

                    return status == 4 ? QuestReply.Event(33) : null;
                })
                .OnEventUpdate(Zone.SealionsDen, 15, q =>
                {
                    if (q.Option == 0) q.Player.UpdateEvent(0, 0, 0, 0, 32, 2, 0, 0);
                })
                .OnEventFinish(Zone.SealionsDen, 15, q => q.Mission.SetVar("Status", 2))
                .OnEventFinish(Zone.SealionsDen, 31, q => q.Mission.SetVar("Status", 3))
                .OnEventFinish(Zone.SealionsDen, 33, q =>
                {
                    q.Mission.Complete();
                    q.Player.SetPos(407.389, -0.262, -72.964, 96, Zone.LufaiseMeadows);
                })
                .OnEventFinish(Zone.SealionsDen, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.OneToBeFeared &&
                        q.Mission.Var("Status") == 3)
                    {
                        q.Mission.SetVar("Status", 4);
                        q.Player.SetPos(612.057, 132.664, 776.920, 188, Zone.SealionsDen);
                    }
                });

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.OneToBeFeared))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.IroncladGorilla)
                    .OnTrigger(QuestReply.Event(306).ReplaceDefault());
        }
    }
}
