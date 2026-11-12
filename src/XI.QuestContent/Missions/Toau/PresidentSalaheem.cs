// President Salaheem (ToAU 3, Log ID 4, Mission ID 2), a line-for-line port of
// scripts/missions/toau/03_President_Salaheem.lua (quest rework C3 T1). Naja Salaheem !pos 22.700 -8.804 -45.591 50.
// One section (the mission current): Naja at mission status 1 plays 3020 (text table 0) when the mission's mustZone is
// clear, else 3003 (the default dialog, the mercenary rank as its option 0); at any other status he plays 73 (the Mog
// Locker scam), whose finish sets status 1 and the mission's mustZone. 3020's finish completes the mission (Knight of
// Gold current). Kept as the Lua has it: the mustZone is set after the 73 (a TODO in the Lua says it is not certain).
// The SOA era module (Modules/Era/Missions/MissionAdjustments.cs) replaces the trigger and the 73 finish.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PresidentSalaheem : MissionScript
    {
        public PresidentSalaheem() : base(XI.Quests.MissionLog.ToAU, "PRESIDENT_SALAHEEM") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/03_President_Salaheem";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.KnightOfGold,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        // TODO (Lua): This isn't necesarily true. We have proof showing that is both needed and not
                        // needed. More info is necesary.
                        if (!q.Mission.MustZone)
                        {
                            // Trivia: There is a copy of this CS, but as a flashback, with Falzum instead of your
                            // character. CS: 3030
                            return QuestReply.ProgressEvent(3020).TextTable(0); // Enter Not-Trion.
                        }

                        // Default Dialog.
                        return QuestReply.Event(3003).Option(0, q.Player.MercenaryRank).TextTable(0);
                    }

                    return QuestReply.ProgressEvent(73).TextTable(0); // Mog Locker scam.
                })
                // NOTE (Lua): Wikis are wrong, the CS with Rytaal is unlocked by this mission, but isn't needed at all
                // to progress it.
                .OnEventFinish(Zone.AhtUrhganWhitegate, 73, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Mission.SetMustZone();
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3020, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
