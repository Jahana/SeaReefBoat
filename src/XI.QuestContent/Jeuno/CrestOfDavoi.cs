// Crest of Davoi (Log ID 3, Quest ID 0), a line-for-line port of scripts/quests/jeuno/Crest_of_Davoi.lua (quest
// rework B3b). Baudin : !pos -75 0 80 244. With the Silver Bell Baudin offers 174 (option 1 begins it); accepted, his
// 175, and a Slice of Coeurl Meat (npcUtil.tradeHas: other items may come with it) plays 171: completed (the Crest of
// Davoi key item), fame +7 in San d'Oria, Bastok and Windurst, the meat taken (confirmTrade). Completed, Baudin answers
// 122 (replaceDefault) until Save My Sister is completed. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class CrestOfDavoi : QuestScript
    {
        public CrestOfDavoi() : base(QuestLog.Jeuno, "CREST_OF_DAVOI") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Crest_of_Davoi";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.CrestOfDavoi });

            b.State(QuestStatus.Available, when: q => q.Player.HasKeyItem(KeyItem.SilverBell))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Baudin).OnTrigger(q => QuestReply.ProgressEvent(174))
                .OnEventFinish(Zone.UpperJeuno, 174, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Baudin)
                    .OnTrade(q => q.Trade.Has(Item.SliceOfCoeurlMeat) ? QuestReply.ProgressEvent(171) : null)
                    .OnTrigger(QuestReply.ProgressEvent(175))
                .OnEventFinish(Zone.UpperJeuno, 171, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Baudin).OnTrigger(q =>
                {
                    if (!q.Player.HasCompletedQuest(QuestLog.Jeuno, "SAVE_MY_SISTER"))
                    {
                        return QuestReply.Event(122).ReplaceDefault();
                    }
                    return null;
                });
        }
    }
}
