// Calm Before the Storm (Promathia 7-4, Log ID 6, Mission ID 738), a line-for-line port of
// scripts/missions/cop/7_4_Calm_Before_the_Storm.lua (quest rework C3, C4). The mission current; the CID status
// nibble (xi.mission.status.COP.CID, position 6) holds one bit per NM fight: Misareaux Coast (0), Carpenters' Landing
// (1), Bibiki Bay (2). Each place's trigger plays its cutscene once its fight's local var says the NMs died, else pops
// the NMs while none is up: _0p4 spawns Boggelmann (SpawnMob, claimed; CREATURE_HAS_APPEARED) and plays 13 (option 1
// the Ducal Guard's Ring) at nmMisareaux 1, whose finish sets bit 0 and gives the Vessel of Light; qm_cryptonberries
// spawns the Cryptonberry Executor (mob:spawn(), claimed, its enmity on the char set to 0;
// CRYPTONBERRY_EXECUTOR_POP) while none of the Executor and its three Assassins is up, and plays 37 when
// carpentersNm is 15 (bit 1 at its finish); each of the four's deaths sets its bit (id minus the Executor's) in
// carpentersNm, the Executor's also says CRYPTONBERRY_EXECUTOR_DIE (mob:messageText); qm_dalham spawns Dalham (the
// zone's first entity of that name: mob:spawn(), claimed; YOU_ARE_NOT_ALONE) and plays 41 at nmBibiki 1 (bit 2). The
// Boggelmann and Dalham deaths set their local var from 0 to 1 only. Metalworks' Cid at the nibble 7: 892 without the
// Letters from Ulmia and Prishe (its finish gives them), else 895 once per zone. Sealion's Den's Sueleen with the
// letters plays 17, whose finish completes the mission. The Warrior's Path is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class CalmBeforeTheStorm : MissionScript
    {
        public CalmBeforeTheStorm() : base(XI.Quests.MissionLog.CoP, "CALM_BEFORE_THE_STORM") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/7_4_Calm_Before_the_Storm";

        // xi.mission.status.COP.CID
        const int Cid = 6;

        // setMissionStatusBit
        static void SetMissionStatusBit(IQuestContext q, int statusBit)
        {
            int missionStatus = q.Mission.StatusAt(Cid);
            q.Mission.SetStatusAt(SetBit(missionStatus, statusBit), Cid);
        }

        // getMissionStatusBit
        static bool GetMissionStatusBit(IQuestContext q, int statusBit) => GetBit(q.Mission.StatusAt(Cid), statusBit);

        // utils.mask.getBit(mask, pos)
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        // utils.mask.setBit(mask, pos, true)
        static int SetBit(int mask, int pos) => mask | (1 << pos);

        // isCarpentersNmSpawned: the Executor and the three Assassins after it
        static bool IsCarpentersNmSpawned(IQuestContext q)
        {
            int executor = q.Player.MobIdOf(Mob.CarpentersLanding.CRYPTONBERRY_EXECUTOR);

            for (int nmId = executor; nmId <= executor + 3; nmId++)
            {
                if (q.Player.MobIsSpawnedById(nmId))
                {
                    return true;
                }
            }

            return false;
        }

        // the Assassins' and the Executor's deaths: the mob's bit in carpentersNm
        static void SetCarpentersBit(IQuestContext q)
        {
            int nmProgress = q.Mission.LocalVar("carpentersNm");
            int nmOffset = q.Mob.Id - q.Player.MobIdOf(Mob.CarpentersLanding.CRYPTONBERRY_EXECUTOR);
            q.Mission.SetLocalVar("carpentersNm", SetBit(nmProgress, nmOffset));
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheWarriorsPath,
            });

            b.State(current: true)
                .Npc(Zone.BibikiBay, Npc.BibikiBay.qmdalham).OnTrigger(q =>
                {
                    if (!GetMissionStatusBit(q, 2))
                    {
                        int dalham = q.Player.MobIdByName("Dalham");

                        if (q.Mission.LocalVar("nmBibiki") == 1)
                        {
                            return QuestReply.ProgressCutscene(41);
                        }
                        else if (!q.Player.MobIsSpawnedById(dalham))
                        {
                            q.Player.SpawnMobEntityById(dalham, zeroEnmity: false);
                            return QuestReply.MessageSpecial(Text.BibikiBay.YOU_ARE_NOT_ALONE);
                        }
                    }

                    return null;
                })
                .Npc(Zone.BibikiBay, Npc.BibikiBay.Dalham).OnMobDeath(q =>
                {
                    if (q.Mission.LocalVar("nmBibiki") == 0)
                    {
                        q.Mission.SetLocalVar("nmBibiki", 1);
                    }
                })
                .OnEventFinish(Zone.BibikiBay, 41, q => SetMissionStatusBit(q, 2))
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.qmcryptonberries).OnTrigger(q =>
                {
                    if (!GetMissionStatusBit(q, 1))
                    {
                        int nmProgress = q.Mission.LocalVar("carpentersNm");

                        if (nmProgress == 15)
                        {
                            return QuestReply.ProgressCutscene(37);
                        }
                        else if (!IsCarpentersNmSpawned(q))
                        {
                            q.Player.SpawnMobEntityById(
                                q.Player.MobIdOf(Mob.CarpentersLanding.CRYPTONBERRY_EXECUTOR), zeroEnmity: true);
                            return QuestReply.MessageSpecial(Text.CarpentersLanding.CRYPTONBERRY_EXECUTOR_POP);
                        }
                    }

                    return null;
                })
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.CryptonberryAssassin).OnMobDeath(SetCarpentersBit)
                .Npc(Zone.CarpentersLanding, Npc.CarpentersLanding.CryptonberryExecutor).OnMobDeath(q =>
                {
                    SetCarpentersBit(q);
                    q.Mob.MessageTextOwn(Text.CarpentersLanding.CRYPTONBERRY_EXECUTOR_DIE);
                })
                .OnEventFinish(Zone.CarpentersLanding, 37, q => SetMissionStatusBit(q, 1))
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p4).OnTrigger(q =>
                {
                    if (!GetMissionStatusBit(q, 0))
                    {
                        if (q.Mission.LocalVar("nmMisareaux") == 1)
                        {
                            return QuestReply.ProgressCutscene(13).Option(1, Item.DucalGuardsRing);
                        }
                        else if (!q.Player.MobIsSpawned(Mob.MisareauxCoast.BOGGELMANN))
                        {
                            q.Player.SpawnMob(Mob.MisareauxCoast.BOGGELMANN, 0, claim: true);
                            return QuestReply.MessageSpecial(Text.MisareauxCoast.CREATURE_HAS_APPEARED);
                        }
                    }

                    return null;
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.Boggelmann).OnMobDeath(q =>
                {
                    if (q.Mission.LocalVar("nmMisareaux") == 0)
                    {
                        q.Mission.SetLocalVar("nmMisareaux", 1);
                    }
                })
                .OnEventFinish(Zone.MisareauxCoast, 13, q =>
                {
                    SetMissionStatusBit(q, 0);
                    q.Player.GiveKeyItem(KeyItem.VesselOfLight);
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    if (q.Mission.StatusAt(Cid) == 7)
                    {
                        if (!q.Player.HasKeyItem(KeyItem.LettersFromUlmiaAndPrishe))
                        {
                            return QuestReply.ProgressEvent(892);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(895).OncePerZone();
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 892, q => q.Player.GiveKeyItem(KeyItem.LettersFromUlmiaAndPrishe))
                .Npc(Zone.SealionsDen, Npc.SealionsDen.Sueleen).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.LettersFromUlmiaAndPrishe) ? QuestReply.ProgressEvent(17) : null)
                .OnEventFinish(Zone.SealionsDen, 17, q => q.Mission.Complete());
        }
    }
}
