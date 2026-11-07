// From Saplings Grow (Log ID 2, Quest ID 85), a line-for-line port of scripts/quests/windurst/From_Saplings_Grow.lua
// (quest rework B3b). Perih Vashai !pos 117 -3 92 241; qm1 !pos -157 -8 198.2 113.
// The archery weapon-skill quest (Empyreal Arrow): offered when the char can equip the Bow of Trials, has archery skill
// 250 and no Weapon Training Guide (661, oncePerZone: the bow and the guide, begun); the bow with 300 weapon skill points
// traded to Perih Vashai (664) gives the Map to the Annals of Truth; the qm1 in Cape Teriggan pops Stolas, whose death
// lets the qm1 give the Annals of Truth; Perih Vashai's 666 completes the quest (fame +20, Empyreal Arrow learned, the
// key items taken). 662 option 3 abandons the quest. The RoE record reads its completion.
// Kept as the Lua file has it (as Blood and Glory): 661's finish begins the quest without a second bow when one is held;
// 662's option 1 with the bow held does nothing; the qm1's Annals of Truth needs only the local var a kill sets (the map
// is not tested again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class FromSaplingsGrow : QuestScript
    {
        public FromSaplingsGrow() : base(QuestLog.Windurst, "FROM_SAPLINGS_GROW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/From_Saplings_Grow";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst });

            // Section: Quest available
            // Lua: player:getCharSkillLevel(xi.skill.ARCHERY) / 10 >= 250,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2500)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.BowOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Archery) / 10 >= 250 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai)
                    .OnTrigger(q => QuestReply.Event(661).OncePerZone()) // start
                .OnEventFinish(Zone.WindurstWoods, 661, q =>
                {
                    if (q.Player.HasItem(Item.BowOfTrials) || q.Player.GiveItem(Item.BowOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(666); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(665); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(662, 0, Item.BowOfTrials, 0, 0, q.Player.HasItem(Item.BowOfTrials) ? 2 : 0); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BowOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(663); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(664, 0, 0, KeyItem.AnnalsOfTruth); // finished weapon
                            }
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWoods, 662, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.BowOfTrials))
                    {
                        q.Player.GiveItem(Item.BowOfTrials);
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.DelQuest(QuestLog.Windurst, "FROM_SAPLINGS_GROW");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 664, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.WindurstWoods, 666, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.EmpyrealArrow);
                        q.Text.Special(Text.WindurstWoods.EMPYREAL_ARROW_LEARNED);
                    }
                })
                .Npc(Zone.CapeTeriggan, Npc.CapeTeriggan.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.CapeTeriggan.STOLAS, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.CapeTeriggan.SENSE_OMINOUS_PRESENCE);
                    }

                    return null;
                })
                .Npc(Zone.CapeTeriggan, Npc.CapeTeriggan.Stolas).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
