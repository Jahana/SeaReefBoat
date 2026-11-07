// Elderly Pursuits (Log ID 4, Quest ID 75), a line-for-line port of scripts/quests/otherAreas/Elderly_Pursuits.lua (quest
// rework B3b; its Carpenters' Landing onZoneOut on P7's OnZoneOut). Tavnazian Safehold:
// Despachiaire !pos 111.2090 -40.0148 -85.4810 (no script, default 106); Southern San d'Oria: Rouva !pos -16.3326 2.1
// 10.3829 (a script with an empty onTrigger; default 662); Carpenters' Landing: qm_para !pos -414.915 0 -362.9870
// (default NOTHING_OUT_OF_ORDINARY) and the NM Para.
// Available once Secrets of Ovens Lost is completed: Despachiaire 517, whose finish begins the quest and gives the Antique
// Amulet (npcUtil.giveKeyItem). Accepted at Prog <= 2: Rouva with the amulet: 747 (Prog 1); the ??? at Prog 1: Para popped
// (claimed, the ??? not hidden) and STENCH_OF_DECAY, when the pop succeeds; at Prog 2 with the amulet: Prog 3, the amulet
// taken, the Cathedral Medallion added (player:addKeyItem), POLISH_MUSHROOM_SPORE, and the reply KEYITEM_OBTAINED; Para's
// death (the first Para, ID.mob.PARA) at Prog 1: Prog 2; leaving Carpenters' Landing at Prog 2: Prog 1 (the fight again).
// Accepted at Prog >= 3: Rouva with the medallion: 748 (Prog 4); Despachiaire at Prog 4: 518, whose finish completes the
// quest (the Elegant Ribbon) and takes the medallion.
// Kept as the Lua has it: 518's finish takes the medallion even when quest:complete fails (no room for the ribbon); a
// ??? at Prog 1 with Para already up answers nil (the pop fails); only the first Para's death counts.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ElderlyPursuits : QuestScript
    {
        public ElderlyPursuits() : base(QuestLog.OtherAreas, "ELDERLY_PURSUITS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Elderly_Pursuits";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ElegantRibbon,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.OtherAreas, "SECRETS_OF_OVENS_LOST"))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(QuestReply.ProgressEvent(517))
                .OnEventFinish(Zone.TavnazianSafehold, 517, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.AntiqueAmulet);
                });

            var accepted = b.State(QuestStatus.Accepted, when: q => q.Quest.Step <= 2);
            accepted
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rouva).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.AntiqueAmulet))
                    {
                        return QuestReply.ProgressEvent(747);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 747, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.qmpara).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1 &&
                        q.Player.PopFromQm(Mob.CarpentersLanding.PARA, hide: 0, claim: true))
                    {
                        return QuestReply.MessageSpecial(Text.CarpentersLanding.STENCH_OF_DECAY);
                    }
                    else if (q.Quest.Step == 2 &&
                             q.Player.HasKeyItem(KeyItem.AntiqueAmulet))
                    {
                        q.Quest.SetStep(3);
                        q.Player.DelKeyItem(KeyItem.AntiqueAmulet);
                        q.Player.AddKeyItem(KeyItem.CathedralMedallion);
                        q.Text.Special(Text.CarpentersLanding.POLISH_MUSHROOM_SPORE, KeyItem.AntiqueAmulet);
                        return QuestReply.MessageSpecial(Text.CarpentersLanding.KEYITEM_OBTAINED, KeyItem.CathedralMedallion);
                    }
                    return null;
                })
                //The player only needs to kill the original Para. There are player reports of them killing the original getting the KI and chaining the clones to zone.
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.Para).OnMobDeath(q =>
                {
                    if (q.Mob.Id == q.Player.MobIdOf(Mob.CarpentersLanding.PARA) &&
                        q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                });

            // Resets the progress if the player killed Para but didn't check the ??? before zoning. Will result in the player needing to refight.
            // OnZoneOut(zone, Action<IQuestContext>) (P7) = the section's [xi.zone.CARPENTERS_LANDING].onZoneOut = function(player)
            // (interaction_lookup's zone-wide onZoneOut key, run by luautils::OnZoneOut as the char leaves the zone)
            accepted.OnZoneOut(Zone.CarpentersLanding, q =>
            {
                if (q.Quest.Step == 2)
                {
                    q.Quest.SetStep(1);
                }
            });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step >= 3)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rouva).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CathedralMedallion))
                    {
                        return QuestReply.ProgressEvent(748);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 748, q =>
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(518);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 518, q =>
                {
                    q.Quest.Complete();
                    q.Player.DelKeyItem(KeyItem.CathedralMedallion);
                });
        }
    }
}
