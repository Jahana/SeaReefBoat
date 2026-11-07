// Trial by Earth (Log ID 1, Quest ID 61), a line-for-line port of scripts/quests/bastok/Trial_by_Earth.lua (quest
// rework B3b). Juroro : !pos 32 7 -41 236. At Bastok fame level 6 Juroro offers 249 (option 1: the Tuning Fork of
// Earth, begun); the arguments show The Puppet Master's earth pendulum when that quest is accepted at Prog 1. Accepted:
// 250 with the fork, 284 without it (a new fork), 252 with the Whisper of Tremors. The Cloister of Tremors battlefield
// (scripts/battlefields/Cloister_of_Tremors/trial_by_earth.lua, a BattlefieldQuest that names this quest: its entry
// needs the quest accepted or completed, a status read) wins with event 32001, whose finish here gives the Whisper of
// Tremors and the Heir of the Great Earth title when the char's local var battlefieldWin is the battlefield's id.
// 252's option picks the reward: 1-4 an item, 5 10000 gil, 6 the Titan spell; then the quest completes, the whisper
// goes and the quest's Timer var runs to the next JST day. Completed with the Timer gone, Juroro offers 249 again and
// option 1 deletes the quest and begins it anew. Kept as the Lua file has it: option 0 indexes rewardItems[-1], nil,
// and npcUtil.giveItem(player, nil) gives nothing and returns true, so 252's option 0 (and any option above 6)
// completes the quest with no reward.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TrialByEarth : QuestScript
    {
        // the Lua's rewardItems, [0] .. [3]
        static readonly int[] RewardItems =
        {
            Item.TitansCudgel,
            Item.EarthBelt,
            Item.EarthRing,
            Item.DoseOfDesertLight,
        };

        public TrialByEarth() : base(QuestLog.Bastok, "TRIAL_BY_EARTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Trial_by_Earth";

        // getRewardMask: a bit per reward item held (0-3), bit 5 for the Titan spell
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

            if (player.HasSpell(Spell.Titan))
            {
                rewardMask |= 1 << 5;
            }

            return rewardMask;
        }

        // giveQuestReward
        static bool GiveQuestReward(IQuestContext q, int eventOption)
        {
            bool wasRewarded = true;

            if (eventOption <= 4)
            {
                // rewardItems[eventOption - 1]: option 0 reads rewardItems[-1], which is nil, and
                // npcUtil.giveItem(player, nil) gives nothing and returns true (kept)
                wasRewarded = eventOption >= 1 ? q.Player.GiveItem(RewardItems[eventOption - 1]) : true;
            }
            else if (eventOption == 5)
            {
                q.Player.GiveCurrency("gil", 10000);
            }
            else if (eventOption == 6)
            {
                q.Player.AddSpell(Spell.Titan);
                q.Text.Special(Text.PortBastok.TITAN_UNLOCKED, 0, 0, 1);
            }

            return wasRewarded;
        }

        // Juroro's offer (sections 1 and 3): the arguments name The Puppet Master's earth pendulum while that quest is
        // accepted at Prog 1
        static QuestReply Offer(IQuestContext q)
        {
            bool smnAF = q.Player.QuestStatus(QuestLog.Windurst, "THE_PUPPET_MASTER") == QuestStatus.Accepted &&
                // player:getVar('Quest[2][81]Prog'): getVar is getCharVar's binding (ZoneHost.Bindings)
                q.Player.CharVar("Quest[2][81]Prog") == 1;
            int param4 = smnAF ? 1 : 0;
            int param6 = smnAF ? Item.EarthPendulum : 0;
            int param7 = smnAF ? 1 : 0;

            return QuestReply.ProgressEvent(249, 0, KeyItem.TuningForkOfEarth, 0, param4, 0, param6, param7);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 60, FameArea = FameArea.Bastok });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 6)
                .Npc(Zone.PortBastok, Npc.PortBastok.Juroro).OnTrigger(Offer)
                .OnEventFinish(Zone.PortBastok, 249, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.TuningForkOfEarth);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Juroro).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.WhisperOfTremors))
                    {
                        if (!q.Player.HasKeyItem(KeyItem.TuningForkOfEarth))
                        {
                            // the char lost the fight and needs a new fork
                            return QuestReply.ProgressEvent(284, 0, KeyItem.TuningForkOfEarth);
                        }
                        else
                        {
                            return QuestReply.Event(250, 0, KeyItem.TuningForkOfEarth, 1);
                        }
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(252, 0, KeyItem.TuningForkOfEarth, 1, 0,
                            GetRewardMask(q.Player));
                    }
                })
                .OnEventFinish(Zone.PortBastok, 252, q =>
                {
                    if (GiveQuestReward(q, q.Option))
                    {
                        q.Quest.Complete();
                        q.Player.DelKeyItem(KeyItem.WhisperOfTremors);
                        // quest:setTimedVar(player, 'Timer', NextJstDay()): setVar(prefix .. 'Timer', 1, expiry)
                        q.Quest.SetVar("Timer", 1, (uint)q.Player.NextJstDay);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 284, q => q.Player.GiveKeyItem(KeyItem.TuningForkOfEarth))
                // the battlefield's win event (BattlefieldQuest:onBattlefieldWin sets battlefieldWin while accepted)
                .OnEventFinish(Zone.CloisterOfTremors, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.TrialByEarth)
                    {
                        q.Player.GiveKeyItem(KeyItem.WhisperOfTremors);
                        q.Player.AddTitle(Title.HeirOfTheGreatEarth);
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.Var("Timer") == 0)
                .Npc(Zone.PortBastok, Npc.PortBastok.Juroro).OnTrigger(Offer)
                .OnEventFinish(Zone.PortBastok, 249, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.Bastok, "TRIAL_BY_EARTH");

                        q.Player.GiveKeyItem(KeyItem.TuningForkOfEarth);

                        q.Quest.Begin();
                    }
                });
        }
    }
}
