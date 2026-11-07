// A Moral Manifest (Log ID 4, Quest ID 108), ported from the quest branches of scripts/zones/Altar_Room/Zone.lua,
// mobs/Yagudo_Avatar.lua and npcs/Stone_Lid.lua and scripts/zones/Windurst_Woods/npcs/Ponono.lua (quest rework B3b prep).
// Stone Lid : !pos -316.4390 24.7654 12.1590 152; Ponono : !pos -38.243 -2.25 -120.954 241. The quest lives in four
// scripts: the Altar Room's zone-ins (46 / 47 / 51) and their event finishes, the Yagudo Avatar's death, the Stone Lid and
// Ponono (whose guild master stays in his script). Each calls it (xi.cs.questZoneIn / quest / questEvent). Its state is the
// char var moral (1-8); as the scripts have it, the headgear zone-ins and the Stone Lid's triggers look at moral (and the
// head) only, and Ponono's 705 at moral, the local var moralZone and moralWait, whatever the quest's status. The scripts'
// event finishes are keyed on the csid (and option) alone, so they are one any-status state with no check, the last.
// 51's finish is npcUtil.completeQuest step for step (the Gold Beastcoin, stopping when it cannot be given; moral 0;
// the raw player:completeQuest, which keeps the quest's own Quest[4][108] vars).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class AMoralManifest : QuestScript
    {
        public AMoralManifest() : base(QuestLog.OtherAreas, "A_MORAL_MANIFEST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // 1: the offer on entering the Altar Room at level 60, unless declined before this conquest tally
            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= 60 && q.Player.CharVar("moraldecline") == 0)
                .OnZoneIn(Zone.AltarRoom, q => QuestReply.Event(46));

            // 2: entering in the Yagudo Headgear with the cutting made: the Vault Quipus
            b.AnyState(when: q => q.Player.CharVar("moral") == 4 && q.Player.EquipId(Slot.Head) == Item.YagudoHeadgear)
                .OnZoneIn(Zone.AltarRoom, q => QuestReply.Event(47));

            // 3: entering in Tsoo Haja's Headgear: the end
            b.AnyState(when: q => q.Player.CharVar("moral") == 8 && q.Player.EquipId(Slot.Head) == Item.TsooHajasHeadgear)
                .OnZoneIn(Zone.AltarRoom, q => QuestReply.Event(51));

            // 4: Ponono, the cloth asked for
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("moral") == 1)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Ponono).Called().OnTrigger(QuestReply.Event(700));

            // 5: the Velvet and Rainbow Cloth and 10,000 gil shown (nothing is taken); the cutting is ready after a zone and
            // the next Vana'diel midnight
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("moral") == 2)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Ponono).Called().OnTrade(q =>
                {
                    if (q.Trade.HasWithGil(10000, Item.SquareOfVelvetCloth, Item.SquareOfRainbowCloth))
                    {
                        q.Player.SetCharVar("moral", 3);
                        q.Player.SetLocalVar("moralZone", 1);
                        q.Player.SetCharVar("moralWait", q.Player.VanaMidnight);
                        return QuestReply.Event(703);
                    }
                    return null;
                });

            // 6: Ponono once the cutting is made, and after the quest
            b.State(QuestStatus.Completed)
                .Or(QuestStatus.Accepted, when: q => q.Player.CharVar("moral") >= 4)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Ponono).Called().OnTrigger(QuestReply.Event(704));

            // 7: the Yagudo Headdress Cutting, after the zone and the wait
            b.AnyState(when: q => q.Player.CharVar("moral") == 3 && q.Player.LocalVar("moralZone") == 0 &&
                                  q.Player.CharVar("moralWait") <= q.Player.SystemTime)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Ponono).Called().OnTrigger(QuestReply.Event(705));

            // 8: the Stone Lid in the Yagudo Headgear: the Yagudo Avatar and its NMs
            b.AnyState(when: q => q.Player.CharVar("moral") == 5 && q.Player.EquipId(Slot.Head) == Item.YagudoHeadgear)
                .Npc(Zone.AltarRoom, Npc.AltarRoom.StoneLid).Called().OnTrigger(QuestReply.Event(48));

            // 9: the Stone Lid after the Avatar's death
            b.AnyState(when: q => q.Player.CharVar("moral") == 6)
                .Npc(Zone.AltarRoom, Npc.AltarRoom.StoneLid).Called().OnTrigger(QuestReply.Event(49));

            // 10: the Yagudo Headgear to the Stone Lid: Tsoo Haja's Headgear
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("moral") == 7)
                .Npc(Zone.AltarRoom, Npc.AltarRoom.StoneLid).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.YagudoHeadgear, 1))
                    {
                        q.Trade.Complete();
                        return QuestReply.Event(50);
                    }
                    return null;
                });

            // 11: the Yagudo Avatar defeated
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("moral") == 5)
                .Npc(Zone.AltarRoom, Npc.AltarRoom.YagudoAvatar).Called().OnMobDeath(q =>
                {
                    q.Player.SetCharVar("moral", 6);
                    q.Player.DelKeyItem(KeyItem.VaultQuipus);
                });

            // 12: the event finishes, keyed on the csid (and option) alone as the scripts' onEventFinish branches are
            b.AnyState()
                // the offer: accepted (0), or declined until the next conquest tally (1)
                .OnEventFinish(Zone.AltarRoom, 46, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetCharVar("moral", 1);
                        q.Quest.Begin();
                    }
                    else if (q.Option == 1)
                    {
                        q.Player.SetCharVar("moraldecline", 1, (uint)q.Player.NextConquestTally);
                    }
                })
                .OnEventFinish(Zone.AltarRoom, 47, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.VaultQuipus);
                    q.Player.SetCharVar("moral", 5);
                })
                .OnEventFinish(Zone.AltarRoom, 51, q =>
                {
                    q.Player.SetCharVar("moralrebuy", 1);
                    // npcUtil.completeQuest(player, OTHER_AREAS, A_MORAL_MANIFEST, { item = 748, var = 'moral' }), step
                    // for step: not quest:complete, whose cleanup would clear the quest's own Quest[4][108] vars
                    if (!q.Player.GiveItem(Item.GoldBeastcoin))
                    {
                        return; // no free slot: npcUtil.completeQuest returns false
                    }

                    q.Player.SetCharVar("moral", 0);
                    q.Player.CompleteQuest(QuestLog.OtherAreas, "A_MORAL_MANIFEST");
                })
                .OnEventFinish(Zone.WindurstWoods, 700, q => q.Player.SetCharVar("moral", 2))
                .OnEventFinish(Zone.WindurstWoods, 705, q =>
                {
                    if (q.Player.GiveItem(Item.YagudoHeaddressCutting))
                    {
                        q.Player.SetCharVar("moral", 4);
                    }
                })
                .OnEventFinish(Zone.AltarRoom, 48, q => q.Player.PopFromQm(new[]
                {
                    Mob.AltarRoom.YAGUDO_AVATAR,
                    Mob.AltarRoom.DUU_MASA_THE_ONECUT,
                    Mob.AltarRoom.FEE_JUGE_THE_RAMFIST,
                    Mob.AltarRoom.GOO_PAKE_THE_BLOODHOUND,
                    Mob.AltarRoom.KEE_TAW_THE_NIGHTINGALE,
                    Mob.AltarRoom.LAA_YAKU_THE_AUSTERE,
                    Mob.AltarRoom.POO_YOZO_THE_BABBLER,
                }, hide: 1))
                .OnEventFinish(Zone.AltarRoom, 49, q => q.Player.SetCharVar("moral", 7))
                .OnEventFinish(Zone.AltarRoom, 50, q =>
                {
                    if (q.Player.GiveItem(Item.TsooHajasHeadgear))
                    {
                        q.Player.SetCharVar("moral", 8);
                    }
                });
        }
    }
}
