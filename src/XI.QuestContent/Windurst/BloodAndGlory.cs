// Blood and Glory (Log ID 2, Quest ID 87), a line-for-line port of scripts/quests/windurst/Blood_and_Glory.lua (quest
// rework B3b). Shantotto !pos 122 -2 112 239; qm3 !pos 119 20 144 205.
// The staff weapon-skill quest (Retribution): offered when the char can equip the Pole of Trials, has staff skill 230
// and no Weapon Training Guide (445, oncePerZone: the pole and the guide, begun); the pole with 300 weapon skill points
// traded to Shantotto (448) gives the Map to the Annals of Truth; the qm3 in Ifrit's Cauldron pops Cailleach Bheur, whose
// death lets the qm3 give the Annals of Truth; Shantotto's 450 completes the quest (fame +20, Retribution learned, the
// key items taken, the char moved). 446 option 3 abandons the quest. The RoE record reads its completion.
// Kept as the Lua file has it: 445's finish begins the quest without a second pole when one is held; 446's option 1
// with the pole held does nothing; the qm3's Annals of Truth needs only the local var a kill sets (the map is not
// tested again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class BloodAndGlory : QuestScript
    {
        public BloodAndGlory() : base(QuestLog.Windurst, "BLOOD_AND_GLORY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Blood_and_Glory";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst });

            // Section: Quest available
            // Lua: player:getCharSkillLevel(xi.skill.STAFF) / 10 >= 230,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2300)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.PoleOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Staff) / 10 >= 230 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto)
                    .OnTrigger(q => QuestReply.Event(445).OncePerZone()) // start
                .OnEventFinish(Zone.WindurstWalls, 445, q =>
                {
                    if (q.Player.HasItem(Item.PoleOfTrials) || q.Player.GiveItem(Item.PoleOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(450); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(449); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(446, 0, Item.PoleOfTrials, 0, 0, q.Player.HasItem(Item.PoleOfTrials) ? 2 : 0); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.PoleOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(447); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(448, 0, 0, KeyItem.AnnalsOfTruth); // finished weapon
                            }
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWalls, 446, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.PoleOfTrials))
                    {
                        q.Player.GiveItem(Item.PoleOfTrials);
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.DelQuest(QuestLog.Windurst, "BLOOD_AND_GLORY");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 448, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.WindurstWalls, 450, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.Retribution);
                        q.Text.Special(Text.WindurstWalls.RETRIBUTION_LEARNED);
                        q.Player.SetPos(121, -3, 111);
                    }
                })
                .Npc(Zone.IfritsCauldron, Npc.IfritsCauldron.qm3).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.IfritsCauldron.CAILLEACH_BHEUR, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.IfritsCauldron.SENSE_OMINOUS_PRESENCE);
                    }

                    return null;
                })
                .Npc(Zone.IfritsCauldron, Npc.IfritsCauldron.CailleachBheur).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
