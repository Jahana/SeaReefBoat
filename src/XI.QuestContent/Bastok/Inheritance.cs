// Inheritance (Log ID 1, Quest ID 68), a line-for-line port of scripts/quests/bastok/Inheritance.lua (quest rework
// B3b). Gumbah : !pos 52 0 -36 234; qm1 : !pos -660 0 -338 125. Offered (190, once per zone) to a char who can wear
// the Sword of Trials with great sword skill 250 and no Weapon Training Guide; 190's finish gives the sword (unless
// held) and the guide and begins. A trade of the sword plays 192 under 300 weapon skill points and 193 from 300 (the
// Map to the Annals of Truth); at Western Altepa Desert's qm1 the map pops Maharaja, his death sets the char's local
// var killed_wsnm, and the next qm1 talk gives the Annals of Truth; Gumbah's 194 teaches Ground Strike and completes
// it. 191's option 1 gives the sword again, option 2 drops the quest. Kept as the Lua file has it: 194 deletes the key
// items, teaches the weapon skill and shows its message before quest:complete, and does not test what complete
// returns (the other three weapon-skill quests do both inside `if quest:complete(player)`); with no item reward the
// complete cannot fail, so only the packet order differs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Inheritance : QuestScript
    {
        public Inheritance() : base(QuestLog.Bastok, "INHERITANCE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Inheritance";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok });

            // Lua: player:getCharSkillLevel(xi.skill.GREAT_SWORD) / 10 >= 250,
            // a float division; C# divides as ints, with the same result for every stored value (both false under 2500)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.SwordOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.GreatSword) / 10 >= 250 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah).OnTrigger(QuestReply.Event(190).OncePerZone())
                .OnEventFinish(Zone.BastokMines, 190, q =>
                {
                    if (q.Player.HasItem(Item.SwordOfTrials) || q.Player.GiveItem(Item.SwordOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Gumbah)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(194);
                        }
                        else
                        {
                            int hideReacquireMenuItem = q.Player.HasItem(Item.SwordOfTrials) ||
                                q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth) ? 1 : 0;

                            return QuestReply.Event(191, hideReacquireMenuItem);
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.SwordOfTrials, 1)))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(192);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(193);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.BastokMines, 191, q =>
                {
                    if (q.Option == 1 && !q.Player.HasItem(Item.SwordOfTrials))
                    {
                        q.Player.GiveItem(Item.SwordOfTrials);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Bastok, "INHERITANCE");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.BastokMines, 193, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.BastokMines, 194, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                    q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                    q.Player.AddLearnedWeaponskill(WsUnlock.GroundStrike);
                    q.Text.Special(Text.BastokMines.GROUND_STRIKE_LEARNED);
                    q.Quest.Complete();
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.WesternAltepaDesert.MAHARAJA, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.WesternAltepaDesert.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.WesternAltepaDesert, Npc.WesternAltepaDesert.Maharaja).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
