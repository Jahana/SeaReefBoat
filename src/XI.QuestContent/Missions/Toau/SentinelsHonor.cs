// Sentinels' Honor (ToAU 33, Log ID 4, Mission ID 32), a line-for-line port of
// scripts/missions/toau/33_Sentinels_Honor.lua (quest rework C3 T3). One section (the mission current), all in Aht
// Urhgan Whitegate. Naja Salaheem: hasZoned (the mustZone is clear) and hasTimePassed (VanadielUniqueDay has reached
// the Timer var; In the Blood's finish sets it) both true plays the plain event 3130, else 3120. The dialog (the
// eighth operand) is 2, or while the time has not passed the Option var, which then flips between 0 and 2 (0 to 2,
// anything else to 0). 3130's finish completes the mission (Testing the Waters current). The ROV era module
// (Modules/Era/Missions/MissionAdjustments.cs) replaces Naja's trigger.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class SentinelsHonor : MissionScript
    {
        public SentinelsHonor() : base(XI.Quests.MissionLog.ToAU, "SENTINELS_HONOR") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/33_Sentinels_Honor";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.TestingTheWaters,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    bool hasZoned = !q.Mission.MustZone;
                    bool hasTimePassed = q.Player.VanadielUniqueDay >= q.Mission.Var("Timer");

                    int eventId = (hasZoned && hasTimePassed) ? 3130 : 3120;
                    int dialog = 2;
                    // Dialog parameter cycles between 0 and 2 until time lockout is over. At that point, option 0
                    // stops happening.
                    if (!hasTimePassed)
                    {
                        dialog = q.Mission.Var("Option");
                        if (dialog == 0)
                        {
                            q.Mission.SetVar("Option", 2);
                        }
                        else
                        {
                            q.Mission.SetVar("Option", 0);
                        }
                    }

                    return QuestReply.Event(eventId, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3130, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
