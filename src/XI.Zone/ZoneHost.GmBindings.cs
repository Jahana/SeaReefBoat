using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Binding batch 11: CLuaBaseEntity's job points (addJobPoints, delJobPoints, getJobPoints, setJobPoints,
    // setCapacityPoints, masterJob over CJobPoints; SetJobPoints / SetCapacityPoints are ZoneHost.Roe's), the GM moves (gotoPlayer, gotoEntity, bringPlayer, resetPlayer) with
    // the IPC they ride (IPCClient::handleMessage_EntityInformationRequest / EntityInformationResponse /
    // SendPlayerToLocation), and setUnityLeader (charutils::SetUnityLeader).
    public sealed partial class ZoneHost
    {
        private const byte EntityTypePc = 0x01, EntityTypeNpc = 0x02, EntityTypeMob = 0x04; // ENTITYTYPE
        private const ushort ZoneLowerJeuno = 245;

        // ---- CJobPoints ---------------------------------------------------------------------------------------------

        // CJobPoints::AddJobPoints: up to 500 at a time onto the job's row (made if missing), then reloaded
        private void AddJobPoints(CharData c, byte job, ushort amount)
        {
            if (job == 0 || job > 22)
            {
                log_($"Attempt to adjust job points for an invalid job for ({c.Name}).");
                return;
            }
            short clamped = Math.Clamp((short)amount, (short)0, (short)500);
            Exec("INSERT INTO char_job_points SET charid = ?, jobid = ?, job_points = ? ON DUPLICATE KEY UPDATE job_points = job_points + ?",
                 BoundValue.Of(c.Id), BoundValue.Of(job), BoundValue.Of(clamped), BoundValue.Of(clamped));
            loader_.LoadJobPoints(c);
        }

        // CJobPoints::DelJobPoints: refused below 0
        private void DelJobPoints(CharData c, byte job, short amount)
        {
            int current = (short)JobPointsByJob(c, job);
            amount = Math.Clamp(amount, (short)-500, (short)500);
            if (current < amount)
            {
                log_($"Attempt to reduce job points below 0 for ({c.Name}).");
                return;
            }
            Exec("UPDATE char_job_points SET job_points = ? WHERE charid = ? AND jobid = ?",
                 BoundValue.Of((short)(current - amount)), BoundValue.Of(c.Id), BoundValue.Of(job));
            loader_.LoadJobPoints(c);
        }

        // CJobPoints::GetJobPointsByJob: the row's points, 0 without one
        private ushort JobPointsByJob(CharData c, byte job)
        {
            var r = Exec("SELECT job_points FROM char_job_points WHERE charid = ? AND jobid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(job));
            return r != null && r.Next() ? r.Get<ushort>("job_points") : (ushort)0;
        }

        // masterJob: each of the main job's ten types raised to 20, the points for every step granted first; the
        // points held before are what is left
        private void MasterJob(CharData c)
        {
            int category = 0x20 * c.MJob;
            for (int type = category; type < category + 0xA; type++)
            {
                int needed = 20 - JobPointUtil.Level(c, type);
                ushort current = c.JobPoints[c.MJob].Current;
                for (int x = 0; x < needed; x++)
                {
                    byte cost = JobPointUtil.Cost(JobPointUtil.Level(c, type));
                    SetJobPoints(c, (ushort)(current + cost));
                    RaiseJobPoint(c, (ushort)type);
                }
            }
        }

        // ---- the GM moves -------------------------------------------------------------------------------------------

        // the warp IPCClient::handleMessage_EntityInformationResponse and _SendPlayerToLocation make: the char at the
        // position, zoning into the zone (its mog house id taken), the pet kept
        private void WarpByIpc(uint charId, ushort zone, float x, float y, float z, byte rot, uint moghouseId)
        {
            var (c, s) = CharById(charId);
            if (c == null) return;
            (c.X, c.Y, c.Z, c.Rotation) = (x, y, z, rot);
            c.MoghouseId = moghouseId;
            c.Boundary = 0;
            c.UpdateMask = 0;
            c.Animation = 0;
            s.PacketList.Clear();
            zoneChanges_[s] = zone;
            SavePetForZoning(c);
        }

        // zoneutils::GetEntity(id): a mob or NPC of a zone this map has loaded
        private object EntityById(uint id)
        {
            foreach (var (info, _, _) in zones_.Values)
            {
                if (info.MobById.TryGetValue(id, out var mob)) return mob;
                if (info.NpcById.TryGetValue(id, out var npc)) return npc;
            }
            return null;
        }

        // IPCClient::handleMessage_EntityInformationRequest: where the char, mob or NPC is (an unspawned mob: its spawn
        // point), a warp only to one spawned
        private void OnEntityInformationRequest(EntityInformationRequest m)
        {
            ushort zone;
            float x, y, z;
            byte rot;
            bool spawned;
            uint moghouseId = 0;
            if ((m.EntityType & EntityTypePc) != 0)
            {
                var (c, s) = CharById(m.TargetId);
                if (c == null)
                {
                    log_($"EntityInformationRequest for entity {m.TargetId} failed");
                    return;
                }
                (zone, x, y, z, rot, spawned, moghouseId) = (c.Zone, c.X, c.Y, c.Z, c.Rotation, !zoneChanges_.ContainsKey(s), c.MoghouseId);
            }
            else
            {
                switch (EntityById(m.TargetId))
                {
                    case MobEntity mob:
                        spawned = mob.IsSpawned;
                        (zone, rot) = (mob.Zone, mob.Rotation);
                        (x, y, z) = (m.EntityType & EntityTypeMob) != 0 && !spawned ? (mob.SpawnX, mob.SpawnY, mob.SpawnZ) : (mob.X, mob.Y, mob.Z);
                        break;
                    case NpcEntity npc:
                        (zone, x, y, z, rot, spawned) = ((ushort)(npc.Id >> 12 & 0x0FFF), npc.X, npc.Y, npc.Z, npc.Rotation, npc.Status != NpcEntity.StatusDisappear);
                        break;
                    default:
                        log_($"EntityInformationRequest for entity {m.TargetId} failed");
                        return;
                }
            }
            SendIpc(new EntityInformationResponse
            {
                RequesterId = m.RequesterId, TargetId = m.TargetId, EntityType = m.EntityType, Warp = m.Warp && spawned,
                ZoneId = (ZoneId)zone, X = x, Y = y, Z = z, Rot = rot, MoghouseId = moghouseId,
            });
        }

        // charutils::SetUnityLeader: 1..11, the unity chat rejoined and the row saved
        private void SetUnityLeader(CharData c, byte leader)
        {
            if (leader < 1 || leader > 11) return;
            c.UnityLeader = leader;
            LeaveUnityChat(c);
            JoinUnityChat(c, c.UnityLeader);
            Exec("UPDATE char_profile SET unity_leader = ? WHERE charid = ?", BoundValue.Of(c.UnityLeader), BoundValue.Of(c.Id));
        }

        private void RegisterGmBindings(Dictionary<string, XI.Script.LuaFunction> m)
        {
            // addJobPoints(job, amount) / delJobPoints(job, amount) / setJobPoints(amount) / setCapacityPoints(amount) /
            // masterJob: the job points packet after each
            m["addJobPoints"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Warning: Attempt to add Job Points for non-PC type!");
                    return 0;
                }
                AddJobPoints(c, (byte)l.ToIntArg(2), (ushort)l.ToIntArg(3));
                PushTo(c, SelfPackets.JobPoints(c));
                return 0;
            };
            m["delJobPoints"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Warning: Attempt to delete Job Points from non-PC type!");
                    return 0;
                }
                DelJobPoints(c, (byte)l.ToIntArg(2), (short)l.ToIntArg(3));
                PushTo(c, SelfPackets.JobPoints(c));
                return 0;
            };
            m["getJobPoints"] = l => Ret(l, Entity(l) is CharData c ? JobPointsByJob(c, (byte)l.ToEnumArg(2)) : 0);
            m["setJobPoints"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Warning: Attempt to set Job Points for non-PC type!");
                    return 0;
                }
                SetJobPoints(c, (ushort)l.ToIntArg(2));
                PushTo(c, SelfPackets.JobPoints(c));
                return 0;
            };
            m["setCapacityPoints"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Warning: Attempt to set Capacity Points for non-PC type!");
                    return 0;
                }
                SetCapacityPoints(c, (ushort)l.ToIntArg(2));
                PushTo(c, SelfPackets.JobPoints(c));
                return 0;
            };
            m["masterJob"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("Warning: Attempt to master job for non-PC type!");
                    return 0;
                }
                MasterJob(c);
                PushTo(c, SelfPackets.JobPoints(c));
                return 0;
            };

            // gotoPlayer(name): ask the world where the char is, warping there; false for no such char
            m["gotoPlayer"] = l =>
            {
                uint charId = CharIdFromName(l.ToStringArg(2) ?? "");
                if (charId == 0) return Ret(l, false);
                SendIpc(new EntityInformationRequest { RequesterId = IdOf(Entity(l)), TargetId = charId, EntityType = EntityTypePc, Warp = true });
                return Ret(l, true);
            };
            // gotoEntity(id, spawnedOnly): the same for a mob or NPC by its id. The C++ narrows the requester's id to 16
            // bits; the full id is sent here
            m["gotoEntity"] = l =>
            {
                if (Pc(l, "gotoEntity") is not CharData c) return 0;
                SendIpc(new EntityInformationRequest
                {
                    RequesterId = c.Id, TargetId = l.ToUIntArg(2), EntityType = EntityTypeNpc | EntityTypeMob, Warp = true,
                    SpawnedOnly = !l.IsNil(3) && l.ToBoolean(3),
                });
                return 0;
            };
            // bringPlayer(name): the char sent to the caller's position (and mog house); false for no such char
            m["bringPlayer"] = l =>
            {
                uint charId = CharIdFromName(l.ToStringArg(2) ?? "");
                if (charId == 0) return Ret(l, false);
                var (zone, x, y, z, rot) = Entity(l) switch
                {
                    BattleEntity b => (b.Zone, b.X, b.Y, b.Z, b.Rotation),
                    NpcEntity n => ((ushort)(n.Id >> 12 & 0x0FFF), n.X, n.Y, n.Z, n.Rotation),
                    _ => ((ushort)0, 0f, 0f, 0f, (byte)0),
                };
                SendIpc(new SendPlayerToLocation
                {
                    TargetId = charId, ZoneId = (ZoneId)zone, X = x, Y = y, Z = z, Rot = rot,
                    MoghouseId = Entity(l) is CharData c ? c.MoghouseId : 0,
                });
                return Ret(l, true);
            };
            // resetPlayer(name): the char's session dropped and its position set to Lower Jeuno
            m["resetPlayer"] = l =>
            {
                string name = l.ToStringArg(2) ?? "";
                uint id = CharIdFromName(name);
                if (id == 0)
                {
                    log_($"Could not get the character {name} from database");
                    return 0;
                }
                Exec("DELETE FROM accounts_sessions WHERE charid = ?", BoundValue.Of(id));
                Exec("UPDATE chars SET pos_zone = ?, pos_prevzone = ?, pos_rot = ?, pos_x = ?, pos_y = ?, pos_z = ?, boundary = ?, moghouse = ? WHERE charid = ?",
                     BoundValue.Of(ZoneLowerJeuno), BoundValue.Of(ZoneLowerJeuno), BoundValue.Of((byte)86), BoundValue.Of(33.464f),
                     BoundValue.Of(-5.000f), BoundValue.Of(69.162f), BoundValue.Of((ushort)0), BoundValue.Of(0u), BoundValue.Of(id));
                log_("Player reset was successful.");
                return 0;
            };
            // setUnityLeader(leader): the old leader's trust unlearned, the new leader set, its trust by the accolades
            m["setUnityLeader"] = l =>
            {
                if (Pc(l, "setUnityLeader") is not CharData c) return 0;
                if (c.UnityLeader > 0 && c.UnityLeader <= RoeTrustId.Length)
                {
                    ushort oldTrust = RoeTrustId[c.UnityLeader - 1];
                    if (HasBit(c.SpellList, oldTrust))
                    {
                        c.SpellList[oldTrust / 8] &= (byte)~(1 << oldTrust % 8);
                        Exec("DELETE FROM char_spells WHERE charid = ? AND spellid = ? LIMIT 1", BoundValue.Of(c.Id), BoundValue.Of(oldTrust));
                    }
                }
                SetUnityLeader(c, (byte)l.ToIntArg(2));
                UpdateUnityTrust(c);
                return 0;
            };
        }
    }
}
