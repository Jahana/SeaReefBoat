// The Walls of Your Mind (Log ID 1, Quest ID 69), a line-for-line port of
// scripts/quests/bastok/The_Walls_of_Your_Mind.lua (quest rework B3b). Oggbi : !pos -159 -7 5 236; qm1 :
// !pos 20 17 -140 167. Offered (286, once per zone) to a char who can wear the Knuckles of Trials with hand-to-hand
// skill 250 and no Weapon Training Guide; 286's finish gives the knuckles (unless held) and the guide and begins. A
// trade of the knuckles plays 288 under 300 weapon skill points and 289 from 300 (the Map to the Annals of Truth); at
// Bostaunieux Oubliette's qm1 the map pops Bodach, his death sets the char's local var killed_wsnm, and the next qm1
// talk gives the Annals of Truth; Oggbi's 290 completes it and teaches Asuran Fists. 287's option 1 gives the knuckles
// again, option 2 drops the quest. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheWallsOfYourMind : QuestScript
    {
        public TheWallsOfYourMind() : base(QuestLog.Bastok, "THE_WALLS_OF_YOUR_MIND") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Walls_of_Your_Mind";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok });

            // Lua: player:getCharSkillLevel(xi.skill.HAND_TO_HAND) / 10 >= 250,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2500)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.KnucklesOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.HandToHand) / 10 >= 250 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.PortBastok, Npc.PortBastok.Oggbi).OnTrigger(QuestReply.Event(286).OncePerZone())
                .OnEventFinish(Zone.PortBastok, 286, q =>
                {
                    if (q.Player.HasItem(Item.KnucklesOfTrials) || q.Player.GiveItem(Item.KnucklesOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Oggbi)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(290);
                        }
                        else
                        {
                            int hideReacquireMenuItem = q.Player.HasItem(Item.KnucklesOfTrials) ||
                                q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth) ? 1 : 0;

                            return QuestReply.Event(287, hideReacquireMenuItem);
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.KnucklesOfTrials, 1)))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(288);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(289);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.PortBastok, 287, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.KnucklesOfTrials))
                    {
                        q.Player.GiveItem(Item.KnucklesOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Bastok, "THE_WALLS_OF_YOUR_MIND");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 289, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.PortBastok, 290, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.AsuranFists);
                        q.Text.Special(Text.PortBastok.ASURAN_FISTS_LEARNED);
                    }
                })
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.BostaunieuxOubliette.BODACH, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.BostaunieuxOubliette.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Bodach).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
