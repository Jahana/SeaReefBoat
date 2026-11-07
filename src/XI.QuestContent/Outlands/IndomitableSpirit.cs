// Indomitable Spirit (Log ID 5, Quest ID 201), ported from the quest branches of scripts/zones/Rabao/npcs/Irmilant.lua
// (quest rework B3a). Irmilant : !pos 3.78 9.54 56.21 247. The NPC script calls this quest after The Immortal Lu Shang.
// Kept as the Lua file has it: the waiting (133) and the ending (134) follow the char var IndomitableSpiritTimer alone,
// whatever the quest's status; 131, 132 and 134 act on the quest whatever it is. 134's npcUtil.completeQuest
// (the Ebisu Fishing Rod, the title, the timer 0) is written out step for step, ending in the raw
// player:completeQuest, which keeps the quest's own Quest[5][201] vars.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class IndomitableSpirit : QuestScript
    {
        public IndomitableSpirit() : base(QuestLog.Outlands, "INDOMITABLE_SPIRIT") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Or(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.Rabao, Npc.Rabao.Irmilant).Called()
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status is QuestStatus.Accepted or QuestStatus.Completed &&
                            q.Trade.Has(Item.SaberShoot, Item.SpoolOfOpalSilk))
                        {
                            return QuestReply.Event(132);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int timer = q.Player.CharVar("IndomitableSpiritTimer");

                        if (q.Player.HasKeyItem(KeyItem.SerpentRumors) && q.Quest.Status == QuestStatus.Available)
                        {
                            return QuestReply.Event(131);
                        }
                        else if (timer != 0 && timer > q.Player.SystemTime)
                        {
                            return QuestReply.Event(133);
                        }
                        else if (timer != 0)
                        {
                            return QuestReply.Event(134);
                        }
                        else if (q.Quest.Status == QuestStatus.Completed)
                        {
                            return QuestReply.Event(135);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Rabao, 131, q => q.Quest.Begin())
                .OnEventFinish(Zone.Rabao, 132, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetCharVar("IndomitableSpiritTimer", q.Player.NextConquestTally);
                })
                .OnEventFinish(Zone.Rabao, 134, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[5][201] vars
                    if (q.Player.GiveItem(Item.EbisuFishingRod))
                    {
                        q.Player.AddTitle(Title.IndomitableFisher);
                        q.Player.SetCharVar("IndomitableSpiritTimer", 0);
                        q.Player.CompleteQuest(QuestLog.Outlands, "INDOMITABLE_SPIRIT");
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                    }
                });
        }
    }
}
