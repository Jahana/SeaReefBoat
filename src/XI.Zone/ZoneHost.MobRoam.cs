using System;
using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // Mob roaming, walking home and path following: the roam half of CMobController::DoRoamTick (Wait, the idle
    // action every RoamCool seconds: walking home or despawning when far, the worm's dive, scripted roam actions and
    // RoamAround), FollowRoamPath (the next rest drawn from RoamRate, the worm surfacing, RoamResetFacing, onPath),
    // Move's scripted path in combat, the patrol or first roam that ends CMobEntity::Spawn, CMobEntity's roam helpers
    // (CanRoam, CanRoamHome, GetRoamAnchor, GetRoamDistance, DistanceFromHome, IsFarFromHome), EntityPathOwner (the
    // speed and the onPathPoint / onPathComplete hooks, a mob's or an NPC's) and the path bindings (pathTo,
    // pathThrough, isFollowingPath, clearPath, continuePath, atPoint), for a mob or an NPC given a path by initNpcAi
    // (ZoneHost.NpcAi.cs). The path itself is PathFind.cs: on the zone's navmesh (straight lines in a zone without
    // one), cut at the roam region's edge.
    //
    // Following is ZoneHost.MobFollow.cs, DoBuffTick ZoneHost.MobMagic.cs and wait the inactive state
    // (ZoneHost.Inactive.cs). Not ported: the idle tick skip (NextIdleEventTime / m_IdleCheckedPosition: the C# roam
    // tick runs every logic tick; the off-mesh despawn is in MobRoamTick). A charmed char's path is
    // ZoneHost.Charm.cs's. A mob pet does not roam: it shadows its roaming master and despawns at home
    // once its master is dead; the idle action tries the special skill and an SMN's summoning first.
    public sealed partial class ZoneHost : IPathFindOwner
    {
        private const long MobRoamWormDiveMs = 3000;     // the dive's Wait(3s): sinking takes 2 s, padded for magic
        private const long MobRoamWormSurfaceMs = 2000;  // the emerge's Wait(2s)
        private const float MobRoamHomeStepDistance = 10.0f; // kRoamHomeStepDistance: re-evaluated every 10 yalms
        private const float MobRoamHomeMaxDistance = 60.0f;  // CanRoamHome: past 60 yalms it despawns instead
        private const long MobRoamFarRespawnSec = 60;    // the far-from-home despawn's respawn
        private const byte MobRoamStatusInvisible = 3;   // xi::Status::Invisible

        // PAI->PathFind: a mob's, made on first use
        private PathFind PathOf(MobEntity m) => m.PathFind ??= new PathFind(m, this);

        // ---- EntityPathOwner -------------------------------------------------------------------------------------

        Random IPathFindOwner.PathFindRng => MobRng;

        // an NPC's is CBaseEntity::UpdateSpeed: its base speed, which is its speed
        byte IPathFindOwner.PathFindUpdateSpeed(IPathMover e, bool run) =>
            e is MobEntity m ? MobStats.UpdateSpeed(m, run, ChatCfg.MobRunSpeedMultiplier) : e.Speed;

        void IPathFindOwner.PathFindOnPathPoint(IPathMover e) => CallPathHook(e, "onPathPoint");

        void IPathFindOwner.PathFindOnPathComplete(IPathMover e) => CallPathHook(e, "onPathComplete");

        // EntityPathOwner::navMesh: the entity's zone's
        DetourNavMesh IPathFindOwner.PathFindNavMesh(IPathMover e) => e switch
        {
            BattleEntity b => NavMeshOf(b.Zone),
            NpcEntity npc => NavMeshOf(ZoneOf(npc)),
            _ => null,
        };

        private void CallPathHook(IPathMover e, string fn)
        {
            if (e is MobEntity m) CallMobHook(m, fn, m);
            else if (e is NpcEntity npc) CallNpcHook(npc, fn, npc);
        }

        // PAI->PathFind for a binding: a mob's (made on first use) or an NPC's from initNpcAi (null without)
        private PathFind PathOf(object e) => e switch
        {
            MobEntity m => PathOf(m),
            NpcEntity npc => npc.PathFind,
            _ => null,
        };

        // CAIContainer::CanFollowPath for a mob: not in a state that holds it still (a mob skill, death, the despawn)
        private static bool MobCanFollowPath(MobEntity m) => m.SkillState == null && m.MagicState == null && m.RangedState == null &&
                                                             m.InactiveState == null && !m.IsDead && m.FadeAtMs == 0;

        // ---- CMobEntity's roam helpers ---------------------------------------------------------------------------

        // CanRoam: not scripted, not a pet, able to move (a worm moves underground at speed 0)
        private static bool MobCanRoam(MobEntity m) =>
            (m.RoamFlags & RoamFlag.Scripted) == 0 && m.PMaster == null && (m.Speed > 0 || (m.RoamFlags & RoamFlag.Worm) != 0) &&
            m.GetMobMod(MobMod.NoMove) == 0;

        // CanRoamHome: a mob that can move walks home if it never despawns, else only without a region and within 60
        private bool MobCanRoamHome(MobEntity m)
        {
            if ((m.Speed == 0 && (m.RoamFlags & RoamFlag.Worm) == 0) || m.GetMobMod(MobMod.NoMove) > 0) return false;
            if (m.GetMobMod(MobMod.NoDespawn) != 0 || ChatCfg.MobNoDespawn) return true;
            // a mob that left its region does not walk back, it despawns and respawns somewhere inside
            if (m.RoamRegion != null) return false;
            return MobDistanceFromHome(m) < MobRoamHomeMaxDistance;
        }

        // GetRoamAnchor: inside a region it wanders on from where it stands
        private static (float X, float Y, float Z) MobRoamAnchor(MobEntity m) =>
            m.RoamRegion != null ? (m.X, m.Y, m.Z) : (m.SpawnX, m.SpawnY, m.SpawnZ);

        private static float MobRoamDistance(MobEntity m) => m.GetMobMod(MobMod.RoamDistance);

        // DistanceFromHome: how far outside the region, else from the spawn point
        private static float MobDistanceFromHome(MobEntity m)
        {
            if (m.RoamRegion != null) return m.RoamRegion.DistanceOutside(m.X, m.Y, m.Z);
            float dx = m.X - m.SpawnX, dy = m.Y - m.SpawnY, dz = m.Z - m.SpawnZ;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static bool MobIsFarFromHome(MobEntity m) => MobDistanceFromHome(m) > m.MaxRoamDistance;

        // ---- DoRoamTick's roam half ------------------------------------------------------------------------------

        // from the wait on: the C++ order, with the 3 s ROAM_TICK / onMobRoam closing it
        private void MobRoamAction(MobEntity m, long now)
        {
            // wait out the timer first (the worm's dive and surfacing)
            if (now < m.WaitTimeMs)
            {
                MobRoamScriptTick(m, now);
                return;
            }

            // don't aggro for a moment after disengaging
            m.Neutral = now <= m.NeutralAtMs + MobNeutralMs;

            var path = PathOf(m);
            if (path.IsFollowingPath) MobFollowRoamPath(m, ref now);
            else if (path.IsPatrolling)
            {
                path.ResumePatrol();
                MobFollowRoamPath(m, ref now);
            }
            else if (now >= m.LastActionMs + m.GetMobMod(MobMod.RoamCool) * 1000L && !MobRoamIdleAction(m, ref now)) return;

            MobRoamScriptTick(m, now);
        }

        private void MobRoamScriptTick(MobEntity m, long now)
        {
            if (now < m.LastRoamScriptMs + MobRoamScriptMs) return;
            TriggerListener(m, "ROAM_TICK", m);
            OnMobRoam(m);
            m.LastRoamScriptMs = now;
        }

        // the idle action, once RoamCool has passed: false when the mob despawned (the C++ returns before the roam
        // script)
        private bool MobRoamIdleAction(MobEntity m, ref long now)
        {
            if ((m.EntityFlags & MobEntity.EntityFlagCallForHelp) != 0)
            {
                m.EntityFlags &= ~MobEntity.EntityFlagCallForHelp;
                m.UpdateMask |= EntityUpdatePacket.UpdateCombat;
            }
            m.IsPathingHome = false;

            bool wantsToHeadHome = m.GetMobMod(MobMod.DontRoamHome) == 0 && MobIsFarFromHome(m);
            bool noDespawn = m.GetMobMod(MobMod.NoDespawn) != 0;
            var path = PathOf(m);

            // walk home or despawn after wandering too far
            if (wantsToHeadHome)
            {
                if (MobCanRoamHome(m))
                {
                    m.IsPathingHome = true;
                    // heading home means the nearest edge of the region, not the spot it happened to spawn on
                    (float X, float Y, float Z) home = m.RoamRegion != null ? m.RoamRegion.ClosestPoint(m.X, m.Y, m.Z) : (m.SpawnX, m.SpawnY, m.SpawnZ);
                    if (!path.IsFollowingPath && !path.PathTo(home.X, home.Y, home.Z))
                        path.PathInRange(home.X, home.Y, home.Z, m.MaxRoamDistance, PathFlag.Run);
                    // capped, so the way home is re-evaluated every few seconds
                    path.LimitDistance(MobRoamHomeStepDistance);
                    MobFollowRoamPath(m, ref now);
                    m.LastActionMs = now - (m.GetMobMod(MobMod.RoamCool) * 1000L + 10000);
                }
                else if (!noDespawn && !ChatCfg.MobNoDespawn)
                {
                    // PAI->Despawn (refused while already despawning), then a 60 s respawn over CDespawnState's own
                    var info = AreaOf(m);
                    if (m.FadeAtMs == 0) DespawnMob(info, m, now);
                    info.Spawner?.RegisterForRespawn(m, (uint)MobRoamFarRespawnSec, now);
                    return false;
                }
            }
            // at home: a pet whose master died despawns
            else if (!noDespawn && m.PMaster != null && !m.PMaster.IsAlive)
            {
                if (m.FadeAtMs == 0) DespawnMob(AreaOf(m), m, now);
                return false;
            }
            else
            {
                // the special skill (probably a pet's call), else an SMN summoning (not in an open battlefield), else
                // the scripted or ordinary roam
                bool battlefieldIsOpen = m.PBattlefield != null && m.PBattlefield.Status == Battlefield.StatusOpen;
                if (IsSpecialSkillReady(m, 0, now) && TrySpecialSkill(m, now)) { }
                else if (!battlefieldIsOpen && m.MJob == MobStats.JobSmn && now >= m.NextMagicMs && CanCastSpells(m, false) && m.SpellContainer.HasBuffSpells())
                    TryCastSpell(m, now);
                else if ((m.RoamFlags & RoamFlag.Scripted) != 0)
                {
                    MobRoamScriptedAction(m, now);
                }
                else if (MobCanRoam(m))
                {
                    byte minTurns = (byte)m.GetMobMod(MobMod.RoamTurnsMin);
                    byte maxTurns = (byte)m.GetMobMod(MobMod.RoamTurns);
                    // a worm dives underground (not while casting); m_LastActionTime is left alone so it re-emerges
                    // promptly
                    bool isWormSurfacing = (m.RoamFlags & RoamFlag.Worm) != 0 && (m.NameVis & MobEntity.NameVisHideName) == 0;
                    var anchor = MobRoamAnchor(m);
                    if (isWormSurfacing && m.MagicState == null)
                    {
                        m.AnimationSub = 1;
                        m.HideName(true);
                        MobRoamSetUntargetable(m, true);
                        MobRoamWait(m, ref now, MobRoamWormDiveMs);
                        QueueNative(m, MobRoamWormDiveMs, false, e => ((MobEntity)e).Status = MobRoamStatusInvisible);
                    }
                    else if ((m.RoamFlags & RoamFlag.Scripted) != 0)
                    {
                        MobRoamScriptedAction(m, now);
                    }
                    else if (!isWormSurfacing &&
                             path.RoamAround(anchor.X, anchor.Y, anchor.Z, MobRoamDistance(m), minTurns, maxTurns, m.RoamFlags, m.RoamRegion))
                    {
                        if ((m.RoamFlags & RoamFlag.Stealth) != 0)
                        {
                            m.HideName(true);
                            MobRoamSetUntargetable(m, true);
                            m.UpdateMask |= EntityUpdatePacket.UpdateHp;
                        }
                        else MobFollowRoamPath(m, ref now);
                    }
                    else m.LastActionMs = now;
                }
                else m.LastActionMs = now;
            }
            return true;
        }

        // the scripted roam action: ROAM_ACTION and onMobRoamAction
        private void MobRoamScriptedAction(MobEntity m, long now)
        {
            TriggerListener(m, "ROAM_ACTION", m);
            CallMobHook(m, "onMobRoamAction", m);
            m.LastActionMs = now;
        }

        // CMobController::Wait: the wait runs from now (moving the tick with it) or adds to one under way
        private static void MobRoamWait(MobEntity m, ref long now, long durationMs)
        {
            if (now > m.WaitTimeMs) m.WaitTimeMs = now += durationMs;
            else m.WaitTimeMs += durationMs;
        }

        // CMobEntity::SetUntargetable
        private static void MobRoamSetUntargetable(MobEntity m, bool untargetable)
        {
            m.Untargetable = untargetable;
            m.UpdateMask |= EntityUpdatePacket.UpdateHp;
        }

        // FollowRoamPath: a step along the path; when it ends, the next rest, the worm surfacing and the spawn facing
        private void MobFollowRoamPath(MobEntity m, ref long now)
        {
            if (!MobCanFollowPath(m)) return;
            var path = PathOf(m);
            path.FollowPath(now);

            // pets shadow their roaming master about 2.1 yalms directly behind
            if (m.PPet is MobEntity pet && pet.IsSpawned && !pet.Engaged)
            {
                var (x, y, z) = NearPosition(m, 2.1f, MathF.PI);
                PathOf(pet).PathTo(x, y, z);
            }

            if (!path.IsFollowingPath)
            {
                // rest a whole number of seconds drawn from [RoamCool - RoamCool * 10 / RoamRate, RoamCool]; a
                // negative RoamRate rests exactly RoamCool
                uint roamCool = (uint)Math.Max(m.GetMobMod(MobMod.RoamCool), (short)0);
                short roamRate = m.GetMobMod(MobMod.RoamRate);
                uint roamRange = roamRate > 0 ? Math.Min(roamCool * 10 / (uint)roamRate, roamCool) : 0;
                m.LastActionMs = now - MobRng.Next((int)roamRange + 1) * 1000L;

                // a worm finished its underground roam: it pops back up
                if ((m.RoamFlags & RoamFlag.Worm) != 0 && m.Untargetable)
                {
                    // a final position update before emerging, so it does not visibly snap
                    BroadcastMobUpdate(m, EntityUpdatePacket.UpdatePos);
                    m.Status = NpcEntity.StatusUpdate;
                    MobRoamSetUntargetable(m, false);
                    MobRoamWait(m, ref now, MobRoamWormSurfaceMs);
                    QueueNative(m, MobRoamWormSurfaceMs, false, e =>
                    {
                        var worm = (MobEntity)e;
                        worm.AnimationSub = 0;
                        worm.HideName(false);
                    });
                }

                // back to the spawn facing after pathing home, for mobs that face a fixed way
                if (m.GetMobMod(MobMod.RoamResetFacing) != 0 && MobDistanceFromHome(m) <= m.MaxRoamDistance) m.Rotation = m.SpawnRotation;
            }

            if (path.OnPoint)
            {
                TriggerListener(m, "PATH", m);
                CallMobHook(m, "onPath", m);
            }
        }

        // Move's first step in combat: a scripted path is walked instead of the chase
        private bool MobFollowScriptedPath(MobEntity m, long now)
        {
            if (m.PathFind is not { IsFollowingScriptedPath: true } path || !MobCanFollowPath(m)) return false;
            path.FollowPath(now);
            return true;
        }

        // the end of CMobEntity::Spawn: a route replaces roaming, the mob walks its waypoints while left alone;
        // otherwise it roams at once (a worm waits for its first dive)
        private void MobRoamOnSpawn(MobEntity m, long now)
        {
            var path = PathOf(m);
            if (m.Route.Count > 0)
            {
                var waypoints = new List<PathPoint>(m.Route.Count);
                foreach (var (x, y, z) in m.Route) waypoints.Add(new PathPoint(x, y, z));
                if (path.PathThrough(waypoints, PathFlag.Patrol) && MobCanFollowPath(m)) path.FollowPath(now);
                return;
            }
            byte minTurns = (byte)m.GetMobMod(MobMod.RoamTurnsMin);
            byte maxTurns = (byte)m.GetMobMod(MobMod.RoamTurns);
            bool isWorm = (m.RoamFlags & RoamFlag.Worm) != 0;
            var anchor = MobRoamAnchor(m);
            if (!path.IsFollowingPath && MobCanRoam(m) && !isWorm &&
                path.RoamAround(anchor.X, anchor.Y, anchor.Z, MobRoamDistance(m), minTurns, maxTurns, m.RoamFlags, m.RoamRegion) &&
                MobCanFollowPath(m))
                path.FollowPath(now);
        }

        // ---- bindings --------------------------------------------------------------------------------------------

        private void RegisterMobRoamBindings(Dictionary<string, LuaFunction> m)
        {
            // atPoint(x, y, z) or atPoint({x, y, z}): within 0.01 of the point
            m["atPoint"] = l =>
            {
                float px = 0, py = 0, pz = 0;
                if (l.IsNumber(2))
                {
                    px = (float)l.ToNumber(2);
                    py = (float)l.ToNumber(3);
                    pz = (float)l.ToNumber(4);
                }
                else if (l.IsTable(2))
                {
                    l.RawGetI(2, 1); px = (float)l.ToNumber(-1); l.Pop(1);
                    l.RawGetI(2, 2); py = (float)l.ToNumber(-1); l.Pop(1);
                    l.RawGetI(2, 3); pz = (float)l.ToNumber(-1); l.Pop(1);
                }
                var (x, y, z, _) = PosOf(Entity(l));
                float dx = x - px, dy = y - py, dz = z - pz;
                return Ret(l, dx * dx + dy * dy + dz * dz <= 0.01f * 0.01f);
            };
            // pathTo(x, y, z, flags = RUN | SCRIPT)
            m["pathTo"] = l =>
            {
                if (Entity(l) is CharData c)
                {
                    log_($"Invalid entity (Player: {c.Name}) calling function.");
                    return 0;
                }
                if (PathOf(Entity(l)) is { } path)
                {
                    byte flags = l.IsNil(5) ? (byte)(PathFlag.Run | PathFlag.Script) : (byte)l.ToIntArg(5);
                    path.PathTo((float)l.ToNumber(2), (float)l.ToNumber(3), (float)l.ToNumber(4), flags);
                }
                return 0;
            };
            // pathThrough(points, flags = 0): with PATROL or COORDS, a list of {x, y, z, rotation, wait} (a missing
            // coordinate carries over from the point before, the first from where the entity stands); otherwise a
            // flat x, y, z, x, y, z... list
            m["pathThrough"] = l =>
            {
                // the C++ dereferences an NPC's missing PathFind; refused here
                if (Entity(l) is not IPathMover mob || PathOf(Entity(l)) is not { } path)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                byte flags = l.IsNumber(3) ? (byte)l.ToIntArg(3) : (byte)0;
                var points = new List<PathPoint>();
                int size = l.IsTable(2) ? l.ObjLen(2) : 0;
                if ((flags & PathFlag.Patrol) != 0 || (flags & PathFlag.Coords) != 0)
                {
                    float x = mob.X, y = mob.Y, z = mob.Z;
                    for (int i = 1; i <= size; i++)
                    {
                        l.RawGetI(2, i);
                        int t = l.Top;
                        x = (float)MobRoamFieldNumber(l, t, "x", x);
                        y = (float)MobRoamFieldNumber(l, t, "y", y);
                        z = (float)MobRoamFieldNumber(l, t, "z", z);
                        var point = new PathPoint(x, y, z);
                        l.GetField(t, "rotation");
                        point.SetRotation = !l.IsNil(-1);
                        if (point.SetRotation) point.Rotation = (byte)l.ToIntegral(-1);
                        l.Pop(1);
                        l.GetField(t, "wait");
                        point.WaitMs = l.IsNil(-1) ? 0 : (uint)l.ToIntegral(-1);
                        l.Pop(1);
                        l.Pop(1);
                        points.Add(point);
                    }
                }
                else
                {
                    for (int i = 1; i < size; i += 3)
                    {
                        l.RawGetI(2, i); float x = (float)l.ToNumber(-1); l.Pop(1);
                        l.RawGetI(2, i + 1); float y = (float)l.ToNumber(-1); l.Pop(1);
                        l.RawGetI(2, i + 2); float z = (float)l.ToNumber(-1); l.Pop(1);
                        points.Add(new PathPoint(x, y, z));
                    }
                }
                return Ret(l, path.PathThrough(points, flags, msg => log_(msg)));
            };
            m["isFollowingPath"] = l => Ret(l, Entity(l) switch
            {
                MobEntity mob => mob.PathFind is { IsFollowingPath: true },
                NpcEntity npc => npc.PathFind is { IsFollowingPath: true },
                _ => false,
            });
            // clearPath(pause): an NPC told to pause stops its onPath ticks
            m["clearPath"] = l =>
            {
                bool pause = l.Type(2) == LuaNative.LUA_TBOOLEAN && l.ToBoolean(2);
                if (Entity(l) is NpcEntity npc && pause) LocalVars(npc)["pauseNPCPathing"] = 1;
                else if (Entity(l) is MobEntity mob) mob.PathFind?.Clear();
                else if (Entity(l) is NpcEntity npcWithPath) npcWithPath.PathFind?.Clear();
                return 0;
            };
            m["continuePath"] = l =>
            {
                if (Entity(l) is NpcEntity npc) LocalVars(npc)["pauseNPCPathing"] = 0;
                return 0;
            };
        }

        // sol's get_or for a number field
        private static double MobRoamFieldNumber(LuaState l, int idx, string key, double fallback)
        {
            l.GetField(idx, key);
            double v = l.IsNumber(-1) ? l.ToNumber(-1) : fallback;
            l.Pop(1);
            return v;
        }
    }
}
