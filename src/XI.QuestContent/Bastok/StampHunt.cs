// Stamp Hunt (Log ID 1, Quest ID 16), a line-for-line port of scripts/quests/bastok/Stamp_Hunt.lua (quest rework B3b).
// Arawn : !pos -121.492 -4.000 -123.923 235; Pavel : !pos -349.798 -10.002 -181.296 235; Deadly Spider : !pos -57.601
// -8 -55.944 234; Tall Mountain : !pos 71 7 -7 234; Elayne : !pos -36.414 8 -1.465 237; Romualdo : !pos 135.035
// -18.475 -37.419 237; Ehrhard : !pos -70.661 4.898 44.886 236; Latifah : !pos 51.241 7.499 -55.407 236. Arawn's 225
// gives the Stamp Sheet and begins it (no guard). Seven NPCs stamp it once each (their bit of Prog: Pavel 0, Deadly
// Spider 1, Tall Mountain 2, Elayne 3, Romualdo 4, Ehrhard 5, Latifah 6); with all seven Arawn's 226 completes it and
// takes the sheet. Kept as the Lua file has it: npcUtil.giveKeyItem always returns true, so 225's guard never fails.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class StampHunt : QuestScript
    {
        public StampHunt() : base(QuestLog.Bastok, "STAMP_HUNT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Stamp_Hunt";

        // progressOnUnsetBit
        static QuestReply ProgressOnUnsetBit(IQuestContext q, int bitValue, int eventId)
        {
            if (!q.Quest.IsVarBitsSet("Prog", bitValue))
            {
                return QuestReply.ProgressEvent(eventId);
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.LeatherGorget,
                Title = Title.Stampeder,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Arawn).OnTrigger(QuestReply.ProgressEvent(225))
                .OnEventFinish(Zone.BastokMarkets, 225, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.StampSheet))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Arawn).OnTrigger(q =>
                    q.Quest.IsVarBitsSet("Prog", 0, 1, 2, 3, 4, 5, 6) ? QuestReply.ProgressEvent(226) : null)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Pavel).OnTrigger(q => ProgressOnUnsetBit(q, 0, 227))
                .OnEventFinish(Zone.BastokMarkets, 226, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.StampSheet);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 227, q => q.Quest.SetVarBit("Prog", 0))
                .Npc(Zone.BastokMines, Npc.BastokMines.DeadlySpider).OnTrigger(q => ProgressOnUnsetBit(q, 1, 86))
                .Npc(Zone.BastokMines, Npc.BastokMines.TallMountain).OnTrigger(q => ProgressOnUnsetBit(q, 2, 85))
                .OnEventFinish(Zone.BastokMines, 85, q => q.Quest.SetVarBit("Prog", 2))
                .OnEventFinish(Zone.BastokMines, 86, q => q.Quest.SetVarBit("Prog", 1))
                .Npc(Zone.Metalworks, Npc.Metalworks.Elayne).OnTrigger(q => ProgressOnUnsetBit(q, 3, 725))
                .Npc(Zone.Metalworks, Npc.Metalworks.Romualdo).OnTrigger(q => ProgressOnUnsetBit(q, 4, 726))
                .OnEventFinish(Zone.Metalworks, 725, q => q.Quest.SetVarBit("Prog", 3))
                .OnEventFinish(Zone.Metalworks, 726, q => q.Quest.SetVarBit("Prog", 4))
                .Npc(Zone.PortBastok, Npc.PortBastok.Ehrhard).OnTrigger(q => ProgressOnUnsetBit(q, 5, 121))
                .Npc(Zone.PortBastok, Npc.PortBastok.Latifah).OnTrigger(q => ProgressOnUnsetBit(q, 6, 120))
                .OnEventFinish(Zone.PortBastok, 120, q => q.Quest.SetVarBit("Prog", 6))
                .OnEventFinish(Zone.PortBastok, 121, q => q.Quest.SetVarBit("Prog", 5));
        }
    }
}
