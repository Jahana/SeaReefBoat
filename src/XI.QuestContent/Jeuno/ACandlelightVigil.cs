// A Candlelight Vigil (Log ID 3, Quest ID 6), a line-for-line port of scripts/quests/jeuno/A_Candlelight_Vigil.lua
// (quest rework B3b). Ilumida : !pos -75 -1 58 244. From Jeuno fame level 4 Ilumida offers 192 while Option is 0
// (option 1: begun; any other: Option 1), then 193 while Option is 1 (option 1: begun, Option 0; else nothing).
// Accepted: 191 without the Holy Candle (Candle Making's reward), 194 with it: completed (Flower Necklace, Activist for
// Kindness), fame +13 x3, the candle taken, and Searching for the Right Words must wait for a zone change. The Lua file
// has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ACandlelightVigil : QuestScript
    {
        public ACandlelightVigil() : base(QuestLog.Jeuno, "A_CANDLELIGHT_VIGIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/A_Candlelight_Vigil";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.FlowerNecklace, Title = Title.ActivistForKindness });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 4)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ilumida).OnTrigger(q =>
                {
                    int questOption = q.Quest.Var("Option");

                    if (questOption == 0)
                    {
                        return QuestReply.ProgressEvent(192);
                    }
                    else if (questOption == 1)
                    {
                        return QuestReply.ProgressEvent(193);
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 192, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 193, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetVar("Option", 0);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Ilumida).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.HolyCandle))
                    {
                        return QuestReply.ProgressEvent(194);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(191);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 194, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                        q.Player.DelKeyItem(KeyItem.HolyCandle);
                        q.Player.SetQuestMustZone(QuestLog.Jeuno, "SEARCHING_FOR_THE_RIGHT_WORDS");
                    }
                });
        }
    }
}
