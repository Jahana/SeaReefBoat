// What Friends Are For (Log ID 6, Quest ID 19), a line-for-line port of
// scripts/quests/ahtUrhgan/What_Friends_Are_For.lua (quest rework B3b Aht Urhgan, draft). Nashmau: Tsetseroon !pos -13
// -6 69 (no script; default event 4; a second, cutscene-only Tsetseroon 16994406). Aydeewa Subterrane: trigger area 2
// (never registered: Zone.lua registers area 1 only), qm9 !pos -405.9 6.5 -440.2 (no script; default
// NOTHING_OUT_OF_ORDINARY). Available at Prog 0: Aydeewa's area 2: 7 (Prog 1); Tsetseroon 4 (an Event). Available at
// Prog 1: Tsetseroon 16, whose finish sets Prog 2 and begins it. Accepted: Tsetseroon 17 / 19 (Events) / 20 at Prog 2 /
// 3 / 4; at Prog 2 a trade of exactly a Chunk of Tin Ore and a Cobalt Jellyfish: 18 (the trade confirmed, the Pot of
// Tsetseroon's Stew, Prog 3); 20's finish: with the Map of Aydeewa Subterrane, an Imperial Bronze Piece and, when it
// fits, completed; without it, completed and the map. qm9 at Prog 3 with the stew: 8, whose finish sets Prog 4, takes
// the stew and gives the map on option 1. Completed: Tsetseroon 21 (an Event). Kept as the Lua has it: area 2 is never
// registered, so the available section's 7 is unreachable (the flows set Prog 1 by a GM); 16's finish sets Prog 2
// before the begin; the quest has no reward table (complete() is always true); 8 takes the stew on any option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class WhatFriendsAreFor : QuestScript
    {
        public WhatFriendsAreFor() : base(QuestLog.AhtUrghan, "WHAT_FRIENDS_ARE_FOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/What_Friends_Are_For";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4);

            // Section: Quest available
            b.State(QuestStatus.Available, when: q => q.Quest.Step == 0)
                .OnTriggerAreaEnter(Zone.AydeewaSubterrane, 2, q => QuestReply.ProgressEvent(7))
                .OnEventFinish(Zone.AydeewaSubterrane, 7, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Nashmau, Npc.Nashmau.Tsetseroon).OnTrigger(q => QuestReply.Event(4));

            // Section: Quest available, step 2
            b.State(QuestStatus.Available, when: q => q.Quest.Step == 1)
                .Npc(Zone.Nashmau, Npc.Nashmau.Tsetseroon).OnTrigger(q => QuestReply.ProgressEvent(16))
                .OnEventFinish(Zone.Nashmau, 16, q =>
                {
                    q.Quest.SetStep(2);
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Nashmau, Npc.Nashmau.Tsetseroon).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.Event(17);
                    }
                    else if (q.Quest.Step == 3)
                    {
                        return QuestReply.Event(19);
                    }
                    else if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(20);
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (q.Quest.Step == 2 && q.Trade.HasExactly(Item.ChunkOfTinOre, Item.CobaltJellyfish))
                    {
                        return QuestReply.ProgressEvent(18);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 18, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.PotOfTsetseroonsStew);
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.Nashmau, 20, q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapOfAydeewaSubterrane))
                    {
                        if (q.Player.GiveItem(Item.ImperialBronzePiece))
                        {
                            q.Quest.Complete();
                        }
                    }
                    else
                    {
                        if (q.Quest.Complete())
                        {
                            q.Player.GiveKeyItem(KeyItem.MapOfAydeewaSubterrane);
                        }
                    }
                })
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.qm9).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3 && q.Player.HasKeyItem(KeyItem.PotOfTsetseroonsStew))
                    {
                        return QuestReply.ProgressEvent(8);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 8, q =>
                {
                    q.Quest.SetStep(4);
                    q.Player.DelKeyItem(KeyItem.PotOfTsetseroonsStew);
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.MapOfAydeewaSubterrane);
                    }
                });

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.Nashmau, Npc.Nashmau.Tsetseroon).OnTrigger(q => QuestReply.Event(21));
        }
    }
}
