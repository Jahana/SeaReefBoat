// Chasing Shadows (Log ID 7, Quest ID 60), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_11_Chasing_Shadows.lua (quest rework B3b Crystal War, draft). Southern San d'Oria
// [S]: Rholont !pos -168 -2 56 (default 32), Rongelouts N Distaud !pos 0.067 2 -22 (default 604). Xarcabard [S]:
// Backfilled Pit !pos -172 -2.818 -85, Compact Footprint !pos 242.145 -15.42 -147.417, Sunken Footprint !pos 188.429
// -27.623 163.764 (defaults NOTHING_OUT_OF_ORDINARY). Batallia Downs [S]: Fresh Snowmelt !pos -71.660 -2.58 450.114 (no
// default), Menechme (MENECHME).
// Available, Blood of Heroes completed and the quest's Timer at or before today (Blood of Heroes' 7 sets it to
// tomorrow: see BloodOfHeroes.cs): Rholont: 656 (an Event); Rongelouts N Distaud: 165,
// whose finish begins it. Accepted: Rholont: 167, Rongelouts: 168 (Events). The Xarcabard [S] zone-in from Beaucedine
// Glacier [S] at Prog 0: 30 (-> Prog 1). Backfilled Pit: Prog 1: 31 (-> 2). Compact Footprint: Prog 2: 32 (-> 3); Prog
// 4: 34 (-> 5). Sunken Footprint: Prog 3: 33 (-> 4). The Batallia Downs [S] zone-in from Beaucedine Glacier [S] at Prog
// 5: 114 (-> 6). Fresh Snowmelt: Prog 6: 115 (-> 7); Prog 7 with Menechme not spawned: 116, whose finish spawns him (no
// claim); Prog 8: 117, whose finish completes it (a Darksteel Sheet), adds Face of the Future (accepted) and sets the
// char down in Batallia Downs. Menechme's death at Prog 7: Prog 8.
// Kept as the Lua has it: 165 begins on any option; Menechme is spawned unclaimed (the Lua's TODO: he goes for
// Excenmille); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class ChasingShadows : QuestScript
    {
        public ChasingShadows() : base(QuestLog.CrystalWar, "CHASING_SHADOWS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_11_Chasing_Shadows";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8);

            b.Reward(new QuestReward
            {
                Item = Item.DarksteelSheet,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "BLOOD_OF_HEROES") &&
                    q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.Event(656))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.RongeloutsNDistaud)
                .OnTrigger(QuestReply.ProgressEvent(165))
                .OnEventFinish(Zone.SouthernSanDoriaS, 165, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.Event(167))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.RongeloutsNDistaud).OnTrigger(QuestReply.Event(168))
                .Npc(Zone.XarcabardS, Npc.XarcabardS.BackfilledPit).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(31);
                    }
                    return null;
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.CompactFootprint).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(32);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(34);
                    }
                    return null;
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.SunkenFootprint).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(33);
                    }
                    return null;
                })
                .OnZoneIn(Zone.XarcabardS, q =>
                {
                    if (q.Player.PreviousZone == Zone.BeaucedineGlacierS &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.Event(30);
                    }
                    return null;
                })
                .OnEventFinish(Zone.XarcabardS, 30, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.XarcabardS, 31, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.XarcabardS, 32, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.XarcabardS, 33, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.XarcabardS, 34, q =>
                {
                    q.Quest.SetStep(5);
                })
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.FreshSnowmelt).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 6)
                    {
                        return QuestReply.ProgressEvent(115);
                    }
                    else if (questProgress == 7 &&
                        !q.Player.MobIsSpawned(Mob.BatalliaDownsS.MENECHME))
                    {
                        // the Lua's TODO: Menechme is level 150 until proper data; Excenmille (level 0, not spawned)
                        // assists in this fight
                        return QuestReply.ProgressEvent(116);
                    }
                    else if (questProgress == 8)
                    {
                        return QuestReply.ProgressEvent(117);
                    }
                    return null;
                })
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Menechme).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 7)
                    {
                        q.Quest.SetStep(8);
                    }
                })
                .OnZoneIn(Zone.BatalliaDownsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.BeaucedineGlacierS &&
                        q.Quest.Step == 5)
                    {
                        return QuestReply.Event(114);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BatalliaDownsS, 114, q =>
                {
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 115, q =>
                {
                    q.Quest.SetStep(7);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 116, q =>
                {
                    q.Player.SpawnMob(Mob.BatalliaDownsS.MENECHME);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 117, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // Lua: player:addQuest(CRYSTAL_WAR, FACE_OF_THE_FUTURE)
                        q.Player.AddQuest(QuestLog.CrystalWar, "FACE_OF_THE_FUTURE");
                        q.Player.SetPos(-49.903, 0.293, 436.922, 0, Zone.BatalliaDowns);
                    }
                });
        }
    }
}
