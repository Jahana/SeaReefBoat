// A Flash in the Pan (Log ID 1, Quest ID 14), a line-for-line port of scripts/quests/bastok/A_Flash_in_the_Pan.lua (quest
// rework B3b). Aquillina : !pos -97 -5 -81 235. Aquillina's 217 begins it; four Flint Stones traded play 219, which
// completes it (100 gil, fame +10), takes the stones and sets her local var tradeCooldown to now + 900 s; while it runs,
// any trade plays 218 and her trigger falls to her default. Repeatable: after completion the same trade completes it
// again, and her trigger is a plain 217.
// Kept as the Lua file has it: 219 needs no earlier talk (the available state takes the trade too); the cooldown is on the
// NPC, so it holds for every char; the cooldown check comes before the trade match, so any trade (even a wrong one)
// during the cooldown plays 218.
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): the lookup checks each state in turn, so a 219
// started while available runs the available state's finish (the completion) and then the completed state's 219 finish
// too, which completed the quest again (100 gil and fame +10 twice). The available state's finish sets the quest's
// local var firstComplete; the other state's finish then only clears it, as The Sweetest Things' latch does, so it pays
// once.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class AFlashInThePan : QuestScript
    {
        public AFlashInThePan() : base(QuestLog.Bastok, "A_FLASH_IN_THE_PAN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/A_Flash_in_the_Pan";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 100 });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Aquillina)
                    .OnTrade(Trade)
                    .OnTrigger(q =>
                        q.Npc.LocalVar("tradeCooldown") <= q.Player.SystemTime ? QuestReply.ProgressEvent(217) : null)
                .OnEventFinish(Zone.BastokMarkets, 217, q => q.Quest.Begin())
                .OnEventFinish(Zone.BastokMarkets, 219, q =>
                {
                    if (Finish219(q))
                    {
                        // the latch: the completed state's 219 finish runs next in this lookup (user ruling 2026-11-05)
                        q.Quest.SetLocalVar("firstComplete", 1);
                    }
                });

            // status ~= QUEST_AVAILABLE: accepted and completed
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Aquillina)
                    .OnTrade(Trade)
                    .OnTrigger(q =>
                        q.Npc.LocalVar("tradeCooldown") <= q.Player.SystemTime ? QuestReply.Event(217) : null)
                .OnEventFinish(Zone.BastokMarkets, 219, q =>
                {
                    // right after the available state's completion: paid already (user ruling 2026-11-05)
                    if (q.Quest.LocalVar("firstComplete") == 1)
                    {
                        q.Quest.SetLocalVar("firstComplete", 0);
                    }
                    else
                    {
                        Finish219(q);
                    }
                });
        }

        // both sections' onTrade, the same in each
        static QuestReply Trade(IQuestContext q)
        {
            if (q.Npc.LocalVar("tradeCooldown") > q.Player.SystemTime)
            {
                return QuestReply.Event(218);
            }

            if (q.Trade.Matches((Item.FlintStone, 4)))
            {
                return QuestReply.ProgressEvent(219); // She does NOT care if you ever spoke to her.
            }
            return null;
        }

        // both sections' 219 finish, the same in each; true when the quest completed
        static bool Finish219(IQuestContext q)
        {
            if (q.Quest.Complete())
            {
                q.Trade.Complete();
                // GetNPCByID(bastokMarketsID.npc.AQUILLINA): the Lua names her by her IDs.lua key (no generated constant for
                // an IDs.lua npc key: the string)
                q.Player.SetNpcLocalVar("AQUILLINA", "tradeCooldown", q.Player.SystemTime + 900);
                return true;
            }
            return false;
        }
    }
}
