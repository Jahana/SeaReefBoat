// Two Horn the Savage (Log ID 6, Quest ID 17), a line-for-line port of scripts/quests/ahtUrhgan/Two_Horn_the_Savage.lua
// (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate (no scripts): Milazahn !pos -56.5 0 2.2 (default 594, the
// offer's own csid), Cacaroon !pos -72 0 -82.3 (default 248). Mamook: six Viscous Liquids (script: 214, its finish on
// option 1 an Illusion after a 100 ms timer); the quest's is the sixth, mamookID.npc.QUEST_LIQUID (17044014, !pos
// -260.2 3.9 -141.1); the Mamool Ja NM (mob script: paths and the weapon-break mixin, no onMobDeath). Available (no
// other condition): Milazahn 594, finished with option 1: begun. Accepted: Cacaroon 608 once per zone at Prog 1 or
// more, and at Prog 0 a trade of exactly 1000 gil and nothing else: 595 (Prog 1; the gil is not confirmed by the
// quest); Milazahn 596 at Prog 4 (completed: an Imperial Mythril Piece). Mamook, the quest's Viscous Liquid only: Prog
// 1: 212 (Prog 2); Prog 2: the Mamool Ja popped (claimed, the liquid not hidden) and IMPENDING_BATTLE when it pops;
// Prog 3: 213 (Prog 4). The Mamool Ja's death at Prog 2: Prog 3. Kept as the Lua has it: 594 begins on option 1 only;
// 595's finish neither confirms nor takes the gil (the trade is never confirmed: the gil stays); the other liquids and
// the quest's at Prog 0 / 4 return nil (the script's 214); a liquid whose pop fails (the NM up) returns nil; the death
// sets Prog 3 for any char at Prog 2 that the lookup runs it for; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class TwoHornTheSavage : QuestScript
    {
        public TwoHornTheSavage() : base(QuestLog.AhtUrghan, "TWO_HORN_THE_SAVAGE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Two_Horn_the_Savage";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialMythrilPiece,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Milazahn).OnTrigger(QuestReply.ProgressEvent(594))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 594, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Cacaroon).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 1)
                    {
                        return QuestReply.Event(608).OncePerZone();
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    // npcUtil.tradeHasExactly(trade, { { 'gil', 1000 } }): exactly 1000 gil, no item
                    if (q.Quest.Step == 0 && q.Trade.HasExactlyWithGil(1000))
                    {
                        return QuestReply.ProgressEvent(595);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Milazahn).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(596);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 596, q =>
                {
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 595, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Mamook, Npc.Mamook.ViscousLiquid).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    // ID.npc.QUEST_LIQUID: GetTableOfIDs('Viscous_Liquid')[6], the sixth liquid
                    if (q.Npc.Id == q.Player.NpcIdOf("QUEST_LIQUID"))
                    {
                        if (progress == 1)
                        {
                            return QuestReply.ProgressEvent(212);
                        }
                        else if (progress == 2 && q.Player.PopFromQm(Mob.Mamook.MAMOOL_JA, hide: 0, claim: true))
                        {
                            return QuestReply.MessageSpecial(Text.Mamook.IMPENDING_BATTLE);
                        }
                        else if (progress == 3)
                        {
                            return QuestReply.ProgressEvent(213);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mamook, 212, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Mamook, 213, q =>
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.Mamook, Npc.Mamook.MamoolJa).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        // Zoning will NOT reset progress. Progress is gained and kept as long as the NM has died.
                        q.Quest.SetStep(3);
                    }
                });
        }
    }
}
