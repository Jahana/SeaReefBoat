// The Doorman (Log ID 1, Quest ID 54, WAR AF1), a line-for-line port of scripts/quests/bastok/WAR_AF1_The_Doorman.lua
// (quest rework B3b). Phara : !pos 75 0 -80 234; Hide Flap (1) : !pos 293 3 -213 149; Naji : !pos 64 -14 -4 237. The hide
// flap, searched from within a yalm with Gavotvut and Barakbok both not alive, plays 110: its finish spawns both (claimed)
// and marks the search (nmClaimed); their deaths after it set nmKilled bits 1 and 0 (quest local vars), and with both the
// Sword Grip Material is found. Phara's 152 takes it (Prog 1, Timer the next Vana'diel day), her 153 the day after gives
// Yasin's Sword (Prog 2), Naji's 750 completes it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheDoorman : QuestScript
    {
        public TheDoorman() : base(QuestLog.Bastok, "THE_DOORMAN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/WAR_AF1_The_Doorman";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.RazorAxe });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.War &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.BastokMines, Npc.BastokMines.Phara).OnTrigger(QuestReply.ProgressEvent(151))
                .OnEventFinish(Zone.BastokMines, 151, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Davoi, Npc.Davoi.HideFlap1).OnTrigger(q =>
                {
                    q.Text.Special(Text.Davoi.FIND_ORC_TENT);

                    if (q.Npc.PlayerDistance > 1)
                    {
                        return QuestReply.MessageSpecial(Text.Davoi.CLOSER_TO_SEARCH);
                    }
                    else if (!q.Player.MobIsAlive(Mob.Davoi.GAVOTVUT) && !q.Player.MobIsAlive(Mob.Davoi.BARAKBOK))
                    {
                        return QuestReply.ProgressEvent(110);
                    }
                    else
                    {
                        return QuestReply.NoAction();
                    }
                })
                .OnEventFinish(Zone.Davoi, 110, q =>
                {
                    if (q.Option != 0)
                    {
                        return;
                    }

                    if (q.Player.HasKeyItem(KeyItem.SwordGripMaterial))
                    {
                        q.Text.Special(Text.Davoi.YOU_FIND_NOTHING);
                        return;
                    }

                    if (q.Quest.LocalVar("nmKilled") == 3)
                    {
                        q.Player.GiveKeyItem(KeyItem.SwordGripMaterial);
                        return;
                    }

                    if (!q.Player.MobIsSpawned(Mob.Davoi.GAVOTVUT) && !q.Player.MobIsSpawned(Mob.Davoi.BARAKBOK))
                    {
                        q.Text.Special(Text.Davoi.YOU_FIND_NOTHING);
                        q.Player.SpawnMob(Mob.Davoi.GAVOTVUT, 0, claim: true);
                        q.Player.SpawnMob(Mob.Davoi.BARAKBOK, 0, claim: true);
                        q.Quest.SetLocalVar("nmClaimed", 1);
                        return;
                    }
                })
                .Npc(Zone.Davoi, Npc.Davoi.Barakbok).OnMobDeath(q =>
                {
                    if (q.Quest.LocalVar("nmClaimed") == 1)
                    {
                        int nmStatus = q.Quest.LocalVar("nmKilled");

                        q.Quest.SetLocalVar("nmKilled", nmStatus | 1 << 0);
                    }
                })
                .Npc(Zone.Davoi, Npc.Davoi.Gavotvut).OnMobDeath(q =>
                {
                    if (q.Quest.LocalVar("nmClaimed") == 1)
                    {
                        int nmStatus = q.Quest.LocalVar("nmKilled");

                        q.Quest.SetLocalVar("nmKilled", nmStatus | 1 << 1);
                    }
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Phara).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SwordGripMaterial))
                    {
                        return QuestReply.ProgressEvent(152);
                    }
                    else if (q.Quest.Var("Prog") == 1 && q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(153);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 152, q =>
                {
                    q.Player.DelKeyItem(KeyItem.SwordGripMaterial);

                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Quest.SetVar("Prog", 1);
                })
                .OnEventFinish(Zone.BastokMines, 153, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.YasinsSword);

                    q.Quest.SetVar("Prog", 2);
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.YasinsSword) ? QuestReply.ProgressEvent(750) : null)
                .OnEventFinish(Zone.Metalworks, 750, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.YasinsSword);
                    }
                });
        }
    }
}
