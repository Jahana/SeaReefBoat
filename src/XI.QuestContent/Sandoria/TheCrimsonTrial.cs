// The Crimson Trial (Log ID 0, Quest ID 84, RDM AF1), a line-for-line port of scripts/quests/sandoria/RDM_AF1_The_Crimson_Trial.lua
// (quest rework B3b, San d'Oria group 4). Sharzalion : !pos 95 0 111 230; Valderotaux : !pos 97 0.1 113 230. Offered to an
// RDM of AF1_QUEST_LEVEL: Sharzalion's 70 (option 1 begins it, else Prog 1: declined once), then 71 (option 1 begins it).
// Valderotaux's 54 (the playtime) at Prog 1 while available, and always while accepted; its update with option 1 sends
// updateEvent(54, 0). Accepted: entering Davoi without the Orcish Dried Food spawns Purpleflash Brukdok when not up (no
// claim, no enmity); Davoi's Storage Hole says AN_ORCISH_STORAGE_HOLE, and exactly a Davoi Storage Key (food not held) is
// taken (tradeComplete) for the Orcish Dried Food, the handler returning nil. Sharzalion's 74 without the food, 75 with
// it: completed (Fencing Degen, fame +20), the food deleted. Enveloped in Darkness reads this quest's status; RoE records
// and Vingijard read it (stay).
// Kept as the Lua has it: Prog 1 means "declined once" and stays 1 after the quest begins, until completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheCrimsonTrial : QuestScript
    {
        public TheCrimsonTrial() : base(QuestLog.Sandoria, "THE_CRIMSON_TRIAL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/RDM_AF1_The_Crimson_Trial";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.FencingDegen, Fame = 20, FameArea = FameArea.Sandoria });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Rdm &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sharzalion).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        // first starting event
                        return QuestReply.ProgressEvent(70);
                    }
                    else
                    {
                        // next starting event if originally declined
                        return QuestReply.ProgressEvent(71);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Valderotaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        // player:getPlaytime()
                        return QuestReply.Event(54, q.Player.Playtime);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.SouthernSanDoria, 54, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(54, 0);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 70, q =>
                {
                    if (q.Option == 1)
                    {
                        // accept quest
                        q.Quest.Begin();
                    }
                    else
                    {
                        // this variable only means you rejected the quest when first offered
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 71, q =>
                {
                    if (q.Option == 1)
                    {
                        // accept quest
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Davoi, Npc.Davoi.StorageHole)
                    .OnTrigger(q => QuestReply.MessageSpecial(Text.Davoi.AN_ORCISH_STORAGE_HOLE))
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.DAVOI_STORAGE_KEY)
                        if (q.Trade.HasExactly(Item.DavoiStorageKey) &&
                            !q.Player.HasKeyItem(KeyItem.OrcishDriedFood))
                        {
                            q.Trade.Complete();
                            q.Player.GiveKeyItem(KeyItem.OrcishDriedFood);
                        }
                        return null;
                    })
                .OnZoneIn(Zone.Davoi, q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OrcishDriedFood) &&
                        !q.Player.MobIsSpawned(Mob.Davoi.PURPLEFLASH_BRUKDOK))
                    {
                        // spawned by The Crimson Trial upon entering the zone: SpawnMob(davoiID.mob.PURPLEFLASH_BRUKDOK)
                        q.Player.SpawnMob(Mob.Davoi.PURPLEFLASH_BRUKDOK);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sharzalion).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.OrcishDriedFood))
                    {
                        // finish quest
                        return QuestReply.ProgressEvent(75);
                    }
                    else
                    {
                        // reminder
                        return QuestReply.Event(74);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Valderotaux).OnTrigger(q => QuestReply.Event(54, q.Player.Playtime))
                .OnEventUpdate(Zone.SouthernSanDoria, 54, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(54, 0);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 75, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OrcishDriedFood);
                    }
                });
        }
    }
}
