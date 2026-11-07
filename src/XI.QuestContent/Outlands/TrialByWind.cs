// Trial by Wind (Log ID 5, Quest ID 194), ported from the quest branches of scripts/zones/Rabao/npcs/Agado-Pugado.lua
// and the win finish of scripts/battlefields/Cloister_of_Gales/trial_by_wind.lua (quest rework B3b, Outlands group 9;
// call-only: both scripts now call this quest). Agado-Pugado : !pos -17 7 -10 247. Available at Selbina-Rabao fame
// level 5, or completed once GetSystemTime() is past the char var TrialByWind_date (strictly): 66 (0, 331: the Lua's
// literal, the Tuning Fork of Wind's id); option 1 deletes a completed quest, begins it, sets TrialByWind_date 0 and gives
// the fork. Accepted: 107 with neither the fork nor the Whisper of Gales (finish: a new fork), 67 with the fork, 69 with
// the whisper (its last argument a mask of the rewards held: Garuda's Dagger 1, Wind Belt 2, Wind Ring 4, Bubbly Water 8,
// the Garuda spell 32). 69's option picks the reward: 1-4 an item, 5 10000 gil, 6 the Garuda spell (GARUDA_UNLOCKED 0, 0,
// 3); with no free slot ITEM_CANNOT_BE_OBTAINED (but 5 and 6); else Heir of the Great Wind, the whisper taken,
// TrialByWind_date set to the next JST midnight, San d'Oria and Bastok fame +30 each, completed. The Cloister of Gales
// battlefield (a BattlefieldQuest naming this quest: its entry wants the quest accepted or completed, a status read that
// stays in Lua) wins with 32001, whose finish (content:onEventFinishWin, which calls xi.cs.questEvent with no npc) gives
// Heir of the Great Wind and the Whisper of Gales. Agado-Pugado's Carbuncle Debacle branches (86 / 87 / 88, Windurst's
// quest, before this quest's call in his script) and his 70 (the standard dialog) stay in his script. The Lua's state is
// the status and the char var TrialByWind_date (not a quest var).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 69's free-slot test is
// `option ~= 5 and option ~= 6`; the Lua's `or` always held, so a full inventory refused the gil and the spell too.
// Kept as the Lua has it: option 0 or above 6 with room calls player:addItem(0), which the engine only warns
// about, so the finish goes on and completes with no reward (the #no_reward flow, under its warning: line; a fake
// check pins it too); 66, 69 and 107 do not look at the status; 32001's
// finish gives the title and the whisper in any status; 66 option 1 gives the fork again when one is held.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrialByWind : QuestScript
    {
        // the Lua's literal event argument 331 (xi.keyItem.TUNING_FORK_OF_WIND's id)
        const int ForkArg = 331;

        public TrialByWind() : base(QuestLog.Outlands, "TRIAL_BY_WIND") { }

        // the 69 argument: a bit per reward held
        static int NumItem(IQuestPlayerView player)
        {
            int numitem = 0;

            if (player.HasItem(Item.GarudasDagger))
            {
                numitem = numitem + 1;
            }

            if (player.HasItem(Item.WindBelt))
            {
                numitem = numitem + 2;
            }

            if (player.HasItem(Item.WindRing))
            {
                numitem = numitem + 4;
            }

            if (player.HasItem(Item.BottleOfBubblyWater))
            {
                numitem = numitem + 8;
            }

            if (player.HasSpell(Spell.Garuda))
            {
                numitem = numitem + 32;
            }

            return numitem;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Agado-Pugado.lua onTrigger, after the Carbuncle Debacle branches, in its order: start and restart the quest
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.SelbinaRabao) >= 5)
                .Npc(Zone.Rabao, Npc.Rabao.AgadoPugado).Called()
                    .OnTrigger(QuestReply.Event(66, 0, ForkArg));

            b.State(QuestStatus.Completed, when: q => q.Player.SystemTime > q.Player.CharVar("TrialByWind_date"))
                .Npc(Zone.Rabao, Npc.Rabao.AgadoPugado).Called()
                    .OnTrigger(QuestReply.Event(66, 0, ForkArg));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Rabao, Npc.Rabao.AgadoPugado).Called().OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TuningForkOfWind) &&
                        !q.Player.HasKeyItem(KeyItem.WhisperOfGales))
                    {
                        // defeat against Garuda: a new fork
                        return QuestReply.Event(107, 0, ForkArg);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.WhisperOfGales))
                    {
                        return QuestReply.Event(67, 0, ForkArg, 3);
                    }
                    else
                    {
                        return QuestReply.Event(69, 0, ForkArg, 3, 0, NumItem(q.Player));
                    }
                });

            // Agado-Pugado.lua onEventFinish (66, 107, 69); trial_by_wind.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Rabao, 66, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Quest.Status == QuestStatus.Completed)
                        {
                            q.Player.DelQuest(QuestLog.Outlands, "TRIAL_BY_WIND");
                        }

                        q.Quest.Begin();
                        q.Player.SetCharVar("TrialByWind_date", 0);
                        q.Player.GiveKeyItem(KeyItem.TuningForkOfWind);
                    }
                })
                .OnEventFinish(Zone.Rabao, 107, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TuningForkOfWind);
                })
                .OnEventFinish(Zone.Rabao, 69, q =>
                {
                    int item = 0;

                    if (q.Option == 1)
                    {
                        item = Item.GarudasDagger;
                    }
                    else if (q.Option == 2)
                    {
                        item = Item.WindBelt;
                    }
                    else if (q.Option == 3)
                    {
                        item = Item.WindRing;
                    }
                    else if (q.Option == 4)
                    {
                        item = Item.BottleOfBubblyWater;
                    }

                    // the gil and the spell need no slot (the Lua's `or` always held: fixed, user ruling 2026-11-04)
                    if (q.Player.FreeSlotCount == 0 && (q.Option != 5 && q.Option != 6))
                    {
                        q.Text.Special(Text.Rabao.ITEM_CANNOT_BE_OBTAINED, item);
                    }
                    else
                    {
                        if (q.Option == 5)
                        {
                            q.Player.GiveCurrency("gil", 10000);
                        }
                        else if (q.Option == 6)
                        {
                            q.Player.AddSpell(Spell.Garuda); // Garuda Spell
                            q.Text.Special(Text.Rabao.GARUDA_UNLOCKED, 0, 0, 3);
                        }
                        else
                        {
                            // option 0 or above 6: player:addItem(0) is only a warning in the engine; the finish goes on and completes, no reward
                            q.Player.AddItem(item);
                            q.Text.Special(Text.Rabao.ITEM_OBTAINED, item);
                        }

                        q.Player.AddTitle(Title.HeirOfTheGreatWind);
                        q.Player.DelKeyItem(KeyItem.WhisperOfGales); // as a trade for the above rewards
                        q.Player.SetCharVar("TrialByWind_date", q.Player.JstMidnight);
                        q.Player.AddFame(FameArea.Sandoria, 30);
                        q.Player.AddFame(FameArea.Bastok, 30);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][194] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "TRIAL_BY_WIND");
                    }
                })
                .OnEventFinish(Zone.CloisterOfGales, 32001, q =>
                {
                    q.Player.AddTitle(Title.HeirOfTheGreatWind);
                    q.Player.GiveKeyItem(KeyItem.WhisperOfGales);
                });
        }
    }
}
