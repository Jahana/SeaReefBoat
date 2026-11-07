// Orastery Woes (Log ID 2, Quest ID 86), a line-for-line port of scripts/quests/windurst/Orastery_Woes.lua (quest
// rework B3b). Kuroido-Moido !pos -112.5 -4.2 102.9 240; qm1 !pos 197 -8 -27.5 122.
// The club weapon-skill quest (Black Halo): offered when the char can equip the Club of Trials, has club skill 230 and
// no Weapon Training Guide (578, oncePerZone: the club and the guide, begun); the club with 300 weapon skill points
// traded to Kuroido-Moido (581) gives the Map to the Annals of Truth; the qm1 in Ro'Maeve pops Eldhrimnir, whose death
// lets the qm1 give the Annals of Truth; Kuroido-Moido's 583 completes the quest (fame +20, Black Halo learned, the key
// items taken). 579 option 3 abandons the quest. The RoE record reads its completion.
// Kept as the Lua file has it (as Blood and Glory and From Saplings Grow): 578's finish begins the quest without a
// second club when one is held; 579's option 1 with the club held does nothing; the qm1's Annals of Truth needs only the
// local var a kill sets (the map is not tested again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class OrasteryWoes : QuestScript
    {
        public OrasteryWoes() : base(QuestLog.Windurst, "ORASTERY_WOES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Orastery_Woes";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst });

            // Section: Quest available
            // Lua: player:getCharSkillLevel(xi.skill.CLUB) / 10 >= 230,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2300)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.ClubOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Club) / 10 >= 230 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido)
                    .OnTrigger(q => QuestReply.Event(578).OncePerZone()) // start
                .OnEventFinish(Zone.PortWindurst, 578, q =>
                {
                    if (q.Player.HasItem(Item.ClubOfTrials) || q.Player.GiveItem(Item.ClubOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KuroidoMoido)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(583); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(582); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(579, 0, Item.ClubOfTrials, 0, 0, q.Player.HasItem(Item.ClubOfTrials) ? 2 : 0); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.ClubOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(580); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(581, 0, 0, KeyItem.AnnalsOfTruth); // finished weapon
                            }
                        }

                        return null;
                    })
                .OnEventFinish(Zone.PortWindurst, 579, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.ClubOfTrials))
                    {
                        q.Player.GiveItem(Item.ClubOfTrials);
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.DelQuest(QuestLog.Windurst, "ORASTERY_WOES");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 581, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.PortWindurst, 583, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.BlackHalo);
                        q.Text.Special(Text.PortWindurst.BLACK_HALO_LEARNED);
                    }
                })
                .Npc(Zone.Romaeve, Npc.Romaeve.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.Romaeve.ELDHRIMNIR, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.Romaeve.SENSE_OMINOUS_PRESENCE);
                    }

                    return null;
                })
                .Npc(Zone.Romaeve, Npc.Romaeve.Eldhrimnir).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
