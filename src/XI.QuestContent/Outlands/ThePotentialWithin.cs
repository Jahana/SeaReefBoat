// The Potential Within (Log ID 5, Quest ID 146), a line-for-line port of scripts/quests/outlands/The_Potential_Within.lua
// (quest rework B3b Outlands, group 1). Norg: Jaucribaix !pos 91 -7 -8 (no script; default 71). Kuftal Tunnel: qm3 !pos 200 11
// 99 (no script; default NOTHING_OUT_OF_ORDINARY), the NM Kettenkaefer.
// Offered (178, once per zone) to a char who can wear the Tachi of Trials with great katana skill 250 and no Weapon
// Training Guide; 178's option 1 gives the tachi (unless held) and the guide and begins. Accepted: Jaucribaix 183 with the
// Annals of Truth, 182 with the Map to the Annals of Truth, else 179 (1 while the tachi is held). A trade of the tachi alone
// plays 180 under 300 weapon skill points and 181 from 300 (the trade confirmed, the map); at Kuftal Tunnel's qm3 the map
// pops Kettenkaefer (popFromQM, claimed, the qm not hidden), his death with the map sets the char's local var killed_wsnm,
// and the next qm3 talk clears it and gives the Annals of Truth; 183 runs quest:complete (Norg fame 20) and on true takes the
// three key items, teaches Tachi: Kasha and says TACHI_KASHA_LEARNED. 179's option 2 drops the quest (the guide and the map
// taken); any other option gives the tachi again when it is not held.
// Kept as the Lua has it: 178's option check comes first (option 0: nothing, the offer gone until a zone change); 181
// takes the tachi by confirmTrade (tradeHasExactly confirmed it); qm3 reads the local var before the map; 179's
// reacquire arm keys on the tachi alone (with the map held, 182 comes first and 179 is not offered).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class ThePotentialWithin : QuestScript
    {
        public ThePotentialWithin() : base(QuestLog.Outlands, "THE_POTENTIAL_WITHIN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/The_Potential_Within";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Norg });

            // Lua: player:getCharSkillLevel(xi.skill.GREAT_KATANA) / 10 >= 250, a float division (kept as one)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.TachiOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.GreatKatana) / 10.0 >= 250 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(QuestReply.Event(178).OncePerZone()) // start
                .OnEventFinish(Zone.Norg, 178, q =>
                {
                    if (q.Option == 1 &&
                        (q.Player.HasItem(Item.TachiOfTrials) || q.Player.GiveItem(Item.TachiOfTrials)))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(183); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(182); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(179, q.Player.HasItem(Item.TachiOfTrials) ? 1 : 0); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.TachiOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(180); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(181); // finished weapon
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 179, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Outlands, "THE_POTENTIAL_WITHIN");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                    else if (!q.Player.HasItem(Item.TachiOfTrials))
                    {
                        q.Player.GiveItem(Item.TachiOfTrials);
                    }
                })
                .OnEventFinish(Zone.Norg, 181, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.Norg, 183, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.TachiKasha);
                        q.Text.Special(Text.Norg.TACHI_KASHA_LEARNED);
                    }
                })
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.qm3).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.KuftalTunnel.KETTENKAEFER, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.KuftalTunnel.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.Kettenkaefer).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
