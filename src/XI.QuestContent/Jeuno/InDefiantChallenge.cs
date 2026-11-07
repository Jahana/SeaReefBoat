// In Defiant Challenge (Log ID 3, Quest ID 128), a line-for-line port of scripts/quests/jeuno/LB01_In_Defiant_Challenge.lua
// (quest rework B3b). Maat : !pos 8 3 118 243; the ??? qm10-qm12 in Crawlers' Nest, qm18-qm20 in Garlaige Citadel, qm7-qm9
// in The Eldieme Necropolis. At level 50 with level cap 50 (MAX_LEVEL 55 or more) Maat offers 79 (option 1: begun).
// Accepted: Maat 80; the three items exactly (tradeHasExactly) play 81: completed (Horizon Breaker), the nine key items
// cleared, the trade confirmed, level cap 55. Each ??? (unless OLDSCHOOL_G1) gives its key item when neither the item
// nor that key item is held, and once a zone's three key items are held: with the item not held and a free slot, the
// key items taken, COMBINE_INTO_A_CLUMP (or its zone's text) and the item; else ITEM_CANNOT_BE_OBTAINED. The handlers
// return nil after acting (the Lua's handleQMTrigger returns nothing).
// Kept Lua quirks: the available check wants level exactly 50; a ??? whose zone's item is held gives no key item and
// says nothing until the zone's three key items are held (then ITEM_CANNOT_BE_OBTAINED, the key items kept);
// OLDSCHOOL_G1 makes every ??? silent.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class InDefiantChallenge : QuestScript
    {
        public InDefiantChallenge() : base(QuestLog.Jeuno, "IN_DEFIANT_CHALLENGE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB01_In_Defiant_Challenge";

        sealed class KeyItemSet
        {
            public int Item;
            public ZoneText Combined, CannotObtain;
            public int Ki1, Ki2, Ki3;
        }

        static readonly Dictionary<ushort, KeyItemSet> KeyItemTable = new Dictionary<ushort, KeyItemSet>
        {
            [Zone.CrawlersNest] = new KeyItemSet
            {
                Item = Item.ClumpOfExorayMold, Combined = Text.CrawlersNest.COMBINE_INTO_A_CLUMP, CannotObtain = Text.CrawlersNest.ITEM_CANNOT_BE_OBTAINED,
                Ki1 = KeyItem.ExorayMoldCrumb1, Ki2 = KeyItem.ExorayMoldCrumb2, Ki3 = KeyItem.ExorayMoldCrumb3,
            },
            [Zone.GarlaigeCitadel] = new KeyItemSet
            {
                Item = Item.ChunkOfBombCoal, Combined = Text.GarlaigeCitadel.COMBINE_INTO_A_CHUNK, CannotObtain = Text.GarlaigeCitadel.ITEM_CANNOT_BE_OBTAINED,
                Ki1 = KeyItem.BombCoalFragment1, Ki2 = KeyItem.BombCoalFragment2, Ki3 = KeyItem.BombCoalFragment3,
            },
            [Zone.TheEldiemeNecropolis] = new KeyItemSet
            {
                Item = Item.PieceOfAncientPapyrus, Combined = Text.TheEldiemeNecropolis.PUT_TOGUETHER_TO_COMPLETE, CannotObtain = Text.TheEldiemeNecropolis.ITEM_CANNOT_BE_OBTAINED,
                Ki1 = KeyItem.AncientPapyrusShred1, Ki2 = KeyItem.AncientPapyrusShred2, Ki3 = KeyItem.AncientPapyrusShred3,
            },
        };

        // Key Item removals. Needs to be called separately by the quest cleanup.
        static void CleanKeyItemSet(IQuestContext q, ushort zoneId)
        {
            q.Player.DelKeyItem(KeyItemTable[zoneId].Ki1);
            q.Player.DelKeyItem(KeyItemTable[zoneId].Ki2);
            q.Player.DelKeyItem(KeyItemTable[zoneId].Ki3);
        }

        // NOTE: This is handled in such an unconventional manner just so the text appears in the same order as in retail.
        static QuestReply HandleQmTrigger(IQuestContext q, ushort zoneId, int rewardKi)
        {
            // Era setting. (if xi.settings.main.OLDSCHOOL_G1 then: Lua's truthiness, Settings.MainTruthy (quest rework P9):
            // only false and nil are off, a 0 is on)
            if (q.Settings.MainTruthy("OLDSCHOOL_G1"))
            {
                return null;
            }

            int itemId = KeyItemTable[zoneId].Item;

            // Give KI.
            if (!q.Player.HasItem(itemId) && // KI wont be given if we already possess the regular item.
                !q.Player.HasKeyItem(rewardKi)) // KI wont be given if we already possess the key item.
            {
                q.Player.GiveKeyItem(rewardKi);
            }

            // Handle KI set fusion.
            if (q.Player.HasKeyItem(KeyItemTable[zoneId].Ki1) &&
                q.Player.HasKeyItem(KeyItemTable[zoneId].Ki2) &&
                q.Player.HasKeyItem(KeyItemTable[zoneId].Ki3))
            {
                if (!q.Player.HasItem(itemId) &&
                    q.Player.FreeSlotCount > 0)
                {
                    CleanKeyItemSet(q, zoneId);
                    q.Text.Special(KeyItemTable[zoneId].Combined, rewardKi);
                    q.Player.GiveItem(itemId);
                }
                else
                {
                    q.Text.Special(KeyItemTable[zoneId].CannotObtain, itemId);
                }
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.HorizonBreaker });

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel == 50 &&
                    q.Player.LevelCap == 50 &&
                    q.Settings.MainNumber("MAX_LEVEL") >= 55)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(79);
                })
                .OnEventFinish(Zone.RuludeGardens, 79, q =>
                {
                    if (q.Option == 1) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    return QuestReply.Event(80);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.ClumpOfExorayMold, Item.ChunkOfBombCoal, Item.PieceOfAncientPapyrus))
                    {
                        return QuestReply.ProgressEvent(81);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 81, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // Remove all remaining/lingering KIs
                        CleanKeyItemSet(q, Zone.CrawlersNest);
                        CleanKeyItemSet(q, Zone.GarlaigeCitadel);
                        CleanKeyItemSet(q, Zone.TheEldiemeNecropolis);
                        // Finish
                        q.Trade.Confirm();
                        q.Player.SetLevelCap(55);
                    }
                })
                .Npc(Zone.CrawlersNest, Npc.CrawlersNest.qm10).OnTrigger(q => HandleQmTrigger(q, Zone.CrawlersNest, KeyItem.ExorayMoldCrumb1))
                .Npc(Zone.CrawlersNest, Npc.CrawlersNest.qm11).OnTrigger(q => HandleQmTrigger(q, Zone.CrawlersNest, KeyItem.ExorayMoldCrumb2))
                .Npc(Zone.CrawlersNest, Npc.CrawlersNest.qm12).OnTrigger(q => HandleQmTrigger(q, Zone.CrawlersNest, KeyItem.ExorayMoldCrumb3))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm18).OnTrigger(q => HandleQmTrigger(q, Zone.GarlaigeCitadel, KeyItem.BombCoalFragment1))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm19).OnTrigger(q => HandleQmTrigger(q, Zone.GarlaigeCitadel, KeyItem.BombCoalFragment2))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm20).OnTrigger(q => HandleQmTrigger(q, Zone.GarlaigeCitadel, KeyItem.BombCoalFragment3))
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.qm7).OnTrigger(q => HandleQmTrigger(q, Zone.TheEldiemeNecropolis, KeyItem.AncientPapyrusShred1))
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.qm8).OnTrigger(q => HandleQmTrigger(q, Zone.TheEldiemeNecropolis, KeyItem.AncientPapyrusShred2))
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.qm9).OnTrigger(q => HandleQmTrigger(q, Zone.TheEldiemeNecropolis, KeyItem.AncientPapyrusShred3));
        }
    }
}
