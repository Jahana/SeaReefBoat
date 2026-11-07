// In the Name of Science (Log ID 4, Quest ID 76), ported from scripts/zones/Tavnazian_Safehold/npcs/Yurim.lua (quest
// rework B3b, Other Areas group 12; call-only: every branch of his script is this quest's, so his script only calls
// it). Yurim : !pos 83.697 -25.000 3.250 26 (his DefaultActions 527). His script draws math.randomInt (the seeded
// script RNG; q.Player.RandomInt is the same call).
// The table nosTrades: each target item (8 obis, 8 gorgets, 2 earrings) with its hint, its base (a Silver Obi /
// Gorget / Silver Earring and a chip) and its organs.
// Trigger, CoP The Warriors' Path completed: available: 524 (Silver Obi, Silver Earring, Gorget; its finish: begun);
// accepted or completed with no target (NAME_OF_SCIENCE_target 0): 525 (the same); accepted or completed with a
// target: randomInt(1, 100) <= 30: 532 (the target's base), else 528 (the same). Else nothing (his default).
// Trade: a target and its organs (npcUtil.tradeHas; no status read): 529 (Gorget, Silver Earring, Silver Obi); else
// accepted or completed, an Apple Pie and a target: 531 (Apple Pie, 0, the target's hint); else accepted or completed
// with no target: the first base the trade holds, in pairs(nosTrades) order (BaseOrder below): the target set, 526
// (the base's two items). 531 / 526's finishes: the confirmed items taken. 529's: npcUtil.completeQuest with the
// target item and the target var cleared; done: the organs taken.
// Kept as the Lua has it: the organs' trade reads no status; 529's finish reads no status (completes again on a
// repeat); the pie arm's tradeHas confirms the pie before its target test, so a pie traded with a base and no target
// is taken by 526 too; a target var naming no nosTrades entry (only a GM can set one) raised in the Lua on the trade
// and the trigger (indexing nil), and the dictionary throws there too; the base loop's pairs order decides a trade
// holding two bases.
// Yurim.lua's onTrigger ended in a commented-out else: "STANDARD DIALOG -- TODO: This is not a true default action,
// and needs to be verified for when this begins to be displayed -- player:startEvent(519)". 519 is a candidate for
// his real default; the C# plays nothing there (his DefaultActions 527), as the Lua did.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class InTheNameOfScience : QuestScript
    {
        const string TargetVar = "NAME_OF_SCIENCE_target";

        sealed class NosTrade
        {
            public int Hint;
            public int[] Base;
            public (int Item, int Qty)[] Organs;
        }

        // Yurim.lua nosTrades
        static readonly Dictionary<int, NosTrade> NosTrades = new Dictionary<int, NosTrade>
        {
            [Item.KarinObi] = new NosTrade
            {
                Hint = 1,
                Base = new[] { Item.SilverObi, Item.RedChip },
                Organs = new[] { (Item.PhuaboOrgan, 7), (Item.XzomitOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.HyorinObi] = new NosTrade
            {
                Hint = 2,
                Base = new[] { Item.SilverObi, Item.ClearChip },
                Organs = new[] { (Item.XzomitOrgan, 7), (Item.PhuaboOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.FurinObi] = new NosTrade
            {
                Hint = 3,
                Base = new[] { Item.SilverObi, Item.GreenChip },
                Organs = new[] { (Item.AernOrgan, 7), (Item.HpemdeOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.DorinObi] = new NosTrade
            {
                Hint = 4,
                Base = new[] { Item.SilverObi, Item.YellowChip },
                Organs = new[] { (Item.HpemdeOrgan, 7), (Item.AernOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.RairinObi] = new NosTrade
            {
                Hint = 5,
                Base = new[] { Item.SilverObi, Item.PurpleChip },
                Organs = new[] { (Item.PhuaboOrgan, 7), (Item.HpemdeOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.SuirinObi] = new NosTrade
            {
                Hint = 6,
                Base = new[] { Item.SilverObi, Item.BlueChip },
                Organs = new[] { (Item.HpemdeOrgan, 7), (Item.PhuaboOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.KorinObi] = new NosTrade
            {
                Hint = 7,
                Base = new[] { Item.SilverObi, Item.WhiteChip },
                Organs = new[] { (Item.XzomitOrgan, 7), (Item.AernOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.AnrinObi] = new NosTrade
            {
                Hint = 8,
                Base = new[] { Item.SilverObi, Item.BlackChip },
                Organs = new[] { (Item.AernOrgan, 7), (Item.XzomitOrgan, 3), (Item.SampleOfLuminianTissue, 3) },
            },
            [Item.FlameGorget] = new NosTrade
            {
                Hint = 9,
                Base = new[] { Item.Gorget, Item.RedChip },
                Organs = new[] { (Item.PhuaboOrgan, 10), (Item.XzomitOrgan, 5), (Item.YovraOrgan, 1) },
            },
            [Item.SnowGorget] = new NosTrade
            {
                Hint = 10,
                Base = new[] { Item.Gorget, Item.ClearChip },
                Organs = new[] { (Item.PhuaboOrgan, 10), (Item.AernOrgan, 5), (Item.YovraOrgan, 1) },
            },
            [Item.BreezeGorget] = new NosTrade
            {
                Hint = 11,
                Base = new[] { Item.Gorget, Item.GreenChip },
                Organs = new[] { (Item.PhuaboOrgan, 10), (Item.HpemdeOrgan, 5), (Item.YovraOrgan, 1) },
            },
            [Item.SoilGorget] = new NosTrade
            {
                Hint = 12,
                Base = new[] { Item.Gorget, Item.YellowChip },
                Organs = new[] { (Item.XzomitOrgan, 10), (Item.AernOrgan, 5), (Item.YovraOrgan, 1) },
            },
            [Item.ThunderGorget] = new NosTrade
            {
                Hint = 13,
                Base = new[] { Item.Gorget, Item.PurpleChip },
                Organs = new[] { (Item.XzomitOrgan, 10), (Item.HpemdeOrgan, 5), (Item.YovraOrgan, 1) },
            },
            [Item.AquaGorget] = new NosTrade
            {
                Hint = 14,
                Base = new[] { Item.Gorget, Item.BlueChip },
                Organs = new[] { (Item.AernOrgan, 10), (Item.HpemdeOrgan, 5), (Item.YovraOrgan, 1) },
            },
            [Item.LightGorget] = new NosTrade
            {
                Hint = 15,
                Base = new[] { Item.Gorget, Item.WhiteChip },
                Organs = new[]
                {
                    (Item.AernOrgan, 7), (Item.PhuaboOrgan, 3), (Item.HpemdeOrgan, 3), (Item.YovraOrgan, 2),
                },
            },
            [Item.ShadowGorget] = new NosTrade
            {
                Hint = 16,
                Base = new[] { Item.Gorget, Item.BlackChip },
                Organs = new[]
                {
                    (Item.HpemdeOrgan, 7), (Item.PhuaboOrgan, 3), (Item.AernOrgan, 3), (Item.YovraOrgan, 2),
                },
            },
            [Item.SanativeEarring] = new NosTrade
            {
                Hint = 17,
                Base = new[] { Item.SilverEarring, Item.WhiteChip },
                Organs = new[] { (Item.SampleOfLuminianTissue, 5), (Item.EuvhiOrgan, 5) },
            },
            [Item.RelaxingEarring] = new NosTrade
            {
                Hint = 17,
                Base = new[] { Item.SilverEarring, Item.BlackChip },
                Organs = new[] { (Item.SampleOfLuminianTissue, 5), (Item.EuvhiOrgan, 5) },
            },
        };

        // pairs(nosTrades) as the engine's LuaJIT walks the table its constructor builds (number keys hash with no
        // seed, so the order is the same in every process): 15435 14792 15495 15436 15496 15437 15497 15500 15502 15501
        // 15438 14791 15498 15499 15442 15441 15440 15439 (read from an instrumented Lua run; otherAreas
        // .in_the_name_of_science#order shows every pair of it a trade can show)
        static readonly int[] BaseOrder =
        {
            Item.KarinObi, Item.RelaxingEarring, Item.FlameGorget, Item.HyorinObi, Item.SnowGorget, Item.FurinObi,
            Item.BreezeGorget, Item.AquaGorget, Item.ShadowGorget, Item.LightGorget, Item.DorinObi,
            Item.SanativeEarring, Item.SoilGorget, Item.ThunderGorget, Item.AnrinObi, Item.KorinObi, Item.SuirinObi,
            Item.RairinObi,
        };

        public InTheNameOfScience() : base(QuestLog.OtherAreas, "IN_THE_NAME_OF_SCIENCE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Yurim).Called()
                    // Yurim.lua onTrade
                    .OnTrade(q =>
                    {
                        QuestStatus nameOfScience = q.Quest.Status;
                        int itemInProgress = q.Player.CharVar(TargetVar);
                        bool acceptedOrCompleted =
                            nameOfScience == QuestStatus.Accepted || nameOfScience == QuestStatus.Completed;

                        // a target naming no entry: the Lua indexed nil and raised (only a GM can set one)
                        if (itemInProgress > 0 &&
                            q.Trade.HasQty(NosTrades[itemInProgress].Organs))
                        {
                            return QuestReply.Event(529, Item.Gorget, Item.SilverEarring, Item.SilverObi);
                        }
                        else if (acceptedOrCompleted &&
                            q.Trade.Has(Item.ApplePie) &&
                            itemInProgress > 0)
                        {
                            // apple pie hint
                            return QuestReply.Event(531, Item.ApplePie, 0, NosTrades[itemInProgress].Hint);
                        }
                        else if (acceptedOrCompleted &&
                            itemInProgress == 0)
                        {
                            foreach (int k in BaseOrder)
                            {
                                NosTrade v = NosTrades[k];

                                if (q.Trade.Has(v.Base))
                                {
                                    q.Player.SetCharVar(TargetVar, k);
                                    return QuestReply.Event(526, v.Base[0], v.Base[1]);
                                }
                            }
                        }

                        return null;
                    })
                    // Yurim.lua onTrigger
                    .OnTrigger(q =>
                    {
                        // IN THE NAME OF SCIENCE
                        if (q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheWarriorsPath))
                        {
                            QuestStatus nameOfScience = q.Quest.Status;
                            int itemInProgress = q.Player.CharVar(TargetVar);

                            if (nameOfScience == QuestStatus.Available)
                            {
                                return QuestReply.Event(524, Item.SilverObi, Item.SilverEarring, Item.Gorget);
                            }
                            else if ((nameOfScience == QuestStatus.Accepted ||
                                nameOfScience == QuestStatus.Completed) &&
                                itemInProgress == 0)
                            {
                                return QuestReply.Event(525, Item.SilverObi, Item.SilverEarring, Item.Gorget);
                            }
                            else if (nameOfScience == QuestStatus.Accepted ||
                                nameOfScience == QuestStatus.Completed)
                            {
                                // the draw first, then the table (a target naming no entry draws, then throws)
                                if (q.Player.RandomInt(1, 100) <= 30)
                                {
                                    int[] baseItems = NosTrades[itemInProgress].Base;
                                    return QuestReply.Event(532, baseItems[0], baseItems[1]);
                                }
                                else
                                {
                                    int[] baseItems = NosTrades[itemInProgress].Base;
                                    return QuestReply.Event(528, baseItems[0], baseItems[1]);
                                }
                            }
                        }

                        return null; // his default 527
                    });

            b.AnyState()
                // Yurim.lua onEventFinish 524 / 531 / 526 / 529
                .OnEventFinish(Zone.TavnazianSafehold, 524, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.TavnazianSafehold, 531, q =>
                {
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.TavnazianSafehold, 526, q =>
                {
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.TavnazianSafehold, 529, q =>
                {
                    int itemInProgress = q.Player.CharVar(TargetVar);

                    // npcUtil.completeQuest(player, ..., { item = itemInProgress,
                    // var = { 'NAME_OF_SCIENCE_target' } }), step for step: the item is the char's target (no fixed
                    // QuestReward): giveItem first (false: nothing), the var 0, then the raw player:completeQuest
                    if (q.Player.GiveItem(itemInProgress))
                    {
                        q.Player.SetCharVar(TargetVar, 0);
                        // the raw player:completeQuest, which keeps the quest's own Quest[4][76] vars
                        q.Player.CompleteQuest(QuestLog.OtherAreas, "IN_THE_NAME_OF_SCIENCE");
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
