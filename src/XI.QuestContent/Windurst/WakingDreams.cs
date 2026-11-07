// Waking Dreams (Log ID 2, Quest ID 93), a line-for-line port of scripts/quests/windurst/Waking_Dreams.lua (quest rework
// B3b). Kerutoto : !pos 13 -5 -157 238. Offered once CoP 3-5 (Darkness Named) is completed: Kerutoto's 918 begins it with
// the Vial of Dream Incense; the Diabolos battlefield in The Shrouded Maw (its own script) gives the Whisper of Dreams;
// Kerutoto's 920 then offers a reward (options 1-4 an item, 5 15000 gil, 6 the Diabolos pact) and completes the quest
// (fame +60, mustZone, a JST-midnight lockout in the char var Darkness_Named_date). Repeatable after the lockout: 918
// gives another vial (option 1), and 920 the reward again. Event 920 hides the pact option (bit 5) for players who have
// not unlocked SMN or already know it. A seventh event param of 1 plays Kerutoto's repeat lines in 918 and 920. Kept as
// the Lua file has it: the quest has no reward table, the fame comes from addFame after quest:complete; the pact is
// given whatever the job (the event only hides it); the repeat's 920 sets no mustZone.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class WakingDreams : QuestScript
    {
        static readonly Dictionary<int, int> RewardItems = new Dictionary<int, int>
        {
            [1] = Item.DiabolossPole,
            [2] = Item.DiabolossEarring,
            [3] = Item.DiabolossRing,
            [4] = Item.DiabolossTorque,
        };

        public WakingDreams() : base(QuestLog.Windurst, "WAKING_DREAMS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Waking_Dreams";

        // Setup reward table to display available awards in event properly
        static int GetAvailableRewards(IQuestPlayerView player)
        {
            int rewardMask = 0;

            foreach (var entry in RewardItems)
            {
                if (player.HasItem(entry.Value))
                {
                    // utils.mask.setBit(rewardMask, bit - 1, true)
                    rewardMask |= 1 << (entry.Key - 1);
                }
            }

            if (player.HasSpell(Spell.Diabolos) ||
                !player.HasJob(Job.Smn))
            {
                rewardMask |= 1 << 5;
            }

            return rewardMask;
        }

        static bool GiveQuestReward(IQuestContext q, int option)
        {
            if (option <= 4)
            {
                return q.Player.GiveItem(RewardItems[option]);
            }
            else if (option == 5)
            {
                q.Player.GiveCurrency("gil", 15000);
            }
            else if (option == 6)
            {
                q.Player.AddSpell(Spell.Diabolos, silentLog: true);
                // player:messageText(npc, windurstWatersID.text.DIABOLOS_UNLOCKED, false, 6): the finish's npc
                q.Text.MessageText(Text.WindurstWaters.DIABOLOS_UNLOCKED, false, 6);
            }

            return true;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Section 1: Quest available (first time)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.DarknessNamed))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(q => QuestReply.ProgressEvent(918))
                .OnEventFinish(Zone.WindurstWaters, 918, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.VialOfDreamIncense);
                });

            // Section 2: Quest accepted (First time)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(q =>
                {
                    // Quest complete.
                    if (q.Player.HasKeyItem(KeyItem.WhisperOfDreams))
                    {
                        return QuestReply.ProgressEvent(920, Item.DiabolossPole, Item.DiabolossEarring, Item.DiabolossRing, Item.DiabolossTorque, 0, 0, 0, GetAvailableRewards(q.Player));
                    }
                    // 1-time optional dialog.
                    else if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(919);
                    }
                    // In-quest default.
                    else
                    {
                        return QuestReply.Event(789);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 919, q => q.Quest.SetVar("Option", 1))
                .OnEventFinish(Zone.WindurstWaters, 920, q =>
                {
                    // Retail does not advance the quest on an escaped cutscene.
                    if (q.Option < 1 || q.Option > 6)
                    {
                        return;
                    }

                    if (!GiveQuestReward(q, q.Option))
                    {
                        return;
                    }

                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Windurst, 60);
                        q.Player.DelKeyItem(KeyItem.WhisperOfDreams);
                        q.Player.SetCharVar("Darkness_Named_date", q.Player.JstMidnight);
                        q.Quest.SetMustZone();
                    }
                });

            // Section 3: Quest completed (repeats)
            b.State(QuestStatus.Completed, when: q => !q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(q =>
                {
                    // Time lockout. Retail keeps the in-quest lines until the next offer.
                    if (q.Player.SystemTime < q.Player.CharVar("Darkness_Named_date"))
                    {
                        return QuestReply.Event(789).ReplaceDefault();
                    }
                    // Quest complete.
                    else if (q.Player.HasKeyItem(KeyItem.WhisperOfDreams))
                    {
                        return QuestReply.ProgressEvent(920, Item.DiabolossPole, Item.DiabolossEarring, Item.DiabolossRing, Item.DiabolossTorque, 0, 0, 1, GetAvailableRewards(q.Player));
                    }
                    // "Re-start" quest
                    else if (!q.Player.HasKeyItem(KeyItem.VialOfDreamIncense))
                    {
                        return QuestReply.ProgressEvent(918, 0, 0, 0, 0, 0, 0, 1);
                    }
                    // 1-time optional dialog.
                    else if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(919);
                    }
                    // In-quest default.
                    else
                    {
                        return QuestReply.Event(789);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 918, q =>
                {
                    // "Not yet." returns the escape value.
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.VialOfDreamIncense);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 919, q => q.Quest.SetVar("Option", 1))
                .OnEventFinish(Zone.WindurstWaters, 920, q =>
                {
                    // Retail does not advance the quest on an escaped cutscene.
                    if (q.Option < 1 || q.Option > 6)
                    {
                        return;
                    }

                    if (!GiveQuestReward(q, q.Option))
                    {
                        return;
                    }

                    q.Player.DelKeyItem(KeyItem.WhisperOfDreams);
                    q.Player.SetCharVar("Darkness_Named_date", q.Player.JstMidnight);
                });
        }
    }
}
