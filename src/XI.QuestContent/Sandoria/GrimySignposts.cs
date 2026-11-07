// Grimy Signposts (Log ID 0, Quest ID 66), a line-for-line port of scripts/quests/sandoria/Grimy_Signposts.lua (quest
// rework B3b, San d'Oria group 9). Maugie : !pos 105 2 -16 230; the four Signposts of Jugner Forest (!pos 301 0 419,
// -457 2 -416, -260 0 -23, -73 2 100 104). Offered at San d'Oria fame level 2: Maugie's 45 (option 0 begins it).
// Accepted, Prog under 15: Maugie's 43; each Signpost whose bit of Prog is unset plays its cutscene (9, 8, 7, 6 for
// the first to the fourth Signpost by id, with 1), and option 1 cleans it (SIGNPOST_NEW, its bit set). Prog 15 (all
// four): Maugie's 44 completes it (fame +20, 1500 gil). Completed: Maugie's 42 (replaceDefault). No outside reference.
// Kept as the Lua file has it: the Signpost table maps id offsets 0-3 (Jugner Forest has exactly four Signposts, so
// `data` is never nil); a Prog above 15 matches no section (Maugie's default); a Prog below 0 is under 15 and has
// every bit set (each Signpost's own event).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class GrimySignposts : QuestScript
    {
        // the Lua's info table: the Signpost's offset from ID.npc.SIGNPOST[1] -> its cutscene; the bit is data - 6
        static readonly int[] Info = { 9, 8, 7, 6 };

        public GrimySignposts() : base(QuestLog.Sandoria, "GRIMY_SIGNPOSTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Grimy_Signposts";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Gil = 1500 });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Maugie).OnTrigger(QuestReply.ProgressEvent(45))
                .OnEventFinish(Zone.SouthernSanDoria, 45, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            var signposts = b.State(QuestStatus.Accepted, when: q => q.Quest.Step < 15)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Maugie).OnTrigger(QuestReply.Event(43))
                .Npc(Zone.JugnerForest, Npc.JugnerForest.Signpost).OnTrigger(q =>
                {
                    // local offset = npc:getID() - ID.npc.SIGNPOST[1] (SIGNPOST = GetTableOfIDs('Signpost'), a table of ids)
                    int offset = q.Npc.Id - q.Player.NpcIdsOf("SIGNPOST")[0];
                    int? data = offset >= 0 && offset < Info.Length ? Info[offset] : (int?)null;

                    if (data != null && !q.Quest.IsVarBitsSet("Prog", data.Value - 6))
                    {
                        return QuestReply.ProgressCutscene(data.Value, 1);
                    }
                    return null;
                });

            // onEventFinish [6] to [9]: option 1 cleans the Signpost, bit csid - 6
            for (int csid = 6; csid <= 9; csid++)
            {
                int bit = csid - 6;

                signposts.OnEventFinish(Zone.JugnerForest, csid, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Text.Special(Text.JugnerForest.SIGNPOST_NEW);
                        q.Quest.SetVarBit("Prog", bit);
                    }
                });
            }

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 15)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Maugie).OnTrigger(QuestReply.ProgressEvent(44))
                .OnEventFinish(Zone.SouthernSanDoria, 44, q =>
                {
                    q.Quest.Complete();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Maugie).OnTrigger(QuestReply.Event(42).ReplaceDefault());
        }
    }
}
