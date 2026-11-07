// Bugi Soden (Log ID 5, Quest ID 147), a line-for-line port of scripts/quests/outlands/Bugi_Soden.lua (quest rework B3b
// Outlands, group 3). Norg: Ryoma !pos -26 0.5 -8 (his script: Twenty in Pirate Years / True Will; default 94). Labyrinth of
// Onzozo: qm1 !pos 110.5 15 161.9 (no script; default NOTHING_OUT_OF_ORDINARY), the NM Megapod Megalops.
// Offered (184, once per zone) to a char who can wear the Kodachi of Trials (its level checked) with katana skill 250 and
// no Weapon Training Guide; 184's option 1 gives the kodachi (unless held) and the guide and begins. Accepted: Ryoma 189
// with the Annals of Truth, 188 with the Map to the Annals of Truth, else 185. A trade of the kodachi alone plays 186 under
// 300 weapon skill points and 187 from 300 (the trade confirmed, the map); at Onzozo's qm1 the map pops Megapod Megalops
// (popFromQM, claimed, the qm not hidden), his death with the map sets the char's local var killed_wsnm, and the next qm1
// talk clears it and gives the Annals of Truth; 189 runs quest:complete (Norg fame 20) and on true takes the three key
// items, teaches Blade: Ku and says BLADE_KU_LEARNED. 185's option 2 drops the quest (the guide and the map taken); any
// other option gives the kodachi again when it is not held.
// Kept as the Lua has it: 184's option check comes first (option 0: nothing, the offer gone until a zone change); 187
// takes the kodachi by confirmTrade (tradeHasExactly confirmed it); qm1 reads the local var before the map; 185's
// reacquire arm keys on the kodachi alone. Ryoma is shared with I'll Take the Big Box (135) and Ayame and Kaede (95):
// both Progress, so 135 and 95 beat 184 (#kinds).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class BugiSoden : QuestScript
    {
        public BugiSoden() : base(QuestLog.Outlands, "BUGI_SODEN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Bugi_Soden";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Norg });

            // Lua: player:getCharSkillLevel(xi.skill.KATANA) / 10 >= 250, a float division (kept as one)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CanEquipItem(Item.KodachiOfTrials, true) &&
                    q.Player.CharSkillLevel(Skill.Katana) / 10.0 >= 250 &&
                    !q.Player.HasKeyItem(KeyItem.WeaponTrainingGuide))
                .Npc(Zone.Norg, Npc.Norg.Ryoma).OnTrigger(q =>
                {
                    return QuestReply.Event(184).OncePerZone(); // start
                })
                .OnEventFinish(Zone.Norg, 184, q =>
                {
                    if (q.Option == 1 &&
                        (q.Player.HasItem(Item.KodachiOfTrials) || q.Player.GiveItem(Item.KodachiOfTrials)))
                    {
                        q.Player.GiveKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Ryoma)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AnnalsOfTruth))
                        {
                            return QuestReply.ProgressEvent(189); // complete
                        }
                        else if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                        {
                            return QuestReply.Event(188); // cont 2
                        }
                        else
                        {
                            return QuestReply.Event(185); // cont 1
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.KodachiOfTrials))
                        {
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints < 300)
                            {
                                return QuestReply.Event(186); // unfinished weapon
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(187); // finished weapon
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 185, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.DelQuest(QuestLog.Outlands, "BUGI_SODEN");
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                    }
                    else if (!q.Player.HasItem(Item.KodachiOfTrials))
                    {
                        q.Player.GiveItem(Item.KodachiOfTrials);
                    }
                })
                .OnEventFinish(Zone.Norg, 187, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                })
                .OnEventFinish(Zone.Norg, 189, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.MapToTheAnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.AnnalsOfTruth);
                        q.Player.DelKeyItem(KeyItem.WeaponTrainingGuide);
                        q.Player.AddLearnedWeaponskill(WsUnlock.BladeKu);
                        q.Text.Special(Text.Norg.BLADE_KU_LEARNED);
                    }
                })
                .Npc(Zone.LabyrinthOfOnzozo, Npc.LabyrinthOfOnzozo.qm1).OnTrigger(q =>
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
                        q.Player.PopFromQm(Mob.LabyrinthOfOnzozo.MEGAPOD_MEGALOPS, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.LabyrinthOfOnzozo.SENSE_OMINOUS_PRESENCE);
                    }
                    return null;
                })
                .Npc(Zone.LabyrinthOfOnzozo, Npc.LabyrinthOfOnzozo.MegapodMegalops).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MapToTheAnnalsOfTruth))
                    {
                        q.Player.SetLocalVar("killed_wsnm", 1);
                    }
                });
        }
    }
}
