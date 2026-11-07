// Trial by Lightning (Log ID 4, Quest ID 27), ported from the quest branches of scripts/zones/Mhaura/npcs/Ripapa.lua
// and the win finish of scripts/battlefields/Cloister_of_Storms/trial_by_lightning.lua (quest rework B3b, Other Areas
// group 11; call-only: both scripts now call this quest). Ripapa : !pos 29 -15 55 249. Available at Windurst fame level
// 6, or completed once GetSystemTime() is past the char var TrialByLightning_date (strictly): 10016 (0, Tuning Fork of
// Lightning); option 1 deletes a completed quest, begins it, sets TrialByLightning_date 0 and gives the fork
// (npcUtil.giveKeyItem). Accepted: 10024 with neither the fork nor the Whisper of Storms (finish: a new fork), 10017
// (0, the fork, 5) without the whisper, 10019 (0, the fork, 5, 0, the mask) with it (a bit per reward held: Ramuh's
// Staff 1, Lightning Belt 2, Lightning Ring 4, Elder Branch 8, the Ramuh spell 32). 10019's option picks the reward:
// 1-4 an item, 5 10000 gil (npcUtil.giveCurrency), 6 the Ramuh spell (RAMUH_UNLOCKED 0, 0, 5); with no free slot
// ITEM_CANNOT_BE_OBTAINED (for any option but 5 and 6, which need none); else Heir of the Great Lightning, the whisper
// taken, TrialByLightning_date set to the next JST midnight, Windurst fame +60, completed (player:completeQuest). The
// Cloister of Storms battlefield (a BattlefieldQuest naming this quest: its entry wants the quest accepted or
// completed, a status read that stays in Lua) wins with 32001, whose finish (content:onEventFinishWin, which calls
// xi.cs.questEvent with no npc) gives Heir of the Great Lightning and the Whisper of Storms. Ripapa's Carbuncle Debacle
// branches (10022 / 10023, Windurst's quest, before this quest's call in his script, with their finish) and his 10020
// (the standard dialog) stay in his script.
// The Lua's state is the status and the char var TrialByLightning_date (not a quest var).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 10019's free-slot test is
// `option ~= 5 and option ~= 6`; the Lua's `or` always held, so a full inventory refused the gil and the spell too.
// Kept as the Lua has it: option 0 or above 6 with room calls player:addItem(0), which the engine only warns
// about, so the finish goes on and completes with no reward (the #no_reward flow, under its warning: line; a fake
// check pins it too); 10016, 10019 and 10024 do not look at the status;
// 32001's finish gives the title and the whisper in any status; 10016 option 1 gives the fork again when one is held.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TrialByLightning : QuestScript
    {
        const string DateVar = "TrialByLightning_date";

        public TrialByLightning() : base(QuestLog.OtherAreas, "TRIAL_BY_LIGHTNING") { }

        // the 10019 argument: a bit per reward held
        static int NumItem(IQuestPlayerView player)
        {
            int numitem = 0;

            if (player.HasItem(Item.RamuhsStaff))
            {
                numitem = numitem + 1;
            }

            if (player.HasItem(Item.LightningBelt))
            {
                numitem = numitem + 2;
            }

            if (player.HasItem(Item.LightningRing))
            {
                numitem = numitem + 4;
            }

            if (player.HasItem(Item.ElderBranch))
            {
                numitem = numitem + 8;
            }

            if (player.HasSpell(Spell.Ramuh))
            {
                numitem = numitem + 32;
            } // Ability to summon Ramuh

            return numitem;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Ripapa.lua onTrigger, after his Carbuncle Debacle arms, in its order: start and restart the quest
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 6)
                .Npc(Zone.Mhaura, Npc.Mhaura.Ripapa).Called()
                    .OnTrigger(QuestReply.Event(10016, 0, KeyItem.TuningForkOfLightning));

            b.State(QuestStatus.Completed, when: q => q.Player.SystemTime > q.Player.CharVar(DateVar))
                .Npc(Zone.Mhaura, Npc.Mhaura.Ripapa).Called()
                    .OnTrigger(QuestReply.Event(10016, 0, KeyItem.TuningForkOfLightning));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Ripapa).Called().OnTrigger(q =>
                {
                    bool hasWhisperOfStorms = q.Player.HasKeyItem(KeyItem.WhisperOfStorms);

                    if (!q.Player.HasKeyItem(KeyItem.TuningForkOfLightning) &&
                        !hasWhisperOfStorms)
                    {
                        // Defeat against Ramuh : Need new Fork
                        return QuestReply.Event(10024, 0, KeyItem.TuningForkOfLightning);
                    }
                    else if (!hasWhisperOfStorms)
                    {
                        return QuestReply.Event(10017, 0, KeyItem.TuningForkOfLightning, 5);
                    }
                    else
                    {
                        return QuestReply.Event(10019, 0, KeyItem.TuningForkOfLightning, 5, 0, NumItem(q.Player));
                    }
                });

            // Ripapa.lua onEventFinish (10016, 10024, 10019); trial_by_lightning.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Mhaura, 10016, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Quest.Status == QuestStatus.Completed)
                        {
                            q.Player.DelQuest(QuestLog.OtherAreas, "TRIAL_BY_LIGHTNING");
                        }

                        q.Quest.Begin();
                        q.Player.SetCharVar(DateVar, 0);
                        q.Player.GiveKeyItem(KeyItem.TuningForkOfLightning);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 10024, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TuningForkOfLightning);
                })
                .OnEventFinish(Zone.Mhaura, 10019, q =>
                {
                    int item = 0;

                    if (q.Option == 1)
                    {
                        item = Item.RamuhsStaff;
                    }
                    else if (q.Option == 2)
                    {
                        item = Item.LightningBelt;
                    }
                    else if (q.Option == 3)
                    {
                        item = Item.LightningRing;
                    }
                    else if (q.Option == 4)
                    {
                        item = Item.ElderBranch;
                    }

                    // the gil and the spell need no slot (the Lua's `or` always held: fixed, user ruling 2026-11-04)
                    if (q.Player.FreeSlotCount == 0 && (q.Option != 5 && q.Option != 6))
                    {
                        q.Text.Special(Text.Mhaura.ITEM_CANNOT_BE_OBTAINED, item);
                    }
                    else
                    {
                        if (q.Option == 5)
                        {
                            q.Player.GiveCurrency("gil", 10000);
                        }
                        else if (q.Option == 6)
                        {
                            q.Player.AddSpell(Spell.Ramuh); // Ramuh Spell
                            q.Text.Special(Text.Mhaura.RAMUH_UNLOCKED, 0, 0, 5);
                        }
                        else
                        {
                            // option 0 or above 6: player:addItem(0) is only a warning in the engine; the finish
                            // goes on and completes, no reward
                            q.Player.AddItem(item);
                            q.Text.Special(Text.Mhaura.ITEM_OBTAINED, item); // Item
                        }

                        q.Player.AddTitle(Title.HeirOfTheGreatLightning);
                        q.Player.DelKeyItem(KeyItem.WhisperOfStorms); // as a trade for the above rewards
                        q.Player.SetCharVar(DateVar, q.Player.JstMidnight);
                        q.Player.AddFame(FameArea.Windurst, 60);
                        // the raw player:completeQuest, which keeps the quest's own Quest[4][27] vars
                        q.Player.CompleteQuest(QuestLog.OtherAreas, "TRIAL_BY_LIGHTNING");
                    }
                })
                .OnEventFinish(Zone.CloisterOfStorms, 32001, q =>
                {
                    q.Player.AddTitle(Title.HeirOfTheGreatLightning);
                    q.Player.GiveKeyItem(KeyItem.WhisperOfStorms);
                });
        }
    }
}
