// A Little Knowledge (Log ID 7, Quest ID 6), a line-for-line port of
// scripts/quests/crystalWar/A_Little_Knowledge.lua (quest rework B3b Crystal War, draft). The Eldieme Necropolis [S]:
// Erlene !pos 376.936 -39.999 17.914 (default 15). Crawlers' Nest [S]: Tucker !pos 216.763 -32.441 -20.239 (no default,
// no script).
// Available, the main level at or above ADVANCED_JOB_LEVEL: Erlene: 10 (1), whose finish begins it on option 0.
// Accepted: Tucker (Crawlers' Nest [S]): a trade, Option 1-3, at least 12 Rolanberries and nothing else: the vellum
// count (a stack of 12 a sheet, at most 4) into the numAwarded local var, the berries for it confirmed, and Option 1 /
// 2 / 3: 8 / 10 / 11, whose finish gives that many Sheets of Vellum and, given, confirms the trade and adds 1 to
// Option;
// any other trade: 14. A talk at Prog 0: Option 0: 6 (-> Option 1); 1: 7; 2-3: 9; 4: 12 (Events). Erlene: a trade of
// exactly 12 Sheets of Vellum at Prog 0: 12 (1 for a BLM / RDM / SMN / BLU main job, else 0), whose finish confirms the
// trade and sets Prog 1. A talk: Prog 0: 11 (an Event); Prog 1: under Manafont, Chainspell, Astral Flow or Azure Lore:
// 14, whose finish completes it (the Grimoire and the Scholar job gesture, Schultz Scholar), unlocks Scholar and says
// YOU_CAN_NOW_BECOME_A_SCHOLAR; else 13 (an Event). Completed: Erlene: a Scholar of level 5 or more knowing neither
// Embrava nor Kaustra: 47, whose finish teaches both (silently) and says YOU_LEARN_EMBRAVA_AND_KAUSTRA; else 15 (an
// Event, replaceDefault).
// The reward's key items: the Lua's `keyItem = { GRIMOIRE, JOB_GESTURE_SCHOLAR }` is a list, and QuestReward holds one
// key item (PushRewardTable sets keyItem as one number). npcUtil.completeQuest gives the list first (each with
// KEYITEM_OBTAINED), then the title, then completeQuest; with no item in the reward it never fails. So the 14 finish
// gives the two key items (npcUtil.giveKeyItem each: the same packets, in the same order) and then completes with the
// title alone. A `QuestReward.KeyItems` list (builder + PushRewardTable) would say it in the reward (README).
// Kept as the Lua has it: 10 begins on option 0 only; Tucker's 14 answers every trade that fails the test, Option 0
// and 4 included; Erlene's vellum need not come from Tucker; Tucker's talk at Option 5+ or Prog 1 is nil.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class ALittleKnowledge : QuestScript
    {
        public ALittleKnowledge() : base(QuestLog.CrystalWar, "A_LITTLE_KNOWLEDGE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/A_Little_Knowledge";

        // xi.effect (scripts/enum/effect.codegen.lua): no generated constant class
        const int Manafont = 47;
        const int Chainspell = 48;
        const int AstralFlow = 55;
        const int AzureLore = 163;

        // xi.magic.spell (scripts/enum/magic.lua): no generated constant class
        const int Embrava = 478;
        const int Kaustra = 502;

        // the Lua's validMageJobs set
        static bool IsValidMageJob(int job) => job == Job.Blm || job == Job.Rdm || job == Job.Smn || job == Job.Blu;

        // the Lua's tuckerEventFinish (8, 10, 11)
        static void TuckerEventFinish(IQuestContext q)
        {
            int numAwarded = q.Quest.LocalVar("numAwarded");

            if (q.Player.GiveItems((Item.SheetOfVellum, numAwarded)))
            {
                q.Trade.Confirm();
                q.Quest.IncrementVar("Option", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            b.Reward(new QuestReward
            {
                // keyItem = { GRIMOIRE, JOB_GESTURE_SCHOLAR }: given in the 14 finish (above)
                Title = Title.SchultzScholar,
            });

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene)
                .OnTrigger(QuestReply.ProgressEvent(10, 1))
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 10, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.CrawlersNestS, Npc.CrawlersNestS.Tucker).OnTrade(q =>
                {
                    int questOption = q.Quest.Var("Option");

                    // the Lua's note: the vellum per trade follows the stacks traded; at least one stack (to be
                    // verified);
                    // whether partial stacks are taken is not known
                    if (questOption >= 1 &&
                        questOption <= 3 &&
                        q.Trade.ItemQty(Item.Rolanberry) >= 12 &&
                        q.Trade.HasItemQty(Item.Rolanberry, q.Trade.ItemCount))
                    {
                        int numAwardedVellum = Math.Min(4, q.Trade.ItemCount / 12);

                        q.Trade.ConfirmItem(Item.Rolanberry, numAwardedVellum * 12);
                        q.Quest.SetLocalVar("numAwarded", numAwardedVellum);

                        if (questOption == 1)
                        {
                            return QuestReply.ProgressEvent(8);
                        }
                        else if (questOption == 2)
                        {
                            return QuestReply.ProgressEvent(10);
                        }
                        else if (questOption == 3)
                        {
                            return QuestReply.ProgressEvent(11);
                        }
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        int questOption = q.Quest.Var("Option");

                        if (questOption == 0)
                        {
                            return QuestReply.ProgressEvent(6);
                        }
                        else if (questOption == 1)
                        {
                            return QuestReply.Event(7);
                        }
                        else if (questOption <= 3)
                        {
                            return QuestReply.Event(9);
                        }
                        else if (questOption == 4)
                        {
                            return QuestReply.Event(12);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.CrawlersNestS, 6, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .OnEventFinish(Zone.CrawlersNestS, 8, TuckerEventFinish)
                .OnEventFinish(Zone.CrawlersNestS, 10, TuckerEventFinish)
                .OnEventFinish(Zone.CrawlersNestS, 11, TuckerEventFinish)
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene).OnTrade(q =>
                {
                    if (q.Quest.Step == 0 &&
                        q.Trade.HasExactly((Item.SheetOfVellum, 12)))
                    {
                        int isMageJob = IsValidMageJob(q.Player.MainJob) ? 1 : 0;

                        return QuestReply.ProgressEvent(12, isMageJob);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(11);
                    }
                    else if (questProgress == 1)
                    {
                        if (q.Player.HasStatusEffect(Manafont) ||
                            q.Player.HasStatusEffect(Chainspell) ||
                            q.Player.HasStatusEffect(AstralFlow) ||
                            q.Player.HasStatusEffect(AzureLore))
                        {
                            return QuestReply.ProgressEvent(14);
                        }
                        else
                        {
                            return QuestReply.Event(13);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 12, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 14, q =>
                {
                    // the reward's keyItem list, as npcUtil.completeQuest gives it before the title (see the header)
                    q.Player.GiveKeyItem(KeyItem.Grimoire);
                    q.Player.GiveKeyItem(KeyItem.JobGestureScholar);

                    if (q.Quest.Complete())
                    {
                        q.Player.UnlockJob(Job.Sch);
                        q.Text.Special(Text.TheEldiemeNecropolisS.YOU_CAN_NOW_BECOME_A_SCHOLAR);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene).OnTrigger(q =>
                {
                    if (q.Player.MainJob == Job.Sch &&
                        q.Player.MainLevel >= 5 &&
                        !q.Player.HasSpell(Embrava) &&
                        !q.Player.HasSpell(Kaustra))
                    {
                        return QuestReply.ProgressEvent(47);
                    }
                    else
                    {
                        return QuestReply.Event(15).ReplaceDefault();
                    }
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 47, q =>
                {
                    q.Player.AddSpell(Embrava, true);
                    q.Player.AddSpell(Kaustra, true);
                    q.Text.Special(Text.TheEldiemeNecropolisS.YOU_LEARN_EMBRAVA_AND_KAUSTRA);
                });
        }
    }
}
