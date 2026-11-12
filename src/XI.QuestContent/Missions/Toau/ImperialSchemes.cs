// Imperial Schemes (ToAU 11, Log ID 4, Mission ID 10), a line-for-line port of
// scripts/missions/toau/11_Imperial_Schemes.lua (quest rework C3 T1). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current), in Aht Urhgan Whitegate: Naja plays 3053 (a plain event; the mercenary rank, and the
// dialog as its eighth operand), the mission Option var cycling 2, 0, 2, 0 (the Lua's altOption is always 0). Trigger
// area 6 plays 3070 (text table 0) once the mission's mustZone is clear and the Vana'diel day has reached the mission's
// Timer (set by Astral Waves' finish); its update answers option 2 with 4 as the eighth operand; its finish completes
// the mission (Royal Puppeteer current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class ImperialSchemes : MissionScript
    {
        public ImperialSchemes() : base(XI.Quests.MissionLog.ToAU, "IMPERIAL_SCHEMES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/11_Imperial_Schemes";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.RoyalPuppeteer,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    // Option cycles between 2 and either a 1 or a 0.
                    int dialog = q.Mission.Var("Option");
                    if (dialog == 2)
                    {
                        // Condition for Naja being friendly is unknown.
                        // My character was a BLU with no mercenary rank and got friendly alt dialog.
                        int altOption = 0; // 1 -> Friendly. 0 -> Not.
                        dialog = altOption;
                    }
                    else
                    {
                        dialog = 2;
                    }

                    q.Mission.SetVar("Option", dialog);

                    return QuestReply.Event(3053, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 6, q =>
                {
                    if (!q.Mission.MustZone && q.Player.VanadielUniqueDay >= q.Mission.Var("Timer"))
                    {
                        return QuestReply.ProgressEvent(3070).TextTable(0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3070, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, 4);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3070, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
