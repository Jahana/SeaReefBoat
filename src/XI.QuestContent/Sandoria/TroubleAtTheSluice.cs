// Trouble at the Sluice (Log ID 0, Quest ID 68), a line-for-line port of
// scripts/quests/sandoria/Trouble_at_the_Sluice.lua (quest rework B3b, San d'Oria group 5). Belgidiveau : !pos -98 0 69
// 231; Novalmauge : !pos 70 -24 21 167. Offered at San d'Oria fame level 3 with The Rumor completed: Belgidiveau's 57
// (option 0, not 1, begins it). Accepted without the Neutralizer: Belgidiveau's 55; at Prog 0 Novalmauge's 15 (Prog 1);
// at Prog 1 his 16, and a trade holding a Dahlia (not only a Dahlia: npcUtil.tradeHas) plays 17 (the Neutralizer; the
// Dahlia taken). With the Neutralizer: Belgidiveau's 56 (completed: Heavy Axe, fame +30, the Neutralizer deleted).
// goblinfootprint.lua reads the status (stays).
// Kept as the Lua has it: 57's option 0 accepts (the client's decline is option 1); the Dahlia trade is
// npcUtil.tradeHas (other items are handed back by confirmTrade); a GM-set Prog other than 0 and 1 without the
// Neutralizer matches no state (both NPCs fall to their defaults, Novalmauge to The Rumor's 14). npcUtil.giveKeyItem
// always returns true, so 17's finish always takes the trade.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TroubleAtTheSluice : QuestScript
    {
        public TroubleAtTheSluice() : base(QuestLog.Sandoria, "TROUBLE_AT_THE_SLUICE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Trouble_at_the_Sluice";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Sandoria, Item = Item.HeavyAxe });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "THE_RUMOR") == QuestStatus.Completed &&
                    q.Player.FameLevel(FameArea.Sandoria) >= 3)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Belgidiveau).OnTrigger(QuestReply.ProgressEvent(57))
                .OnEventFinish(Zone.NorthernSanDoria, 57, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    !q.Player.HasKeyItem(KeyItem.Neutralizer) &&
                    q.Quest.Step == 0)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Belgidiveau).OnTrigger(QuestReply.Event(55))
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge).OnTrigger(QuestReply.ProgressEvent(15))
                .OnEventFinish(Zone.BostaunieuxOubliette, 15, q => q.Quest.SetStep(1));

            b.State(QuestStatus.Accepted, when: q =>
                    !q.Player.HasKeyItem(KeyItem.Neutralizer) &&
                    q.Quest.Step == 1)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Belgidiveau).OnTrigger(QuestReply.Event(55))
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.Dahlia))
                        {
                            return QuestReply.ProgressEvent(17);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(16))
                .OnEventFinish(Zone.BostaunieuxOubliette, 17, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.Neutralizer))
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.Neutralizer))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Belgidiveau).OnTrigger(QuestReply.ProgressEvent(56))
                .OnEventFinish(Zone.NorthernSanDoria, 56, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Neutralizer);
                    }
                });
        }
    }
}
