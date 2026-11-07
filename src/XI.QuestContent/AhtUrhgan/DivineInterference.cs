// Divine Interference (Log ID 6, Quest ID 75), a line-for-line port of scripts/quests/ahtUrhgan/Divine_Interference.lua
// (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Imperial Whitegate !pos 154 -4 0 (no script; default
// GATE_IS_FIRMLY_CLOSED), Naja Salaheem !pos 22.7 -7.8 -45.6 (no script, no default; ToAU 48's 3151 replaceDefault
// while that mission is current), trigger area 13 (30 -7 -51 .. 39 -5 -40). Mount Zhayolm: the Acid-eaten Door (no
// script; default ACID_EATEN_DOOR). Alzadaal Undersea Ruins: blank_lamp !pos 206.55 -1.5 20.05 (no script; default
// DEVICE_MALFUNCTIONING; it loads hidden), the zone-in. Available while ToAU 48 Eternal Mercenary is current, with
// Waking the Colossus completed and the Timer 0 (Waking the Colossus' 929 and this quest's 932 set it until the next
// JST midnight): area 13 at Prog 0 plays 930 ([0] the mercenary rank; Prog 1); Imperial Whitegate at Prog 1: 931,
// option 1: begun. Accepted: the door at Prog 1: 157 (Prog 2); a trade of exactly three Slabs of Plumbago at Prog 2 or
// more without the Lightning Cell: 158 (the cell; the trade taken); blank_lamp at Prog 2 with the cell: 308 (Prog 3);
// the Alzadaal zone-in at Prog 4 without the Whisper of Radiance: 309 (the Whisper). With the Whisper: Naja 932 ([0]
// the rank, [7] the reward mask), whose option picks the reward as Waking the Colossus' 929; given: completed (Heir of
// the Blessed Radiance, an Imperial Gold Piece), the Whisper taken, Timer 1 until the next JST midnight. Completed with
// the Timer 0: Imperial Whitegate 931, whose finish (any option) deletes the quest, begins it again and sets Prog 1.
// Prog 3 -> 4 is the Nyzul Isle instance's (waking_the_colossus.lua onInstanceComplete; the instance's menu name is
// Divine Interference once this quest is accepted, globals/instance.lua). Kept as the Lua has it: 932's option 0 (or
// below) and 6 or more complete it with no pick (rewardItems[-1] is nil: npcUtil.giveItem(player, nil) returns true);
// complete's result unread (one free slot: the Colossus item fits, the Gold Piece does not: the Whisper taken, the
// Timer set, the quest left accepted at Prog 4, so the next Alzadaal zone-in plays 309 again); 158 at Prog 3 and 4 too
// (a second Lightning Cell once the instance took the first); 930 sets Prog 1 on every finish; the repeat's 931 begins
// it whatever the option.
// Rival containers (golden flows #rivals): Naja Salaheem (Promotion: Private First Class 5001), the Imperial Whitegate
// (Trust: Nashmeira 5092) and the blank lamp (Nashmeira's Plea 8) answer the same talks; the reply kinds above are
// pinned against them there.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class DivineInterference : QuestScript
    {
        public DivineInterference() : base(QuestLog.AhtUrghan, "DIVINE_INTERFERENCE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Divine_Interference";

        // rewardItems: the event option minus one is the key (option 1 the mantle)
        static readonly Dictionary<int, int> RewardItems = new Dictionary<int, int>
        {
            [0] = Item.ColossussMantle,
            [1] = Item.ColossussEarring,
            [2] = Item.ColossussTorque,
        };

        static int GetRewardMask(IQuestPlayerView player)
        {
            int rewardMask = 0;

            // pairs(rewardItems): the bits are set whatever the order
            foreach (var kv in RewardItems)
            {
                if (player.HasItem(kv.Value))
                {
                    rewardMask |= 1 << kv.Key; // utils.mask.setBit(rewardMask, bitNum, true)
                }
            }

            if (player.HasSpell(Spell.Alexander))
            {
                rewardMask |= 1 << 4;
            }

            return rewardMask;
        }

        static bool GiveQuestReward(IQuestContext q, int eventOption)
        {
            bool wasRewarded = true;

            if (eventOption <= 3)
            {
                // rewardItems[eventOption - 1]: nil below 1, and npcUtil.giveItem(player, nil) gives nothing and
                // returns true
                wasRewarded = RewardItems.TryGetValue(eventOption - 1, out int item) ? q.Player.GiveItem(item) : true;
            }
            else if (eventOption == 4)
            {
                q.Player.GiveCurrency("gil", 10000);
            }
            else if (eventOption == 5)
            {
                q.Player.AddSpell(Spell.Alexander);
                q.Text.Special(Text.AhtUrhganWhitegate.ALEXANDER_UNLOCKED, 0, 0, 1);
            }

            return wasRewarded;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.HeirOfTheBlessedRadiance,
                Items = new[] { Item.ImperialGoldPiece }, // item = { xi.item.IMPERIAL_GOLD_PIECE }: a list
            });
            // Prog 4 is the Nyzul Isle instance's
            b.Steps(1, 2, 3);

            // Section: Quest available
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Toau) == Mission.Toau.EternalMercenary &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "WAKING_THE_COLOSSUS") &&
                    q.Quest.Var("Timer") == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(931).TextTable(0);
                    }
                    return null;
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 13, q =>
                    q.Quest.Step == 0
                        ? QuestReply.ProgressEvent(930).TextTable(0).Option(0, q.Player.MercenaryRank).Option(1, 1)
                        : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 930, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 931, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AlzadaalUnderseaRuins, Npc.AlzadaalUnderseaRuins.blanklamp).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2 &&
                        q.Player.HasKeyItem(KeyItem.LightningCell))
                    {
                        return QuestReply.ProgressEvent(308);
                    }
                    return null;
                })
                .OnZoneIn(Zone.AlzadaalUnderseaRuins, q =>
                    q.Quest.Step == 4 && !q.Player.HasKeyItem(KeyItem.WhisperOfRadiance) ? QuestReply.Event(309) : null)
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 308, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 309, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WhisperOfRadiance);
                })
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.AcideatenDoor).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(157);
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (q.Quest.Step >= 2 &&
                        q.Trade.HasExactly((Item.SlabOfPlumbago, 3)) &&
                        !q.Player.HasKeyItem(KeyItem.LightningCell))
                    {
                        return QuestReply.ProgressEvent(158);
                    }
                    return null;
                })
                .OnEventFinish(Zone.MountZhayolm, 157, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.MountZhayolm, 158, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.LightningCell))
                    {
                        q.Trade.Confirm();
                    }
                });

            // Section: Complete quest
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasKeyItem(KeyItem.WhisperOfRadiance))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                    QuestReply.ProgressEvent(932).TextTable(0).Option(0, q.Player.MercenaryRank)
                        .Option(7, GetRewardMask(q.Player)))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 932, q =>
                {
                    if (GiveQuestReward(q, q.Option))
                    {
                        q.Quest.Complete();
                        q.Player.DelKeyItem(KeyItem.WhisperOfRadiance);
                        // quest:setTimedVar(player, 'Timer', NextJstDay()): player:setVar(varPrefix .. 'Timer', 1,
                        // expiry)
                        q.Quest.SetVar("Timer", 1, (uint)q.Player.NextJstDay);
                    }
                });

            // Section: Repeat quest
            b.State(QuestStatus.Completed, when: q =>
                    q.Quest.Var("Timer") == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate)
                .OnTrigger(QuestReply.ProgressEvent(931).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 931, q =>
                {
                    q.Player.DelQuest(QuestLog.AhtUrghan, "DIVINE_INTERFERENCE");
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });
        }
    }
}
