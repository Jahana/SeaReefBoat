// Trial by Water (Log ID 5, Quest ID 133), ported from the quest branches of scripts/zones/Norg/npcs/Edal-Tahdal.lua and
// the win finish of scripts/battlefields/Cloister_of_Tides/trial_by_water.lua (quest rework B3b, Outlands group 9;
// call-only: both scripts now call this quest). Edal-Tahdal : !pos -13 1 -20 252. Available at Norg fame level 4, or
// completed once GetSystemTime() is past the char var TrialByWater_date (strictly): 109 (0, Tuning Fork of Water); option
// 1 deletes a completed quest, begins it, sets TrialByWater_date 0 and gives the fork. Accepted: 190 with neither the fork
// nor the Whisper of Tides (finish: a new fork), 110 with the fork, 112 with the whisper (its last argument a mask of the
// rewards held: Leviathan's Rod 1, Water Belt 2, Water Ring 4, Eye of Nept 8, the Leviathan spell 32). 112's option picks
// the reward: 1-4 an item, 5 10000 gil, 6 the Leviathan spell (AVATAR_UNLOCKED 0, 0, 2); with no free slot
// ITEM_CANNOT_BE_OBTAINED (but 5 and 6); else Heir of the Great Water, the whisper taken, TrialByWater_date set to the
// next JST midnight, Norg fame +60, completed. The Cloister of Tides battlefield (a BattlefieldQuest naming this quest:
// its entry wants the quest accepted or completed, a status read that stays in Lua) wins with 32001, whose finish
// (content:onEventFinishWin, which calls xi.cs.questEvent with no npc) gives Heir of the Great Water and the Whisper of
// Tides. Edal-Tahdal's 113 (the standard dialog) stays in his script. The Lua's state is the status and the char var
// TrialByWater_date (not a quest var).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 112's free-slot test is
// `option ~= 5 and option ~= 6`; the Lua's `or` always held, so a full inventory refused the gil and the spell too.
// Kept as the Lua has it: option 0 or above 6 with room calls player:addItem(0), which the engine only warns
// about, so the finish goes on and completes with no reward (the #no_reward flow, under its warning: line; a fake
// check pins it too); 109, 112 and 190 do not look at the status; 32001's
// finish gives the title and the whisper in any status; 109 option 1 gives the fork again when one is held.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrialByWater : QuestScript
    {
        public TrialByWater() : base(QuestLog.Outlands, "TRIAL_BY_WATER") { }

        // the 112 argument: a bit per reward held
        static int NumItem(IQuestPlayerView player)
        {
            int numitem = 0;

            if (player.HasItem(Item.LeviathansRod))
            {
                numitem = numitem + 1;
            }

            if (player.HasItem(Item.WaterBelt))
            {
                numitem = numitem + 2;
            }

            if (player.HasItem(Item.WaterRing))
            {
                numitem = numitem + 4;
            }

            if (player.HasItem(Item.EyeOfNept))
            {
                numitem = numitem + 8;
            }

            if (player.HasSpell(Spell.Leviathan))
            {
                numitem = numitem + 32;
            }

            return numitem;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Edal-Tahdal.lua onTrigger, in its order: start and restart the quest
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Norg) >= 4)
                .Npc(Zone.Norg, Npc.Norg.EdalTahdal).Called()
                    .OnTrigger(QuestReply.Event(109, 0, KeyItem.TuningForkOfWater));

            b.State(QuestStatus.Completed, when: q => q.Player.SystemTime > q.Player.CharVar("TrialByWater_date"))
                .Npc(Zone.Norg, Npc.Norg.EdalTahdal).Called()
                    .OnTrigger(QuestReply.Event(109, 0, KeyItem.TuningForkOfWater));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.EdalTahdal).Called().OnTrigger(q =>
                {
                    bool hasWhisperOfTides = q.Player.HasKeyItem(KeyItem.WhisperOfTides);

                    if (!q.Player.HasKeyItem(KeyItem.TuningForkOfWater) &&
                        !hasWhisperOfTides)
                    {
                        // defeat against Leviathan: a new fork
                        return QuestReply.Event(190, 0, KeyItem.TuningForkOfWater);
                    }
                    else if (!hasWhisperOfTides)
                    {
                        return QuestReply.Event(110, 0, KeyItem.TuningForkOfWater, 2);
                    }
                    else
                    {
                        return QuestReply.Event(112, 0, KeyItem.TuningForkOfWater, 2, 0, NumItem(q.Player));
                    }
                });

            // Edal-Tahdal.lua onEventFinish (109, 190, 112); trial_by_water.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Norg, 109, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Quest.Status == QuestStatus.Completed)
                        {
                            q.Player.DelQuest(QuestLog.Outlands, "TRIAL_BY_WATER");
                        }

                        q.Quest.Begin();
                        q.Player.SetCharVar("TrialByWater_date", 0);
                        q.Player.GiveKeyItem(KeyItem.TuningForkOfWater);
                    }
                })
                .OnEventFinish(Zone.Norg, 190, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TuningForkOfWater);
                })
                .OnEventFinish(Zone.Norg, 112, q =>
                {
                    int item = 0;

                    if (q.Option == 1)
                    {
                        item = Item.LeviathansRod;
                    }
                    else if (q.Option == 2)
                    {
                        item = Item.WaterBelt;
                    }
                    else if (q.Option == 3)
                    {
                        item = Item.WaterRing;
                    }
                    else if (q.Option == 4)
                    {
                        item = Item.EyeOfNept;
                    }

                    // the gil and the spell need no slot (the Lua's `or` always held: fixed, user ruling 2026-11-04)
                    if (q.Player.FreeSlotCount == 0 && (q.Option != 5 && q.Option != 6))
                    {
                        q.Text.Special(Text.Norg.ITEM_CANNOT_BE_OBTAINED, item);
                    }
                    else
                    {
                        if (q.Option == 5)
                        {
                            q.Player.GiveCurrency("gil", 10000);
                        }
                        else if (q.Option == 6)
                        {
                            q.Player.AddSpell(Spell.Leviathan); // Avatar
                            q.Text.Special(Text.Norg.AVATAR_UNLOCKED, 0, 0, 2);
                        }
                        else
                        {
                            // option 0 or above 6: player:addItem(0) is only a warning in the engine; the finish goes on and completes, no reward
                            q.Player.AddItem(item);
                            q.Text.Special(Text.Norg.ITEM_OBTAINED, item);
                        }

                        q.Player.AddTitle(Title.HeirOfTheGreatWater);
                        q.Player.DelKeyItem(KeyItem.WhisperOfTides); // as a trade for the above rewards
                        q.Player.SetCharVar("TrialByWater_date", q.Player.JstMidnight);
                        q.Player.AddFame(FameArea.Norg, 60);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][133] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "TRIAL_BY_WATER");
                    }
                })
                .OnEventFinish(Zone.CloisterOfTides, 32001, q =>
                {
                    q.Player.AddTitle(Title.HeirOfTheGreatWater);
                    q.Player.GiveKeyItem(KeyItem.WhisperOfTides);
                });
        }
    }
}
