// Waking the Colossus (Log ID 6, Quest ID 74), a line-for-line port of scripts/quests/ahtUrhgan/Waking_the_Colossus.lua
// (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Imperial Whitegate !pos 154 -4 0 (no script; default
// GATE_IS_FIRMLY_CLOSED), Naja Salaheem !pos 22.7 -7.8 -45.6 (no script, no default; ToAU 48 Eternal Mercenary's 3151
// replaceDefault while that mission is current), trigger area 13 (30 -7 -51 .. 39 -5 -40, by the Sentinels; the zone's
// own switch has no case for it). Ru'Lude Gardens: the Audience Chamber door _6r9 (its script: 138). Chateau
// d'Oraguille: Halver (his script: 577 / 31 for San d'Oria, a showText for Bastok). Metalworks: Iron Eater (no script;
// default 604). Heaven's Tower: Kupipi (his script: the trust events, else 408 / 251). Mount Zhayolm: the Acid-eaten
// Door (no script; default ACID_EATEN_DOOR). Alzadaal Undersea Ruins: blank_lamp !pos 206.55 -1.5 20.05 (no script;
// default DEVICE_MALFUNCTIONING; npcs.yaml gives it no status, so it loads hidden), the zone-in. Available while ToAU
// 48 Eternal Mercenary is current: area 13 at Prog 0 plays 925 (Prog 1); Imperial Whitegate at Prog 1 without the
// Imperial Missive: 926, option 0: the missive and begun. Accepted with the missive: _6r9 at Prog 1: 10115 (Prog 2);
// _6r9 with the San d'Orian, Bastokan and Windurstian approval letters and no Jeuno one: 10116, option 0: the Jeuno
// letter; Halver / Iron Eater / Kupipi at Prog 2 without their nation's letter: 567 / 968 / 429, option 0: the letter.
// With the Jeuno letter: Imperial Whitegate without the Present for Megomak: 927, option 0: the five key items taken,
// the Present. With the Present or Megomak's Shopping List: the door with the Present and no list: 154, option 1: the
// Present taken, the list; a trade of exactly three Slabs of Plumbago with the list and no Lightning Cell: 155 (the
// cell; the trade taken when the cell is given). With the Lightning Cell or at Prog 4: blank_lamp with the cell at Prog
// 2: 306 (Prog 3); the Alzadaal zone-in at Prog 4 without the Whisper of Radiance: 307 (the Whisper). With the Whisper:
// Imperial Whitegate at Prog 4: 928, option 0: Prog 5; Naja at Prog 5: 929 ([0] the mercenary rank, [7] the reward
// mask), whose option picks the reward (1-3 a Colossus item, 4 10000 gil, 5 Alexander); given: completed (Heir of the
// Blessed Radiance, an Imperial Gold Piece), the Whisper taken, Divine Interference's Timer 1 until the next JST
// midnight. Prog 3 -> 4 is the Nyzul Isle instance's (zones/Nyzul_Isle/instances/waking_the_colossus.lua
// onInstanceComplete; its afterInstanceRegister takes the Lightning Cell), not this file's. Kept as the Lua has it:
// 929's option 0 (or below) reads rewardItems[-1], nil: npcUtil.giveItem(player, nil) gives nothing and returns true,
// so the quest completes with no pick; an option of 6 or more completes it too; complete's result unread (with one free
// slot the Colossus item fills it, the Gold Piece does not fit: the Whisper is taken and the Timer set, the quest stays
// accepted at Prog 5 in no section); 925 sets Prog 1 on every finish; 155 confirms the trade only when giveKeyItem says
// true (always: it returns true).
// Rival containers (golden flows #rivals, #rivals2): Naja Salaheem (Promotion: Private First Class 5001), the Imperial
// Whitegate (Trust: Nashmeira 5092), Ru'Lude's _6r9 (its door script 138), Iron Eater (Bastok 3-1 715 and Windurst
// 2-3-4 702) and the blank lamp (Path of Darkness 6) answer the same talks; the reply kinds above are pinned against
// them there, 968 included (the #rivals Iron Eater talks, with the Imperial Missive); the rest are pinned in
// tests/XI.Verify.Quests/AhtUrhganGroup5.cs.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class WakingTheColossus : QuestScript
    {
        public WakingTheColossus() : base(QuestLog.AhtUrghan, "WAKING_THE_COLOSSUS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Waking_the_Colossus";

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
            b.Steps(1, 2, 3, 5);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Toau) == Mission.Toau.EternalMercenary)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1 &&
                        !q.Player.HasKeyItem(KeyItem.ImperialMissive))
                    {
                        return QuestReply.ProgressEvent(926).TextTable(0).Option(0, 50).Option(1, 1).Option(2, 4)
                            .Option(5, 1);
                    }
                    return null;
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 13, q =>
                    q.Quest.Step == 0
                        ? QuestReply.ProgressEvent(925).TextTable(0).Option(0, 50).Option(1, 1).Option(2, 4)
                            .Option(5, 1)
                        : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 925, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 926, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.ImperialMissive);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasKeyItem(KeyItem.ImperialMissive))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r9).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(10115);
                    }

                    if (!q.Player.HasKeyItem(KeyItem.JeunoanApprovalLetter) &&
                        q.Player.HasKeyItem(KeyItem.SanDorianApprovalLetter) &&
                        q.Player.HasKeyItem(KeyItem.BastokanApprovalLetter) &&
                        q.Player.HasKeyItem(KeyItem.WindurstianApprovalLetter))
                    {
                        return QuestReply.ProgressEvent(10116);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 10115, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.RuludeGardens, 10116, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.JeunoanApprovalLetter);
                    }
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2 &&
                        !q.Player.HasKeyItem(KeyItem.SanDorianApprovalLetter))
                    {
                        return QuestReply.ProgressEvent(567).Option(0, 4).Option(1, 4).Option(2, 2964).Option(6, 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 567, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.SanDorianApprovalLetter);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2 &&
                        !q.Player.HasKeyItem(KeyItem.BastokanApprovalLetter))
                    {
                        return QuestReply.ProgressEvent(968).Option(1, 4).Option(2, 2964).Option(4, 58195967)
                            .Option(5, 212046073).Option(7, 4095);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 968, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.BastokanApprovalLetter);
                    }
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2 &&
                        !q.Player.HasKeyItem(KeyItem.WindurstianApprovalLetter))
                    {
                        return QuestReply.ProgressEvent(429).Option(0, 74).Option(1, 4).Option(2, 2964).Option(4, 582)
                            .Option(7, 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.HeavensTower, 429, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.WindurstianApprovalLetter);
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasKeyItem(KeyItem.JeunoanApprovalLetter))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PresentForMegomak))
                    {
                        return QuestReply.ProgressEvent(927).TextTable(0).Option(2, 3).Option(4, 66420735)
                            .Option(5, 66497777);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 927, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelKeyItem(KeyItem.ImperialMissive);
                        q.Player.DelKeyItem(KeyItem.SanDorianApprovalLetter);
                        q.Player.DelKeyItem(KeyItem.BastokanApprovalLetter);
                        q.Player.DelKeyItem(KeyItem.WindurstianApprovalLetter);
                        q.Player.DelKeyItem(KeyItem.JeunoanApprovalLetter);
                        q.Player.GiveKeyItem(KeyItem.PresentForMegomak);
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasKeyItem(KeyItem.PresentForMegomak) || q.Player.HasKeyItem(KeyItem.MegomaksShoppingList))
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.AcideatenDoor).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.PresentForMegomak) &&
                        !q.Player.HasKeyItem(KeyItem.MegomaksShoppingList))
                    {
                        return QuestReply.ProgressEvent(154);
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MegomaksShoppingList) &&
                        q.Trade.HasExactly((Item.SlabOfPlumbago, 3)) &&
                        !q.Player.HasKeyItem(KeyItem.LightningCell))
                    {
                        return QuestReply.ProgressEvent(155);
                    }
                    return null;
                })
                .OnEventFinish(Zone.MountZhayolm, 154, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.PresentForMegomak);
                        q.Player.GiveKeyItem(KeyItem.MegomaksShoppingList);
                    }
                })
                .OnEventFinish(Zone.MountZhayolm, 155, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.LightningCell))
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasKeyItem(KeyItem.LightningCell) || q.Quest.Step == 4)
                .Npc(Zone.AlzadaalUnderseaRuins, Npc.AlzadaalUnderseaRuins.blanklamp).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LightningCell) &&
                        q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(306);
                    }
                    return null;
                })
                .OnZoneIn(Zone.AlzadaalUnderseaRuins, q =>
                    q.Quest.Step == 4 && !q.Player.HasKeyItem(KeyItem.WhisperOfRadiance) ? QuestReply.Event(307) : null)
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 306, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 307, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WhisperOfRadiance);
                });

            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.HasKeyItem(KeyItem.WhisperOfRadiance))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(928).TextTable(0).Option(2, 255).Option(4, 67108863)
                            .Option(5, 47467207);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(929).TextTable(0).Option(0, q.Player.MercenaryRank)
                            .Option(7, GetRewardMask(q.Player));
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 928, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(5);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 929, q =>
                {
                    if (GiveQuestReward(q, q.Option))
                    {
                        q.Quest.Complete();
                        q.Player.DelKeyItem(KeyItem.WhisperOfRadiance);
                        // Divine Interference cannot start until next JP midnight: xi.quest.setVar(player, AHT_URHGAN,
                        // DIVINE_INTERFERENCE, 'Timer', 1, NextJstDay()), player:setVar('Quest[6][75]Timer', 1, expiry)
                        q.Player.SetCharVar("Quest[6][75]Timer", 1, (uint)q.Player.NextJstDay);
                    }
                });
        }
    }
}
