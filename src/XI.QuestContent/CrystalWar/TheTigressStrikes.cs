// The Tigress Strikes (Log ID 7, Quest ID 18), a line-for-line port of
// scripts/quests/crystalWar/WOTG_WIN_2_The_Tigress_Strikes.lua (quest rework B3b Crystal War group 2). Windurst Waters
// [S]: Dhea Prandoleh !pos 1 -1 15. Fort Karugo-Narugo [S]: Rotih Moalghett !pos -64 -75 4, qm4 !pos 208 -30 54, the
// War Lynx TIGRESS_STRIKES_WAR_LYNX.
// Available, The Tigress Stirs completed: Dhea Prandoleh 133, whose finish begins it. Accepted: Rotih Moalghett: Prog 0:
// 101 (-> Prog 1), else 104 (an Event). qm4: Prog 1: 102 (-> Prog 2); LynxKilled 1: 103 (-> Prog 3); Prog 2 with the
// lynx not spawned: it spawns, claimed by the char, and the quest answers noAction. The War Lynx's death (that id):
// LynxKilled 1. Dhea Prandoleh: Prog 3: 134, whose finish completes it (a Star Globe, Ajido-Marujido's Minder) and sets
// needToZone; else 135 (an Event).
// Kept as the Lua has it: 133 begins on any option; LynxKilled is checked before Prog 2 and stays 1 at Prog 3 (qm4 then
// answers 103 again, whose finish sets Prog 3 again); qm4 answers nil at Prog 0 / 3+ without LynxKilled and while the
// lynx is up (its default message plays); every War_Lynx-scripted mob's death in the zone runs the handler (the id test
// picks the quest's); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheTigressStrikes : QuestScript
    {
        public TheTigressStrikes() : base(QuestLog.CrystalWar, "THE_TIGRESS_STRIKES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_WIN_2_The_Tigress_Strikes";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.StarGlobe,
                Title = Title.AjidoMarujidosMinder,
            });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "THE_TIGRESS_STIRS"))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DheaPrandoleh).OnTrigger(QuestReply.ProgressEvent(133))
                .OnEventFinish(Zone.WindurstWatersS, 133, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.FortKarugoNarugoS, Npc.FortKarugoNarugoS.RotihMoalghett).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(101);
                    }
                    else
                    {
                        return QuestReply.Event(104);
                    }
                })
                .Npc(Zone.FortKarugoNarugoS, Npc.FortKarugoNarugoS.qm4).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    if (prog == 1)
                    {
                        return QuestReply.ProgressEvent(102);
                    }
                    else if (q.Quest.Var("LynxKilled") == 1)
                    {
                        return QuestReply.ProgressEvent(103);
                    }
                    else if (
                        prog == 2 &&
                        !q.Player.MobIsSpawned(Mob.FortKarugoNarugoS.TIGRESS_STRIKES_WAR_LYNX))
                    {
                        q.Player.SpawnMob(Mob.FortKarugoNarugoS.TIGRESS_STRIKES_WAR_LYNX, 0, claim: true);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.FortKarugoNarugoS, Npc.FortKarugoNarugoS.WarLynx).OnMobDeath(q =>
                {
                    if (q.Mob.Id == q.Player.MobIdOf(Mob.FortKarugoNarugoS.TIGRESS_STRIKES_WAR_LYNX))
                    {
                        q.Quest.SetVar("LynxKilled", 1);
                    }
                })
                .OnEventFinish(Zone.FortKarugoNarugoS, 101, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.FortKarugoNarugoS, 102, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.FortKarugoNarugoS, 103, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DheaPrandoleh).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(134);
                    }
                    else
                    {
                        return QuestReply.Event(135);
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 134, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetNeedToZone(true);
                    }
                });
        }
    }
}
