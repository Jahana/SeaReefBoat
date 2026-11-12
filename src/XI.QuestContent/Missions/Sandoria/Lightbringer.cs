// Lightbringer (San d'Oria 8-2, Log ID 0, Mission ID 21), a line-for-line port of
// scripts/missions/sandoria/8_2_Lightbringer.lua (quest rework C3 N6). Ambrotien !pos 93.419 -0.001 -57.347 230;
// Grilau !pos -241.987 6.999 57.887 231; Endracion !pos -110 1 -34 230; (_6h4) Great Hall !pos 0 -1 13 233; Rahal
// !pos -28 0.1 -6 233; Door: Prince Royal's !pos -38 -3 73 233; Door: Prince Regent's !pos -37 -3 31 233; Halver !pos 2
// 0.1 0.1 233; qm_key1 !pos -13 -17 -151 159; qm_key2 !pos -32 -17 -153 159; qm_key3 !pos -68 -17 -153 159; Granite
// Door !pos -50 -17 -154 159. Reward: rank 9 and 80000 gil. Section 1 (no San d'Oria mission current, a San d'Orian):
// the gate guards' 1009 and 2009 (Southern San d'Oria) and 1009 (Northern) finish with option 21
// (handleAcceptMission) begins the mission. Section 2 (current): in the Chateau, _6h4 plays 100 at status 0 and 104 at
// 6; Aramaviont 15 at 6; Curilla 57 at 2 and 103 at 6; Milchupain 36 at 6; Rahal 106 at 1, 107 at 2 and 105 at 6 (all
// progress); Halver at 6 says LIGHTBRINGER_EXTRA (messageText, replaceDefault). 100's finish sets status 1; 106's
// sets status 2 and gives the Crystal Dowser (npcUtil.giveKeyItem); 104's completes the mission and then takes the
// dowser. In the Temple of Uggalepih, the Granite Door _4fv at status 5 with the three key pieces and both Nio NMs
// found, each not spawned or dead, plays 65 (progress) with Prog above 0, else spawns Nio-A and Nio-Hum and says
// BEGINS_TO_QUIVER (the Crystal Dowser); otherwise it says YOU_CANNOT_OPEN_THIS_DOOR. 65's finish sets status 6 and
// takes the three pieces. Nio-A's and Nio-Hum's deaths at status 5, the other found and dead or not spawned, set Prog
// 1. qm_key1, qm_key2 and qm_key3 without their piece at status 2 or more raise the status by one and give the piece
// (keyItem); qm17 says SLIGHTLY_QUIVERS (the Crystal Dowser). Section 3 (completed, San d'Oria rank 9, The Heir to the
// Light not current): _6h0 and _6h1 play 63 and 74 (progress) until Option's bit 0 and bit 1 are set; their finishes
// set the bits. Section 4 (completed, rank 9, no rank points): Aramaviont and Milchupain say LIGHTBRINGER_EXTRA + 1
// and + 3 (messageText); Rahal plays 42 (event, replaceDefault). Kept as the Lua has it: section 1 compares the nation
// with mission.areaId (the log id, 0, which is xi.nation.SANDORIA) and has no rank check and no completed check (it
// reads only the current mission and the nation); sections 3 and 4 read getRank(mission.areaId), a log id passed as a
// nation (the same 0); the Nio spawns take neither claim nor enmity.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class Lightbringer : MissionScript
    {
        public Lightbringer() : base(XI.Quests.MissionLog.Sandoria, "LIGHTBRINGER") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/8_2_Lightbringer";

        // mission.areaId and mission.missionId
        const int AreaId = MissionLog.Sandoria;
        const int MissionId = Mission.Sandoria.Lightbringer;

        static void HandleAcceptMission(IQuestContext q)
        {
            if (q.Option == 21)
            {
                q.Mission.Begin();
            }
        }

        // a qm_key: without its piece at status 2 or more, the status raised by one and the piece given
        static QuestReply KeyPiece(IQuestContext q, int keyItem)
        {
            if (!q.Player.HasKeyItem(keyItem) &&
                q.Player.MissionStatus(AreaId) >= 2)
            {
                q.Player.SetMissionStatus(AreaId, q.Player.MissionStatus(AreaId) + 1);
                return QuestReply.KeyItem(keyItem);
            }

            return null;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 9, Gil = 80000 });

            // There is a cutscene that must be viewed after completing M8-1 (Coming of Age).  This is
            // handled with the mission variable 'Mission[0][20]Progress', and is handled in that mission
            // script. If this is non-zero, gate guard interaction will be blocked.

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Sandoria) == Mission.Sandoria.None &&
                    q.Player.Nation == AreaId)
                .OnEventFinish(Zone.SouthernSanDoria, 1009, HandleAcceptMission)
                .OnEventFinish(Zone.SouthernSanDoria, 2009, HandleAcceptMission)
                .OnEventFinish(Zone.NorthernSanDoria, 1009, HandleAcceptMission);

            // Player has accepted the mission
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h4).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(100);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(104);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Aramaviont).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 6)
                    {
                        return QuestReply.ProgressEvent(15);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(57);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(103);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 6)
                    {
                        return QuestReply.MessageText(Text.ChateauDoraguille.LIGHTBRINGER_EXTRA).ReplaceDefault();
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Milchupain).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 6)
                    {
                        return QuestReply.ProgressEvent(36);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.ProgressEvent(106);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(105);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 100, q => q.Player.SetMissionStatus(AreaId, 1))
                .OnEventFinish(Zone.ChateauDoraguille, 104, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.CrystalDowser);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 106, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.GiveKeyItem(KeyItem.CrystalDowser);
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih._4fv).OnTrigger(q =>
                {
                    // local nioA = GetMobByID(uggalepihID.mob.NIO_A)
                    // local nioHum = GetMobByID(uggalepihID.mob.NIO_HUM)
                    if (q.Player.MissionStatus(AreaId) == 5 &&
                        q.Player.HasKeyItem(KeyItem.PieceOfABrokenKey1) &&
                        q.Player.HasKeyItem(KeyItem.PieceOfABrokenKey2) &&
                        q.Player.HasKeyItem(KeyItem.PieceOfABrokenKey3) &&
                        q.Player.MobExists(Mob.TempleOfUggalepih.NIO_A) &&
                        q.Player.MobExists(Mob.TempleOfUggalepih.NIO_HUM) &&
                        (!q.Player.MobIsSpawned(Mob.TempleOfUggalepih.NIO_A) ||
                         q.Player.MobIsDead(Mob.TempleOfUggalepih.NIO_A)) &&
                        (!q.Player.MobIsSpawned(Mob.TempleOfUggalepih.NIO_HUM) ||
                         q.Player.MobIsDead(Mob.TempleOfUggalepih.NIO_HUM)))
                    {
                        if (q.Mission.Var("Prog") > 0)
                        {
                            return QuestReply.ProgressEvent(65);
                        }
                        else
                        {
                            q.Player.SpawnMob(Mob.TempleOfUggalepih.NIO_A);
                            q.Player.SpawnMob(Mob.TempleOfUggalepih.NIO_HUM);
                            return QuestReply.MessageSpecial(Text.TempleOfUggalepih.BEGINS_TO_QUIVER,
                                KeyItem.CrystalDowser);
                        }
                    }

                    return QuestReply.MessageSpecial(Text.TempleOfUggalepih.YOU_CANNOT_OPEN_THIS_DOOR);
                })
                // Only one of these NMs are required to progress, but the mob must despawn while in combat with
                // the other target.
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.NioA).OnMobDeath(q =>
                {
                    // local mobNioHum = GetMobByID(uggalepihID.mob.NIO_HUM)
                    if (q.Player.MissionStatus(AreaId) == 5 &&
                        q.Player.MobExists(Mob.TempleOfUggalepih.NIO_HUM) &&
                        (q.Player.MobIsDead(Mob.TempleOfUggalepih.NIO_HUM) ||
                         !q.Player.MobIsSpawned(Mob.TempleOfUggalepih.NIO_HUM)))
                    {
                        q.Mission.SetVar("Prog", 1);
                    }
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.NioHum).OnMobDeath(q =>
                {
                    // local mobNioA = GetMobByID(uggalepihID.mob.NIO_A)
                    if (q.Player.MissionStatus(AreaId) == 5 &&
                        q.Player.MobExists(Mob.TempleOfUggalepih.NIO_A) &&
                        (q.Player.MobIsDead(Mob.TempleOfUggalepih.NIO_A) ||
                         !q.Player.MobIsSpawned(Mob.TempleOfUggalepih.NIO_A)))
                    {
                        q.Mission.SetVar("Prog", 1);
                    }
                })
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qmkey1)
                .OnTrigger(q => KeyPiece(q, KeyItem.PieceOfABrokenKey1))
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qmkey2)
                .OnTrigger(q => KeyPiece(q, KeyItem.PieceOfABrokenKey2))
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qmkey3)
                .OnTrigger(q => KeyPiece(q, KeyItem.PieceOfABrokenKey3))
                .Npc(Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.qm17).OnTrigger(q =>
                    QuestReply.MessageSpecial(Text.TempleOfUggalepih.SLIGHTLY_QUIVERS, KeyItem.CrystalDowser))
                .OnEventFinish(Zone.TempleOfUggalepih, 65, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 6);
                    q.Player.DelKeyItem(KeyItem.PieceOfABrokenKey1);
                    q.Player.DelKeyItem(KeyItem.PieceOfABrokenKey2);
                    q.Player.DelKeyItem(KeyItem.PieceOfABrokenKey3);
                });

            // Optional once-per-character Prince cutscenes, available from completing the Mission until
            // The Heir to the Light begins.  The 'Option' mission variable is cleared on M9-2 completion.
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Player.Rank(AreaId) == 9 &&
                    q.Player.CurrentMission(AreaId) != Mission.Sandoria.TheHeirToTheLight)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Option", 0))
                    {
                        return QuestReply.ProgressEvent(63);
                    }

                    return null;
                })
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h1).OnTrigger(q =>
                {
                    if (!q.Mission.IsVarBitsSet("Option", 1))
                    {
                        return QuestReply.ProgressEvent(74);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 63, q => q.Mission.SetVarBit("Option", 0))
                .OnEventFinish(Zone.ChateauDoraguille, 74, q => q.Mission.SetVarBit("Option", 1));

            // Optional dialogue immediately after completing the Mission.
            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(AreaId, MissionId) &&
                    q.Player.Rank(AreaId) == 9 &&
                    q.Player.RankPoints == 0)
                // mission:messageText(chateauID.text.LIGHTBRINGER_EXTRA + n): a text id plus an offset, so a closure
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Aramaviont).OnTrigger(q =>
                    QuestReply.MessageText(q.Text.Id(Text.ChateauDoraguille.LIGHTBRINGER_EXTRA) + 1))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Milchupain).OnTrigger(q =>
                    QuestReply.MessageText(q.Text.Id(Text.ChateauDoraguille.LIGHTBRINGER_EXTRA) + 3))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Rahal)
                .OnTrigger(QuestReply.Event(42).ReplaceDefault());
        }
    }
}
