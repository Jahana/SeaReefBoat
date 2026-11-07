// The Die is Cast (Log ID 6, Quest ID 16), a line-for-line port of scripts/quests/ahtUrhgan/The_Die_is_Cast.lua (quest
// rework B3b Aht Urhgan, group 2). Aht Urhgan Whitegate: Ratihb !pos 75 -6 -137 (no script; default 603), Ekhu Pesshyadha
// !pos -13 2 103 (no script; default 532). Nashmau: Jijiroon !pos 16 0 -33 (no script; default 1). Arrapago Reef: qm9
// !pos 311 -4 170 (no script; default NOTHING_OUT_OF_ORDINARY), Bukki (BUKKI).
// Available (no other requirement): Ratihb 591, whose finish begins it. Accepted, one section per Prog: Prog 0 Ekhu 592
// (Prog 1); Prog 1 Jijiroon 245 (Prog 2); Prog 2 qm9 212 (Prog 3); Prog 3 qm9 pops Bukki (popFromQM, the qm not hidden)
// and answers SPINE_CHILL, or nil when no pop; Bukki's death (Prog 3's section): Prog 4; Prog 4 qm9 213 (Prog 5, the
// Bag of Gold Pieces); Prog 5 Ratihb 593: completed (a Random Ring), the bag taken.
// Kept as the Lua has it: 591 begins it on any option; Bukki's death sets Prog 4 whoever claimed it (Prog 3's section
// only: the handler lives in it).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class TheDieIsCast : QuestScript
    {
        public TheDieIsCast() : base(QuestLog.AhtUrghan, "THE_DIE_IS_CAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/The_Die_is_Cast";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.RandomRing });
            b.Steps(1, 2, 3, 4, 5);

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(QuestReply.ProgressEvent(591))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 591, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.EkhuPesshyadha).OnTrigger(QuestReply.ProgressEvent(592))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 592, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.Nashmau, Npc.Nashmau.Jijiroon).OnTrigger(QuestReply.ProgressEvent(245))
                .OnEventFinish(Zone.Nashmau, 245, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm9).OnTrigger(QuestReply.ProgressEvent(212))
                .OnEventFinish(Zone.ArrapagoReef, 212, q =>
                {
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm9).OnTrigger(q =>
                {
                    if (q.Player.PopFromQm(Mob.ArrapagoReef.BUKKI, 0))
                    {
                        return QuestReply.MessageSpecial(Text.ArrapagoReef.SPINE_CHILL);
                    }
                    return null;
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.Bukki).OnMobDeath(q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm9).OnTrigger(QuestReply.ProgressEvent(213))
                .OnEventFinish(Zone.ArrapagoReef, 213, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.GiveKeyItem(KeyItem.BagOfGoldPieces);
                });

            b.State(QuestStatus.Accepted, step: 5)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(QuestReply.ProgressEvent(593))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 593, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.BagOfGoldPieces);
                    }
                });
        }
    }
}
