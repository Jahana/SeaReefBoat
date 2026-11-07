// Embers of His Past (Log ID 6, Quest ID 48), a line-for-line port of scripts/quests/ahtUrhgan/Embers_of_His_Past.lua
// (quest rework B3b Aht Urhgan group 6). Aht Urhgan Whitegate: Fari-Wari !pos 80 -6 -137 (no script;
// default event 831). Wajaom Woodlands: the zone-in (the zone's own sets a char at 0 0 0 down at 610.5 -28.5 356.2).
// Mount Zhayolm: Sprightly Footsteps !pos 822 -18 176.3 (no script; default NOTHING_OUT_OF_ORDINARY),
// Withered Petals !pos 857.2 -14.5 248.6 (no script, no default).
// Available with Soothing Waters completed: Fari-Wari 916, whose finish sets Prog 1, begins it and sets the char
// at 0 0 0 in Wajaom. Accepted: Prog 1: every Wajaom zone-in plays 16 (Prog 2, Wajaom again at 0 0 0); Prog 2: the
// zone-in 17 (Prog 3, the char set down in Whitegate at 80 -6 -123); Prog 3: Fari-Wari 917 (an Event),
// Sprightly Footsteps from 18:00 to 06:59: the cutscene 15 (Prog 4); Prog 4: Fari-Wari 917, Withered Petals in those
// hours: the cutscene 16 (Prog 5, the char set down in Whitegate at 141.74 -2 0); Prog 5: Fari-Wari 924 (an Event), a
// trade of exactly one Hydrangea to her: 918 (Prog 6, Wajaom at 0 0 0; the Hydrangea kept); Prog 6: the zone-in 18
// (Prog 7, Wajaom again); Prog 7: the zone-in 19 (ITEM_RETURNED with the Hydrangea, Prog 8, and the char set down in
// Whitegate at 80 -6 -123 by a 1000 ms player timer, the callback toWhitegate); Prog 8: Fari-Wari 919 (an Event), the
// Hydrangea traded to Withered Petals in those hours: the cutscene 17 (the trade taken, the cutscene 18 started; its
// finish: Prog 9); Prog 9: Fari-Wari 920, whose finish completes it (an Imperial Gold Piece, Flameserpent Facilitator)
// and, when it does, 500 imperial standing, BESIEGED_OFFSET, the zoning flag and Option 1. Completed and not needing to
// zone: Fari-Wari 921 at Option 1 (its finish: the Mercenary Camp Entry Slip, Option 0 when it fits), else 923
// (replaceDefault).
// Kept as the Lua has it: 918 keeps the Hydrangea (19 only messages ITEM_RETURNED); the zone-in arms play on every
// Wajaom zone-in at their Prog, whatever the previous zone; Option is set after the completion's cleanup.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class EmbersOfHisPast : QuestScript
    {
        public EmbersOfHisPast() : base(QuestLog.AhtUrghan, "EMBERS_OF_HIS_PAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Embers_of_His_Past";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialGoldPiece,
                Title = Title.FlameserpentFacilitator,
            });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9);

            // 19's player:timer(1000, function(playerArg) playerArg:setPos(80, -6, -123, 55,
            // xi.zone.AHT_URHGAN_WHITEGATE) end)
            var toWhitegate = b.Callback("toWhitegate", q =>
                q.Player.SetPos(80, -6, -123, 55, Zone.AhtUrhganWhitegate));

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "SOOTHING_WATERS") == QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(QuestReply.ProgressEvent(916).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 916, q =>
                {
                    q.Quest.SetStep(1);
                    q.Quest.Begin();
                    q.Player.SetPos(0, 0, 0, 0, Zone.WajaomWoodlands);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .OnZoneIn(Zone.WajaomWoodlands, q => QuestReply.Event(16))
                .OnEventFinish(Zone.WajaomWoodlands, 16, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.SetPos(0, 0, 0, 0, Zone.WajaomWoodlands);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .OnZoneIn(Zone.WajaomWoodlands, q => QuestReply.Event(17))
                .OnEventFinish(Zone.WajaomWoodlands, 17, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.SetPos(80, -6, -123, 55, Zone.AhtUrhganWhitegate);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.Event(917))
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.SprightlyFootsteps).OnTrigger(q =>
                {
                    if (q.Player.VanadielHour >= 18 || q.Player.VanadielHour <= 6)
                    {
                        return QuestReply.ProgressCutscene(15);
                    }
                    return null;
                })
                .OnEventFinish(Zone.MountZhayolm, 15, q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 4)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.Event(917))
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.WitheredPetals).OnTrigger(q =>
                {
                    if (q.Player.VanadielHour >= 18 || q.Player.VanadielHour <= 6)
                    {
                        return QuestReply.ProgressCutscene(16);
                    }
                    return null;
                })
                .OnEventFinish(Zone.MountZhayolm, 16, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.SetPos(141.740, -2, 0, 132, Zone.AhtUrhganWhitegate);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 5)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.Hydrangea))
                        {
                            return QuestReply.ProgressEvent(918).TextTable(0);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(924))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 918, q =>
                {
                    q.Quest.SetStep(6);
                    q.Player.SetPos(0, 0, 0, 0, Zone.WajaomWoodlands);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 6)
                .OnZoneIn(Zone.WajaomWoodlands, q => QuestReply.Event(18))
                .OnEventFinish(Zone.WajaomWoodlands, 18, q =>
                {
                    q.Quest.SetStep(7);
                    q.Player.SetPos(0, 0, 0, 0, Zone.WajaomWoodlands);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 7)
                .OnZoneIn(Zone.WajaomWoodlands, q => QuestReply.Event(19))
                .OnEventFinish(Zone.WajaomWoodlands, 19, q =>
                {
                    q.Text.Special(Text.WajaomWoodlands.ITEM_RETURNED, Item.Hydrangea);
                    q.Quest.SetStep(8);
                    q.Player.Timer(1000, toWhitegate);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 8)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.Event(919))
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.WitheredPetals).OnTrade(q =>
                {
                    if (q.Player.VanadielHour >= 18 || q.Player.VanadielHour <= 6)
                    {
                        if (q.Trade.HasExactly(Item.Hydrangea))
                        {
                            return QuestReply.ProgressCutscene(17);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.MountZhayolm, 17, q =>
                {
                    q.Trade.Confirm();
                    q.Player.StartCutscene(18);
                })
                .OnEventFinish(Zone.MountZhayolm, 18, q =>
                {
                    q.Quest.SetStep(9);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 9)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(QuestReply.ProgressEvent(920).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 920, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddCurrency("imperial_standing", 500);
                        // whitegateID.text.BESIEGED_OFFSET
                        q.Text.Special(Text.AhtUrhganWhitegate.BESIEGED_OFFSET);
                        q.Player.SetNeedToZone(true);
                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Completed, when: q => !q.Player.NeedToZone)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(921).TextTable(0);
                    }
                    else
                    {
                        return QuestReply.Event(923).TextTable(0).ReplaceDefault();
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 921, q =>
                {
                    if (q.Player.GiveItem(Item.MercenaryCampEntrySlip))
                    {
                        q.Quest.SetVar("Option", 0);
                    }
                });
        }
    }
}
