// The Moonlit Path (Log ID 2, Quest ID 9), a line-for-line port of scripts/quests/windurst/The_Moonlit_Path.lua (quest
// rework B3b). Leepe-Hoppe : !pos 11.432 -9.749 -197.475 238. Offered at fame level 6 (842, option 2: begun). With the six
// avatar whispers Leepe-Hoppe's 844 gives the Moon Bauble (and takes the whispers); the Full Moon Fountain battlefield's
// win (its 32001, after BattlefieldQuest sets the local var battlefieldWin) takes the bauble and gives the Whisper of the
// Moon; Leepe-Hoppe's 846 then offers a reward (options 1-5 an item, 6 15000 gil, 7 the Fenrir pact, 8 the Fenrir
// Whistle) and completes the quest (fame +60, Heir of the New Moon, a Timer that expires at the next JST day; a Windurst
// char at rank 10 who completed The Promise also gets the Dark Mana Orb). Events 846 and 850 hide the pact (bit 6)
// without SMN and the mount (bit 7) without the trainer's whistle. A repeat (848, option 2, once the Timer is gone) puts
// the quest back in the log; Leepe-Hoppe plays 850 and 851 in place of 846 and 847 from then on. Its era module is
// Modules/Era/Windurst/TheMoonlitPath.cs. Kept as the Lua file has it: the turn-in ignores quest:complete's result;
// quest:complete wipes the vars, so the 850 finish sets Repeat again after it; the pact is given whatever the job.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheMoonlitPath : QuestScript
    {
        static readonly int[] AvatarWhispers =
        {
            KeyItem.WhisperOfFlames,
            KeyItem.WhisperOfTremors,
            KeyItem.WhisperOfTides,
            KeyItem.WhisperOfGales,
            KeyItem.WhisperOfFrost,
            KeyItem.WhisperOfStorms,
        };

        static readonly Dictionary<int, int> RewardItems = new Dictionary<int, int>
        {
            [1] = Item.FenrirsStone,
            [2] = Item.FenrirsCape,
            [3] = Item.FenrirsTorque,
            [4] = Item.FenrirsEarring,
            [5] = Item.AncientsKey,
        };

        public TheMoonlitPath() : base(QuestLog.Windurst, "THE_MOONLIT_PATH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/The_Moonlit_Path";

        // 846 and 850 (the same function in Lua)
        static void TurnInOnEventFinish(IQuestContext q)
        {
            int option = q.Option;

            // Retail does not advance the quest on an escaped cutscene.
            if (option < 1 || option > 8)
            {
                return;
            }

            if (option <= 5)
            {
                if (!q.Player.GiveItem(RewardItems[option]))
                {
                    return;
                }
            }
            else if (option == 6)
            {
                q.Player.GiveCurrency("gil", 15000);
            }
            else if (option == 7)
            {
                q.Player.AddSpell(Spell.Fenrir, silentLog: true);
                // player:messageText(npc, windurstWatersID.text.FENRIR_UNLOCKED, false, 6): the finish's npc
                q.Text.MessageText(Text.WindurstWaters.FENRIR_UNLOCKED, false, 6);
            }
            else if (option == 8)
            {
                q.Player.GiveKeyItem(KeyItem.FenrirWhistle);
            }

            q.Quest.Complete();
            q.Player.DelKeyItem(KeyItem.WhisperOfTheMoon);
            // quest:setTimedVar(player, 'Timer', NextJstDay()): player:setVar(varPrefix .. 'Timer', 1, expiry)
            q.Quest.SetVar("Timer", 1, (uint)q.Player.NextJstDay);

            // quest:complete() wipes the vars. Set again so Leepe-Hoppe plays 851 in place of 847.
            if (q.Csid == 850)
            {
                q.Quest.SetVar("Repeat", 1);
            }

            if (q.Player.Nation == Nation.Windurst &&
                q.Player.Rank(Nation.Windurst) == 10 &&
                q.Player.HasCompletedQuest(QuestLog.Windurst, "THE_PROMISE"))
            {
                q.Player.GiveKeyItem(KeyItem.DarkManaOrb);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 60, FameArea = FameArea.Windurst, Title = Title.HeirOfTheNewMoon });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 6)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe)
                    .OnTrigger(QuestReply.ProgressEvent(842, 0, Item.CarbunclesRuby))
                .OnEventFinish(Zone.WindurstWaters, 842, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .OnEventFinish(Zone.FullMoonFountain, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.MoonlitPath)
                    {
                        q.Player.DelKeyItem(KeyItem.MoonBauble);
                        q.Player.GiveKeyItem(KeyItem.WhisperOfTheMoon);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MoonBauble))
                    {
                        return QuestReply.Event(845, 0, Item.CarbunclesRuby, KeyItem.MoonBauble); // Reminder: take the moon bauble to the Full Moon Fountain.
                    }

                    if (!q.Player.HasKeyItem(KeyItem.WhisperOfTheMoon))
                    {
                        foreach (int whisper in AvatarWhispers)
                        {
                            if (!q.Player.HasKeyItem(whisper))
                            {
                                return QuestReply.Event(843, 0, Item.CarbunclesRuby); // Reminder: gather the six whispers.
                            }
                        }

                        return QuestReply.ProgressEvent(844, 0, Item.CarbunclesRuby, KeyItem.MoonBauble);
                    }

                    // Set bits hide reward menu options.
                    int rewardMask = 0;

                    foreach (var entry in RewardItems)
                    {
                        if (q.Player.HasItem(entry.Value))
                        {
                            // utils.mask.setBit(rewardMask, option - 1, true)
                            rewardMask |= 1 << (entry.Key - 1);
                        }
                    }

                    if (q.Player.HasSpell(Spell.Fenrir) || !q.Player.HasJob(Job.Smn))
                    {
                        rewardMask |= 1 << 6;
                    }

                    if (q.Player.HasKeyItem(KeyItem.FenrirWhistle) ||
                        !q.Player.HasKeyItem(KeyItem.TrainersWhistle))
                    {
                        rewardMask |= 1 << 7;
                    }

                    if (q.Quest.Var("Repeat") == 1)
                    {
                        return QuestReply.ProgressEvent(850, 0, Item.FenrirsEarring, Item.AncientsKey, Item.CarbunclesRuby, rewardMask, Item.FenrirsStone, Item.FenrirsCape);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(846, 0, Item.FenrirsEarring, Item.AncientsKey, Item.CarbunclesRuby, rewardMask, Item.FenrirsStone, Item.FenrirsCape);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 844, q =>
                {
                    // Retail does not advance the quest on an escaped cutscene.
                    if (q.Option != 0)
                    {
                        return;
                    }

                    q.Player.GiveKeyItem(KeyItem.MoonBauble);

                    foreach (int whisper in AvatarWhispers)
                    {
                        q.Player.DelKeyItem(whisper);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 846, TurnInOnEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 850, TurnInOnEventFinish);

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).OnTrigger(q =>
                {
                    // Repeatable once per JST day.
                    if (q.Quest.Var("Timer") == 0)
                    {
                        return QuestReply.ProgressEvent(848, 0, Item.CarbunclesRuby, KeyItem.MoonBauble);
                    }
                    else if (q.Quest.Var("Repeat") == 1)
                    {
                        return QuestReply.Event(851, 0, Item.CarbunclesRuby, Item.AncientsKey, Item.CarbunclesRuby, 0, Item.FenrirsStone, Item.FenrirsCape);
                    }
                    else
                    {
                        return QuestReply.Event(847, 0, Item.CarbunclesRuby);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 848, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Windurst, "THE_MOONLIT_PATH");
                        q.Player.GiveKeyItem(KeyItem.MoonBauble);
                        q.Quest.Begin();
                        q.Quest.SetVar("Repeat", 1);
                    }
                });
        }
    }
}
