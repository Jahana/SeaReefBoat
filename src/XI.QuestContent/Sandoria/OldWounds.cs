// Old Wounds (Log ID 0, Quest ID 102), a line-for-line port of scripts/quests/sandoria/Old_Wounds.lua (quest rework B3b,
// San d'Oria group 4). Curilla : !pos 27 0.1 0.1 233; qm3 : !pos -145 2 446 208. Offered (43, once per zone) to a char who
// can wear the Sapara of Trials with sword skill 240 and no Weapon Training Guide; 43's option 1 gives the sapara (unless
// held), the guide, and begins it. Accepted: Curilla's 46 (argument 1 when the sapara is held; option 1 gives the sapara
// again when not held, option 2 drops the quest and the guide and map), 47 with the map, 48 with the Annals. A trade of
// exactly the sapara plays 45 under 300 weapon skill points and 44 from 300 (the sapara taken, the Map to the Annals of
// Truth). In the Quicksand Caves the map pops Girtablulu at qm3 (SENSE_OMINOUS_PRESENCE); its death sets the char's local
// var killed_wsnm, and the next qm3 talk gives the Annals of Truth. 48 completes it (fame +20), deletes the three key
// items and teaches Savage Blade (SAVAGE_BLADE_LEARNED). Curilla's script (Trust memories) and RoE records read the
// status (stay). The same shape as Methods Create Madness (group 2).
// Kept as the Lua has it: 45 has no finish handler (the sapara stays with the char); 46's option 2 deletes the map,
// which 46 never meets (Curilla plays 47 while it is held).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class OldWounds : QuestScript
    {
        public OldWounds() : base(QuestLog.Sandoria, "OLD_WOUNDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Old_Wounds";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria });

            // Lua: player:getCharSkillLevel(xi.skill.SWORD) / 10 >= 240 (a float division; the same as >= 2400)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.SaparaOfTrials, true) &&
                    // player:getCharSkillLevel(xi.skill.SWORD)
                    q.Player.CharSkillLevel(Skill.Sword) / 10.0 >= 240 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                // start
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(QuestReply.Event(43).OncePerZone())
                .OnEventFinish(Zone.ChateauDoraguille, 43, q =>
                {
                    if (q.Option == 1 &&
                        (q.Player.HasItem(Item.SaparaOfTrials) || q.Player.GiveItem(Item.SaparaOfTrials)))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            // complete
                            return QuestReply.ProgressEvent(48);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            // cont 2
                            return QuestReply.Event(47);
                        }
                        else
                        {
                            // cont 1
                            return QuestReply.Event(46, q.Player.HasItem(Item.SaparaOfTrials) ? 1 : 0);
                        }
                    })
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.SAPARA_OF_TRIALS)
                        if (q.Trade.HasExactly(Item.SaparaOfTrials))
                        {
                            // trade:getItem(0):getWeaponskillPoints()
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                // unfinished weapon
                                return QuestReply.Event(45);
                            }
                            else
                            {
                                // finished weapon
                                return QuestReply.ProgressEvent(44);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.ChateauDoraguille, 46, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.SaparaOfTrials))
                    {
                        q.Player.GiveItem(Item.SaparaOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Sandoria, "OLD_WOUNDS");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 44, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.ChateauDoraguille, 48, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        // player:addLearnedWeaponskill(xi.wsUnlock.SAVAGE_BLADE)
                        q.Player.AddLearnedWeaponskill(WsUnlock.SavageBlade);
                        q.Text.Special(Text.ChateauDoraguille.SAVAGE_BLADE_LEARNED);
                    }
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.qm3).OnTrigger(q =>
                {
                    if (q.Player.LocalVar("killed_wsnm") == 1)
                    {
                        q.Player.SetLocalVar("killed_wsnm", 0);
                        return QuestReply.KeyItem(KeyItem.AnnalsOfTruth);
                    }
                    else if (
                        q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth) &&
                        !q.Player.HasKeyItem(KeyItem.AnnalsOfTruth) &&
                        // { hide = 0 } with no claim: popFromQM's claim defaults to true
                        q.Player.PopFromQm(Mob.QuicksandCaves.GIRTABLULU, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.QuicksandCaves.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.QuicksandCaves, Npc.QuicksandCaves.Girtablulu).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
