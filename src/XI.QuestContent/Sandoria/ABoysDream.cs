// A Boy's Dream (Log ID 0, Quest ID 91, PLD AF2), ported from the quest branches of
// scripts/zones/Northern_San_dOria/npcs/Ailbeche.lua, scripts/zones/Crawlers_Nest/npcs/qm9.lua and
// scripts/zones/Chateau_dOraguille/npcs/_6h0.lua (quest rework B3b, San d'Oria group 14; call-only: each script calls
// the quest at the spot its branch was). Ailbeche : !pos 4 -1 24 231; qm9 : !pos -18 -8 124 197;
// _6h0 : !pos -38 -3 73 233. The quest's state is the status and the char var aBoysDreamCS (not a quest var).
// Ailbeche, for a PLD of main level AF2_QUEST_LEVEL with Sharpening the Sword completed and this quest available: 41
// (aBoysDreamCS 0) or 40 (any other value); 41 or 40 with option 1: begun, aBoysDreamCS 2; 41 with option 0:
// aBoysDreamCS 1. Then by aBoysDreamCS alone (any status): 2: 46, 3: 39, 4: 60, 5: 47, 6 or more: 25 (its
// finish at 6: 7). His trade, aBoysDreamCS 3 or more: exactly a Giant Shell Bug at 3: 15 (its finish at 3: 4); exactly
// an Odontotyrannus at 4: 47 (its finish at 4: 5); neither takes the item (no tradeComplete). Crawlers' Nest qm9,
// accepted and not popped this Vana'diel day (DreadbugNM_Day): GetSystemTime() past DreadbugNM_Timer + 30 and
// npcUtil.popFromQM(DREADBUG, claim, hide 0) succeeding: SENSE_OF_FOREBODING, DreadbugNM_Timer now + 180,
// DreadbugNM_Day today; else NOTHING_SEEMS_TO_HAPPEN; not accepted or popped today: NOTHING_WILL_HAPPEN_YET. The door
// _6h0 at aBoysDreamCS 8 (after Under Oath's two arms and the Trion trust arm, which stay first): 88; its finish: no
// free slot: ITEM_CANNOT_BE_OBTAINED (Gallant Leggings); else a PLD is given Under Oath (player:addQuest), the
// Knight's Boots taken, the Gallant Leggings (addItem), ITEM_OBTAINED, aBoysDreamCS 0, San d'Oria fame 20, completed.
// The scripts keep their own answers when the quest does not act: Ailbeche's Under Oath 59 / 13, the door's trust 574
// and rank 10's 62. Stay Lua (char var / key item only, they never name the quest): Exoroche (Southern San d'Oria;
// aBoysDreamCS 2: 50 -> 3; 7 or more: 32 -> 8 at 7) and Zaldon (Selbina; aBoysDreamCS 5 and exactly an Odontotyrannus:
// 85 -> the Knight's Boots, 6, the fish taken). Read only: roe_records (questComplete), Borghertz's Stalwart Hands (its
// required quest), Vingijard; sql/fishing_mob.sql's Odontotyrannus (log 0, quest 91: hookable while accepted, read from
// the quest log bits by ZoneHost.Fishing), SharpeningTheSword.cs (its completed section while this one is available).
// Kept as the Lua has it: Ailbeche's CS arms and trades read no status; his trades never take the item; 40 with option
// 0 or 2 and 41 with option 2 do nothing; 47 is both the CS 5 talk and the CS 4 trade event (its finish moves only at
// 4); the 88 finish reads no status (a completed quest at aBoysDreamCS 8 completes again), checks the free slots
// itself, gives the leggings with addItem and completes with the raw player:completeQuest (no quest:cleanup, so a
// Quest[0][91] var survives it).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): Ailbeche.lua's offer reads the literal 50,
// the C# reads AF2_QUEST_LEVEL as the other AF2 quests do.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ABoysDream : QuestScript
    {
        const string Cs = "aBoysDreamCS";

        public ABoysDream() : base(QuestLog.Sandoria, "A_BOYS_DREAM") { }

        // the offer's guard: Sharpening the Sword completed, a PLD of main level AF2_QUEST_LEVEL (the Lua's literal 50)
        static bool Offered(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Sandoria, "SHARPENING_THE_SWORD") == QuestStatus.Completed &&
            q.Player.MainJob == Job.Pld &&
            q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL");

        protected override void Build(QuestScriptBuilder b)
        {
            // Ailbeche.lua onTrade: aBoysDreamCS 3 or more (any status)
            b.AnyState(when: q => q.Player.CharVar(Cs) >= 3)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrade(q =>
                {
                    int cs = q.Player.CharVar(Cs);

                    // npcUtil.tradeHasExactly(trade, xi.item.GIANT_SHELL_BUG), then the CS, as the Lua tests them
                    if (q.Trade.HasExactly(Item.GiantShellBug) && cs == 3)
                    {
                        return QuestReply.Event(15); // trading the bug
                    }
                    else if (q.Trade.HasExactly(Item.Odontotyrannus) && cs == 4)
                    {
                        return QuestReply.Event(47); // trading the odontotyrannus
                    }

                    return null;
                });

            // Ailbeche.lua onTrigger: the offer
            b.State(QuestStatus.Available, when: q => Offered(q) && q.Player.CharVar(Cs) == 0)
                // long cs
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(41));
            b.State(QuestStatus.Available, when: q => Offered(q))
                // short cs
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(40));

            // Ailbeche.lua onTrigger: by aBoysDreamCS alone (any status)
            b.AnyState(when: q => q.Player.CharVar(Cs) == 2)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(46));
            b.AnyState(when: q => q.Player.CharVar(Cs) == 3)
                // after Exoroche
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(39));
            b.AnyState(when: q => q.Player.CharVar(Cs) == 4)
                // after the bug
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(60));
            b.AnyState(when: q => q.Player.CharVar(Cs) == 5)
                // after the fish
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(47));
            b.AnyState(when: q => q.Player.CharVar(Cs) >= 6)
                // after Zaldon
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).Called().OnTrigger(QuestReply.Event(25));

            // Crawlers_Nest qm9.lua: accepted and not popped this Vana'diel day
            b.State(QuestStatus.Accepted, when: q => q.Player.VanadielUniqueDay != q.Player.CharVar("DreadbugNM_Day"))
                .Npc(Zone.CrawlersNest, Npc.CrawlersNest.qm9).Called().OnTrigger(q =>
                {
                    if (q.Player.SystemTime > q.Player.CharVar("DreadbugNM_Timer") + 30 &&
                        q.Player.PopFromQm(Mob.CrawlersNest.DREADBUG, hide: 0, claim: true))
                    {
                        q.Text.Special(Text.CrawlersNest.SENSE_OF_FOREBODING);
                        q.Player.SetCharVar("DreadbugNM_Timer", q.Player.SystemTime + 180);
                        q.Player.SetCharVar("DreadbugNM_Day", q.Player.VanadielUniqueDay);
                        return QuestReply.NoAction();
                    }

                    return QuestReply.MessageSpecial(Text.CrawlersNest.NOTHING_SEEMS_TO_HAPPEN);
                });
            b.AnyState()
                .Npc(Zone.CrawlersNest, Npc.CrawlersNest.qm9).Called()
                    .OnTrigger(QuestReply.MessageSpecial(Text.CrawlersNest.NOTHING_WILL_HAPPEN_YET));

            // _6h0.lua onTrigger: aBoysDreamCS 8 (any status), after Under Oath's arms and the trust's
            b.AnyState(when: q => q.Player.CharVar(Cs) == 8)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).Called().OnTrigger(QuestReply.Event(88));

            b.AnyState()
                // Ailbeche.lua onEventFinish
                .OnEventFinish(Zone.NorthernSanDoria, 41, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Cs, 2);
                    }
                    else if (q.Option == 0)
                    {
                        q.Player.SetCharVar(Cs, 1);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 40, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Cs, 2);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 15, q =>
                {
                    if (q.Player.CharVar(Cs) == 3)
                    {
                        q.Player.SetCharVar(Cs, 4);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 47, q =>
                {
                    if (q.Player.CharVar(Cs) == 4)
                    {
                        q.Player.SetCharVar(Cs, 5);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 25, q =>
                {
                    if (q.Player.CharVar(Cs) == 6)
                    {
                        q.Player.SetCharVar(Cs, 7);
                    }
                })
                // _6h0.lua onEventFinish 88
                .OnEventFinish(Zone.ChateauDoraguille, 88, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.ChateauDoraguille.ITEM_CANNOT_BE_OBTAINED, Item.GallantLeggings);
                    }
                    else
                    {
                        if (q.Player.MainJob == Job.Pld)
                        {
                            q.Player.AddQuest(QuestLog.Sandoria, "UNDER_OATH");
                        }

                        q.Player.DelKeyItem(KeyItem.KnightsBoots);
                        q.Player.AddItem(Item.GallantLeggings);
                        q.Text.Special(Text.ChateauDoraguille.ITEM_OBTAINED, Item.GallantLeggings);
                        q.Player.SetCharVar(Cs, 0);
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        // the raw player:completeQuest: no quest:cleanup, as the Lua
                        q.Player.CompleteQuest(QuestLog.Sandoria, "A_BOYS_DREAM");
                    }
                });
        }
    }
}
