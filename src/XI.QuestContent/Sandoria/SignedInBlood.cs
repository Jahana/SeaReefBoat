// Signed in Blood (Log ID 0, Quest ID 108), a line-for-line port of scripts/quests/sandoria/Signed_in_Blood.lua (quest
// rework B3b, San d'Oria group 3). Sobane : !pos -190 -3 97 230; Abelard : !pos -52 -11 -13 248. Offered at San d'Oria fame
// level 3: Sobane's 732 (option 1 begins it). Prog 0: Sobane's 733; a Cathedral Tapestry (tradeHas) plays 734 (Prog 1,
// the tapestry taken). Prog 1: Sobane's 735; Abelard (Selbina) 1104 (Prog 2). Prog 2: an Ordelle's Caves Treasure Chest
// trade without the Torn-Out Pages runs xi.treasure.onTrade with the pages as its reward (noAction); Abelard 1105, or
// 1106 with the pages (Prog 3); Sobane 735. Prog 3: Abelard's 48; Sobane's 736: completed (Cunning Earring, fame +30,
// 3500 gil), the pages deleted, needToZone set. Completed with needToZone: Sobane's 737. Tea with a Tonberry reads this
// quest's status. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class SignedInBlood : QuestScript
    {
        public SignedInBlood() : base(QuestLog.Sandoria, "SIGNED_IN_BLOOD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Signed_in_Blood";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Sandoria, Item = Item.CunningEarring, Gil = 3500 });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 3)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q =>
                    QuestReply.ProgressEvent(732, 0, Item.CathedralTapestry))
                .OnEventFinish(Zone.SouthernSanDoria, 732, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane)
                    .OnTrigger(q => QuestReply.Event(733, 0, Item.CathedralTapestry))
                    // npcUtil.tradeHas(trade, xi.item.CATHEDRAL_TAPESTRY)
                    .OnTrade(q => q.Trade.Has(Item.CathedralTapestry) ? QuestReply.ProgressEvent(734, 0, Item.CathedralTapestry) : null)
                .OnEventFinish(Zone.SouthernSanDoria, 734, q =>
                {
                    q.Quest.SetStep(1);
                    q.Trade.Confirm();
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q => QuestReply.Event(735))
                .Npc(Zone.Selbina, Npc.Selbina.Abelard).OnTrigger(q => QuestReply.ProgressEvent(1104))
                .OnEventFinish(Zone.Selbina, 1104, q => q.Quest.SetStep(2));

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.TreasureChest).OnTrade(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TornOutPages))
                    {
                        q.Trade.TreasureTrade(2, KeyItem.TornOutPages);

                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.Selbina, Npc.Selbina.Abelard).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TornOutPages))
                    {
                        return QuestReply.ProgressEvent(1106);
                    }
                    else
                    {
                        return QuestReply.Event(1105);
                    }
                })
                .OnEventFinish(Zone.Selbina, 1106, q => q.Quest.SetStep(3))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q => QuestReply.Event(735));

            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.Selbina, Npc.Selbina.Abelard).OnTrigger(q => QuestReply.Event(48))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q => QuestReply.ProgressEvent(736))
                .OnEventFinish(Zone.SouthernSanDoria, 736, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.TornOutPages);
                        // player:needToZone(true)
                        q.Player.SetNeedToZone(true);
                    }
                });

            // just completed: zone out and back in before Tea with a Tonberry is offered
            // player:needToZone()
            b.State(QuestStatus.Completed, when: q => q.Player.NeedToZone)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrigger(q => QuestReply.ProgressEvent(737));
        }
    }
}
