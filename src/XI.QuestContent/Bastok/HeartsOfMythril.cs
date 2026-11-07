// Hearts of Mythril (Log ID 1, Quest ID 6), a line-for-line port of scripts/quests/bastok/Hearts_of_Mythril.lua (quest
// rework B2). Elki : !pos -17.087 -0.05 52.745 234; Monument : !pos 300 -62.803 498.2 106. The Lua file has no known bug to
// keep. Its completion sets the mustZone of the next quest, The Eleventh's Hour, not its own.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class HeartsOfMythril : QuestScript
    {
        public HeartsOfMythril() : base(QuestLog.Bastok, "HEARTS_OF_MYTHRIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Hearts_of_Mythril";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.Sitabaki, Title = Title.PursuerOfThePast });

            // the offer: option 0 accepts it, with the bouquet
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMines, Npc.BastokMines.Elki).OnTrigger(QuestReply.ProgressEvent(41))
                .OnEventFinish(Zone.BastokMines, 41, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.BouquetForThePioneers);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Elki).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(42);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 42, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestMustZone(QuestLog.Bastok, "THE_ELEVENTHS_HOUR");
                    }
                })
                // the bouquet laid at the Monument (option 0)
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.Monument).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.BouquetForThePioneers))
                    {
                        return QuestReply.ProgressEvent(11);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthGustaberg, 11, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                        q.Player.DelKeyItem(KeyItem.BouquetForThePioneers);
                    }
                });
        }
    }
}
