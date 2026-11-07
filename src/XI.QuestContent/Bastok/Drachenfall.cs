// Drachenfall (Log ID 1, Quest ID 31), a line-for-line port of scripts/quests/bastok/Drachenfall.lua (quest rework B3b).
// Black Mud : !pos 63.514 7 -0.972 234; Waterfall Base : !pos -217.594 98.644 464.722 106. Black Mud's 101 (Bastok fame
// level 2) gives the Brass Canteen and begins it; the canteen traded at the Waterfall Base becomes a Canteen of
// Drachenfall Water; the water traded to Black Mud plays 103, which completes it. Without either canteen Black Mud's 102
// gives a new Brass Canteen.
// Kept as the Lua file has it: 101 with a full inventory neither gives the canteen nor begins the quest; 102's give is
// unchecked; the Waterfall Base's trade returns no action whatever happens (giveItem's own message only).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Drachenfall : QuestScript
    {
        public Drachenfall() : base(QuestLog.Bastok, "DRACHENFALL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Drachenfall";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Gil = 2000, Title = Title.DrachenfallAscetic });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.BastokMines, Npc.BastokMines.BlackMud).OnTrigger(QuestReply.ProgressEvent(101))
                .OnEventFinish(Zone.BastokMines, 101, q =>
                {
                    if (q.Player.GiveItem(Item.BrassCanteen))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.BlackMud)
                    .OnTrade(q => q.Trade.Matches((Item.CanteenOfDrachenfallWater, 1)) ? QuestReply.ProgressEvent(103) : null)
                    .OnTrigger(q =>
                        !q.Player.FindItem(Item.BrassCanteen) && !q.Player.FindItem(Item.CanteenOfDrachenfallWater)
                            ? QuestReply.ProgressEvent(102)
                            : null)
                .OnEventFinish(Zone.BastokMines, 102, q => q.Player.GiveItem(Item.BrassCanteen))
                .OnEventFinish(Zone.BastokMines, 103, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.WaterfallBase).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.BrassCanteen, 1)) &&
                        q.Player.GiveItem(Item.CanteenOfDrachenfallWater))
                    {
                        q.Trade.Complete();
                    }
                    return null;
                });
        }
    }
}
