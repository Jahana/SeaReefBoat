// The Immortal Lu Shang (Log ID 5, Quest ID 196), ported from the quest branches of scripts/zones/Rabao/npcs/Irmilant.lua
// (quest rework B3a). Irmilant : !pos 3.78 9.54 56.21 247. The NPC script calls this quest first and Indomitable Spirit second.
// Kept as the Lua file has it: the offer (77) and the trade (78) work again once the quest is Completed, and 77 adds the
// quest whatever its status. 78's npcUtil.completeQuest (Lu Shang's Fishing Rod, the title) is written out step for
// step, ending in the raw player:completeQuest, which keeps the quest's own Quest[5][196] vars.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheImmortalLuShang : QuestScript
    {
        public TheImmortalLuShang() : base(QuestLog.Outlands, "THE_IMMORTAL_LU_SHANG") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Or(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.Rabao, Npc.Rabao.Irmilant).Called()
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status is QuestStatus.Accepted or QuestStatus.Completed &&
                            q.Trade.Has(Item.PieceOfAncientLumber, Item.BrokenLuShangsFishingRod, Item.LightCrystal))
                        {
                            return QuestReply.Event(78);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.BrokenLuShangsFishingRod) &&
                            q.Quest.Status is QuestStatus.Available or QuestStatus.Completed)
                        {
                            return QuestReply.Event(77);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Rabao, 77, q => q.Quest.Begin())
                .OnEventFinish(Zone.Rabao, 78, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[5][196] vars
                    if (q.Player.GiveItem(Item.LuShangsFishingRod))
                    {
                        q.Player.AddTitle(Title.TheImmortalFisherLuShang);
                        q.Player.CompleteQuest(QuestLog.Outlands, "THE_IMMORTAL_LU_SHANG");
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
