// Shoot First, Ask Questions Later (Log ID 1, Quest ID 67), a line-for-line port of
// scripts/quests/bastok/Shoot_First_Ask_Questions_Later.lua (quest rework B3b). Cid : !pos -12 -12 1 237; qm1 :
// !pos -11 -19 -177 153. Offered (795, once per zone) to a char who can wear the Gun of Trials with marksmanship
// skill 250 and no Weapon Training Guide; 795's finish gives the gun (unless held) and the guide and begins. A trade
// of the gun plays 797 under 300 weapon skill points and 798 from 300 (the Map to the Annals of Truth); at the
// Boyahda Tree's qm1 the map pops the Beet Leafhopper, its death sets the char's local var killed_wsnm, and the next
// qm1 talk gives the Annals of Truth; Cid's 799 completes it and teaches Detonator. 796's option 1 gives the gun
// again, option 2 drops the quest. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class ShootFirstAskQuestionsLater : QuestScript
    {
        public ShootFirstAskQuestionsLater() : base(QuestLog.Bastok, "SHOOT_FIRST_ASK_QUESTIONS_LATER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Shoot_First_Ask_Questions_Later";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok });

            // Lua: player:getCharSkillLevel(xi.skill.MARKSMANSHIP) / 10 >= 250,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2500)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.GunOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Marksmanship) / 10 >= 250 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.Event(795).OncePerZone())
                .OnEventFinish(Zone.Metalworks, 795, q =>
                {
                    if (q.Player.HasItem(Item.GunOfTrials) || q.Player.GiveItem(Item.GunOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(799);
                        }
                        else
                        {
                            int hideReacquireMenuItem = q.Player.HasItem(Item.GunOfTrials) ||
                                q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth) ? 1 : 0;

                            return QuestReply.Event(796, hideReacquireMenuItem);
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.GunOfTrials, 1)))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(797);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(798);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Metalworks, 796, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.GunOfTrials))
                    {
                        q.Player.GiveItem(Item.GunOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Bastok, "SHOOT_FIRST_ASK_QUESTIONS_LATER");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 798, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.Metalworks, 799, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.Detonator);
                        q.Text.Special(Text.Metalworks.DETONATOR_LEARNED);
                    }
                })
                .Npc(Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.TheBoyahdaTree.BEET_LEAFHOPPER, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.TheBoyahdaTree.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.BeetLeafhopper).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
