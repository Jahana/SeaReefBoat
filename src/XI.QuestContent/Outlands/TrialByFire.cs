// Trial by Fire (Log ID 5, Quest ID 12), a line-for-line port of scripts/quests/outlands/Trial_by_Fire.lua (quest rework
// B3b Outlands, group 2). Kazham: Ronta-Onta !pos 100.4 -14 -97.7 (no script; default 274), Dodmos !pos 102.6 -14 -97.7 (his
// script: Trial-Size Trial by Fire; default 275). The Cloister of Flames battlefield
// (scripts/battlefields/Cloister_of_Flames/trial_by_fire.lua, a BattlefieldQuest that names this quest: a status read,
// entry by the Tuning Fork of Fire) wins with event 32001, whose finish here gives the Whisper of Flames and the Heir of the
// Great Fire title when the char's local var battlefieldWin is the battlefield's id.
// At Windurst fame level 6 Ronta-Onta offers 270 (0, fork, 0, 1: the injured researcher's introduction); option 1: the
// Tuning Fork of Fire, begun. Accepted: Ronta 273 with the Whisper of Flames (0, 1, 0, 0, the reward mask), 285 without the
// fork (a new fork), else 271 (4, fork); Dodmos 272. 273's option picks the reward: an escape (below 1 or above 6) does
// nothing; 1-4 an item (Ifrit's Blade, Fire Belt, Fire Ring, Egil's Torch), 5 10000 gil, 6 the Ifrit spell
// (IFRIT_UNLOCKED); when given, the quest completes (Windurst fame 60), the whisper goes and the Timer var runs to the next
// JST day. Completed with the Timer gone, Ronta offers 270 (0, fork) and option 1 deletes the quest, gives the fork and
// begins it anew; while the Timer runs he answers nil (the default).
// The mask: a bit per reward item held (0-3) and bit 5 for the Ifrit spell held or SMN not unlocked (player:hasJob).
// Kept as the Lua has it: option 6 reports no failure (the spell given, completed); the fork given again on a repeat while
// one is still held (npcUtil.giveKeyItem); 270's finish in the completed section deletes the quest before it begins.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrialByFire : QuestScript
    {
        // the Lua's rewardItems, [0] .. [3]
        static readonly int[] RewardItems =
        {
            Item.IfritsBlade,
            Item.FireBelt,
            Item.FireRing,
            Item.EgilsTorch,
        };

        public TrialByFire() : base(QuestLog.Outlands, "TRIAL_BY_FIRE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Trial_by_Fire";

        // getRewardMask: a bit per reward item held (0-3), bit 5 for the Ifrit spell or no SMN unlocked
        static int GetRewardMask(IQuestPlayerView player)
        {
            int rewardMask = 0;

            for (int bitNum = 0; bitNum < RewardItems.Length; bitNum++)
            {
                if (player.HasItem(RewardItems[bitNum]))
                {
                    rewardMask |= 1 << bitNum;
                }
            }

            if (player.HasSpell(Spell.Ifrit) || !player.HasJob(Job.Smn))
            {
                rewardMask |= 1 << 5;
            }

            return rewardMask;
        }

        // giveQuestReward: called with 1 <= eventOption <= 6 only (273's finish returns first otherwise)
        static bool GiveQuestReward(IQuestContext q, int eventOption)
        {
            bool wasRewarded = true;

            if (eventOption <= 4)
            {
                wasRewarded = q.Player.GiveItem(RewardItems[eventOption - 1]);
            }
            else if (eventOption == 5)
            {
                q.Player.GiveCurrency("gil", 10000);
            }
            else if (eventOption == 6)
            {
                q.Player.AddSpell(Spell.Ifrit);
                q.Text.Special(Text.Kazham.IFRIT_UNLOCKED, 0, 0, 0);
            }

            return wasRewarded;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 60, FameArea = FameArea.Windurst });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 6)
                // Fourth event param plays the injured researcher introduction only seen before the first completion.
                .Npc(Zone.Kazham, Npc.Kazham.RontaOnta).OnTrigger(QuestReply.ProgressEvent(270, 0, KeyItem.TuningForkOfFire, 0, 1))
                .OnEventFinish(Zone.Kazham, 270, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.TuningForkOfFire);
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.Dodmos).OnTrigger(QuestReply.Event(272))
                .Npc(Zone.Kazham, Npc.Kazham.RontaOnta).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.WhisperOfFlames))
                    {
                        return QuestReply.ProgressEvent(273, 0, 1, 0, 0, GetRewardMask(q.Player));
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.TuningForkOfFire))
                    {
                        // Player has failed the BCNM and requires a new Tuning Fork.
                        return QuestReply.ProgressEvent(285, 0, KeyItem.TuningForkOfFire);
                    }
                    else
                    {
                        return QuestReply.Event(271, 4, KeyItem.TuningForkOfFire);
                    }
                })
                .OnEventFinish(Zone.Kazham, 273, q =>
                {
                    // Retail does not advance the quest on an escaped cutscene.
                    if (q.Option < 1 || q.Option > 6)
                    {
                        return;
                    }

                    if (GiveQuestReward(q, q.Option))
                    {
                        q.Quest.Complete();
                        q.Player.DelKeyItem(KeyItem.WhisperOfFlames);
                        // quest:setTimedVar(player, 'Timer', NextJstDay()): setVar(prefix .. 'Timer', 1, expiry)
                        q.Quest.SetVar("Timer", 1, (uint)q.Player.NextJstDay);
                    }
                })
                .OnEventFinish(Zone.Kazham, 285, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TuningForkOfFire);
                })
                // the battlefield's win event (BattlefieldQuest:onBattlefieldWin sets battlefieldWin while accepted)
                .OnEventFinish(Zone.CloisterOfFlames, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.TrialByFire)
                    {
                        q.Player.GiveKeyItem(KeyItem.WhisperOfFlames);
                        q.Player.AddTitle(Title.HeirOfTheGreatFire);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.RontaOnta).OnTrigger(q =>
                {
                    // Repeatable once per JST day.
                    if (q.Quest.Var("Timer") == 0)
                    {
                        return QuestReply.ProgressEvent(270, 0, KeyItem.TuningForkOfFire);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Kazham, 270, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.Outlands, "TRIAL_BY_FIRE");

                        q.Player.GiveKeyItem(KeyItem.TuningForkOfFire);

                        q.Quest.Begin();
                    }
                });
        }
    }
}
