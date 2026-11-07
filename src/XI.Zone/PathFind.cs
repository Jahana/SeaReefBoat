using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // pathfind_types.h's PATHFLAG
    public static class PathFlag
    {
        public const byte None = 0x00;
        public const byte Run = 0x01;      // run (MOB_RUN_SPEED_MULTIPLIER)
        public const byte Wallhack = 0x02; // deprecated in the C++; kept so the bit is not reused
        public const byte Reverse = 0x04;  // reverse the point order
        public const byte Script = 0x08;   // not overwritten before completion, except by another SCRIPT path
        public const byte Slide = 0x10;    // unused in the C++, reserved for Lua
        public const byte Patrol = 0x20;   // loop the path while roaming
        public const byte Coords = 0x40;   // walk through to the end once
    }

    // pathpoint_t: a point, an optional facing to take there, and how long to wait there
    public struct PathPoint
    {
        public float X, Y, Z;
        public byte Rotation;
        public long WaitMs;
        public bool SetRotation;

        public PathPoint(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
            Rotation = 0;
            WaitMs = 0;
            SetRotation = false;
        }
    }

    // EntityPathOwner's entity half: what the path moves (a mob, or an NPC given a path by initNpcAi)
    internal interface IPathMover
    {
        float X { get; set; }
        float Y { get; set; }
        float Z { get; set; }
        byte Rotation { get; set; }
        ushort Moving { get; set; }
        byte Speed { get; }
        byte Animation { get; }
        BattleEntity PathBattleTarget { get; } // battleTargetPosition: none for an NPC
        float PathHitboxRadius { get; }        // hitboxRadius: half the model hitbox
        void MarkPositionDirty();              // markPositionDirty: updatemask |= UPDATE_POS
    }

    // pathfind::PathOwner (EntityPathOwner): what the path needs from the zone
    internal interface IPathFindOwner
    {
        Random PathFindRng { get; }
        byte PathFindUpdateSpeed(IPathMover e, bool run); // CBaseEntity::UpdateSpeed / CBattleEntity's
        void PathFindOnPathPoint(IPathMover e);           // luautils::OnPathPoint
        void PathFindOnPathComplete(IPathMover e);        // luautils::OnPathComplete
        DetourNavMesh PathFindNavMesh(IPathMover e);      // the zone's navmesh; null when it has none
    }

    // CPathFind (ai/helpers/pathfind/): the request API (PathTo, PathInRange, PathAround, PathThrough, RoamAround,
    // WarpTo, LimitDistance, ResumePatrol), the per-tick FollowPath loop with FinishedPath's continuation (the next
    // chunk of a far destination, roam turns, patrol loops), StepTo, LookAt, Pause / Unpause, ValidPosition (its
    // small cache) / NearValidPosition, IsPathDirect / BuildDirectPath and Clear; pathfind::Path, ChunkedPath,
    // NavPathBuilder (findPath's off-mesh fallbacks, findRoamTurnPoints) and pathfind::stepTowards.
    //
    // Paths run on the zone's navmesh (DetourNavMesh, navmeshes/<zone>.nav). A zone without one (the C++ would bake
    // it from the collision mesh, which is not ported) walks straight lines: FindPathInternal gives the single
    // point [end], every position is valid, and RoamAround's turn points without a region are uniform points within
    // maxRadius of the anchor. Either way a path is cut where it first leaves the roam region. Not ported: InWater
    // (the collision mesh's terrain) and the zone's proximity grid update in markPositionDirty.
    public sealed class PathFind
    {
        private const int PathFindMaxPoints = 50;               // kMaxPathPoints
        private const int PathFindRecoveryPointAttempts = 8;    // kRecoveryPointAttempts
        private const float PathFindYalmsPerSecondPerSpeed = 1.0f / 17.0f;
        private const float PathFindTickSeconds = 0.4f;         // kLogicUpdateInterval
        private const float PathFindWormSpeed = 20.0f;          // a worm underground has speed 0
        private const float PathFindNearThreshold = 1.0f;       // isNear's kNearThreshold

        private readonly IPathMover owner_;
        private readonly IPathFindOwner host_;

        // pathfind::Path
        private List<PathPoint> points_ = new List<PathPoint>();
        private int cursor_;
        private bool partial_;

        private List<PathPoint> patrol_ = new List<PathPoint>();
        private byte patrolFlags_;
        private readonly List<(float X, float Y, float Z)> turnPoints_ = new List<(float, float, float)>();
        private int currentTurn_;
        private float distanceFromPoint_;
        private byte pathFlags_;
        private ushort roamFlags_;
        private RoamRegion roamRegion_;
        private bool recoveringToRegion_;
        private bool onPoint_;
        private long timeAtPoint_ = long.MinValue;  // timer::time_point::min: not waiting at a point
        private long unpauseTime_ = long.MinValue;
        private float distanceMoved_;
        private float maxDistance_;

        // ChunkedPath: a PathTo's eventual destination, walked one navmesh query at a time
        private const int PathFindMaxStalls = 3; // kMaxStalls
        private bool chunkActive_;
        private (float X, float Y, float Z) chunkTarget_;
        private float chunkRange_, chunkLastDistance_;
        private int chunkStallCount_, chunkCount_;

        // validPositionCache_: the last kValidPositionCacheSize answers, each good for 10 s within 0.05 yalms
        private const int PathFindValidPositionCacheSize = 8;
        private const long PathFindValidPositionMaxAgeMs = 10_000;
        private readonly List<(float X, float Y, float Z, long CheckedAtMs, bool Valid)> validPositionCache_ = new List<(float, float, float, long, bool)>();

        internal PathFind(IPathMover owner, IPathFindOwner host)
        {
            owner_ = owner;
            host_ = host;
            Clear();
        }

        // ---- requests --------------------------------------------------------------------------------------------

        // PathToImpl: a single-destination PathTo always chunks, since one navmesh query cannot span a far destination
        private bool PathToImpl(float x, float y, float z, byte pathFlags)
        {
            pathFlags_ = pathFlags;
            ChunkBegin(x, y, z, 0.0f);
            chunkCount_ = 1;
            bool found = FindPathInternal(owner_.X, owner_.Y, owner_.Z, x, y, z);
            if (!found) Clear();
            return found;
        }

        // RoamAround: a random walk of minTurns..maxTurns legs about the point, kept in the region when there is one
        public bool RoamAround(float x, float y, float z, float maxRadius, byte minTurns, byte maxTurns, ushort roamFlags, RoamRegion region)
        {
            Clear();
            roamFlags_ = roamFlags;
            roamRegion_ = region;
            if (FindRandomPath(x, y, z, maxRadius, minTurns, maxTurns, region)) return true;
            Clear();
            return false;
        }

        // PathTo: never overwrites an active scripted path with a non-scripted one
        public bool PathTo(float x, float y, float z, byte pathFlags = 0)
        {
            if (IsFollowingPath && (pathFlags_ & PathFlag.Script) != 0 && (pathFlags & PathFlag.Script) == 0) return false;
            Clear();
            return PathToImpl(x, y, z, pathFlags);
        }

        // PathInRange: a path that stops `range` short of the point
        public bool PathInRange(float x, float y, float z, float range, byte pathFlags = 0)
        {
            Clear();
            distanceFromPoint_ = range;
            bool found = PathToImpl(x, y, z, pathFlags);
            chunkRange_ = range; // re-chunks stop short too
            PruneTailWithin(range);
            return found;
        }

        // PathAround: a path that ends within distanceFromPoint of the point
        public bool PathAround(float x, float y, float z, float distanceFromPoint, byte pathFlags = 0)
        {
            Clear();
            distanceFromPoint_ = distanceFromPoint;
            return PathToImpl(x, y, z, pathFlags);
        }

        // PathThrough: the given points in order (reversed with REVERSE), looping with PATROL; warn takes AddPoints'
        // too-many-points warning
        public bool PathThrough(List<PathPoint> points, byte pathFlags = 0, Action<string> warn = null)
        {
            Clear();
            pathFlags_ = pathFlags;
            AddPoints(points, (pathFlags_ & PathFlag.Reverse) != 0, warn);
            return true;
        }

        // ResumePatrol: back on the patrol from its nearest waypoint
        public void ResumePatrol()
        {
            if ((patrolFlags_ & PathFlag.Patrol) == 0) return;
            pathFlags_ = patrolFlags_;
            points_ = new List<PathPoint>(patrol_);
            cursor_ = 0;
            partial_ = false;
            float closestSq = float.MaxValue;
            for (int i = 0; i < points_.Count; i++)
            {
                float d = DistanceSquared(owner_.X, owner_.Y, owner_.Z, points_[i].X, points_[i].Y, points_[i].Z, false);
                if (d < closestSq)
                {
                    cursor_ = i;
                    closestSq = d;
                }
            }
        }

        // WarpTo: straight onto a point maxDistance from the target along its facing (nearPosition), facing it
        public bool WarpTo(float x, float y, float z, byte targetRotation, float maxDistance = 2.0f)
        {
            Clear();
            float radians = ZoneLine.RotationToRadian(targetRotation) + MathF.PI;
            owner_.X = x + MathF.Cos(2.0f * MathF.PI - radians) * maxDistance;
            owner_.Y = y;
            owner_.Z = z + MathF.Sin(2.0f * MathF.PI - radians) * maxDistance;
            owner_.Moving = 0;
            LookAt(x, y, z);
            MarkPositionDirty();
            return true;
        }

        // ValidPosition: on the navmesh (always without one), the answer cached for 10 s, a hit promoted to the front
        public bool ValidPosition(float x, float y, float z, long nowMs)
        {
            for (int i = 0; i < validPositionCache_.Count; i++)
            {
                var entry = validPositionCache_[i];
                if (!WithinDistance(entry.X, entry.Y, entry.Z, x, y, z, 0.05f, false) || nowMs - entry.CheckedAtMs >= PathFindValidPositionMaxAgeMs) continue;
                validPositionCache_.RemoveAt(i);
                validPositionCache_.Insert(0, entry);
                return entry.Valid;
            }
            bool valid = host_.PathFindNavMesh(owner_) is not { } nav || nav.ValidPosition(x, y, z);
            if (validPositionCache_.Count == PathFindValidPositionCacheSize) validPositionCache_.RemoveAt(validPositionCache_.Count - 1);
            validPositionCache_.Insert(0, (x, y, z, nowMs, valid));
            return valid;
        }

        // NearValidPosition: walkable ground within 30 yalms (always without a navmesh)
        public bool NearValidPosition(float x, float y, float z) =>
            host_.PathFindNavMesh(owner_) is not { } nav || nav.FindClosestValidPoint(x, y, z) != null;

        // LimitDistance: past this much walking the path completes in place
        public void LimitDistance(float maxLength) => maxDistance_ = maxLength;

        // ---- following -------------------------------------------------------------------------------------------

        // FollowPath: one logic tick of walking, through as many waypoints as the step reaches
        public void FollowPath(long tick, float stepCap = float.MaxValue)
        {
            if (!IsFollowingPath) return;

            // paused: hold, facing the battle target
            if (tick < unpauseTime_)
            {
                if (owner_.PathBattleTarget is { } target) LookAt(target.X, target.Y, target.Z);
                return;
            }

            // waiting out a waypoint's wait
            if (timeAtPoint_ != long.MinValue)
            {
                if (tick < timeAtPoint_) return;
                timeAtPoint_ = long.MinValue;
                cursor_++;
                host_.PathFindOnPathPoint(owner_);
                if (Consumed) FinishedPath();
                return;
            }

            onPoint_ = false;

            if (maxDistance_ != 0 && distanceMoved_ >= maxDistance_)
            {
                Clear();
                onPoint_ = true;
                return;
            }

            // the speed is updated before the budget is taken
            bool speedChange = owner_.Speed != host_.PathFindUpdateSpeed(owner_, (pathFlags_ & PathFlag.Run) != 0);

            float budget = Math.Min(StepBudget(), stepCap);

            // a stop-short path ends once within range of the destination and never steps inside it
            if (distanceFromPoint_ > 0.0f)
            {
                var (dx, dy, dz) = Destination;
                float toDestination = Distance(owner_.X, owner_.Y, owner_.Z, dx, dy, dz, false);
                if (toDestination <= distanceFromPoint_ + 0.2f) cursor_ = points_.Count;
                budget = Math.Min(budget, Math.Max(0.0f, toDestination - distanceFromPoint_));
            }

            while (!Consumed)
            {
                var targetPoint = points_[cursor_];
                bool isFinalPoint = cursor_ == points_.Count - 1;

                if (AtPoint(targetPoint.X, targetPoint.Y, targetPoint.Z, isFinalPoint))
                {
                    onPoint_ = true;
                    if (targetPoint.SetRotation)
                    {
                        owner_.Rotation = targetPoint.Rotation;
                        MarkPositionDirty();
                    }
                    if (targetPoint.WaitMs != 0)
                    {
                        timeAtPoint_ = tick + targetPoint.WaitMs;
                        break;
                    }
                    host_.PathFindOnPathPoint(owner_);
                    cursor_++;
                    continue;
                }

                if (budget <= 0.0f) break;

                // only the last waypoint is stopped short of, so corners are hit precisely
                float stopShort = cursor_ >= points_.Count - 1 ? distanceFromPoint_ : 0.0f;
                float moved = StepTowards(owner_, targetPoint.X, targetPoint.Y, targetPoint.Z, budget, stopShort);
                distanceMoved_ += moved;
                budget -= moved;
            }

            owner_.Moving = (ushort)((owner_.Moving + (speedChange ? 0x28 : 0x35)) % 0x2000);
            MarkPositionDirty();

            if (Consumed)
            {
                FinishedPath();
                onPoint_ = true;
            }
        }

        // StepTo: one step at a point, honouring the configured stop-short distance
        public void StepTo(float x, float y, float z, bool run = false)
        {
            bool speedChange = owner_.Speed != host_.PathFindUpdateSpeed(owner_, run);
            distanceMoved_ += StepTowards(owner_, x, y, z, StepBudget(), distanceFromPoint_);
            owner_.Moving = (ushort)((owner_.Moving + (speedChange ? 0x28 : 0x35)) % 0x2000);
            MarkPositionDirty();
        }

        // StepBudget: a logic tick's yalms at the owner's speed (a worm underground walks at 20)
        private float StepBudget()
        {
            float speed = owner_.Speed == 0 && (roamFlags_ & RoamFlag.Worm) != 0 ? PathFindWormSpeed : owner_.Speed;
            return speed * PathFindYalmsPerSecondPerSpeed * PathFindTickSeconds;
        }

        // LookAt: face the point unless within 0.1 of it flat
        public void LookAt(float x, float y, float z)
        {
            if (!WithinDistance(owner_.X, owner_.Y, owner_.Z, x, y, z, 0.1f, true))
            {
                owner_.Rotation = ZoneHost.WorldAngle(owner_.X, owner_.Z, owner_.Rotation, x, z);
                MarkPositionDirty();
            }
        }

        // Pause: 0 holds until Unpause
        public void Pause(long nowMs, long pauseMs) => unpauseTime_ = pauseMs == 0 ? long.MaxValue : nowMs + pauseMs;

        public void Unpause() => unpauseTime_ = long.MinValue;

        public bool OnPoint => onPoint_;
        public bool IsFollowingPath => points_.Count != 0;
        public bool IsFollowingScriptedPath => IsFollowingPath && (pathFlags_ & PathFlag.Script) != 0;
        public bool IsPatrolling => (patrolFlags_ & PathFlag.Patrol) != 0;
        public float DistanceMoved => distanceMoved_;
        public byte Flags => pathFlags_;

        // GetDestination: a chunked journey's eventual destination, else the path's last point
        public (float X, float Y, float Z) Destination => chunkActive_ ? chunkTarget_ : (points_[^1].X, points_[^1].Y, points_[^1].Z);

        // ChunkCount: the navmesh legs this PathTo journey has taken
        public int ChunkCount => chunkCount_;

        // IsPathDirect: the rest of the path no longer than ratio times the straight line to its end; never for a
        // partial (chunked) path
        public bool IsPathDirect(float ratio = 2.0f)
        {
            if (partial_ || points_.Count == 0 || cursor_ >= points_.Count) return false;
            var end = points_[^1];
            float directDistance = Distance(owner_.X, owner_.Y, owner_.Z, end.X, end.Y, end.Z, false);
            if (directDistance < 0.01f) return true;
            float pathLength = Distance(owner_.X, owner_.Y, owner_.Z, points_[cursor_].X, points_[cursor_].Y, points_[cursor_].Z, false);
            for (int i = cursor_ + 1; i < points_.Count; i++)
                pathLength += Distance(points_[i - 1].X, points_[i - 1].Y, points_[i - 1].Z, points_[i].X, points_[i].Y, points_[i].Z, false);
            return pathLength <= directDistance * ratio;
        }

        // BuildDirectPath: a single waypoint straight at the end, ignoring the navmesh
        public bool BuildDirectPath(float x, float y, float z)
        {
            points_ = new List<PathPoint> { new PathPoint(x, y, z) };
            cursor_ = 0;
            partial_ = false;
            return true;
        }

        // Clear: everything but the patrol, which ResumePatrol picks up again
        public void Clear()
        {
            distanceFromPoint_ = 0;
            pathFlags_ = 0;
            roamFlags_ = 0;
            roamRegion_ = null;
            points_.Clear();
            cursor_ = 0;
            partial_ = false;
            timeAtPoint_ = long.MinValue;
            unpauseTime_ = long.MinValue;
            maxDistance_ = 0;
            distanceMoved_ = 0;
            onPoint_ = true;
            currentTurn_ = 0;
            turnPoints_.Clear();
            recoveringToRegion_ = false;
            chunkActive_ = false;
            chunkTarget_ = default;
            chunkRange_ = chunkLastDistance_ = 0;
            chunkStallCount_ = 0;
            chunkCount_ = 0;
        }

        // ---- internals -------------------------------------------------------------------------------------------

        private bool Consumed => cursor_ >= points_.Count;

        // AtPoint: corners within 0.1, the final point within the stop-short distance plus 0.2
        private bool AtPoint(float x, float y, float z, bool isFinalPoint)
        {
            float threshold = isFinalPoint && distanceFromPoint_ != 0 ? distanceFromPoint_ + 0.2f : 0.1f;
            return WithinDistance(owner_.X, owner_.Y, owner_.Z, x, y, z, threshold, false);
        }

        private void MarkPositionDirty() => owner_.MarkPositionDirty();

        // FindPathInternal: the navmesh path (links only for chasing, not roaming; none found clears the path), or the
        // end itself without a navmesh, cut where it first leaves the roam region unless this walk is bringing the
        // mob back onto it
        private bool FindPathInternal(float sx, float sy, float sz, float ex, float ey, float ez)
        {
            if (Distance(sx, sy, sz, ex, ey, ez, false) < PathFindNearThreshold)
            {
                partial_ = false;
                return false;
            }

            List<PathPoint> built;
            bool isPartial = false;
            if (host_.PathFindNavMesh(owner_) is { } nav)
            {
                bool avoidLinks = roamRegion_ != null && !recoveringToRegion_;
                if (BuildNavPath(nav, sx, sy, sz, ex, ey, ez, owner_.PathHitboxRadius, avoidLinks) is not { } result)
                {
                    points_ = new List<PathPoint>();
                    cursor_ = 0;
                    partial_ = false;
                    return false;
                }
                built = result.Points;
                isPartial = result.IsPartial;
            }
            else built = new List<PathPoint> { new PathPoint(ex, ey, ez) };

            if (roamRegion_ != null && !recoveringToRegion_)
            {
                float fx = sx, fy = sy, fz = sz;
                for (int i = 0; i < built.Count; i++)
                {
                    var to = built[i];
                    float length = Distance(fx, fy, fz, to.X, to.Y, to.Z, true);
                    if (length > 0.0f)
                    {
                        float dirX = (to.X - fx) / length, dirZ = (to.Z - fz) / length;
                        float allowed = roamRegion_.ClampToRegion(fx, fz, dirX, dirZ, length);
                        if (allowed < length)
                        {
                            var edge = new PathPoint(fx + dirX * allowed, fy + (to.Y - fy) * allowed / length, fz + dirZ * allowed);
                            built.RemoveRange(i, built.Count - i);
                            built.Add(edge);
                            isPartial = true;
                            break;
                        }
                    }
                    (fx, fy, fz) = (to.X, to.Y, to.Z);
                }
            }

            points_ = built;
            cursor_ = 0;
            partial_ = isPartial;
            return points_.Count != 0;
        }

        // FindRandomPath with NavPathBuilder::findRoamTurnPoints: the turns, each from the one before inside a
        // region; with none to walk to, a point of the region to recover to; then the path to the first turn
        private bool FindRandomPath(float x, float y, float z, float maxRadius, byte minTurns, byte maxTurns, RoamRegion region)
        {
            var rng = host_.PathFindRng;
            int lowTurns = Math.Max(minTurns, (byte)1);
            int highTurns = Math.Max(maxTurns, lowTurns);
            int desiredTurnCount = rng.Next(lowTurns, highTurns + 1);

            var nav = host_.PathFindNavMesh(owner_);
            turnPoints_.Clear();
            if (region != null)
            {
                var from = (X: x, Y: y, Z: z);
                for (int i = 0; i < desiredTurnCount; i++)
                {
                    if (region.RandomPointAt(from.X, from.Y, from.Z, maxRadius, rng, nav) is not { } candidate) break;
                    turnPoints_.Add(candidate);
                    from = candidate;
                }
            }
            else if (nav != null)
            {
                // findRandomPosition over a tenth of the radius (the polys are dense enough that it saturates anyway),
                // up to twice the turns tried since it may land outside maxRadius; a hard failure bails
                float maxRadiusForPolyQuery = maxRadius / 10.0f;
                int maxAttempts = desiredTurnCount * 2;
                for (int i = 0; i < maxAttempts; i++)
                {
                    if (nav.FindRandomPosition(x, y, z, maxRadiusForPolyQuery, rng) is not { } candidate) return false;
                    if (WithinDistance(x, y, z, candidate.X, candidate.Y, candidate.Z, maxRadius, true)) turnPoints_.Add(candidate);
                    if (turnPoints_.Count >= desiredTurnCount) break;
                }
            }
            else
            {
                // straight-line substitute for findRandomPosition: a uniform point within maxRadius, at the anchor's height
                for (int i = 0; i < desiredTurnCount; i++)
                {
                    double angle = rng.NextDouble() * 2.0 * Math.PI, r = maxRadius * Math.Sqrt(rng.NextDouble());
                    turnPoints_.Add((x + (float)(Math.Cos(angle) * r), y, z + (float)(Math.Sin(angle) * r)));
                }
            }

            if (turnPoints_.Count == 0 && region != null)
            {
                (float X, float Y, float Z)? target = null;
                for (int attempt = 0; attempt < PathFindRecoveryPointAttempts; attempt++)
                {
                    if (region.RandomPoint(rng, nav) is not { } candidate) break;
                    target = candidate;
                    if (WithinDistance(owner_.X, owner_.Y, owner_.Z, candidate.X, candidate.Y, candidate.Z, maxRadius * 2.0f, true)) break;
                }
                if (target is { } t)
                {
                    turnPoints_.Add(t);
                    recoveringToRegion_ = true;
                }
            }

            // the turns are about the anchor, but the walk starts where the owner stands
            if (turnPoints_.Count != 0)
            {
                var first = turnPoints_[0];
                FindPathInternal(owner_.X, owner_.Y, owner_.Z, first.X, first.Y, first.Z);
            }
            return points_.Count != 0;
        }

        // AddPoints: capped at kMaxPathPoints unless patrolling
        private void AddPoints(List<PathPoint> points, bool reverse, Action<string> warn)
        {
            bool isPatrol = (pathFlags_ & PathFlag.Patrol) != 0;
            points = new List<PathPoint>(points);
            if (points.Count > PathFindMaxPoints && !isPatrol)
            {
                warn?.Invoke($"CPathFind::AddPoints Given too many points ({points.Count}). Limiting to max ({PathFindMaxPoints})");
                points.RemoveRange(PathFindMaxPoints, points.Count - PathFindMaxPoints);
            }
            if (reverse) points.Reverse();
            points_ = points;
            cursor_ = 0;
            partial_ = false;
            if (isPatrol)
            {
                patrol_ = new List<PathPoint>(points_);
                patrolFlags_ = pathFlags_;
            }
            else
            {
                patrol_.Clear();
                patrolFlags_ = 0;
            }
        }

        // FinishedPath: onPathComplete, then the next roam turn, the patrol loop while roaming, or done
        private void FinishedPath()
        {
            // a partial chunk silently asks for the next one and fires no onPathComplete
            if (chunkActive_ && partial_ && TryRequestNextChunk()) return;

            host_.PathFindOnPathComplete(owner_);
            ++currentTurn_;
            if (currentTurn_ < turnPoints_.Count)
            {
                var next = turnPoints_[currentTurn_];
                if (!FindPathInternal(owner_.X, owner_.Y, owner_.Z, next.X, next.Y, next.Z)) Clear();
                return;
            }
            recoveringToRegion_ = false;
            if (IsPatrolling && owner_.Animation == Anim.None)
            {
                cursor_ = 0;
                currentTurn_ = 0;
                return;
            }
            Clear();
        }

        // ChunkedPath::begin
        private void ChunkBegin(float x, float y, float z, float range)
        {
            chunkActive_ = true;
            chunkTarget_ = (x, y, z);
            chunkRange_ = range;
            chunkLastDistance_ = 0.0f;
            chunkStallCount_ = 0;
        }

        // TryRequestNextChunk with ChunkedPath::evaluate: arrived within max(0.5, range), or stalled after three
        // chunks gaining under half a yalm, ends the journey; else the next leg, pruned again for a stop-short range
        private bool TryRequestNextChunk()
        {
            float distanceToTarget = Distance(owner_.X, owner_.Y, owner_.Z, chunkTarget_.X, chunkTarget_.Y, chunkTarget_.Z, false);
            if (distanceToTarget <= Math.Max(0.5f, chunkRange_)) return false;
            if (chunkLastDistance_ > 0.0f && distanceToTarget >= chunkLastDistance_ - 0.5f)
            {
                if (++chunkStallCount_ >= PathFindMaxStalls) return false;
            }
            else chunkStallCount_ = 0;
            chunkLastDistance_ = distanceToTarget;

            if (!FindPathInternal(owner_.X, owner_.Y, owner_.Z, chunkTarget_.X, chunkTarget_.Y, chunkTarget_.Z)) return false;
            ++chunkCount_;
            if (chunkRange_ > 0.0f)
            {
                distanceFromPoint_ = chunkRange_;
                PruneTailWithin(chunkRange_);
            }
            return true;
        }

        // NavPathBuilder::findPath: findPath searches 2.5 yalms about each end, so an off-mesh end paths to its
        // nearest point within 30, then an off-mesh start paths from its own; a complete path more than 0.1 short of
        // the end gains the nearest point when that is closer
        private static NavPathResult BuildNavPath(DetourNavMesh nav, float sx, float sy, float sz, float ex, float ey, float ez, float clearance, bool avoidLinks)
        {
            var result = nav.FindPath(sx, sy, sz, ex, ey, ez, clearance, avoidLinks);
            if (result == null && nav.FindClosestValidPoint(ex, ey, ez) is { } closestEnd &&
                Distance(sx, sy, sz, closestEnd.X, closestEnd.Y, closestEnd.Z, false) >= PathFindNearThreshold)
                result = nav.FindPath(sx, sy, sz, closestEnd.X, closestEnd.Y, closestEnd.Z, clearance, avoidLinks);
            if (result == null && nav.FindClosestValidPoint(sx, sy, sz) is { } closestStart &&
                Distance(closestStart.X, closestStart.Y, closestStart.Z, ex, ey, ez, false) >= PathFindNearThreshold)
                result = nav.FindPath(closestStart.X, closestStart.Y, closestStart.Z, ex, ey, ez, clearance, avoidLinks);
            if (result == null) return null;

            if (!result.IsPartial && result.Points.Count != 0)
            {
                var last = result.Points[^1];
                float lastWaypointGap = Distance(last.X, last.Y, last.Z, ex, ey, ez, false);
                if (lastWaypointGap > 0.1f && nav.FindClosestValidPoint(ex, ey, ez) is { } closest &&
                    Distance(closest.X, closest.Y, closest.Z, ex, ey, ez, false) < lastWaypointGap - 0.1f)
                    result.Points.Add(new PathPoint(closest.X, closest.Y, closest.Z));
            }
            return result;
        }

        // Path::pruneTailWithin: drop tail waypoints within `within` of the destination, keeping real bends
        private void PruneTailWithin(float within)
        {
            if (points_.Count == 0) return;
            var dest = points_[^1];
            while (points_.Count > 2)
            {
                int penultimateIndex = points_.Count - 2;
                var penultimate = points_[penultimateIndex];
                if (Distance(dest.X, dest.Y, dest.Z, penultimate.X, penultimate.Y, penultimate.Z, false) > within) break;
                var previous = points_[penultimateIndex - 1];
                int bend = Melee.AngleDifference(ZoneHost.WorldAngle(previous.X, previous.Z, 0, penultimate.X, penultimate.Z),
                                                 ZoneHost.WorldAngle(penultimate.X, penultimate.Z, 0, dest.X, dest.Z));
                if (bend > 2 || bend < -2) break;
                points_.RemoveAt(penultimateIndex);
            }
        }

        // pathfind::stepTowards: face the point, then one step along the facing, settling stopShort away when that
        // is in reach; the height follows the slope, snapping within 0.5 and never overshooting. Returns the distance
        internal static float StepTowards(IPathMover m, float x, float y, float z, float stepDistance, float stopShort)
        {
            float distanceTo = Distance(m.X, m.Y, m.Z, x, y, z, false);
            float diffY = y - m.Y;
            if (!WithinDistance(m.X, m.Y, m.Z, x, y, z, 0.1f, true)) m.Rotation = ZoneHost.WorldAngle(m.X, m.Z, m.Rotation, x, z);
            float radians = 2.0f * MathF.PI - m.Rotation / 256.0f * 2.0f * MathF.PI;

            void StepY()
            {
                if (Math.Abs(diffY) <= 0.5f)
                {
                    m.Y = y;
                    return;
                }
                float flatDistance = Distance(m.X, m.Y, m.Z, x, y, z, true);
                if (flatDistance <= 0.0f)
                {
                    m.Y = y;
                    return;
                }
                float startY = m.Y;
                float newY = startY + stepDistance * (y - startY) / flatDistance;
                m.Y = Math.Clamp(newY, Math.Min(startY, y), Math.Max(startY, y));
            }

            if (distanceTo <= stopShort + stepDistance)
            {
                float advance = Math.Max(distanceTo - stopShort, 0.0f);
                if (stopShort == 0.0f) (m.X, m.Y, m.Z) = (x, y, z);
                else
                {
                    m.X += MathF.Cos(radians) * advance;
                    m.Z += MathF.Sin(radians) * advance;
                    StepY();
                }
                return advance;
            }
            m.X += MathF.Cos(radians) * stepDistance;
            m.Z += MathF.Sin(radians) * stepDistance;
            StepY();
            return stepDistance;
        }

        private static float DistanceSquared(float ax, float ay, float az, float bx, float by, float bz, bool ignoreVertical)
        {
            float dx = ax - bx, dy = ignoreVertical ? 0 : ay - by, dz = az - bz;
            return dx * dx + dy * dy + dz * dz;
        }

        private static float Distance(float ax, float ay, float az, float bx, float by, float bz, bool ignoreVertical) =>
            MathF.Sqrt(DistanceSquared(ax, ay, az, bx, by, bz, ignoreVertical));

        private static bool WithinDistance(float ax, float ay, float az, float bx, float by, float bz, float within, bool ignoreVertical) =>
            DistanceSquared(ax, ay, az, bx, by, bz, ignoreVertical) <= within * within;
    }
}
