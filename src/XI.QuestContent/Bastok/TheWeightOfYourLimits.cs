// The Weight of Your Limits (Log ID 1, Quest ID 66), a line-for-line port of
// scripts/quests/bastok/The_Weight_of_Your_Limits.lua (quest rework B3b). Iron Eater : !pos 92 -19.6 2 237; qm1 :
// !pos -324 1 474 121. Offered (790, once per zone) to a char who can wear the Axe of Trials with great axe skill 240
// and no Weapon Training Guide; 790's finish gives the axe (unless held) and the guide and begins. A trade of the axe
// plays 792 under 300 weapon skill points and 793 from 300 (the Map to the Annals of Truth); at the Sanctuary of
// Zi'Tah's qm1 the map pops Greenman, his death sets the char's local var killed_wsnm, and the next qm1 talk gives the
// Annals of Truth; Iron Eater's 794 completes it and teaches Steel Cyclone. 791's option 1 gives the axe again,
// option 2 drops the quest. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheWeightOfYourLimits : QuestScript
    {
        public TheWeightOfYourLimits() : base(QuestLog.Bastok, "THE_WEIGHT_OF_YOUR_LIMITS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Weight_of_Your_Limits";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok });

            // Lua: player:getCharSkillLevel(xi.skill.GREAT_AXE) / 10 >= 240,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2400)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.AxeOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.GreatAxe) / 10 >= 240 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater).OnTrigger(QuestReply.Event(790).OncePerZone())
                .OnEventFinish(Zone.Metalworks, 790, q =>
                {
                    if (q.Player.HasItem(Item.AxeOfTrials) || q.Player.GiveItem(Item.AxeOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.IronEater)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(794);
                        }
                        else
                        {
                            int hideReacquireMenuItem = q.Player.HasItem(Item.AxeOfTrials) ||
                                q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth) ? 1 : 0;

                            return QuestReply.Event(791, hideReacquireMenuItem);
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.AxeOfTrials, 1)))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(792);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(793);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Metalworks, 791, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.AxeOfTrials))
                    {
                        q.Player.GiveItem(Item.AxeOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Bastok, "THE_WEIGHT_OF_YOUR_LIMITS");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.Metalworks, 793, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.Metalworks, 794, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.SteelCyclone);
                        q.Text.Special(Text.Metalworks.STEEL_CYCLONE_LEARNED);
                    }
                })
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.TheSanctuaryOfZitah.GREENMAN, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.Greenman).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
