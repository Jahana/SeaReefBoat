// Dormant Powers Dislodged (Log ID 3, Quest ID 136), a line-for-line port of
// scripts/quests/jeuno/LB09_1_Dormant_Powers_Dislodged.lua (quest rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883
// 243. With Beyond the Stars completed and level cap 90 the moogle plays 10045 (with the Limit Breaker and MAX_LEVEL over
// 90: quest 4 above level 85, quest 3 stage 2 from 75; else 0, 0; then the 'Option' var and the wanted item, drawn once
// (0-9) and kept as itemWanted + 1); its update with option 8 answers whether Beat Around the Bushin is completed; option
// 11 begins it, option 12 sets Option 1. Accepted: a Kindred's Crest and the wanted item, with more than 9 merit points,
// play 10191 (the trade confirmed, 10 merit points spent, Prog 1); the moogle plays 10192 at Prog 1 (the timing game: the
// update answers 0-4 from the frames in the option; a finish option of 540-659 completes it: the Soul Gem, level cap 95,
// YOUR_LEVEL_LIMIT_IS_NOW_95), else 10045 (4, 1, 0, itemWanted). Kept Lua quirks: the accepted reminder sends the var
// itemWanted (the item's index + 1) where the available one sends the index; a trade with itemWanted 0 (accepted by a GM,
// no draw) or 11 and more indexes past the table and raises a script error (here: the array index throws); 10192's
// finish ignores quest:complete's result (a key item reward: it cannot fail).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class DormantPowersDislodged : QuestScript
    {
        public DormantPowersDislodged() : base(QuestLog.Jeuno, "DORMANT_POWERS_DISLODGED") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB09_1_Dormant_Powers_Dislodged";

        // NOTE: Timing minigame was guesstimated! No capture available.
        // The event seems to handle the actual timing. As in, it counts for us.
        // Always the same camera angle at victory. That is, close up of player, at arround 9 seconds.
        // Wining timing: 10 seconds aprox.

        private static readonly int[] ItemWantedTable =
        {
            Item.PinchOfValkurmSunsand, // [0]
            Item.FadedCrystal,          // [1]
            Item.OrcishPlateArmor,      // [2]
            Item.MagickedSkull,         // [3]
            Item.CupOfDhalmelSaliva,    // [4]
            Item.YagudoCaulk,           // [5]
            Item.SirensTear,            // [6]
            Item.DangrufStone,          // [7]
            Item.OrcishAxe,             // [8]
            Item.QuadavBackscale,       // [9]
            // 10+ = It repeats the pattern ad nauseam.
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.SoulGem });
            b.Steps(1);

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "BEYOND_THE_STARS") &&
                    q.Player.LevelCap == 90)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 0;
                    int lastQuestStage = 0;
                    int option = q.Quest.Var("Option"); // Has rejected signing the contract?
                    int itemChosen = q.Quest.Var("itemWanted") - 1;
                    if (q.Settings.MainNumber("MAX_LEVEL") > 90 &&
                        limitBreaker == 1)
                    {
                        if (playerLevel > 85)
                        {
                            lastQuestNumber = 4;
                        }
                        else if (playerLevel >= 75)
                        {
                            lastQuestNumber = 3;
                            lastQuestStage = 2;
                        }
                    }

                    // Save item Chosen
                    if (itemChosen < 0)
                    {
                        itemChosen = q.Player.RandomInt(0, 9);
                        q.Quest.SetVar("itemWanted", itemChosen + 1);
                    }

                    return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage, option, itemChosen);
                })
                .OnEventUpdate(Zone.RuludeGardens, 10045, q =>
                {
                    // This event update checks if you have completed "Beat Around the Bushin" quest. If so, Atori-Tutori recognizes you.
                    if (q.Option == 8)
                    {
                        int knowAtori = q.Player.HasCompletedQuest(QuestLog.Jeuno, "BEAT_AROUND_THE_BUSHIN") ? 1 : 0;
                        q.Player.UpdateEvent(knowAtori);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    // Accept quest option.
                    if (q.Option == 11)
                    {
                        q.Quest.Begin();
                    }

                    // Reject signing. Quest not added to log.
                    else if (q.Option == 12)
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrade(q =>
                {
                    int itemWanted = q.Quest.Var("itemWanted") - 1;
                    int itemToTrade = ItemWantedTable[itemWanted]; // Lua: itemWantedTable[itemWanted][1]; -1 (no draw): a script error

                    if (q.Trade.HasExactly((Item.KindredsCrest, 1), (itemToTrade, 1)) &&
                        q.Player.MeritCount > 9)
                    {
                        return QuestReply.ProgressEvent(10191);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;

                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(10192, playerLevel, 0, 0, 2); // Timing Minigame starting event.
                    }
                    else
                    {
                        int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                        int lastQuestNumber = 4;
                        int lastQuestStage = 1;
                        int itemChosen = q.Quest.Var("itemWanted");
                        return QuestReply.Event(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage, 0, itemChosen);
                    }
                })
                .OnEventUpdate(Zone.RuludeGardens, 10192, q =>
                {
                    // Option: Push! The option is the number of frames. 1 sec = 60 frames. Probably.
                    if (q.Option >= 780)
                    {
                        q.Player.UpdateEvent(4); // Over 13 seconds. Slime.
                    }
                    else if (q.Option >= 660)
                    {
                        q.Player.UpdateEvent(3); // Between 11 and 13 seconds.Big Egg.
                    }
                    else if (q.Option >= 540)
                    {
                        q.Player.UpdateEvent(2); // Between 9 and 11 seconds. Glowing Egg. Win!
                    }
                    else if (q.Option >= 420)
                    {
                        q.Player.UpdateEvent(1); // Between 7 and 9 seconds. Regular Egg.
                    }
                    else
                    {
                        q.Player.UpdateEvent(0); // Under 7 seconds. Mandragora.
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10191, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetMerits(q.Player.MeritCount - 10);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.RuludeGardens, 10192, q =>
                {
                    if (q.Option >= 540 &&
                        q.Option < 660)
                    {
                        q.Quest.Complete();
                        q.Player.SetLevelCap(95);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_95);
                    }
                });
        }
    }
}
