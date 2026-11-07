// Northward (Log ID 3, Quest ID 24), a line-for-line port of scripts/quests/jeuno/Northward.lua (quest rework B3b,
// Jeuno group 13). Radeivepart : !pos 5 9 -39 243. Available at Jeuno fame level 4: Radeivepart's 159 ([0] = 1, [7] =
// 8); its finish with option 1 begins it. Accepted: 159 ([0] = 2, [7] = 8); a Flame Degen traded alone: 61, whose
// finish completes it (2000 exp, 2000 gil, the Map of Castle Zvahl, Envoy to the North), fame +13 in the three nations
// and takes the degen. Completed: 159 ([0] = 3, [7] = 8), replaceDefault.
// Radeivepart has no script; his default is 159 too (no options), so below fame level 4 his 159 finish finds no
// handler.
// Its era module (src/XI.QuestContent/Modules/Era/Jeuno/Northward.cs) drops the exp and the gil from the reward.
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class Northward : QuestScript
    {
        public Northward() : base(QuestLog.Jeuno, "NORTHWARD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Northward";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfCastleZvahl,
                Title = Title.EnvoyToTheNorth,
            });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 4)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Radeivepart).OnTrigger(QuestReply.ProgressEvent(159).Option(0, 1).Option(7, 8))
                .OnEventFinish(Zone.RuludeGardens, 159, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Radeivepart)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.FlameDegen))
                        {
                            return QuestReply.ProgressEvent(61);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(159).Option(0, 2).Option(7, 8))
                .OnEventFinish(Zone.RuludeGardens, 61, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Radeivepart).OnTrigger(QuestReply.Event(159).Option(0, 3).Option(7, 8).ReplaceDefault());
        }
    }
}
