// I'm on a Boat (Log ID 9, Quest ID 55), a line-for-line port of scripts/quests/adoulin/Im_on_a_Boat.lua (quest rework
// B3b Adoulin group 3). Foret de Hennetiel: Choubollet !pos 380.818 -2.094 290.792 262 (no script; default 2564), the
// six Castoff Points Castoff_Point_0 .. 5 (no script; default RIVER_UNSUITABLE_FOR_SWIMMING). The two Castoff Points of
// each of Cirdas Caverns, Morimar Basalt Fields, Sih Gates and the Yahse Hunting Grounds (no script; the same default).
// Available: Choubollet: 2561, whose finish (any option) begins it.
// Accepted: Choubollet: without the Watercraft 2562 (an Event), with it at Prog 2 2563, else 2577 (an Event); a trade
// of three Squares of Dhalmel Leather, an Umbril Ooze and a Twitherym Scale (exactly): 2576, whose finish confirms the
// trade and gives the Watercraft. Castoff_Point_4 / _5 with the Watercraft: 19 (4 / 5, then 1 when the Toxin Tussler
// title is held, else 0), whose finish, on option 1 below Prog 2, sets Prog 1 at point 4 (STARTED_TO_LEARN_BOAT) or
// Prog 2 at point 5 (FIGURED_OUT_BOAT by messageName, no entity); else on option 3 adds the title Toxin Tussler.
// 2563's finish shows YOU_HAVE_LEARNED Watercrafting, then completes it (500 bayld, the Adoulin fame area), takes the
// Watercraft and adds Watercrafting (no message).
// Completed: each Castoff Point of the five zones: castoffPointWarp, an Event: the zone's event (Cirdas 11, Foret 18,
// Morimar 18, Sih 12, Yahse 16) with the pad's index (npc id - CASTOFF_POINT_OFFSET): Foret's as it is (its river
// route), the others' (pad0Bank + index) % 2.
// Kept as the Lua has it: the offer begins on any option; point 5 may come first (Prog 0 to 2); the trade answers at
// any Prog and with the Watercraft held; the title is added on option 3 at any Prog; the "learned" line comes before
// the completion; the completion cannot fail (no item in the reward).
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class ImOnABoat : QuestScript
    {
        public ImOnABoat() : base(QuestLog.Adoulin, "IM_ON_A_BOAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Im_on_a_Boat";

        // requiredTradeItems
        private static readonly (int Item, int Qty)[] RequiredTradeItems =
        {
            (Item.SquareOfDhalmelLeather, 3),
            (Item.UmbrilOoze, 1),
            (Item.TwitherymScale, 1),
        };

        // Castoff Point navigation, available once the player has earned Watercrafting and completed the quest.
        private sealed class CastoffInfo
        {
            public int EventId;
            public int Pad0Bank;
            public bool IsRiverRoute;
        }

        private static readonly Dictionary<int, CastoffInfo> CastoffData = new Dictionary<int, CastoffInfo>
        {
            [Zone.CirdasCaverns] = new CastoffInfo { EventId = 11, Pad0Bank = 1 },
            [Zone.ForetDeHennetiel] = new CastoffInfo { EventId = 18, IsRiverRoute = true },
            [Zone.MorimarBasaltFields] = new CastoffInfo { EventId = 18, Pad0Bank = 0 },
            [Zone.SihGates] = new CastoffInfo { EventId = 12, Pad0Bank = 1 },
            [Zone.YahseHuntingGrounds] = new CastoffInfo { EventId = 16, Pad0Bank = 0 },
        };

        private static QuestReply CastoffPointWarp(IQuestContext q)
        {
            int zoneId = q.Zone;
            var data = CastoffData[zoneId];
            // npc:getID() - zones[zoneId].npc.CASTOFF_POINT_OFFSET
            int padIndex = q.Npc.Id - q.Player.NpcIdOf("CASTOFF_POINT_OFFSET");
            if (data.IsRiverRoute)
            {
                return QuestReply.Event(data.EventId, padIndex);
            }

            return QuestReply.Event(data.EventId, (data.Pad0Bank + padIndex) % 2);
        }

        // Foret's six river docks all navigatable post quest (the Lua's 'Castoff_Point_' .. i for i = 0, 5).
        private static readonly string[] ForetCastoffPads =
        {
            Npc.ForetDeHennetiel.CastoffPoint0,
            Npc.ForetDeHennetiel.CastoffPoint1,
            Npc.ForetDeHennetiel.CastoffPoint2,
            Npc.ForetDeHennetiel.CastoffPoint3,
            Npc.ForetDeHennetiel.CastoffPoint4,
            Npc.ForetDeHennetiel.CastoffPoint5,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                FameArea = FameArea.Adoulin,
                Bayld = 500,
            });

            // the Prog values 19's finish sets
            b.Steps(1, 2);

            b.State(QuestStatus.Available)
                .Npc(Zone.ForetDeHennetiel, Npc.ForetDeHennetiel.Choubollet).OnTrigger(QuestReply.ProgressEvent(2561))
                .OnEventFinish(Zone.ForetDeHennetiel, 2561, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.ForetDeHennetiel, Npc.ForetDeHennetiel.Choubollet).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(RequiredTradeItems))
                    {
                        return QuestReply.ProgressEvent(2576);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.Watercraft))
                    {
                        return QuestReply.Event(2562);
                    }
                    else if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(2563);
                    }
                    else
                    {
                        return QuestReply.Event(2577);
                    }
                })
                .Npc(Zone.ForetDeHennetiel, Npc.ForetDeHennetiel.CastoffPoint4).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.Watercraft))
                    {
                        int hasTitle = q.Player.HasTitle(Title.ToxinTussler) ? 1 : 0;

                        return QuestReply.Event(19, 4, hasTitle);
                    }

                    return null;
                })
                .Npc(Zone.ForetDeHennetiel, Npc.ForetDeHennetiel.CastoffPoint5).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.Watercraft))
                    {
                        int hasTitle = q.Player.HasTitle(Title.ToxinTussler) ? 1 : 0;

                        return QuestReply.Event(19, 5, hasTitle);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ForetDeHennetiel, 19, q =>
                {
                    // tonumber(string.sub(npc:getName(), -1)): nil (null) when the last character is no digit
                    string name = q.Npc.Name;
                    int? castoffPointNum = int.TryParse(name.Substring(name.Length - 1), out int digit) ? digit : null;
                    int questProgress = q.Quest.Step;

                    if (q.Option == 1 && questProgress < 2)
                    {
                        if (castoffPointNum == 4)
                        {
                            q.Quest.SetStep(1);
                            q.Text.Special(Text.ForetDeHennetiel.STARTED_TO_LEARN_BOAT);
                        }
                        else if (castoffPointNum == 5)
                        {
                            q.Quest.SetStep(2);
                            q.Text.MessageName(Text.ForetDeHennetiel.FIGURED_OUT_BOAT);
                        }
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.AddTitle(Title.ToxinTussler);
                    }
                })
                .OnEventFinish(Zone.ForetDeHennetiel, 2563, q =>
                {
                    q.Text.Special(Text.ForetDeHennetiel.YOU_HAVE_LEARNED, KeyItem.Watercrafting);

                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Watercraft);
                        q.Player.AddKeyItem(KeyItem.Watercrafting);
                    }
                })
                .OnEventFinish(Zone.ForetDeHennetiel, 2576, q =>
                {
                    q.Trade.Confirm();

                    q.Player.GiveKeyItem(KeyItem.Watercraft);
                });

            var completed = b.State(QuestStatus.Completed);
            completed.Npc(Zone.CirdasCaverns, Npc.CirdasCaverns.CastoffPoint).OnTrigger(CastoffPointWarp);
            foreach (string pad in ForetCastoffPads)
            {
                completed.Npc(Zone.ForetDeHennetiel, pad).OnTrigger(CastoffPointWarp);
            }
            completed.Npc(Zone.MorimarBasaltFields, Npc.MorimarBasaltFields.CastoffPoint).OnTrigger(CastoffPointWarp);
            completed.Npc(Zone.SihGates, Npc.SihGates.CastoffPoint).OnTrigger(CastoffPointWarp);
            completed.Npc(Zone.YahseHuntingGrounds, Npc.YahseHuntingGrounds.CastoffPoint).OnTrigger(CastoffPointWarp);
        }
    }
}
