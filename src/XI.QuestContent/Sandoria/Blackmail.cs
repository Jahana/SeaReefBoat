// Blackmail (Log ID 0, Quest ID 71), a line-for-line port of scripts/quests/sandoria/Blackmail.lua (quest rework B3b, San
// d'Oria group 7). Dauperiat : !pos -20 0 -26 231; Halver : !pos 2 0 0 233. Available: Dauperiat's 643 at San d'Oria fame
// level 3 and rank 3 in the char's own nation (the Suspicious Envelope, begun); else 641 (needToZone set), or 642 while
// needToZone. Accepted with the envelope: Dauperiat's 645; Halver's 549 (the envelope deleted, Prog 1). Prog 1:
// Dauperiat's 646 (option [1] the floor plans; option 1: Prog 2). Prog 2: his 647; a trade holding a Copy of the Castle
// Floor Plans (npcUtil.tradeHas) plays 648 (completed: 900 gil, fame +16; the plans taken). Completed, Prog 0: his 650
// (option 1: Prog 1); Prog 1: 647, and the plans again play 648 (Prog 0, the plans taken, 900 gil, fame +16): a repeat.
// The Setting Sun needs it completed. No outside reference. Halver's default text answers when no section does.
// Kept as the Lua has it: a completed char can repeat the plans trade (650, 648); the rank read is the char's own
// nation's; 648's complete in section 4 cannot fail (no item reward).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class Blackmail : QuestScript
    {
        public Blackmail() : base(QuestLog.Sandoria, "BLACKMAIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Blackmail";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 16, FameArea = FameArea.Sandoria, Gil = 900 });

            b.State(QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Dauperiat).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Sandoria) >= 3 &&
                        // player:getRank(player:getNation())
                        q.Player.Rank(q.Player.Nation) >= 3)
                    {
                        return QuestReply.ProgressEvent(643);
                    }
                    else
                    {
                        if (q.Player.NeedToZone)
                        {
                            return QuestReply.Event(642);
                        }
                        else
                        {
                            return QuestReply.Event(641);
                        }
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 641, q => q.Player.SetNeedToZone(true))
                .OnEventFinish(Zone.NorthernSanDoria, 643, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SuspiciousEnvelope);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.SuspiciousEnvelope))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Dauperiat).OnTrigger(QuestReply.Event(645))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(QuestReply.ProgressEvent(549))
                .OnEventFinish(Zone.ChateauDoraguille, 549, q =>
                {
                    q.Player.DelKeyItem(KeyItem.SuspiciousEnvelope);
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Dauperiat).OnTrigger(QuestReply.ProgressEvent(646).Option(1, Item.CopyOfTheCastleFloorPlans))
                .OnEventFinish(Zone.NorthernSanDoria, 646, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Dauperiat)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.CopyOfTheCastleFloorPlans))
                        {
                            return QuestReply.ProgressEvent(648).Option(1, Item.CopyOfTheCastleFloorPlans);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(647).Option(1, Item.CopyOfTheCastleFloorPlans))
                .OnEventFinish(Zone.NorthernSanDoria, 648, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.Step == 0)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Dauperiat).OnTrigger(QuestReply.ProgressEvent(650).Option(1, Item.CopyOfTheCastleFloorPlans))
                .OnEventFinish(Zone.NorthernSanDoria, 650, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.Step == 1)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Dauperiat)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.CopyOfTheCastleFloorPlans))
                        {
                            return QuestReply.ProgressEvent(648).Option(1, Item.CopyOfTheCastleFloorPlans);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(647).Option(1, Item.CopyOfTheCastleFloorPlans))
                .OnEventFinish(Zone.NorthernSanDoria, 648, q =>
                {
                    q.Quest.SetStep(0);
                    q.Trade.Confirm();
                    q.Player.GiveCurrency("gil", 900);
                    q.Player.AddFame(FameArea.Sandoria, 16);
                });
        }
    }
}
