// Songbirds in a Snowstorm (Log ID 7, Quest ID 51), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_9_Songbirds_in_a_Snowstorm.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32), Daigraffeaux !pos -7 2 -89 (default 620). Beaucedine Glacier [S]:
// Colossal Footprint !pos 82.144 -19.038 139.249, Compressed Snow !pos 46.437 -0.762 -370.178 (defaults
// NOTHING_OUT_OF_ORDINARY), Charred Firewood !pos 83.296 -58.472 175.2 (default REMAINS_OF_COOKFIRE), Rocky Perch !pos
// -106.321 0.282 -365.035 (no default), Orcish Bloodletter (ORCISH_BLOODLETTER). Available, Bonds That Never Die and
// the WotG mission The Will of the World completed: Rholont: the mustZone clear and the Timer at or before today
// (Bonds' 650 sets both): Prog 0: 113 (-> Prog 1); Prog 1: Option 0: 655 (-> Option 1); else 49 (an Event); else nil.
// Daigraffeaux: Prog 1: 114, whose finish begins it. Accepted: Rholont: 656 (an Event). The Beaucedine Glacier [S]
// zone-in at Prog 1 (any zone): 1, whose update on option 11 sends (0, 23, 1756, 0, 0, 5832718, 0, 1); its finish sets
// Prog 2. Colossal Footprint: the Lance Fish, the Paladin Lobster and the Scutum Crab (fished at ponds of the zone; no
// script gives them): 3 (136) (-> Prog 4); else Prog 2: 2 (-> 3). Rocky Perch: Prog 3: NONDESCRIPT_MASS and a Goliath
// Worm, no event. Charred Firewood: a Flint Stone alone at Prog 4: 4 (-> 5). Compressed Snow: Prog 5: 5 (-> 6); Prog 6
// with the Orcish Bloodletter not spawned: 27, whose finish spawns it claimed by the char; Prog 7: 6, whose finish
// completes it (an Icarus Wing). The Bloodletter's death at Prog 6: Prog 7. Kept as the Lua has it: Prog and Option are
// written while available; 114 begins on any option; the three key items are never taken, so the Footprint answers 3 at
// any Prog while they are held (its finish sets Prog 4 again, a step back from 5-7); the Flint Stone is never taken;
// the Rocky Perch gives a worm at every Prog 3 talk; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class SongbirdsInASnowstorm : QuestScript
    {
        public SongbirdsInASnowstorm() : base(QuestLog.CrystalWar, "SONGBIRDS_IN_A_SNOWSTORM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_9_Songbirds_in_a_Snowstorm";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6, 7);

            b.Reward(new QuestReward
            {
                Item = Item.IcarusWing,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "BONDS_THAT_NEVER_DIE") &&
                    q.Player.HasCompletedMission(MissionLog.Wotg, Mission.Wotg.TheWillOfTheWorld))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (!q.Quest.MustZone &&
                        q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(113);
                        }
                        else if (questProgress == 1)
                        {
                            if (q.Quest.Var("Option") == 0)
                            {
                                return QuestReply.ProgressEvent(655);
                            }
                            else
                            {
                                return QuestReply.Event(49);
                            }
                        }
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Daigraffeaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(114);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 113, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 114, q =>
                {
                    // the Lua's TODO: the decline answers option 0 too, so it begins on either
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 655, q =>
                {
                    q.Quest.SetVar("Option", 1);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.Event(656))
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.CharredFirewood).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.FlintStone) &&
                        q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.ColossalFootprint).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LanceFish) &&
                        q.Player.HasKeyItem(KeyItem.PaladinLobster) &&
                        q.Player.HasKeyItem(KeyItem.ScutumCrab))
                    {
                        return QuestReply.ProgressEvent(3, 136);
                    }
                    else if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                    return null;
                })
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.CompressedSnow).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 5)
                    {
                        return QuestReply.ProgressEvent(5);
                    }
                    else if (questProgress == 6 &&
                        !q.Player.MobIsSpawned(Mob.BeaucedineGlacierS.ORCISH_BLOODLETTER))
                    {
                        return QuestReply.ProgressEvent(27);
                    }
                    else if (questProgress == 7)
                    {
                        return QuestReply.ProgressEvent(6);
                    }
                    return null;
                })
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.OrcishBloodletter).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 6)
                    {
                        q.Quest.SetStep(7);
                    }
                })
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.RockyPerch).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Text.Special(Text.BeaucedineGlacierS.NONDESCRIPT_MASS);
                        q.Player.GiveItem(Item.GoliathWorm);

                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .OnZoneIn(Zone.BeaucedineGlacierS, q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.Event(1);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.BeaucedineGlacierS, 1, q =>
                {
                    if (q.Option == 11)
                    {
                        q.Player.UpdateEvent(0, 23, 1756, 0, 0, 5832718, 0, 1);
                    }
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 1, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 2, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 3, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 4, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 5, q =>
                {
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 6, q =>
                {
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.BeaucedineGlacierS, 27, q =>
                {
                    q.Player.SpawnMob(Mob.BeaucedineGlacierS.ORCISH_BLOODLETTER, 0, true);
                });
        }
    }
}
