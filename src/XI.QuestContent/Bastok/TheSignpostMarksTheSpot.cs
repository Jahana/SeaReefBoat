// The Signpost Marks the Spot (Log ID 1, Quest ID 22), a line-for-line port of
// scripts/quests/bastok/The_Signpost_Marks_the_Spot.lua (quest rework B3b). Nbu Latteh : !pos -114.777 -4 -113.301 235;
// Roh Latteh : !pos -11.823 6.999 -9.249 234; Signpost : !pos -183 65 599 108. Its guard reads Mom, the Adventurer's
// status and mustZone. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheSignpostMarksTheSpot : QuestScript
    {
        public TheSignpostMarksTheSpot() : base(QuestLog.Bastok, "THE_SIGNPOST_MARKS_THE_SPOT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Signpost_Marks_the_Spot";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.LinenRobe, Fame = 20, FameArea = FameArea.Bastok, Title = Title.TreasureScavenger });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Bastok) >= 2 &&
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "MOM_THE_ADVENTURER") &&
                    !q.Player.QuestMustZone(QuestLog.Bastok, "MOM_THE_ADVENTURER"))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.NbuLatteh).OnTrigger(QuestReply.ProgressEvent(235))
                // option 0 accepts it
                .OnEventFinish(Zone.BastokMarkets, 235, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.Signpost3).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PaintingOfAWindmill))
                    {
                        q.Text.Special(Text.KonschtatHighlands.SIGNPOST3_DIALOG_2);

                        return QuestReply.KeyItem(KeyItem.PaintingOfAWindmill);
                    }

                    return null;
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.RohLatteh).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.PaintingOfAWindmill) ? QuestReply.ProgressEvent(96) : null)
                .OnEventFinish(Zone.BastokMines, 96, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.PaintingOfAWindmill);
                    }
                });
        }
    }
}
