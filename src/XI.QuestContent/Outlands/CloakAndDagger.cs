// Cloak and Dagger (Log ID 5, Quest ID 13), a line-for-line port of scripts/quests/outlands/Cloak_and_Dagger.lua (quest
// rework B3b Outlands, group 2). Kazham: Jakoh Wahcondalo !pos 101 -16 -115 (his script: Tuning Out only; default 113).
// Gustav Tunnel: qm1 !pos 52.8 -1 19.9 (no script; default NOTHING_OUT_OF_ORDINARY), the NM Baronial Bat.
// Offered (279, once per zone) to a char who can wear the Dagger of Trials (its level checked) with dagger skill 230 and
// no Weapon Training Guide; 279's finish gives the dagger (unless held) and the guide and begins (any option). Accepted:
// Jakoh 284 with the Annals of Truth, 283 with the Map to the Annals of Truth, else 280 (0, dagger, 0, 0, 2 while the
// dagger is held). A trade of the dagger alone plays 281 under 300 weapon skill points and 282 from 300 (the trade
// confirmed, the map); at Gustav Tunnel's qm1 the map pops the Baronial Bat (popFromQM, claimed, the qm not hidden), his
// death with the map sets the char's local var killed_wsnm, and the next qm1 talk clears it and gives the Annals of Truth;
// 284 runs quest:complete (Windurst fame 20) and on true takes the three key items, teaches Evisceration and says
// EVISCERATION_LEARNED. 280's option 3 drops the quest (the guide and the map taken); option 1 gives the dagger again when
// it is not held.
// Kept as the Lua has it: 279's finish reads no option (option 0 begins too); 280's option 1 reacquire arm keys on the
// dagger alone; 282 takes the dagger by confirmTrade (tradeHasExactly confirmed it); qm1 reads the local var before the
// map. The Zilart missions at Jakoh are rivals of these replies (#mission): RoTZ 3's 114 (Progress) ties with 284 and
// plays first; RoTZ 4's 115 (an Event) ties with 279 / 280 / 283 and they take turns by the last event id, 115 first,
// so the mission's note ("blocked by Evisceration quest") holds only for 284, which beats 115.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class CloakAndDagger : QuestScript
    {
        public CloakAndDagger() : base(QuestLog.Outlands, "CLOAK_AND_DAGGER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Cloak_and_Dagger";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst });

            // Lua: player:getCharSkillLevel(xi.skill.DAGGER) / 10 >= 230, a float division (kept as one)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.DaggerOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Dagger) / 10.0 >= 230 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(q =>
                {
                    return QuestReply.Event(279).OncePerZone(); // start
                })
                .OnEventFinish(Zone.Kazham, 279, q =>
                {
                    if (q.Player.HasItem(Item.DaggerOfTrials) || q.Player.GiveItem(Item.DaggerOfTrials))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(284); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(283); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(280, 0, Item.DaggerOfTrials, 0, 0, q.Player.HasItem(Item.DaggerOfTrials) ? 2 : 0); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.DaggerOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(281); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(282, 0, KeyItem.AnnalsOfTruth); // finished weapon
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Kazham, 280, q =>
                {
                    if (q.Option == 1 &&
                        !q.Player.HasItem(Item.DaggerOfTrials))
                    {
                        q.Player.GiveItem(Item.DaggerOfTrials);
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.DelQuest(QuestLog.Outlands, "CLOAK_AND_DAGGER");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                })
                .OnEventFinish(Zone.Kazham, 282, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.Kazham, 284, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.Evisceration);
                        q.Text.Special(Text.Kazham.EVISCERATION_LEARNED);
                    }
                })
                .Npc(Zone.GustavTunnel, Npc.GustavTunnel.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.GustavTunnel.BARONIAL_BAT, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.GustavTunnel.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.GustavTunnel, Npc.GustavTunnel.BaronialBat).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
