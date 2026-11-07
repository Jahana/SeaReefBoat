// Cid's Secret (Log ID 1, Quest ID 26), a line-for-line port of scripts/quests/bastok/Cids_Secret.lua (quest rework
// B3b). Cid : !pos -12 -12 1 237; Hilda : !pos -163 -8 13 236. Hilda's 132 sets Prog 1; the Rolanberry (874 C.E.) then
// buys the Unfinished Letter, which Cid takes for the reward. Once completed Hilda answers 49 (replaceDefault), which
// The Usual leaves to her whenever it does not act. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class CidsSecret : QuestScript
    {
        public CidsSecret() : base(QuestLog.Bastok, "CIDS_SECRET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Cids_Secret";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Bastok, Item = Item.RamMantle });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 4)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.ProgressEvent(507))
                .OnEventFinish(Zone.Metalworks, 507, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.UnfinishedLetter))
                    {
                        return QuestReply.ProgressEvent(509);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.Event(508).ImportantEvent();
                    }
                    else
                    {
                        return QuestReply.Event(502).ImportantEvent();
                    }
                })
                .OnEventFinish(Zone.Metalworks, 509, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.UnfinishedLetter);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Hilda)
                    .OnTrade(q => q.Quest.Step == 1 && q.Trade.Matches((Item.Rolanberry874Ce, 1)) ? QuestReply.ProgressEvent(133) : null)
                    .OnTrigger(QuestReply.ProgressEvent(132))
                .OnEventFinish(Zone.PortBastok, 132, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 133, q =>
                {
                    q.Trade.Complete();

                    q.Player.GiveKeyItem(KeyItem.UnfinishedLetter);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Hilda).OnTrigger(QuestReply.Event(49).ReplaceDefault());
        }
    }
}
