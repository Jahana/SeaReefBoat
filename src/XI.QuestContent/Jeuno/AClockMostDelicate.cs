// A Clock Most Delicate (Log ID 3, Quest ID 2), a line-for-line port of scripts/quests/jeuno/A_Clock_Most_Delicate.lua
// (quest rework B3b). Collet : !pos -44 0 107 244; _6s2 (the clockmaker's door) : !pos -80 0 104 244. At Jeuno fame
// level 2 Collet's 112 sets Option 1 (then her 186); the door's 119 begins it on option 1, else sets Option 2, after
// which the door's 118 begins it on option 1. Accepted: the door's 117 until the Clock Tower Oil (Deal with Tenshodo's
// reward), then 202: completed (1200 gil, Engineer's Gloves, Professional Loafer, fame +17 in San d'Oria, Bastok and
// Windurst), the oil deleted. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class AClockMostDelicate : QuestScript
    {
        public AClockMostDelicate() : base(QuestLog.Jeuno, "A_CLOCK_MOST_DELICATE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/A_Clock_Most_Delicate";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 1200, Item = Item.EngineersGloves, Title = Title.ProfessionalLoafer });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 2)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s2).OnTrigger(q =>
                {
                    int questOption = q.Quest.Var("Option");

                    if (questOption == 1)
                    {
                        return QuestReply.ProgressEvent(119);
                    }
                    else if (questOption == 2)
                    {
                        return QuestReply.ProgressEvent(118);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Collet).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(112);
                    }
                    else
                    {
                        return QuestReply.Event(186);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 112, q => q.Quest.SetVar("Option", 1))
                .OnEventFinish(Zone.UpperJeuno, 118, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 119, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 2);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s2).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ClockTowerOil))
                    {
                        return QuestReply.ProgressEvent(202);
                    }
                    else
                    {
                        return QuestReply.Event(117);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 202, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 17);
                        q.Player.AddFame(FameArea.Bastok, 17);
                        q.Player.AddFame(FameArea.Windurst, 17);
                        q.Player.DelKeyItem(KeyItem.ClockTowerOil);
                    }
                });
        }
    }
}
