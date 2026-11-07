// Warding Vampires (Log ID 0, Quest ID 79), a line-for-line port of scripts/quests/sandoria/Warding_Vampires.lua (quest
// rework B3b, San d'Oria group 1). Maloquedil : !pos 35 0.1 60 231. Available: at San d'Oria fame level 3 24 (option 1
// begins it), below it 21. Accepted: 22; completed: 24 with option [7] = 1 (no finish handler then). Accepted or
// completed, two Bulbs of Shaman Garlic (tradeHas) play 23: while accepted the completion (fame +16, Vampire Hunter
// D-Minus), after it fame +16; then 900 gil and the trade confirmed. Maloquedil's own script answers nothing.
// Kept as the Lua has it: the quest repeats without being taken again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class WardingVampires : QuestScript
    {
        public WardingVampires() : base(QuestLog.Sandoria, "WARDING_VAMPIRES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Warding_Vampires";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 16, FameArea = FameArea.Sandoria, Title = Title.VampireHunterDMinus });

            b.State(QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Maloquedil).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Sandoria) >= 3)
                    {
                        return QuestReply.ProgressEvent(24);
                    }
                    else
                    {
                        return QuestReply.Event(21);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 24, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // status ~= QUEST_AVAILABLE
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Maloquedil)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { xi.item.BULB_OF_SHAMAN_GARLIC, 2 } })
                        if (q.Trade.HasQty((Item.BulbOfShamanGarlic, 2)))
                        {
                            return QuestReply.ProgressEvent(23);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(22);
                        }
                        else
                        {
                            return QuestReply.Event(24).Option(7, 1);
                        }
                    })
                .OnEventFinish(Zone.NorthernSanDoria, 23, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        q.Quest.Complete();
                    }
                    else
                    {
                        q.Player.AddFame(FameArea.Sandoria, 16);
                    }

                    q.Player.GiveCurrency("gil", 900);
                    q.Trade.Confirm();
                });
        }
    }
}
