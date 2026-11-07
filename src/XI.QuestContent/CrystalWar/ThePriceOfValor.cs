// The Price of Valor (Log ID 7, Quest ID 44), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_7_The_Price_of_Valor.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32), Tree Hollow, Well (defaults NOTHING_OUT_OF_ORDINARY). Jugner Forest
// [S]: Felled Trees (default NOTHING_OUT_OF_ORDINARY). Vunkerl Inlet [S]: Toppled Cresset 1 / 2, Underbrush (defaults
// NOTHING_OUT_OF_ORDINARY), Madthrasher Zradbodd (no IDs.lua key: found by name). Pashhow Marshlands [S]: Shimmering
// Pondweed (no default).
// Available, In a Haze of Glory completed (Prog set while available): Rholont: the mustZone clear and the Timer at or
// before today (Haze's 82 sets both): Prog 0: 638 (-> Prog 1); Prog 1: the Long-Life Biscuits, the Flask of Kingdom
// Water and the Ronfaure Maple Syrup (logging in East Ronfaure [S]: xi.wotg.helpers.helmTrade reads this quest's Prog
// 1): 642, whose finish gives the Biscuit a la Rholont, takes the three and sets Prog 2; else 643 (an Event); Prog 2:
// 645 (an Event); else (the mustZone or a Timer to come) 49 (an Event). Tree Hollow / Well: Prog 1 without their key
// item: 640 / 641, whose finishes give it. Felled Trees: Prog 2: 211, whose finish begins it. Accepted: Toppled Cresset
// 1: Prog 2: 202 (-> 3); Toppled Cresset 2: Prog 3: 203 (-> 4); Underbrush: Prog 4: 204 (-> 5); Prog 5: the quest's
// nmDefeated local var 0: Madthrasher Zradbodd (the zone's first entity of the name) not spawned: its id into the nmId
// local var and 205, whose finish spawns that id claimed by the char; spawned: NOW_IS_NOT_THE_TIME; nmDefeated set: 206
// (-> Prog 6). The NM's death at Prog 5: nmDefeated 1. Shimmering Pondweed: Prog 6: 105, whose finish completes it (a
// Peiste Skin).
// Kept as the Lua has it: Prog and the Biscuit a la Rholont are written while available; 211 begins on any option; the
// biscuit is never taken; nmDefeated and nmId are the quest's local vars (not saved); the Lua's `if not zoneObj then
// return end` guard has no C# arm (the char is always in a zone); 105 completes with no check but the reward's fit; no
// completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class ThePriceOfValor : QuestScript
    {
        public ThePriceOfValor() : base(QuestLog.CrystalWar, "THE_PRICE_OF_VALOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_7_The_Price_of_Valor";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6);

            b.Reward(new QuestReward
            {
                Item = Item.PeisteSkin,
            });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "IN_A_HAZE_OF_GLORY"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (!q.Quest.MustZone &&
                        q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(638);
                        }
                        else if (questProgress == 1)
                        {
                            if (q.Player.HasKeyItem(KeyItem.LongLifeBiscuits) &&
                                q.Player.HasKeyItem(KeyItem.FlaskOfKingdomWater) &&
                                q.Player.HasKeyItem(KeyItem.RonfaureMapleSyrup))
                            {
                                return QuestReply.ProgressEvent(642);
                            }
                            else
                            {
                                return QuestReply.Event(643);
                            }
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.Event(645);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(49);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.TreeHollow).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.LongLifeBiscuits) &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(640);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Well).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.FlaskOfKingdomWater) &&
                        q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(641);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 638, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 640, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LongLifeBiscuits);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 641, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FlaskOfKingdomWater);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 642, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BiscuitALaRholont);

                    q.Player.DelKeyItem(KeyItem.LongLifeBiscuits);
                    q.Player.DelKeyItem(KeyItem.FlaskOfKingdomWater);
                    q.Player.DelKeyItem(KeyItem.RonfaureMapleSyrup);

                    q.Quest.SetStep(2);
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.FelledTrees).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(211);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForestS, 211, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.ToppledCresset1).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(202);
                    }
                    return null;
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.ToppledCresset2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(203);
                    }
                    return null;
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.Underbrush).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(204);
                    }
                    else if (questProgress == 5)
                    {
                        if (q.Quest.LocalVar("nmDefeated") == 0)
                        {
                            // the Lua's `local zoneObj = player:getZone() if not zoneObj then return end`: the char is
                            // always in a zone here, so the guard has no arm
                            int mobId = q.Player.MobIdByName(Npc.VunkerlInletS.MadthrasherZradbodd);

                            if (!q.Player.MobIsSpawnedById(mobId))
                            {
                                q.Quest.SetLocalVar("nmId", mobId);
                                return QuestReply.ProgressEvent(205);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.VunkerlInletS.NOW_IS_NOT_THE_TIME);
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(206);
                        }
                    }
                    return null;
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.MadthrasherZradbodd).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        q.Quest.SetLocalVar("nmDefeated", 1);
                    }
                })
                .OnEventFinish(Zone.VunkerlInletS, 202, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.VunkerlInletS, 203, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.VunkerlInletS, 204, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.VunkerlInletS, 205, q =>
                {
                    int mobId = q.Quest.LocalVar("nmId");

                    q.Player.SpawnMobById(mobId, true);
                })
                .OnEventFinish(Zone.VunkerlInletS, 206, q =>
                {
                    q.Quest.SetStep(6);
                })
                .Npc(Zone.PashhowMarshlandsS, Npc.PashhowMarshlandsS.ShimmeringPondweed).OnTrigger(q =>
                {
                    if (q.Quest.Step == 6)
                    {
                        return QuestReply.ProgressEvent(105);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PashhowMarshlandsS, 105, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
