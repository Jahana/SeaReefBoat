// An Explorer's Footsteps (Log ID 4, Quest ID 19), a line-for-line port of
// scripts/quests/otherAreas/An_Explorers_Footsteps.lua (quest rework B3b Other Areas group 9). Selbina: Abelard !pos
// -52 -11 -13 (no script; DefaultActions event 1130). The Stone Monument of 25 zones (17 present-day monuments and the
// [S] copies of 8 of them; no scripts; DefaultActions event 900 and the like).
// Available, main level 10 or more: Abelard: the char var [EF]MonumentBitmask 0: 40, else 42; each finish runs
// abelardAccept: option 100: a Lump of Selbina Clay (npcUtil.giveItem) and, when given, the local [EF]ClayRecieved 1,
// the quest var [EF]TargetMonument = the first monument (0 to 16) whose bit is clear (none: unchanged), and
// quest:begin.
// Accepted: a monument's trade of exactly one clay: confirmTrade, a Clay Tablet (npcUtil.giveItem), [EF]TabletZoneId =
// the zone, and the handler returns nil. Abelard's trade of exactly one tablet: the tablet's monument
// (monumentTable[TabletZoneId] [1]) already in the bitmask: 45 (event); else the bitmask += 1 << monument and
// [EF]MonumentCount += 1 (incrementCharVar, in the trade), then the count 17: 47, the monument the requested one: 41,
// else 46. Abelard's trigger: no tablet, no clay and the local ClayRecieved not 1: 44 (event); else TargetMonument >=
// 9: 49 (TargetMonument - 9), else 43 (TargetMonument). 44 finished: the clay, and the local ClayRecieved 1 when given.
// 45 finished: the clay, and when given confirmTrade and ClayRecieved 1. 41 / 46 / 47 finished: abelardCorrectTrade:
// confirmTrade, the monument's gil (monumentTable[TabletZoneId] [2]); csid 47: the bitmask and count 0, the Map of the
// Crawlers' Nest, quest:complete; else option 100: abelardAccept; else option 110: delQuest (the quest available again,
// the bitmask kept). Completed: Abelard 48 (replaceDefault).
// Kept as the Lua has it: the bitmask and count grow in Abelard's trade, before the event plays (a 41 / 46 / 47 left
// unfinished keeps them); 47's quest:complete is not read; abelardAccept at an accepted 41 / 46 calls quest:begin
// again; the monument's trade answers nil (the trade is taken at once, in the handler); a TabletZoneId that is no
// monument's zone (0, a GM) makes monumentTable[...] nil and the Lua errors (the C# throws the same way:
// KeyNotFoundException); the 17-bit search leaves TargetMonument unchanged when every bit is set.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class AnExplorersFootsteps : QuestScript
    {
        public AnExplorersFootsteps() : base(QuestLog.OtherAreas, "AN_EXPLORERS_FOOTSTEPS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/An_Explorers_Footsteps";

        // monumentTable: the zone's monument index and its gil, in the Lua's order, with the zone's Stone Monument
        // (each its zone's constant, for the registration's name check)
        static readonly (ushort Zone, string Monument, int Index, int Gil)[] MonumentList =
        {
            // !pos -183.734 -12.6798 -395.7220 100
            (Zone.WestRonfaure, Npc.WestRonfaure.StoneMonument, 0, 800),
            // !pos 77.277 -2.894 -517.3760 101
            (Zone.EastRonfaure, Npc.EastRonfaure.StoneMonument, 1, 800),
            // !pos 77.277 -2.894 -517.3760 81
            (Zone.EastRonfaureS, Npc.EastRonfaureS.StoneMonument, 1, 800),
            // !pos 334.133 50.6223 141.1630 102
            (Zone.LaTheinePlateau, Npc.LaTheinePlateau.StoneMonument, 2, 1000),
            // !pos -311.299 -4.4211 -138.8780 103
            (Zone.ValkurmDunes, Npc.ValkurmDunes.StoneMonument, 3, 1000),
            // !pos -65.976 -23.8312 -661.3620 104
            (Zone.JugnerForest, Npc.JugnerForest.StoneMonument, 4, 1000),
            // !pos -65.976 -23.8312 -661.3620 82
            (Zone.JugnerForestS, Npc.JugnerForestS.StoneMonument, 4, 1000),
            // !pos 185.669 9.0476 -614.0250 105
            (Zone.BatalliaDowns, Npc.BatalliaDowns.StoneMonument, 5, 10000),
            // !pos 185.669 9.0476 -614.0250 84
            (Zone.BatalliaDownsS, Npc.BatalliaDownsS.StoneMonument, 5, 10000),
            // !pos -199.635 96.0547 505.6240 106
            (Zone.NorthGustaberg, Npc.NorthGustaberg.StoneMonument, 6, 3000),
            // !pos 520.064 -5.8831 -738.3560 107
            (Zone.SouthGustaberg, Npc.SouthGustaberg.StoneMonument, 7, 800),
            // !pos -102.355 7.9796 253.7059 108
            (Zone.KonschtatHighlands, Npc.KonschtatHighlands.StoneMonument, 8, 1000),
            // !pos -300.672 21.6038 304.1790 109
            (Zone.PashhowMarshlands, Npc.PashhowMarshlands.StoneMonument, 9, 1000),
            // !pos -300.672 21.6038 304.1790 90
            (Zone.PashhowMarshlandsS, Npc.PashhowMarshlandsS.StoneMonument, 9, 1000),
            // !pos 362.479 -34.8962 -398.9940 110
            (Zone.RolanberryFields, Npc.RolanberryFields.StoneMonument, 10, 3000),
            // !pos 362.479 -34.8962 -398.9940 91
            (Zone.RolanberryFieldsS, Npc.RolanberryFieldsS.StoneMonument, 10, 3000),
            // !pos -205.593 -23.2401 -119.6700 115
            (Zone.WestSarutabaruta, Npc.WestSarutabaruta.StoneMonument, 11, 800),
            // !pos -205.593 -23.2401 -119.6700 95
            (Zone.WestSarutabarutaS, Npc.WestSarutabarutaS.StoneMonument, 11, 800),
            // !pos 448.045 -7.8100 319.9799 116
            (Zone.EastSarutabaruta, Npc.EastSarutabaruta.StoneMonument, 12, 800),
            // !pos -499.189 12.6524 373.5920 117
            (Zone.TahrongiCanyon, Npc.TahrongiCanyon.StoneMonument, 13, 1000),
            // !pos 320.755 -4.0779 368.7219 118
            (Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.StoneMonument, 14, 1000),
            // !pos 450.741 2.1088 -290.7359 119
            (Zone.MeriphataudMountains, Npc.MeriphataudMountains.StoneMonument, 15, 1000),
            // !pos 450.741 2.1088 -290.7359 97
            (Zone.MeriphataudMountainsS, Npc.MeriphataudMountainsS.StoneMonument, 15, 1000),
            // !pos 77.544 -2.7476 -184.8030 120
            (Zone.SauromugueChampaign, Npc.SauromugueChampaign.StoneMonument, 16, 10000),
            // !pos 77.544 -2.7476 -184.8030 98
            (Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.StoneMonument, 16, 10000),
        };

        static readonly Dictionary<int, (int Index, int Gil)> MonumentTable = BuildTable();

        static Dictionary<int, (int Index, int Gil)> BuildTable()
        {
            var t = new Dictionary<int, (int Index, int Gil)>();
            foreach (var m in MonumentList)
            {
                t[m.Zone] = (m.Index, m.Gil);
            }
            return t;
        }

        // utils.mask.getBit(mask, bit)
        static bool GetBit(int mask, int bit) => ((mask >> bit) & 1) == 1;

        static void AbelardAccept(IQuestContext q, int option)
        {
            if (option == 100)
            {
                if (q.Player.GiveItem(Item.LumpOfSelbinaClay))
                {
                    q.Player.SetLocalVar("[EF]ClayRecieved", 1);

                    // Decide monument to request.
                    int monumentBitmask = q.Player.CharVar("[EF]MonumentBitmask");

                    for (int i = 0; i <= 16; i++)
                    {
                        if (!GetBit(monumentBitmask, i))
                        {
                            q.Quest.SetVar("[EF]TargetMonument", i);

                            break;
                        }
                    }

                    // Start quest.
                    q.Quest.Begin();
                }
            }
        }

        static void AbelardCorrectTrade(IQuestContext q, int csid, int option)
        {
            // Handle gil reward.
            q.Trade.Confirm();
            q.Player.GiveCurrency("gil", MonumentTable[q.Quest.Var("[EF]TabletZoneId")].Gil);

            // Complete quest.
            if (csid == 47)
            {
                q.Player.SetCharVar("[EF]MonumentBitmask", 0);
                q.Player.SetCharVar("[EF]MonumentCount", 0);
                q.Player.GiveKeyItem(KeyItem.MapOfTheCrawlersNest);
                q.Quest.Complete();
            }
            // Continue quest.
            else if (option == 100)
            {
                AbelardAccept(q, option);
            }
            // Abort quest. Delete from log BUT remember tablets already given.
            else if (option == 110)
            {
                q.Player.DelQuest(QuestLog.OtherAreas, "AN_EXPLORERS_FOOTSTEPS");
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            // Level requirement added in the March 8, 2007 update:
            // https://www.playonline.com/pcd/update/ff11us/20070308c2bbd1/detail.html
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 10)
                .Npc(Zone.Selbina, Npc.Selbina.Abelard).OnTrigger(q =>
                {
                    int monumentBitmask = q.Player.CharVar("[EF]MonumentBitmask");

                    if (monumentBitmask == 0)
                    {
                        return QuestReply.ProgressEvent(40); // First time.
                    }
                    else
                    {
                        // Declined further interaction after previously accepting and trading tablet.
                        return QuestReply.ProgressEvent(42);
                    }
                })
                .OnEventFinish(Zone.Selbina, 40, q =>
                {
                    AbelardAccept(q, q.Option);
                })
                .OnEventFinish(Zone.Selbina, 42, q =>
                {
                    AbelardAccept(q, q.Option);
                });

            var accepted = b.State(QuestStatus.Accepted);

            // Monuments
            foreach (var m in MonumentList)
            {
                accepted.Npc(m.Zone, m.Monument).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.LumpOfSelbinaClay))
                    {
                        q.Trade.Confirm();
                        q.Player.GiveItem(Item.ClayTablet);
                        q.Quest.SetVar("[EF]TabletZoneId", q.Zone);
                    }
                    return null;
                });
            }

            // Quest giver
            accepted
                .Npc(Zone.Selbina, Npc.Selbina.Abelard)
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.ClayTablet))
                    {
                        int currentMonument = MonumentTable[q.Quest.Var("[EF]TabletZoneId")].Index;
                        int requestedMonument = q.Quest.Var("[EF]TargetMonument");

                        // We already traded that monument tablet.
                        if (GetBit(q.Player.CharVar("[EF]MonumentBitmask"), currentMonument))
                        {
                            return QuestReply.Event(45);
                        }
                        // We never traded that monument tablet.
                        else
                        {
                            q.Player.IncrementCharVar("[EF]MonumentBitmask", 1 << currentMonument);
                            q.Player.IncrementCharVar("[EF]MonumentCount", 1);

                            if (q.Player.CharVar("[EF]MonumentCount") == 17)
                            {
                                return QuestReply.ProgressEvent(47); // Traded them all.
                            }
                            else if (currentMonument == requestedMonument)
                            {
                                return QuestReply.ProgressEvent(41); // Traded suggested one.
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(46); // Traded different one.
                            }
                        }
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    // You are supposed to have one of them. If not, you must have zoned before getting a new Clay.
                    if (!q.Player.HasItem(Item.ClayTablet) &&
                        !q.Player.HasItem(Item.LumpOfSelbinaClay) &&
                        q.Player.LocalVar("[EF]ClayRecieved") != 1)
                    {
                        return QuestReply.Event(44);
                    }
                    // Monument clue (Events 43 and 49)
                    else
                    {
                        int eventParam = q.Quest.Var("[EF]TargetMonument");
                        if (eventParam >= 9)
                        {
                            return QuestReply.Event(49, eventParam - 9);
                        }
                        else
                        {
                            return QuestReply.Event(43, eventParam);
                        }
                    }
                })
                // Requested monument.
                .OnEventFinish(Zone.Selbina, 41, q =>
                {
                    AbelardCorrectTrade(q, q.Csid, q.Option);
                })
                // Lost Clay and don't have tablet either.
                .OnEventFinish(Zone.Selbina, 44, q =>
                {
                    if (q.Player.GiveItem(Item.LumpOfSelbinaClay))
                    {
                        q.Player.SetLocalVar("[EF]ClayRecieved", 1);
                    }
                })
                // Had already traded this monument tablet.
                .OnEventFinish(Zone.Selbina, 45, q =>
                {
                    if (q.Player.GiveItem(Item.LumpOfSelbinaClay))
                    {
                        q.Trade.Confirm();
                        q.Player.SetLocalVar("[EF]ClayRecieved", 1);
                    }
                })
                // Different than requested monument tablet.
                .OnEventFinish(Zone.Selbina, 46, q =>
                {
                    AbelardCorrectTrade(q, q.Csid, q.Option);
                })
                // Complete quest.
                .OnEventFinish(Zone.Selbina, 47, q =>
                {
                    AbelardCorrectTrade(q, q.Csid, q.Option);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Selbina, Npc.Selbina.Abelard).OnTrigger(QuestReply.Event(48).ReplaceDefault());
        }
    }
}
