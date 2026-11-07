using System;
using System.Collections.Generic;
using System.IO;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;

namespace XI.Zone
{
    // navmesh.h's PathResult: isPartial marks a path that stopped short of the end, so callers can chunk
    public sealed class NavPathResult
    {
        public List<PathPoint> Points = new List<PathPoint>();
        public bool IsPartial;
    }

    // navmesh/detour_navmesh.cpp: a zone's navmesh/<name>.nav (the MSET set of Detour tiles, 32-bit poly refs) and
    // the queries the pathfinder, roam regions and scripts run on it, through DotRecast (the C# port of the same
    // recastnavigation Detour). Detour is right-handed Y-up and FFXI left-handed, so Y and Z flip on the way in and
    // out. Not ported: the NavMeshBuilder (baking a mesh from the zone's collision mesh when no file exists, save,
    // installNavMesh, rebuildNavmesh): a zone without a file has no navmesh and the pathfinder walks straight lines.
    public sealed class DetourNavMesh
    {
        private const int NavmeshSetMagic = 'M' << 24 | 'S' << 16 | 'E' << 8 | 'T'; // 'MSET'
        private const int NavmeshSetVersion = 1;

        // SamplePolyFlags (RecastDemo's Sample.h)
        private const int PolyFlagsJump = 0x0008;
        private const int PolyFlagsDisabled = 0x0010;
        private const int PolyFlagsAll = 0xFFFF;

        private const int PathPolyLimit = 512;  // kPathPolyLimit
        private const int MaxQueryPolys = 256;  // kMaxQueryPolys: moveAlongSurface's visited buffer

        // Detour search extents, used to snap a query point onto the nearest poly
        private static readonly RcVec3f SmallPolyPickExt = new RcVec3f(0.5f, 1.0f, 0.5f);
        private static readonly RcVec3f PolyPickExt = new RcVec3f(2.5f, 5.0f, 2.5f);
        private static readonly RcVec3f LargePolyPickExt = new RcVec3f(30.0f, 60.0f, 30.0f);

        private readonly ushort zoneId_;
        private readonly Action<string> log_;
        private readonly DtNavMesh navMesh_;
        private readonly DtNavMeshQuery query_;
        private readonly long[] polyData_ = new long[PathPolyLimit];
        private readonly DtStraightPath[] straightPath_ = new DtStraightPath[PathPolyLimit];
        private readonly float[] straightFloats_ = new float[PathPolyLimit * 3];
        private readonly long[] visited_ = new long[MaxQueryPolys];

        private DetourNavMesh(ushort zoneId, DtNavMesh navMesh, Action<string> log)
        {
            zoneId_ = zoneId;
            log_ = log ?? (_ => { });
            navMesh_ = navMesh;
            query_ = new DtNavMeshQuery(navMesh_);
            FlagLinks();
        }

        public ushort ZoneId => zoneId_;

        // load: the MSET header (magic, version), then each tile; null when the file is missing or not a navmesh set
        public static DetourNavMesh Load(string filename, ushort zoneId, Action<string> log)
        {
            if (!File.Exists(filename)) return null;
            try
            {
                using var stream = File.OpenRead(filename);
                using var reader = new BinaryReader(stream);
                if (stream.Length < 8 || reader.ReadInt32() != NavmeshSetMagic || reader.ReadInt32() != NavmeshSetVersion) return null;
                stream.Position = 0;
                var mesh = new DtMeshSetReader().Read32Bit(reader, DtDetour.DT_VERTS_PER_POLYGON);
                return mesh == null ? null : new DetourNavMesh(zoneId, mesh, log);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or IndexOutOfRangeException or EndOfStreamException)
            {
                log?.Invoke($"DetourNavMesh::load Could not initialize detour for ({filename}): {e.Message}");
                return null;
            }
        }

        // toDetour / fromDetour: Y and Z are sign-flipped between the two
        private static RcVec3f ToDetour(float x, float y, float z) => new RcVec3f(x, -y, -z);

        private static (float X, float Y, float Z) FromDetour(RcVec3f p) => (p.X, -p.Y, -p.Z);

        private static (float X, float Y, float Z) FromDetour(float[] p, int at) => (p[at], -p[at + 1], -p[at + 2]);

        // makeFilter: include everything, exclude the disabled polys, and the jump links when avoiding them
        private static DtQueryDefaultFilter MakeFilter(bool avoidLinks)
        {
            var filter = new DtQueryDefaultFilter();
            filter.SetIncludeFlags(PolyFlagsAll);
            filter.SetExcludeFlags(avoidLinks ? PolyFlagsDisabled | PolyFlagsJump : PolyFlagsDisabled);
            return filter;
        }

        // flagLinks: every off-mesh connection poly gets the JUMP flag, so a roam can exclude them
        private void FlagLinks()
        {
            for (int i = 0; i < navMesh_.GetMaxTiles(); i++)
            {
                var tile = navMesh_.GetTile(i);
                if (tile?.data?.header == null) continue;
                long polyBase = navMesh_.GetPolyRefBase(tile);
                for (int c = 0; c < tile.data.header.offMeshConCount; c++)
                {
                    int poly = tile.data.offMeshCons[c].poly;
                    navMesh_.SetPolyFlags(polyBase | (long)poly, tile.data.polys[poly].flags | PolyFlagsJump);
                }
            }
        }

        // lookupPoly: the nearest poly within the extents, and the point on it
        private bool LookupPoly(RcVec3f pos, RcVec3f extents, IDtQueryFilter filter, out long polyRef, out RcVec3f nearest)
        {
            var status = query_.FindNearestPoly(pos, extents, filter, out polyRef, out nearest, out _);
            if (status.Failed())
            {
                log_($"DetourNavMesh::lookupPoly findNearestPoly failed ({zoneId_}): {status.Value:x}");
                return false;
            }
            return navMesh_.IsValidPolyRef(polyRef);
        }

        // findPath: the poly corridor, straightened through portals shrunk for the body (pathinset) or by Detour's
        // own string pull; a partial path that ends more than 5 yalms short loses its best-guess last waypoint. The
        // start point is dropped (the entity is standing on it)
        public NavPathResult FindPath(float sx, float sy, float sz, float ex, float ey, float ez, float clearance, bool avoidLinks)
        {
            if (float.IsNaN(sx) || float.IsNaN(sy) || float.IsNaN(sz) || float.IsNaN(ex) || float.IsNaN(ey) || float.IsNaN(ez))
            {
                log_($"DetourNavMesh::findPath NaN position detected ({zoneId_})");
                return null;
            }

            var filter = MakeFilter(avoidLinks);
            if (!LookupPoly(ToDetour(sx, sy, sz), PolyPickExt, filter, out long startRef, out var startNearest)) return null;
            if (!LookupPoly(ToDetour(ex, ey, ez), PolyPickExt, filter, out long endRef, out var endNearest)) return null;

            var status = query_.FindPath(startRef, endRef, startNearest, endNearest, filter, polyData_, out int pathPolyCount, PathPolyLimit);
            if (status.Failed())
            {
                log_($"DetourNavMesh::findPath findPath error ({zoneId_}): {status.Value:x}");
                return null;
            }
            bool detourPartial = status.IsPartial();
            if (pathPolyCount <= 0)
            {
                log_($"DetourNavMesh::findPath Unable to generate polys for path ({sx}, {sy}, {sz})->({ex}, {ey}, {ez}) ({zoneId_})");
                return null;
            }

            int straightPathCount = 0;
            if (clearance > 0.0f)
                straightPathCount = PathInset.PullString(query_, filter, polyData_, pathPolyCount, startNearest, endNearest,
                                                         clearance + PathInset.BerthMargin, straightFloats_, PathPolyLimit);

            // a body that cannot be kept off the walls still has to walk somewhere
            if (straightPathCount == 0)
            {
                status = query_.FindStraightPath(startNearest, endNearest, polyData_, pathPolyCount, straightPath_, out straightPathCount, PathPolyLimit, 0);
                if (status.Failed())
                {
                    log_($"DetourNavMesh::findPath findStraightPath error ({zoneId_}): {status.Value:x}");
                    return null;
                }
                for (int i = 0; i < straightPathCount; i++)
                {
                    straightFloats_[i * 3] = straightPath_[i].pos.X;
                    straightFloats_[i * 3 + 1] = straightPath_[i].pos.Y;
                    straightFloats_[i * 3 + 2] = straightPath_[i].pos.Z;
                }
            }
            if (straightPathCount <= 0) return null;

            int last = (straightPathCount - 1) * 3;
            float dx = straightFloats_[last] - endNearest.X;
            float dy = straightFloats_[last + 1] - endNearest.Y;
            float dz = straightFloats_[last + 2] - endNearest.Z;

            // a straight corridor yields two points however far short it stopped, so the count says nothing about arrival
            bool isPartial = false;
            if (detourPartial && dx * dx + dy * dy + dz * dz > 5.0f * 5.0f)
            {
                isPartial = true;
                if (straightPathCount > 2) straightPathCount--;
            }

            // only the start position: no usable path
            if (straightPathCount <= 1) return null;

            var result = new NavPathResult { IsPartial = isPartial };
            for (int i = 1; i < straightPathCount; i++)
            {
                var (x, y, z) = FromDetour(straightFloats_, i * 3);
                result.Points.Add(new PathPoint(x, y, z));
            }
            return result;
        }

        // findRandomPosition: a random point on the polys around the start within maxRadius
        public (float X, float Y, float Z)? FindRandomPosition(float x, float y, float z, float maxRadius, Random rng)
        {
            var filter = MakeFilter(false);
            var spos = ToDetour(x, y, z);
            if (!LookupPoly(spos, PolyPickExt, filter, out long startRef, out _)) return null;
            var status = query_.FindRandomPointAroundCircle(startRef, spos, maxRadius, filter, new NavRand(rng), out _, out var randomPt);
            if (status.Failed())
            {
                log_($"DetourNavMesh::findRandomPosition error ({zoneId_}): {status.Value:x}");
                return null;
            }
            return FromDetour(randomPt);
        }

        // validPosition: a poly within the small extents
        public bool ValidPosition(float x, float y, float z) => LookupPoly(ToDetour(x, y, z), SmallPolyPickExt, MakeFilter(false), out _, out _);

        // findClosestValidPoint: the nearest point on a poly within the large extents (30 yalms flat)
        public (float X, float Y, float Z)? FindClosestValidPoint(float x, float y, float z)
        {
            if (!LookupPoly(ToDetour(x, y, z), LargePolyPickExt, MakeFilter(false), out _, out var nearest)) return null;
            return FromDetour(nearest);
        }

        // findFurthestValidPoint: how far a walk along the surface from the start toward the end gets
        public (float X, float Y, float Z)? FindFurthestValidPoint(float sx, float sy, float sz, float ex, float ey, float ez)
        {
            var filter = MakeFilter(false);
            if (!LookupPoly(ToDetour(sx, sy, sz), LargePolyPickExt, filter, out long startRef, out var startNearest)) return null;
            var status = query_.MoveAlongSurface(startRef, startNearest, ToDetour(ex, ey, ez), filter, out var result, visited_, out _, MaxQueryPolys);
            if (status.Failed()) return null;
            return FromDetour(result);
        }

        // moveAlongSurface: false when the start is off the mesh (the caller free-moves back on); the height is the
        // poly's the walk ended on, as Detour hands back the start's
        public bool MoveAlongSurface(float sx, float sy, float sz, float ex, float ey, float ez, out (float X, float Y, float Z) reached)
        {
            reached = default;
            var filter = MakeFilter(false);
            if (!LookupPoly(ToDetour(sx, sy, sz), PolyPickExt, filter, out long startRef, out var startNearest)) return false;
            var status = query_.MoveAlongSurface(startRef, startNearest, ToDetour(ex, ey, ez), filter, out var result, visited_, out int visitedCount, MaxQueryPolys);
            if (status.Failed()) return false;
            if (visitedCount > 0 && query_.GetPolyHeight(visited_[visitedCount - 1], result, out float height).Succeeded()) result.Y = height;
            reached = FromDetour(result);
            return true;
        }

        // snapToValidPosition: onto the nearest poly, a cheap small search first, then 30 yalms; unchanged when none
        public void SnapToValidPosition(ref float x, ref float y, ref float z)
        {
            var filter = MakeFilter(false);
            var spos = ToDetour(x, y, z);
            if (!LookupPoly(spos, PolyPickExt, filter, out _, out var nearest) && !LookupPoly(spos, LargePolyPickExt, filter, out _, out nearest)) return;
            (x, y, z) = FromDetour(nearest);
        }

        // xirand::GetRandomNumber(1.0f) for Detour's random queries
        private sealed class NavRand : IRcRand
        {
            private readonly Random rng_;
            public NavRand(Random rng) => rng_ = rng;
            public float Next() => (float)rng_.NextDouble();
            public double NextDouble() => rng_.NextDouble();
            public int NextInt32() => rng_.Next();
        }
    }
}
