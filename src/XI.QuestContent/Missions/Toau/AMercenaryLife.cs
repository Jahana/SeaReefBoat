// A Mercenary Life (ToAU 8, Log ID 4, Mission ID 7), a line-for-line port of
// scripts/missions/toau/08_A_Mercenary_Life.lua (quest rework C3 T1). Two sections, both Aht Urhgan Whitegate.
// Section 1 (the mission current): Naja Salaheem plays 3029 (a plain event; the mercenary rank, and the dialog as its
// eighth operand), the mission Option var cycling 2, 0, 2, 0 (the Lua's altOption is always 0). Section 2 (the mission
// current and its mustZone clear, set by Westerly Winds' completion): trigger area 3 plays 3050; its update keeps the
// topics read in the mission local var Dialog (bit option-1) and answers them with the update 1; its finish completes
// the mission (Undersea Scouting current). Kept as the Lua has it: the update shifts by option-1 unclamped (LuaJIT's
// bit.lshift keeps the low 5 bits of the count, as C#'s << does, so an option of 0 sets bit 31 and 33 sets bit 0), and
// the completion does not test that both topics were read. The SOA era module
// (Modules/Era/Missions/MissionAdjustments.cs) replaces trigger area 3.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class AMercenaryLife : MissionScript
    {
        public AMercenaryLife() : base(XI.Quests.MissionLog.ToAU, "A_MERCENARY_LIFE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/08_A_Mercenary_Life";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.UnderseaScouting,
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

                    return QuestReply.Event(3029, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                });

            b.State(current: true, when: q => !q.Mission.MustZone)
                // NOTE (Lua): Check Naja after zoning
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    return QuestReply.ProgressEvent(3050, 0, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3050, q =>
                {
                    // Both topics must be selected before you can advance.
                    int dialogStatus = q.Mission.LocalVar("Dialog") | SetBit(q.Option - 1);
                    q.Mission.SetLocalVar("Dialog", dialogStatus);

                    q.Player.UpdateEvent(dialogStatus, 1, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3050, q =>
                {
                    q.Mission.Complete();
                });
        }

        // utils.mask.setBit(0, pos, true): bit.lshift(1, pos), the count taken mod 32 in LuaJIT and in C# alike
        static int SetBit(int pos) => 1 << pos;
    }
}
