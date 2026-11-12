// Daughter of a Knight (WotG 10, Log ID 5, Mission ID 9), a line-for-line port of
// scripts/missions/wotg/10_Daughter_of_a_Knight.lua (quest rework C3 T5). Status 0: Southern San d'Oria's Amaura plays
// the progress event 935 (0, 2), whose finish sets status 1. Status 1: a trade of one Cernunnos Bulb to Amaura plays
// the progress event 937 (0, 2); a trigger plays 936 while the mission var Option is 0 (936's finish sets it to 1);
// 937's finish sets status 2 and does not take the bulb. Status 2: Jugner Forest [S]'s Humus-rich Earth takes a traded
// Cernunnos Bulb, sets status 3 and sends YOU_PLANT_ITEM; a trigger sends IDEAL_PLACE_TO_PLANT_ITEM. Status 3: Amaura
// plays 938 once per zone; Humus-rich Earth [S] sends ITEM_IS_PLANTED_HERE; Jugner Forest's Humus-rich Earth plays the
// progress event 33 (104, 0, 0, 53), whose finish sets status 4. Status 4: Jugner Forest's Humus-rich Earth plays the
// progress event 34 (104, 300, 200, 100, 0, 1648, 0, 0) when the char's cernunnosDefeated local var is 1, else spawns
// Cernunnos on the char's claim and sends DRAWN_UNWANTED_ATTENTION when he is not spawned, else sends
// SENSE_UNUSUAL_PRESENCE; 34's finish gives the Cernunnos Resin, clears the local var and sets status 5. Status 5: the
// earth sends DELIVER_TO_AMAURE; Amaura plays the progress event 939 (0, 0, 0, 31, 0, 0, 1), whose finish takes the
// resin, sets status 6, sets the mission var Timer to the next Vana'diel day and sets the mission's must-zone. Status
// 6: Amaura plays the progress event 941 when the char need not zone and the Timer is due; else 940 while the mission
// local var amauraOption is 0 (940's finish sets it to 1 and the must-zone); 941's finish completes the mission (A
// Spoonful of Sugar current). Kept as the Lua has it: Cernunnos is spawned with the char's claim (updateClaim), and the
// char's cernunnosDefeated local var (set by the mob's own script) is all the earth reads of the fight's outcome.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class DaughterOfAKnight : MissionScript
    {
        public DaughterOfAKnight() : base(XI.Quests.MissionLog.WoTG, "DAUGHTER_OF_A_KNIGHT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/10_Daughter_of_a_Knight";

        const int AreaId = MissionLog.Wotg;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.BottleOfTreantTonic,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ASpoonfulOfSugar,
            });

            // 0: Talk to Amaura at (G-6) in present day Southern San d'Oria.
            b.State(current: true, status: 0)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura)
                .OnTrigger(QuestReply.ProgressEvent(935, 0, 2))
                .OnEventFinish(Zone.SouthernSanDoria, 935, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                });

            // 1: Trade a Cernunnos Bulb to Amaura.
            b.State(current: true, status: 1)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura)
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.CernunnosBulb, 1)))
                    {
                        return QuestReply.ProgressEvent(937, 0, 2);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 0)
                    {
                        return QuestReply.Event(936);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 936, q =>
                {
                    q.Mission.SetVar("Option", 1);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 937, q =>
                {
                    // We don't complete the trade, the player needs to keep the CERNUNNOS_BULB
                    q.Player.SetMissionStatus(AreaId, 2);
                });

            // 2: Trade the Cernunnos Bulb to the Humus-rich Earth in Jugner Forest [S]
            b.State(current: true, status: 2)
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.HumusrichEarth)
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.CernunnosBulb, 1)))
                    {
                        q.Trade.Complete();
                        q.Player.SetMissionStatus(AreaId, 3);

                        return QuestReply.MessageSpecial(Text.JugnerForestS.YOU_PLANT_ITEM, Item.CernunnosBulb);
                    }

                    return null;
                })
                .OnTrigger(QuestReply.MessageSpecial(Text.JugnerForestS.IDEAL_PLACE_TO_PLANT_ITEM,
                    Item.CernunnosBulb));

            // 3: Go to present day Jugner Forest at the same spot and examine the Humus-rich Earth for a cutscene.
            b.State(current: true, status: 3)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura)
                .OnTrigger(QuestReply.Event(938).OncePerZone())
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.HumusrichEarth)
                .OnTrigger(QuestReply.MessageSpecial(Text.JugnerForestS.ITEM_IS_PLANTED_HERE, Item.CernunnosBulb))
                .Npc(Zone.JugnerForest, Npc.JugnerForest.HumusrichEarth)
                .OnTrigger(QuestReply.ProgressEvent(33, 104, 0, 0, 53))
                .OnEventFinish(Zone.JugnerForest, 33, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                });

            // 4: Examine it again to fight Cernunnos. Once he is defeated, check the earth again for a cutscene and
            // the Cernunnos Resin.
            b.State(current: true, status: 4)
                .Npc(Zone.JugnerForest, Npc.JugnerForest.HumusrichEarth).OnTrigger(q =>
                {
                    if (q.Player.LocalVar("cernunnosDefeated") == 1)
                    {
                        return QuestReply.ProgressEvent(34, 104, 300, 200, 100, 0, 1648, 0, 0);
                    }
                    // the Lua reads `if mob and not mob:isSpawned()`, so a nil mob would get SENSE_UNUSUAL_PRESENCE;
                    // here MobIsSpawned throws on a missing mob. Cernunnos is in the zone's spawn list: unreachable
                    else if (!q.Player.MobIsSpawned(Mob.JugnerForest.CERNUNNOS))
                    {
                        q.Player.SpawnMob(Mob.JugnerForest.CERNUNNOS, 0, claim: true);

                        return QuestReply.MessageSpecial(Text.JugnerForest.DRAWN_UNWANTED_ATTENTION);
                    }

                    return QuestReply.MessageSpecial(Text.JugnerForest.SENSE_UNUSUAL_PRESENCE);
                })
                .OnEventFinish(Zone.JugnerForest, 34, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CernunnosResin);

                    q.Player.SetLocalVar("cernunnosDefeated", 0);
                    q.Player.SetMissionStatus(AreaId, 5);
                });

            // 5: Go back to Southern San d'Oria and talk to Amaura for a cutscene.
            b.State(current: true, status: 5)
                .Npc(Zone.JugnerForest, Npc.JugnerForest.HumusrichEarth)
                .OnTrigger(QuestReply.MessageSpecial(Text.JugnerForest.DELIVER_TO_AMAURE))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura)
                .OnTrigger(QuestReply.ProgressEvent(939, 0, 0, 0, 31, 0, 0, 1))
                .OnEventFinish(Zone.SouthernSanDoria, 939, q =>
                {
                    q.Player.DelKeyItem(KeyItem.CernunnosResin);
                    q.Player.SetMissionStatus(AreaId, 6);

                    q.Mission.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Mission.SetMustZone();
                });

            // 6: Wait until the next game day, zone, and talk to her again for a cutscene and the Bottle of Treant
            // Tonic. If you talk to her too soon, you must zone before trying again.
            b.State(current: true, status: 6)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura).OnTrigger(q =>
                {
                    if (!q.Mission.MustZone && q.Mission.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(941);
                    }
                    else if (q.Mission.LocalVar("amauraOption") == 0)
                    {
                        // This event is only encountered once per zone, and then reverts to the default dialogue.
                        return QuestReply.ProgressEvent(940);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 940, q =>
                {
                    q.Mission.SetLocalVar("amauraOption", 1);
                    q.Mission.SetMustZone();
                })
                .OnEventFinish(Zone.SouthernSanDoria, 941, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
