// Rock Bottom (Log ID 6, Quest ID 20), a line-for-line port of scripts/quests/ahtUrhgan/Rock_Bottom.lua (quest rework
// B3b Aht Urhgan group 6). Mount Zhayolm: blank11 !pos 838.2 -14.5 231.9 (no script; default NOTHING_HAPPENS).
// Available (no other condition): blank11 7, whose finish sets Prog 1 and begins it. Accepted: blank11's talk:
// NOTHING_HAPPENS; a trade at Prog 1 matching one Pickaxe: 8 (the pickaxe taken, Prog 2, the zoning flag); a trade at
// Prog 2, not needing to zone, matching one Mythril Pick or one Mythril Pick +1: 9 ([0] the traded item's id), whose
// finish completes it and, when it does, takes the trade and gives the Map of Mount Zhayolm, or 2000 gil (times
// GIL_RATE: npcUtil.giveCurrency) when the map is held.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 8's finish takes the traded pickaxe
// (tradeComplete, as 9's finish takes the pick); the Lua's 8 confirmed no trade, so the pickaxe was kept.
// Kept as the Lua has it: 7's finish begins it on any option; 9's completion cannot fail (no reward table, so its
// else is never taken); 9's [0] is trade:getItemId(), the item in the trade's first slot (Trade.ItemId(0)): the
// matched pick, the only item traded. No completed section: blank11 answers with its default NOTHING_HAPPENS, the same
// message as the accepted talk.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class RockBottom : QuestScript
    {
        public RockBottom() : base(QuestLog.AhtUrghan, "ROCK_BOTTOM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Rock_Bottom";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.blank11).OnTrigger(q => QuestReply.ProgressEvent(7))
                .OnEventFinish(Zone.MountZhayolm, 7, q =>
                {
                    q.Quest.SetStep(1);
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.blank11).OnTrade(q =>
                {
                    if (q.Quest.Step == 1 &&
                        q.Trade.Matches((Item.Pickaxe, 1)))
                    {
                        return QuestReply.ProgressEvent(8);
                    }
                    else if (!q.Player.NeedToZone &&
                             q.Quest.Step == 2 &&
                             (q.Trade.Matches((Item.MythrilPick, 1)) ||
                              q.Trade.Matches((Item.MythrilPickHq, 1))))
                    {
                        // trade:getItemId(): the item in the trade's first slot (slot 0), the one matched pick
                        return QuestReply.ProgressEvent(9).Option(0, q.Trade.ItemId(0));
                    }
                    return null;
                })
                .OnTrigger(q => QuestReply.MessageSpecial(Text.MountZhayolm.NOTHING_HAPPENS))
                .OnEventFinish(Zone.MountZhayolm, 8, q =>
                {
                    // the pickaxe taken, as 9 takes the pick (the Lua took nothing: fixed, user ruling 2026-11-04)
                    q.Trade.Complete();
                    q.Quest.SetStep(2);
                    q.Player.SetNeedToZone(true);
                })
                .OnEventFinish(Zone.MountZhayolm, 9, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        if (q.Player.HasKeyItem(KeyItem.MapOfMountZhayolm))
                        {
                            q.Player.GiveCurrency("gil", 2000);
                        }
                        else
                        {
                            q.Player.GiveKeyItem(KeyItem.MapOfMountZhayolm);
                        }
                    }
                });
        }
    }
}
