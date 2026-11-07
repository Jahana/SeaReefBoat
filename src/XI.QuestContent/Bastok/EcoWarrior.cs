// Eco-Warrior (Bastok) (Log ID 1, Quest ID 65), a line-for-line port of scripts/quests/bastok/Eco_Warrior.lua (quest rework
// B3b). Raifa : !pos -166.416 -8.48 7.153 236; Degga : !pos 40 -68 -259 196; qm5 : !pos 22.796 -61.156 -19.687 196.
// Raifa's 278 (Bastok fame level 1, EcoReset 0, no other nation's Eco-Warrior accepted; 283 when one is) begins it on
// option 1; Degga's 13 (option 1) puts on the ointment (Level Restriction 25, ON_ZONE); with it the qm5 pops the two
// Puddings (claimed, looking at the char, the qm kept shown); the death of the last Pudding standing, under the
// restriction, sets Prog 1; the qm5 at Prog 1 gives the KI Indigested Ore; Degga's 16 (option 0) sets Informed, 15
// (option 0) takes the restriction off; Raifa's 282 completes it (Dragon Chronicles, 5000 gil, Cerulean Soldier, fame
// +20), deletes the ore and sets EcoReset 1 until the next conquest tally. Once completed and after the tally, Raifa's
// 278 deletes the quest and begins it again.
// The San d'Oria and Windurst Eco-Warriors read this quest's status (src/XI.QuestContent/Sandoria/EcoWarrior.cs,
// Windurst/EcoWarrior.cs: status reads). modules/era/lua/quests/bastok/eco_warrior.lua (Modules/Era/Bastok/EcoWarrior.cs) replaces
// state 2's finish of 13 with a copy whose restriction is 20.
// Kept as the Lua file has it: the ore alone gates the turn-in (Degga's 16 is optional); 16 does not take the restriction
// off; Raifa's 280 reminder plays at any Prog without the ore; the fame level test (>= 1) always holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class EcoWarrior : QuestScript
    {
        public EcoWarrior() : base(QuestLog.Bastok, "ECO_WARRIOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Eco_Warrior";

        // xi.effect.LEVEL_RESTRICTION (scripts/enum/effect.codegen.lua) and xi.effectFlag.ON_ZONE
        // (scripts/enum/effect_flag.codegen.lua): no generated class has them. Public: the era module uses them too
        public const int LevelRestriction = 143;
        public const int EffectFlagOnZone = 0x00000100;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Gil = 5000,
                Item = Item.DragonChronicles,
                Title = Title.CeruleanSoldier,
            });

            // Section: Quest is available.
            b.State(QuestStatus.Available)
                .Npc(Zone.PortBastok, Npc.PortBastok.Raifa).OnTrigger(RaifaOffer)
                .OnEventFinish(Zone.PortBastok, 278, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.GusgenMines, Npc.GusgenMines.Degga).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.IndigestedOre) && q.Quest.Var("Informed") == 0)
                    {
                        return QuestReply.ProgressEvent(16); // Sends the player to Raifa.
                    }
                    else if (q.Player.HasStatusEffect(LevelRestriction))
                    {
                        return QuestReply.ProgressEvent(15); // Offers to remove the ointment.
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.IndigestedOre))
                    {
                        return QuestReply.ProgressEvent(13); // Offers to apply the ointment.
                    }
                    return null;
                })
                .Npc(Zone.GusgenMines, Npc.GusgenMines.Pudding).OnMobDeath(q =>
                {
                    if (!q.Player.HasStatusEffect(LevelRestriction))
                    {
                        return;
                    }

                    // for i = PUDDING_OFFSET, PUDDING_OFFSET + 1: GetMobByID(i) is never nil here (both are Gusgen Mines
                    // mobs), so the Lua's nmCheck test always holds
                    for (int offset = 0; offset <= 1; offset++)
                    {
                        int i = q.Player.MobIdOf(Mob.GusgenMines.PUDDING_OFFSET) + offset;

                        if (i != q.Mob.Id && q.Player.MobIsAlive(Mob.GusgenMines.PUDDING_OFFSET, offset))
                        {
                            return;
                        }
                    }

                    q.Quest.SetStep(1);
                })
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm5).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0 &&
                        q.Player.HasStatusEffect(LevelRestriction) &&
                        // { PUDDING_OFFSET, PUDDING_OFFSET + 1 }, { claim = true, look = true, hide = 0 }
                        q.Player.PopFromQmSeries(Mob.GusgenMines.PUDDING_OFFSET, 2, hide: 0, claim: true, look: true))
                    {
                        return QuestReply.MessageSpecial(Text.GusgenMines.OINTMENT_DRAWS_MONSTERS);
                    }
                    else if (q.Quest.Step == 1 && !q.Player.HasKeyItem(KeyItem.IndigestedOre))
                    {
                        return QuestReply.KeyItem(KeyItem.IndigestedOre);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GusgenMines, 13, q =>
                {
                    if (q.Option == 1)
                    {
                        // subPower 1: exp uses actual level and not the restricted level; the origin is the char
                        q.Player.AddStatusEffect(LevelRestriction, power: 25, subPower: 1, flag: EffectFlagOnZone);
                    }
                })
                .OnEventFinish(Zone.GusgenMines, 15, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelStatusEffect(LevelRestriction);
                    }
                })
                .OnEventFinish(Zone.GusgenMines, 16, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Informed", 1);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Raifa).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.IndigestedOre))
                    {
                        return QuestReply.ProgressEvent(282); // Completes the quest.
                    }
                    else
                    {
                        return QuestReply.Event(280); // Reminder to see Degga.
                    }
                })
                .OnEventFinish(Zone.PortBastok, 282, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.IndigestedOre);
                        q.Player.SetCharVar("EcoReset", 1, (uint)q.Player.NextConquestTally);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                // Repeatable after the next conquest tally.
                .Npc(Zone.PortBastok, Npc.PortBastok.Raifa).OnTrigger(RaifaOffer)
                .OnEventFinish(Zone.PortBastok, 278, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.Bastok, "ECO_WARRIOR");
                        q.Quest.Begin();
                    }
                });
        }

        // the available and the completed section's Raifa onTrigger (the same code in both)
        static QuestReply RaifaOffer(IQuestContext q)
        {
            if (q.Player.QuestStatus(QuestLog.Sandoria, "ECO_WARRIOR") == QuestStatus.Accepted ||
                q.Player.QuestStatus(QuestLog.Windurst, "ECO_WARRIOR") == QuestStatus.Accepted)
            {
                return QuestReply.Event(283); // Already on another nation's Eco-Warrior.
            }
            else if (q.Player.FameLevel(FameArea.Bastok) >= 1 && q.Player.CharVar("EcoReset") == 0)
            {
                return QuestReply.ProgressEvent(278); // Offers the quest.
            }
            return null;
        }
    }
}
