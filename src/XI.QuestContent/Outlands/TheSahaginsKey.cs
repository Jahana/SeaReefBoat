// The Sahagin's Key (Log ID 5, Quest ID 128), ported from scripts/zones/Norg/npcs/Gimb.lua,
// scripts/zones/Sea_Serpent_Grotto/npcs/Bou_the_Righteous.lua and
// scripts/zones/Sea_Serpent_Grotto/npcs/Hurr_the_Betrayer.lua (the no-log quests, call-only).
// Gimb : !pos -4.975 -1.975 -44.039 252 (Chasing Dreams' 211 answers him first, as a container, with the Storeroom
// Key); Bou the Righteous : !pos -125.029 46.568 -334.778 176; Hurr the Betrayer : !pos 305.882 26.768 234.279 176.
// None of them has a default.
// The scripts never read or write the quest's status (Gimb.lua: "it doesn't appear in the log"): the steps are the char
// vars SahaginKeyProgress and SahaginKeyItems.
// Gimb: always 69 and Progress 1. Bou: always 108 and Progress 2. Hurr, talk: Progress 2 without a Sahagin Key: 105,
// Items 1; Progress 3, Items 0, no key: math.randomInt(1, 100) <= 50: 105, Items 1, else 106, Items 2; Progress 3,
// Items 1: 105; Progress 3, Items 2: 106; else 104. Trade: Items 1 with three Mythril Beastcoins and a Norg Shell, or
// Items 2 with a Gold Beastcoin and a Norg Shell: 107. 107's finish: Progress 2 and the key given: the trade taken,
// Progress 3, Items 0; else Progress 3 and the key given: the trade taken, Items 0.
// Stays Lua: the Ornamented Door _4wa (its trade takes a Sahagin Key from anyone and opens; it reads no quest var).
// Kept as the scripts have it: Gimb and Bou read nothing, so a talk sets Progress back (3 to 2 or 1, 2 to 1); Bou needs
// no Gimb; Hurr's Progress 3 arms at Items 1 and 2 read no key; his trade reads no Progress, so 107 plays at a Progress
// whose finish has no arm (nothing given or taken); the finish's second arm gives the key again after the first arm's
// give failed only at Progress 3 (the first arm's Progress 2 test stops it before a second give).
// Deliberate change (user ruling 2026-11-04; the scripts never wrote the log): Gimb's 69 adds the quest while it is
// available (the first step of SahaginKeyProgress), and 107's finish completes it when it hands a key while the quest
// is not completed (the first key; a char with Progress 3 from before the change completes at its next key). Raw
// player:addQuest / player:completeQuest: no reward, no quest:cleanup.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheSahaginsKey : QuestScript
    {
        const string Progress = "SahaginKeyProgress";
        const string Items = "SahaginKeyItems";

        public TheSahaginsKey() : base(QuestLog.Outlands, "THE_SAHAGINS_KEY") { }

        // deliberate: the key handed completes the quest in the log, once
        private static void CompleteLog(IQuestContext q)
        {
            if (q.Quest.Status != QuestStatus.Completed)
            {
                q.Player.CompleteQuest(QuestLog.Outlands, "THE_SAHAGINS_KEY");
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Gimb.lua onTrigger (the whole of it)
                .Npc(Zone.Norg, Npc.Norg.Gimb).Called().OnTrigger(q =>
                {
                    q.Player.SetCharVar(Progress, 1);
                    if (q.Quest.Status == QuestStatus.Available)
                    {
                        q.Player.AddQuest(QuestLog.Outlands, "THE_SAHAGINS_KEY"); // deliberate: the log entry
                    }
                    return QuestReply.Event(69);
                })
                // Bou_the_Righteous.lua onTrigger (the whole of it)
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.BoutheRighteous).Called().OnTrigger(q =>
                {
                    q.Player.SetCharVar(Progress, 2);
                    return QuestReply.Event(108);
                })
                // Hurr_the_Betrayer.lua onTrade and onTrigger (the whole of each)
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.HurrtheBetrayer).Called().OnTrade(q =>
                {
                    int items = q.Player.CharVar(Items);

                    if (items == 1) // If player was told to use 3 Mythril Beastcoins
                    {
                        if (q.Trade.HasQty((Item.MythrilBeastcoin, 3), (Item.NorgShell, 1)))
                        {
                            return QuestReply.Event(107);
                        }
                    }
                    else if (items == 2) // If player was told to use a Gold Beastcoin
                    {
                        if (q.Trade.Has(Item.GoldBeastcoin, Item.NorgShell))
                        {
                            return QuestReply.Event(107);
                        }
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int progress = q.Player.CharVar(Progress);
                    int items = q.Player.CharVar(Items);

                    if (progress == 2 && !q.Player.HasItem(Item.SahaginKey))
                    {
                        // If player has never before finished the quest
                        q.Player.SetCharVar(Items, 1);
                        return QuestReply.Event(105);
                    }
                    else if (progress == 3 && items == 0 && !q.Player.HasItem(Item.SahaginKey))
                    {
                        if (q.Player.RandomInt(1, 100) <= 50)
                        {
                            q.Player.SetCharVar(Items, 1);
                            return QuestReply.Event(105); // Requires 3 Mythril Beastcoins and a Norg Shell
                        }
                        else
                        {
                            q.Player.SetCharVar(Items, 2);
                            return QuestReply.Event(106); // Requires Gold Beastcoin and a Norg Shell
                        }
                    }
                    else if (progress == 3 && items == 1)
                    {
                        return QuestReply.Event(105); // If player was told to use 3 Mythril Beastcoins
                    }
                    else if (progress == 3 && items == 2)
                    {
                        return QuestReply.Event(106); // If player was told to use a Gold Beastcoin
                    }
                    else
                    {
                        // Progress 2 with the key: doesn't offer the key again; else the player hasn't spoken to the
                        // first two NPCs
                        return QuestReply.Event(104);
                    }
                });

            b.AnyState()
                // Hurr_the_Betrayer.lua onEventFinish 107
                .OnEventFinish(Zone.SeaSerpentGrotto, 107, q =>
                {
                    if (q.Player.CharVar(Progress) == 2 && q.Player.GiveItem(Item.SahaginKey))
                    {
                        q.Trade.Confirm();
                        q.Player.SetCharVar(Progress, 3); // Mark the quest progress
                        q.Player.SetCharVar(Items, 0);
                        CompleteLog(q);
                    }
                    else if (q.Player.CharVar(Progress) == 3 && q.Player.GiveItem(Item.SahaginKey))
                    {
                        q.Trade.Confirm();
                        q.Player.SetCharVar(Items, 0);
                        CompleteLog(q);
                    }
                });
        }
    }
}
