// Passing Glory (ToAU 18, Log ID 4, Mission ID 17), a line-for-line port of scripts/missions/toau/18_Passing_Glory.lua
// (quest rework C3 T2). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One section (the mission current). The mission is
// ready when its mustZone is clear and VanadielUniqueDay has reached the Timer var (Guests of the Empire's finish sets
// the Timer). Kept as the Lua has it: that finish sets this mission's mustZone as a local var, which getMustZone (a
// char var) never reads, so only the Timer delays the mission. Naja, ready, plays the progress event 3090 (the
// mercenary rank, and 1); not ready he plays the plain event 3079, its eighth operand cycling 2, 0, 2, 0 through the
// Option var. The Whitegate's trigger area 3, ready, plays 3090 (text table 0). 3090's update with option 1 answers
// with the rank, 1 and a 1 at the seventh operand; its finish completes the mission (Sweets for the Soul current). The
// ROV era module (Modules/Era/Missions/MissionAdjustments.cs) replaces the section's check.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PassingGlory : MissionScript
    {
        public PassingGlory() : base(XI.Quests.MissionLog.ToAU, "PASSING_GLORY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/18_Passing_Glory";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.SweetsForTheSoul,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (!q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                    {
                        return QuestReply.ProgressEvent(3090, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                    }

                    // Option cycles between 2 and either a 1 or a 0.
                    int dialog = q.Mission.Var("Option");
                    if (dialog == 2)
                    {
                        // Condition for Naja being friendly is unknown. Probably the same as ToAU 8.
                        int altOption = 0; // 1 -> Friendly. 0 -> Not.
                        dialog = altOption;
                    }
                    else
                    {
                        dialog = 2;
                    }

                    q.Mission.SetVar("Option", dialog);

                    return QuestReply.Event(3079, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    if (!q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                    {
                        return QuestReply.ProgressEvent(3090).TextTable(0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3090, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(q.Player.MercenaryRank, 1, 0, 0, 0, 0, 1, 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3090, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
