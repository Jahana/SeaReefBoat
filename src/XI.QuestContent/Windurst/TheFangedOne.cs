// The Fanged One (Log ID 2, Quest ID 31), a line-for-line port of scripts/quests/windurst/The_Fanged_One.lua (quest
// rework B3b). Perih Vashai: !pos 117 -3 92 241; Tiger Bones: !pos 666 -8 -379 120. The RNG unlock: at
// ADVANCED_JOB_LEVEL, Perih Vashai's 351 begins it. Accepted: Perih Vashai's 352 at Prog 0; Kapeh Myohrye's 354, Muhk
// Johldy's 353; a Black Tiger Fang traded alone to Perih Vashai plays 356, whose finish takes it. Tiger Bones
// (Sauromugue Champaign): at Prog 0, with Wait (the quest var Old Sabertooth's death sets to now + 300 when he took
// damage) not ahead and Old Sabertooth not up, he spawns (OLD_SABERTOOTH_DIALOG_I); while Timer (set to now + 180 by his
// natural death, the poison) is ahead and the Old Tiger's Fang is not held, the fang and Prog 1 (no action). With the
// fang, Perih Vashai's 357 completes it (fame 20, the Ranger's Necklace, the title The Fanged One; the fang taken, RNG
// unlocked, the Job Gesture: Ranger, PERIH_VASHAI_DIALOG, the quest's mustZone). Completed: Perih Vashai's 358 while the
// mustZone holds. Old Sabertooth's own script (mobs/Old_Sabertooth.lua) keeps his death: it writes Timer / Wait by name.
// Kept as the Lua file has it: the Black Tiger Fang trade gives nothing but takes the fang; at Prog 0 Perih Vashai
// plays 352 even with the fang held.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheFangedOne : QuestScript
    {
        public TheFangedOne() : base(QuestLog.Windurst, "THE_FANGED_ONE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/The_Fanged_One";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Item = Item.RangersNecklace,
                Title = Title.TheFangedOne,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).OnTrigger(QuestReply.ProgressEvent(351))
                .OnEventFinish(Zone.WindurstWoods, 351, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.Event(352);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.OldTigersFang))
                        {
                            return QuestReply.ProgressEvent(357);
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BlackTigerFang))
                        {
                            return QuestReply.Event(356);
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KapehMyohrye).OnTrigger(q => QuestReply.Event(354))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MuhkJohldy).OnTrigger(q => QuestReply.Event(353))
                .OnEventFinish(Zone.WindurstWoods, 356, q =>
                {
                    q.Trade.Complete(); // capture shows taking black tiger fang if traded.
                })
                .OnEventFinish(Zone.WindurstWoods, 357, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OldTigersFang);
                        q.Player.UnlockJob(Job.Rng);
                        q.Player.GiveKeyItem(KeyItem.JobGestureRanger);
                        q.Text.Special(Text.WindurstWoods.PERIH_VASHAI_DIALOG);
                        q.Quest.SetMustZone();
                    }
                })
                // The logic for this specific mechanic is unique. Therefore, the mob logic and onMobDeath
                // resides in the Old_Sabertooth.lua onMobSpawn, onMobFight, and onMobDeath.
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.TigerBones).OnTrigger(q =>
                {
                    // Lua: local oldTiger = GetMobByID(sauromugueID.mob.OLD_SABERTOOTH); if not oldTiger then return
                    // quest:noAction() end. Old Sabertooth is a placed mob of the zone (GetFirstID): GetMobByID never
                    // returns nil, so that arm is not reachable and not ported
                    if (
                        q.Quest.Var("Timer") > q.Player.SystemTime &&
                        !q.Player.HasKeyItem(KeyItem.OldTigersFang))
                    {
                        q.Player.GiveKeyItem(KeyItem.OldTigersFang);
                        q.Quest.SetStep(1);
                        return QuestReply.NoAction();
                    }
                    else if (
                        q.Quest.Step == 0 &&
                        q.Quest.Var("Wait") <= q.Player.SystemTime &&
                        !q.Player.MobIsSpawned(Mob.SauromugueChampaign.OLD_SABERTOOTH))
                    {
                        // SpawnMob(sauromugueID.mob.OLD_SABERTOOTH): no claim, no enmity
                        q.Player.SpawnMob(Mob.SauromugueChampaign.OLD_SABERTOOTH);
                        return QuestReply.MessageSpecial(Text.SauromugueChampaign.OLD_SABERTOOTH_DIALOG_I);
                    }

                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(358);
                    }

                    return null;
                });
        }
    }
}
