// Foiled Ambition (ToAU 24, Log ID 4, Mission ID 23), a line-for-line port of
// scripts/missions/toau/24_Foiled_Ambition.lua (quest rework C3 T2). One section (the mission current), all in Aht
// Urhgan Whitegate. Naja Salaheem plays the plain event 3096 (the mercenary rank, 1, a 1 at the sixth operand and a 2
// at the eighth). Trigger area 3, when the mustZone is clear and VanadielUniqueDay has reached the Timer var (Social
// Graces' finish sets the Timer), plays the progress event 3097 (text table 0); its update answers 78, 1 whatever the
// option; its finish completes the mission (Playing the Part current, five Imperial Gold Pieces, the Karababa's Secret
// Agent title) and, when it did, sets Playing the Part's mustZone local var and the Timer to the next Vana'diel day.
// Kept as the Lua has it: the finish sets the next mission's mustZone as a local var, which getMustZone (a char var)
// never reads, so only the Timer delays the next mission. The ROV era module
// (Modules/Era/Missions/MissionAdjustments.cs) replaces the finish.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class FoiledAmbition : MissionScript
    {
        public FoiledAmbition() : base(XI.Quests.MissionLog.ToAU, "FOILED_AMBITION") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/24_Foiled_Ambition";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Items = new[] { new MissionRewardItem(Item.ImperialGoldPiece, 5) },
                Title = Title.KarababasSecretAgent,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PlayingThePart,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3096, q.Player.MercenaryRank, 1, 0, 0, 0, 1, 0, 2, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    if (!q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                    {
                        return QuestReply.ProgressEvent(3097).TextTable(0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3097, q =>
                {
                    q.Player.UpdateEvent(78, 1, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3097, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetLocalVar("Mission[4][24]mustZone", 1);
                        q.Player.SetCharVar("Mission[4][24]Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });
        }
    }
}
