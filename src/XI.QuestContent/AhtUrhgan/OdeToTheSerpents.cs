// Ode to the Serpents (Log ID 6, Quest ID 44), a line-for-line port of
// scripts/quests/ahtUrhgan/Ode_to_the_Serpents.lua (quest rework B3b Aht Urhgan group 6). Aht Urhgan Whitegate:
// Fari-Wari !pos 80 -6 -137 (no script; default event 831).
// Available with Saga of the Skyserpent completed: Fari-Wari 882, whose finish begins it and gives Biyaada's Letter.
// Accepted: Fari-Wari 883 once When the Bow Breaks and Fist of the People are both completed, else 891 (an Event);
// 883's finish completes it (an Imperial Gold Piece) and, when it does, takes the letter.
// Kept as the Lua has it: 882 begins it on any option (the letter held already: no message).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class OdeToTheSerpents : QuestScript
    {
        public OdeToTheSerpents() : base(QuestLog.AhtUrghan, "ODE_TO_THE_SERPENTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Ode_to_the_Serpents";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialGoldPiece,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "SAGA_OF_THE_SKYSERPENT"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.ProgressEvent(882))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 882, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.BiyaadasLetter);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(q =>
                {
                    if (q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "WHEN_THE_BOW_BREAKS") &&
                        q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "FIST_OF_THE_PEOPLE"))
                    {
                        return QuestReply.ProgressEvent(883);
                    }
                    else
                    {
                        return QuestReply.Event(891);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 883, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.BiyaadasLetter);
                    }
                });
        }
    }
}
