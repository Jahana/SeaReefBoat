// Trial by Ice (Log ID 0, Quest ID 59), ported from the quest branches of scripts/zones/Northern_San_dOria/npcs/Gulmama.lua
// and the win finish of scripts/battlefields/Cloister_of_Frost/trial_by_ice.lua (quest rework B3b, San d'Oria group 12;
// call-only: both scripts now call this quest). Gulmama : !pos -186 0 107 231. Available at San d'Oria fame level 6, or
// completed once GetSystemTime() is past the char var TrialByIce_date (strictly): 706 (0, Tuning Fork of Ice); option 1
// deletes a completed quest, begins it, sets TrialByIce_date 0 and gives the fork. Accepted: 718 with neither the fork
// nor the Whisper of Frost (finish: a new fork), 707 with the fork, 709 with the whisper (its last argument a mask of the
// rewards held: Shiva's Claws 1, Ice Belt 2, Ice Ring 4, Rust 'B' Gone 8, the Shiva spell 32). 709's option picks the
// reward: 1-4 an item, 5 10000 gil, 6 the Shiva spell (SHIVA_UNLOCKED); with no free slot ITEM_CANNOT_BE_OBTAINED (for any
// option); else Heir of the Great Ice, the whisper taken, TrialByIce_date set to the next JST midnight, fame +60,
// completed (the raw player:completeQuest, which keeps the quest's own Quest[0][59] vars). The Cloister of Frost
// battlefield (a BattlefieldQuest naming this quest: its entry wants the quest accepted or completed, a status read
// that stays in Lua) wins with 32001, whose finish (content:onEventFinishWin, which calls
// xi.cs.questEvent with no npc) gives Heir of the Great Ice and the Whisper of Frost. Gulmama's Class Reunion branches
// (713 / 712, Windurst's quest, before this quest's call in her script) and her 710 stay in her script. The Lua's state is
// the status and the char var TrialByIce_date (not a quest var).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 709's free-slot test is
// `option ~= 5 and option ~= 6`; the Lua's `or` always held, so a full inventory refused the gil and the spell too.
// Kept as the Lua has it: option 0 or above 6 with room calls player:addItem(0), which the engine
// only warns about (it returns nil, raises nothing), so the finish goes on and completes with no reward (no golden trace
// can record it: the recorder refuses any "lua:" warning line; proved by the XI.Verify.Quests fake check); 706, 709 and 718 do not look at the status; 32001's finish
// gives the title and the whisper in any status (a completed char's re-run, or a GM status change inside the arena).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TrialByIce : QuestScript
    {
        public TrialByIce() : base(QuestLog.Sandoria, "TRIAL_BY_ICE") { }

        // the 709 argument: a bit per reward held
        static int NumItem(IQuestPlayerView player)
        {
            int numitem = 0;

            if (player.HasItem(Item.ShivasClaws))
            {
                numitem = numitem + 1;
            }

            if (player.HasItem(Item.IceBelt))
            {
                numitem = numitem + 2;
            }

            if (player.HasItem(Item.IceRing))
            {
                numitem = numitem + 4;
            }

            if (player.HasItem(Item.BottleOfRustBGone))
            {
                numitem = numitem + 8;
            }

            if (player.HasSpell(Spell.Shiva))
            {
                numitem = numitem + 32;
            }

            return numitem;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Gulmama.lua onTrigger, after the Class Reunion branches, in its order: start and restart the quest
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 6)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Gulmama).Called()
                    .OnTrigger(QuestReply.Event(706, 0, KeyItem.TuningForkOfIce));

            b.State(QuestStatus.Completed, when: q => q.Player.SystemTime > q.Player.CharVar("TrialByIce_date"))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Gulmama).Called()
                    .OnTrigger(QuestReply.Event(706, 0, KeyItem.TuningForkOfIce));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Gulmama).Called().OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TuningForkOfIce) &&
                        !q.Player.HasKeyItem(KeyItem.WhisperOfFrost))
                    {
                        // defeat against Shiva: a new fork
                        return QuestReply.Event(718, 0, KeyItem.TuningForkOfIce);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.WhisperOfFrost))
                    {
                        return QuestReply.Event(707, 0, KeyItem.TuningForkOfIce, 4);
                    }
                    else
                    {
                        return QuestReply.Event(709, 0, KeyItem.TuningForkOfIce, 4, 0, NumItem(q.Player));
                    }
                });

            // Gulmama.lua onEventFinish (706, 718, 709); trial_by_ice.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.NorthernSanDoria, 706, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Quest.Status == QuestStatus.Completed)
                        {
                            q.Player.DelQuest(QuestLog.Sandoria, "TRIAL_BY_ICE");
                        }

                        q.Quest.Begin();
                        q.Player.SetCharVar("TrialByIce_date", 0);
                        q.Player.GiveKeyItem(KeyItem.TuningForkOfIce);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 718, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TuningForkOfIce);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 709, q =>
                {
                    int item = 0;

                    if (q.Option == 1)
                    {
                        item = Item.ShivasClaws;
                    }
                    else if (q.Option == 2)
                    {
                        item = Item.IceBelt;
                    }
                    else if (q.Option == 3)
                    {
                        item = Item.IceRing;
                    }
                    else if (q.Option == 4)
                    {
                        item = Item.BottleOfRustBGone;
                    }

                    // the gil and the spell need no slot (the Lua's `or` always held: fixed, user ruling 2026-11-04)
                    if (q.Player.FreeSlotCount == 0 && (q.Option != 5 && q.Option != 6))
                    {
                        q.Text.Special(Text.NorthernSanDoria.ITEM_CANNOT_BE_OBTAINED, item);
                    }
                    else
                    {
                        if (q.Option == 5)
                        {
                            q.Player.GiveCurrency("gil", 10000);
                        }
                        else if (q.Option == 6)
                        {
                            q.Player.AddSpell(Spell.Shiva);
                            q.Text.Special(Text.NorthernSanDoria.SHIVA_UNLOCKED, 0, 0, 4);
                        }
                        else
                        {
                            // option 0 or above 6: player:addItem(0) is only a warning in the engine; the finish goes on and completes, no reward
                            q.Player.AddItem(item);
                            q.Text.Special(Text.NorthernSanDoria.ITEM_OBTAINED, item);
                        }

                        q.Player.AddTitle(Title.HeirOfTheGreatIce);
                        q.Player.DelKeyItem(KeyItem.WhisperOfFrost);
                        q.Player.SetCharVar("TrialByIce_date", q.Player.JstMidnight);
                        q.Player.AddFame(FameArea.Sandoria, 60);
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][59] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "TRIAL_BY_ICE");
                    }
                })
                .OnEventFinish(Zone.CloisterOfFrost, 32001, q =>
                {
                    q.Player.AddTitle(Title.HeirOfTheGreatIce);
                    q.Player.GiveKeyItem(KeyItem.WhisperOfFrost);
                });
        }
    }
}
