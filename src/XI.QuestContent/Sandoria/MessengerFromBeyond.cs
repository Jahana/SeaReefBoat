// Messenger From Beyond (Log ID 0, Quest ID 87, WHM AF1), a line-for-line port of scripts/quests/sandoria/Messenger_From_Beyond.lua
// (quest rework B3b, San d'Oria group 4). Narcheral : !pos 129 -11 126 231; qm2 : !pos -716 -10 66 103. Offered to a WHM
// of AF1_QUEST_LEVEL: Narcheral's 689 begins it (any option). Accepted: Valkurm Dunes' qm2 (shown from 18:00 to 05:00 by
// the zone's script) pops Marchelute while the Tavnazia Pass is not held (FOUL_PRESENCE); exactly the pass to Narcheral
// plays 690 (completed: Blessed Hammer, fame +20; the pass taken only on completion). Prelude of Black and White reads
// this quest's status; RoE records and Vingijard read it (stay).
// Kept as the Lua has it: qm2 pops Marchelute again whenever the pass is not held, also after the pass was used.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class MessengerFromBeyond : QuestScript
    {
        public MessengerFromBeyond() : base(QuestLog.Sandoria, "MESSENGER_FROM_BEYOND") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Messenger_From_Beyond";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.BlessedHammer, Fame = 20, FameArea = FameArea.Sandoria });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                    q.Player.MainJob == Job.Whm)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Narcheral).OnTrigger(QuestReply.ProgressEvent(689))
                .OnEventFinish(Zone.NorthernSanDoria, 689, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.ValkurmDunes, Npc.ValkurmDunes.qm2).OnTrigger(q =>
                {
                    if (!q.Player.HasItem(Item.TavnaziaPass) &&
                        // { hide = 0 } with no claim: popFromQM's claim defaults to true
                        q.Player.PopFromQm(Mob.ValkurmDunes.MARCHELUTE, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.ValkurmDunes.FOUL_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Narcheral).OnTrade(q =>
                {
                    // npcUtil.tradeHasExactly(trade, xi.item.TAVNAZIA_PASS)
                    if (q.Trade.HasExactly(Item.TavnaziaPass))
                    {
                        return QuestReply.ProgressEvent(690);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 690, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
