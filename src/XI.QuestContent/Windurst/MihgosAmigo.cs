// Mihgo's Amigo (Log ID 2, Quest ID 25; the Lua header says 4), a line-for-line port of
// scripts/quests/windurst/Mihgos_Amigo.lua (quest rework B3b). Nanaa Mihgo !pos 62 -4 240 241. Offered by Nanaa Mihgo
// while neither The Tenshodo Showdown nor As Thick as Thieves is accepted: 80 (81 while Crying Over Onions is accepted)
// begins it. Accepted: Nanaa Mihgo's 82, Bopa Greso's 84, Cha Lebagta's 85; four Yagudo Bead Necklaces traded to Nanaa
// Mihgo (tradeHas: at least four, the rest kept) play 88 (200 gil), which completes it (the title Cat Burglar Groupie;
// the four taken, Norg fame +30, 200 gil). Completed, the same trade plays 494 (repeatable: completed again, the four
// taken, the quest's mustZone set, Norg fame +15, 200 gil); while that mustZone holds, Nanaa Mihgo's 89 and Cha
// Lebagta's 91. Kept as the Lua file has it: the first completion (88) sets no mustZone, only the repeat (494) does;
// the reward is a title only, so neither completion can fail.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class MihgosAmigo : QuestScript
    {
        public MihgosAmigo() : base(QuestLog.Windurst, "MIHGOS_AMIGO") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Mihgos_Amigo";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.CatBurglarGroupie });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "THE_TENSHODO_SHOWDOWN") != QuestStatus.Accepted && // Quest cannot be flagged if you have The Tenshodo Showdown in progress
                    q.Player.QuestStatus(QuestLog.Windurst, "AS_THICK_AS_THIEVES") != QuestStatus.Accepted) // Quest cannot be flagged if you have As Thick as Thieves in progress
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(q =>
                {
                    if (q.Player.QuestStatus(QuestLog.Windurst, "CRYING_OVER_ONIONS") == QuestStatus.Accepted)
                    {
                        return QuestReply.ProgressEvent(81); // Start Quest "Mihgo's Amigo" with quest "Crying Over Onions" Activated
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(80);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 80, q => q.Quest.Begin())
                .OnEventFinish(Zone.WindurstWoods, 81, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo)
                    .OnTrigger(q => QuestReply.Event(82))
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { xi.item.YAGUDO_BEAD_NECKLACE, 4 } }): at least four; confirms the four
                        if (q.Trade.HasQty((Item.YagudoBeadNecklace, 4)))
                        {
                            return QuestReply.ProgressEvent(88, 200);
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BopaGreso).OnTrigger(QuestReply.Event(84))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(QuestReply.Event(85, 0, Item.YagudoBeadNecklace))
                .OnEventFinish(Zone.WindurstWoods, 88, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.AddFame(FameArea.Norg, 30);
                        q.Player.AddGil(200); // "Obtained X gil." Text is baked into the CS, so gil needs to be given outside of the quest rewards.
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.MustZone)
                        {
                            return QuestReply.Event(89);
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasQty((Item.YagudoBeadNecklace, 4)))
                        {
                            return QuestReply.ProgressEvent(494, 200);
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.ChaLebagta).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(91, 0, Item.YagudoBeadNecklace);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 494, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Quest.SetMustZone();
                        q.Player.AddFame(FameArea.Norg, 15);
                        q.Player.AddGil(200); // "Obtained X gil." Text is baked into the CS, so gil needs to be given outside of the quest rewards.
                    }
                });
        }
    }
}
