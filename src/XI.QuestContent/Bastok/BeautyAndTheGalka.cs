// Beauty and the Galka (Log ID 1, Quest ID 1), a line-for-line port of scripts/quests/bastok/Beauty_and_the_Galka.lua
// (quest rework B3b). Talib : !pos -101.133 4.649 28.803 236; Parraggoh : !pos -14.467 6.999 55.068 234. Mission 7-2,
// Dalba's hints and Shady Business read its status and keep the read. The Prog var is 1 once Talib's offer is declined.
// Parraggoh's two lines while the logs are missing are drawn with the script RNG (math.randomInt), as the Lua draws
// them. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BeautyAndTheGalka : QuestScript
    {
        public BeautyAndTheGalka() : base(QuestLog.Bastok, "BEAUTY_AND_THE_GALKA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Beauty_and_the_Galka";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Norg, Item = Item.BronzeKnife });

            b.State(QuestStatus.Available)
                // the offer, or 4 once it was declined
                .Npc(Zone.PortBastok, Npc.PortBastok.Talib).OnTrigger(q =>
                    q.Quest.Var("Prog") == 1 ? QuestReply.Event(4) : QuestReply.ProgressEvent(2))
                // option 1 declines it
                .OnEventFinish(Zone.PortBastok, 2, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetVar("Prog", 1);
                    }
                    else
                    {
                        q.Quest.Begin();
                    }
                })
                // once it was declined, Parraggoh asks again
                .Npc(Zone.BastokMines, Npc.BastokMines.Parraggoh).OnTrigger(q =>
                    q.Quest.Var("Prog") == 1 ? QuestReply.ProgressEvent(7) : null)
                .OnEventFinish(Zone.BastokMines, 7, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Talib)
                    .OnTrigger(q => QuestReply.Event(4))
                    .OnTrade(q => !q.Player.HasKeyItem(KeyItem.PalboroughMinesLogs) && q.Trade.Matches((Item.ChunkOfZincOre, 1))
                        ? QuestReply.ProgressEvent(3)
                        : null)
                .OnEventFinish(Zone.PortBastok, 3, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.PalboroughMinesLogs);
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Parraggoh).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.PalboroughMinesLogs))
                    {
                        return QuestReply.ProgressEvent(10, q.Quest.Var("Prog"));
                    }
                    else if (q.Player.RandomInt(1, 100) <= 50)
                    {
                        return QuestReply.Event(8);
                    }
                    else
                    {
                        return QuestReply.Event(9);
                    }
                })
                .OnEventFinish(Zone.BastokMines, 10, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.PalboroughMinesLogs);
                    }
                });

            // new default texts
            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Parraggoh).OnTrigger(QuestReply.Event(12).ReplaceDefault());
        }
    }
}
