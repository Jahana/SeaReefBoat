// Missionary Moblin (Log ID 4, Quest ID 103), a line-for-line port of scripts/quests/otherAreas/Missionary_Moblin.lua
// (quest rework B3b, Other Areas group 8). Oldton Movalpolos: Koblakiq !pos -64.851 21.834 -117.521 (no script; the
// default event 13).
// Available (no other condition): Koblakiq 7 (progressCutscene), whose finish begins the quest on option 1. Accepted:
// his trigger: 8 (event); a trade holding a Soiled Letter (npcUtil.tradeHas: at least it, other items allowed): 9
// (progressCutscene), whose finish runs quest:complete (4000 gil) and, when it answers true, confirmTrade and the
// quest's mustZone. Completed with the mustZone set: Koblakiq 12 (event); once the char has zoned the state is off (For
// the Birds' offer reads this quest's mustZone).
// Kept as the Lua has it: the trade's tradeHas is not exact (a Soiled Letter with anything else plays 9, and 9's
// confirmTrade takes the letter only); a gil reward cannot fail, so complete's false arm (no confirmTrade, no mustZone)
// is not reachable on Lua (XI.Verify.Quests `oa g8 complete` takes it on the fake).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class MissionaryMoblin : QuestScript
    {
        public MissionaryMoblin() : base(QuestLog.OtherAreas, "MISSIONARY_MOBLIN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Missionary_Moblin";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 4000,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq).OnTrigger(QuestReply.ProgressCutscene(7))
                .OnEventFinish(Zone.OldtonMovalpolos, 7, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq)
                .OnTrade(q =>
                {
                    if (q.Trade.Has(Item.SoiledLetter))
                    {
                        return QuestReply.ProgressCutscene(9);
                    }
                    return null;
                })
                .OnTrigger(q => QuestReply.Event(8))
                .OnEventFinish(Zone.OldtonMovalpolos, 9, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.MustZone)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq).OnTrigger(QuestReply.Event(12));
        }
    }
}
