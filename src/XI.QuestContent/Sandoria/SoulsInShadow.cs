// Souls in Shadow (Log ID 0, Quest ID 99), a line-for-line port of scripts/quests/sandoria/Souls_in_Shadow.lua (quest
// rework B3b, San d'Oria group 5). Novalmauge : !pos 70 -24 21 167; qm_mokumokuren : !pos 118 36 -281 160. Offered (0,
// once per zone) to a char who can wear the Scythe of Trials (the level checked) with scythe skill 240 and no Weapon
// Training Guide; 0's option 1 gives the scythe (unless held), the guide, and begins it. Accepted: Novalmauge's 3
// (argument 1 when the scythe is held; option 1 gives the scythe again when not held, option 2 drops the quest and the
// guide and map), 4 with the map, 5 with the Annals. A trade of exactly the scythe plays 2 under 300 weapon skill
// points and 1 from 300 (the scythe taken, the Map to the Annals of Truth). In the Den of Rancor the map pops
// Mokumokuren at qm_mokumokuren (SENSE_OMINOUS_PRESENCE); its death sets the char's local var killed_wsnm, and the next
// qm talk gives the Annals of Truth. 5 completes it (fame +20), deletes the three key items and teaches Spiral Hell
// (SPIRAL_HELL_LEARNED). goblinfootprint.lua and the RoE records read the status (stay). The same shape as Old Wounds
// (group 4) and Methods Create Madness (group 2).
// Kept as the Lua has it: 2 has no finish handler (the scythe stays with the char); 3's option 2 deletes the map, which
// 3 never meets (Novalmauge plays 4 while it is held).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class SoulsInShadow : QuestScript
    {
        public SoulsInShadow() : base(QuestLog.Sandoria, "SOULS_IN_SHADOW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Souls_in_Shadow";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria });

            // Lua: player:getCharSkillLevel(xi.skill.SCYTHE) / 10 >= 240 (a float division; the same as >= 2400)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.ScytheOfTrials, true) &&
                    // player:getCharSkillLevel(xi.skill.SCYTHE)
                    q.Player.CharSkillLevel(Skill.Scythe) / 10.0 >= 240 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                // start
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge).OnTrigger(QuestReply.Event(0).OncePerZone())
                .OnEventFinish(Zone.BostaunieuxOubliette, 0, q =>
                {
                    if (q.Option == 1 &&
                        (q.Player.HasItem(Item.ScytheOfTrials) || q.Player.GiveItem(Item.ScytheOfTrials)))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            // complete
                            return QuestReply.ProgressEvent(5);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            // cont 2
                            return QuestReply.Event(4);
                        }
                        else
                        {
                            // cont 1
                            return QuestReply.Event(3, q.Player.HasItem(Item.ScytheOfTrials) ? 1 : 0);
                        }
                    })
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.SCYTHE_OF_TRIALS)
                        if (q.Trade.HasExactly(Item.ScytheOfTrials))
                        {
                            // trade:getItem(0):getWeaponskillPoints()
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                // unfinished weapon
                                return QuestReply.Event(2);
                            }
                            else
                            {
                                // finished weapon
                                return QuestReply.ProgressEvent(1);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.BostaunieuxOubliette, 3, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.ScytheOfTrials))
                    {
                        q.Player.GiveItem(Item.ScytheOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Sandoria, "SOULS_IN_SHADOW");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.BostaunieuxOubliette, 1, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.BostaunieuxOubliette, 5, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        // player:addLearnedWeaponskill(xi.wsUnlock.SPIRAL_HELL)
                        q.Player.AddLearnedWeaponskill(WsUnlock.SpiralHell);
                        q.Text.Special(Text.BostaunieuxOubliette.SPIRAL_HELL_LEARNED);
                    }
                })
                .Npc(Zone.DenOfRancor, Npc.DenOfRancor.qmmokumokuren).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.DenOfRancor.MOKUMOKUREN, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.DenOfRancor.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.DenOfRancor, Npc.DenOfRancor.Mokumokuren).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
