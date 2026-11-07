// Methods Create Madness (Log ID 0, Quest ID 98), a line-for-line port of scripts/quests/sandoria/Methods_Create_Madness.lua
// (quest rework B3b, San d'Oria group 2). Balasiel : !pos -136 -11 64 230; qm1 : !pos 107 0.7 -125.25 176. Offered (8, once
// per zone) to a char who can wear the Spear of Trials with polearm skill 240 and no Weapon Training Guide; 8's option 1
// gives the spear (unless held), the guide, and begins it. Accepted: Balasiel's 11 (argument 1 when the spear is held;
// option 1 gives the spear again when not held, option 2 drops the quest and the guide and map), 12 with the map, 13
// with the Annals. A trade of exactly the spear plays 10 under 300 weapon skill points and 9 from 300 (the spear taken,
// the Map to the Annals of Truth). In Sea Serpent Grotto the map pops the Water Leaper at qm1 (SENSE_OMINOUS_PRESENCE);
// its death sets the char's local var killed_wsnm, and the next qm1 talk gives the Annals of Truth. 13 completes it
// (fame +20), deletes the three key items and teaches Impulse Drive (IMPULSE_DRIVE_LEARNED). RoE records read the
// status (stays).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class MethodsCreateMadness : QuestScript
    {
        public MethodsCreateMadness() : base(QuestLog.Sandoria, "METHODS_CREATE_MADNESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Methods_Create_Madness";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria });

            // Lua: player:getCharSkillLevel(xi.skill.POLEARM) / 10 >= 240 (a float division; the same as >= 2400)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.SpearOfTrials, true) &&
                    // player:getCharSkillLevel(xi.skill.POLEARM)
                    q.Player.CharSkillLevel(Skill.Polearm) / 10.0 >= 240 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).OnTrigger(QuestReply.Event(8).OncePerZone())
                .OnEventFinish(Zone.SouthernSanDoria, 8, q =>
                {
                    if (q.Option == 1 &&
                        (q.Player.HasItem(Item.SpearOfTrials) || q.Player.GiveItem(Item.SpearOfTrials)))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(13);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(12);
                        }
                        else
                        {
                            return QuestReply.Event(11, q.Player.HasItem(Item.SpearOfTrials) ? 1 : 0);
                        }
                    })
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.SPEAR_OF_TRIALS)
                        if (q.Trade.HasExactly(Item.SpearOfTrials))
                        {
                            // trade:getItem(0):getWeaponskillPoints()
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(10);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(9);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 11, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.SpearOfTrials))
                    {
                        q.Player.GiveItem(Item.SpearOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Sandoria, "METHODS_CREATE_MADNESS");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 9, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 13, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        // player:addLearnedWeaponskill(xi.wsUnlock.IMPULSE_DRIVE)
                        q.Player.AddLearnedWeaponskill(WsUnlock.ImpulseDrive);
                        q.Text.Special(Text.SouthernSanDoria.IMPULSE_DRIVE_LEARNED);
                    }
                })
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.SeaSerpentGrotto.WATER_LEAPER, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.SeaSerpentGrotto.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.WaterLeaper).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
