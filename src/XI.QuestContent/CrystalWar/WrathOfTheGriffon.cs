// Wrath of the Griffon (Log ID 7, Quest ID 25), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_4_Wrath_of_the_Griffon.lua (quest rework B3b Crystal War group 6). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32), Wyatt !pos 124 0 84 (default WYATT_DIALOG; Seeing Spots answers
// him too). Jugner Forest [S]: qm8 !pos -6 0 -295 (default 207), Cobraclaw Buchzvotch (COBRACLAW_BUCHZVOTCH).
// Available, Boy and the Beast completed: Rholont 59, whose finish begins it. Accepted: Rholont: Prog below 3: 61 (an
// Event); else 60, whose finish completes it (the Military Scrip) and sets Perils of the Griffon's Timer to
// VanadielUniqueDay() + 1 and its mustZone. qm8: Prog 0: 204 (-> 1); Prog 1 with the Cobraclaw not spawned: 205,
// whose finish spawns it claimed by the char; Prog 2: 206 (-> 3). The Cobraclaw's death at Prog 1: Prog 2. Completed
// with the Military Scrip: Wyatt 66 (setPriority 1005), whose finish takes the scrip and gives 20147 gil.
// Kept as the Lua has it: 59 begins on any option; qm8 answers nil at Prog 1 with the NM up and at 3+; 60 writes
// Perils' Timer and mustZone only when the completion answers true (the Lua checks it; a key item reward cannot fail to fit).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class WrathOfTheGriffon : QuestScript
    {
        public WrathOfTheGriffon() : base(QuestLog.CrystalWar, "WRATH_OF_THE_GRIFFON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_4_Wrath_of_the_Griffon";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.MilitaryScrip,
            });

            b.State(QuestStatus.Available,
                    when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "BOY_AND_THE_BEAST"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.ProgressEvent(59))
                .OnEventFinish(Zone.SouthernSanDoriaS, 59, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (q.Quest.Step < 3)
                    {
                        return QuestReply.Event(61);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(60);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 60, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(
                            QuestLog.CrystalWar, "PERILS_OF_THE_GRIFFON", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.CrystalWar, "PERILS_OF_THE_GRIFFON");
                    }
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.qm8).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(204);
                    }
                    else if (questProgress == 1 &&
                        !q.Player.MobIsSpawned(Mob.JugnerForestS.COBRACLAW_BUCHZVOTCH))
                    {
                        return QuestReply.ProgressEvent(205);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(206);
                    }
                    return null;
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.CobraclawBuchzvotch).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.JugnerForestS, 204, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.JugnerForestS, 205, q =>
                {
                    q.Player.SpawnMob(Mob.JugnerForestS.COBRACLAW_BUCHZVOTCH, 0, true);
                })
                .OnEventFinish(Zone.JugnerForestS, 206, q =>
                {
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Completed, when: q => q.Player.HasKeyItem(KeyItem.MilitaryScrip))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Wyatt)
                .OnTrigger(QuestReply.ProgressEvent(66).SetPriority(1005))
                .OnEventFinish(Zone.SouthernSanDoriaS, 66, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MilitaryScrip);
                    q.Player.GiveCurrency("gil", 20147);
                });
        }
    }
}
