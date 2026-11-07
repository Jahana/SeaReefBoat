// Thick Shells (Log ID 0, Quest ID 117), ported from the quest branches of scripts/zones/Port_San_dOria/npcs/Vounebariont.lua
// (quest rework B3a). Vounebariont : !pos -1.606 -13 -148.93 232. The NPC calls the quest and keeps its own event 568.
// Kept as the Lua file has it: the trade of 5 beetle shells works again once the quest is Completed (16 fame, the title and
// 750 gil each time). The completion is the raw player:completeQuest, which keeps the quest's own Quest[0][117] vars.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ThickShells : QuestScript
    {
        public ThickShells() : base(QuestLog.Sandoria, "THICK_SHELLS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Or(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Vounebariont).Called()
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status != QuestStatus.Available)
                        {
                            if (q.Trade.HasItemQty(Item.BeetleShell, 5) && q.Trade.ItemCount == 5)
                            {
                                return QuestReply.Event(514);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q => q.Player.FameLevel(FameArea.Sandoria) >= 2 ? QuestReply.Event(516) : null)
                .OnEventFinish(Zone.PortSanDoria, 516, q =>
                {
                    if (q.Quest.Status == QuestStatus.Available)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 514, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][117] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "THICK_SHELLS");
                        q.Player.AddFame(FameArea.Sandoria, 16);
                    }
                    else
                    {
                        q.Player.AddFame(FameArea.Sandoria, 16);
                    }

                    q.Trade.Complete();
                    q.Player.AddTitle(Title.BugCatcher);
                    q.Player.GiveCurrency("gil", 750);
                });
        }
    }
}
