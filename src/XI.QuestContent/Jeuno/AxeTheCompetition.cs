// Axe the Competition (Log ID 3, Quest ID 59), a line-for-line port of scripts/quests/jeuno/Axe_the_Competition.lua
// (quest rework B3b). Brutus !pos -55 8 95 244; qm9 !pos 218 -8 206 159. Offered (12, once per zone) to a char
// who can wear the Pick of Trials with axe skill 240 and no Weapon Training Guide; 12's option 1 gives the pick (unless
// held) and the guide and begins. A trade of exactly the pick plays 14 under 300 weapon skill points and 13 from 300
// (the trade taken, the Map to the Annals of Truth); at the Temple of Uggalepih's qm9 the map pops Yallery Brown, whose
// death sets the char's local var killed_wsnm while the map is held, and the next qm9 talk gives the Annals of Truth;
// Brutus's 17 completes it (fame +7 in the three nations, the three key items taken, Decimation learned). 15's option 1
// gives the pick again when not held, option 2 drops the quest. The Lua file has no known bug to keep (the template:
// Bastok's The Weight of Your Limits).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class AxeTheCompetition : QuestScript
    {
        public AxeTheCompetition() : base(QuestLog.Jeuno, "AXE_THE_COMPETITION") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Axe_the_Competition";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            // Lua: player:getCharSkillLevel(xi.skill.AXE) / 10 >= 240,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2400)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.PickOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Axe) / 10 >= 240 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(QuestReply.Event(12).OncePerZone()) // start
                .OnEventFinish(Zone.UpperJeuno, 12, q =>
                {
                    if (q.Option == 1 &&
                        (q.Player.HasItem(Item.PickOfTrials) || q.Player.GiveItem(Item.PickOfTrials)))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(17); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(16); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(15, q.Player.HasItem(Item.PickOfTrials) ? 1 : 0); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.PickOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(14); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(13); // finished weapon
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.UpperJeuno, 15, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.PickOfTrials))
                    {
                        q.Player.GiveItem(Item.PickOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Jeuno, "AXE_THE_COMPETITION");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 13, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.UpperJeuno, 17, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.Decimation);
                        q.Text.Special(Text.UpperJeuno.DECIMATION_LEARNED);
                    }
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qm9).OnTrigger(q =>
                {
                    if (q.Player.LocalVar("killed_wsnm") == 1)
                    {
                        q.Player.SetLocalVar("killed_wsnm", 0);

                        return QuestReply.KeyItem(KeyItem.AnnalsOfTruth);
                    }
                    else if (
                        q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth) &&
                        !q.Player.HasKeyItem(KeyItem.AnnalsOfTruth) &&
                        // { hide = 0 }: popFromQM's own claim default (true)
                        q.Player.PopFromQm(Mob.TempleOfUggalepih.YALLERY_BROWN, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.TempleOfUggalepih.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.YalleryBrown).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
