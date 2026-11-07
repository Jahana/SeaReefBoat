using System;
using System.Collections.Generic;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;

namespace XI.Zone
{
    // navmesh/path_inset.cpp: a radius-0 navmesh puts every corner Detour returns on a wall vertex, so each portal of
    // the corridor is shrunk by the body's berth before the string pull, keeping corners off the walls without losing
    // any passage. portalBetween and funnel are Detour's own (getPortalPoints, findStraightPath's string pull),
    // Recast & Detour (c) 2009-2010 Mikko Mononen, zlib licence.
    internal static class PathInset
    {
        public const float BerthMargin = 1.5f; // kBerthMargin: how far past the body's edge a path is kept

        private const int MaxWallSegments = 6 * 4; // DT_VERTS_PER_POLYGON * 4

        internal struct Wall
        {
            public RcVec3f A, B;
        }

        internal struct Portal
        {
            public RcVec3f Left, Right;
        }

        // ---- DetourCommon --------------------------------------------------------------------------------------------

        private static RcVec3f Lerp(RcVec3f a, RcVec3f b, float t) =>
            new RcVec3f(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        private static float Dist2D(RcVec3f a, RcVec3f b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        private static float Len(RcVec3f v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

        private static float Dot2D(RcVec3f a, RcVec3f b) => a.X * b.X + a.Z * b.Z;

        // dtVequal: within 1/16384 of each other
        private static bool Equal(RcVec3f a, RcVec3f b)
        {
            const float threshold = 1.0f / 16384.0f * (1.0f / 16384.0f);
            float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            return dx * dx + dy * dy + dz * dz < threshold;
        }

        private static float TriArea2D(RcVec3f a, RcVec3f b, RcVec3f c)
        {
            float abx = b.X - a.X, abz = b.Z - a.Z;
            float acx = c.X - a.X, acz = c.Z - a.Z;
            return acx * abz - abx * acz;
        }

        private static float DistancePtSegSqr2D(RcVec3f pt, RcVec3f p, RcVec3f q)
        {
            float pqx = q.X - p.X, pqz = q.Z - p.Z;
            float dx = pt.X - p.X, dz = pt.Z - p.Z;
            float d = pqx * pqx + pqz * pqz;
            float t = pqx * dx + pqz * dz;
            if (d > 0) t /= d;
            t = Math.Clamp(t, 0.0f, 1.0f);
            dx = p.X + t * pqx - pt.X;
            dz = p.Z + t * pqz - pt.Z;
            return dx * dx + dz * dz;
        }

        private static RcVec3f Vertex(float[] verts, int index) => new RcVec3f(verts[index * 3], verts[index * 3 + 1], verts[index * 3 + 2]);

        // ---- the inset -----------------------------------------------------------------------------------------------

        // wallFrom: the flat unit direction along a wall away from the vertex it meets
        private static bool WallFrom(Wall wall, RcVec3f vertex, out RcVec3f away)
        {
            if (Dist2D(wall.A, vertex) < 0.01f) away = new RcVec3f(wall.B.X - wall.A.X, 0.0f, wall.B.Z - wall.A.Z);
            else if (Dist2D(wall.B, vertex) < 0.01f) away = new RcVec3f(wall.A.X - wall.B.X, 0.0f, wall.A.Z - wall.B.Z);
            else
            {
                away = default;
                return false;
            }
            float length = Len(away);
            if (length < 1e-4f) return false;
            away = new RcVec3f(away.X / length, away.Y / length, away.Z / length);
            return true;
        }

        // vertexOffset: how far along the portal a crossing point must sit to clear the walls meeting it at `vertex`
        private static float VertexOffset(RcVec3f vertex, RcVec3f toward, List<Wall> walls, float berth)
        {
            var along = new RcVec3f(toward.X - vertex.X, 0.0f, toward.Z - vertex.Z);
            float length = Len(along);
            if (length < 1e-4f) return 0.0f;
            along = new RcVec3f(along.X / length, along.Y / length, along.Z / length);

            float offset = 0.0f;
            foreach (var wall in walls)
            {
                if (!WallFrom(wall, vertex, out var away)) continue;
                // while the wall runs alongside, distance from it grows at the sine of the angle; once it falls
                // behind, at the full rate
                float clearanceAlong = berth;
                float alongPortal = Dot2D(along, away);
                if (alongPortal > 0.0f)
                {
                    float sinAngle = MathF.Sqrt(Math.Max(1.0f - alongPortal * alongPortal, 0.0f));
                    clearanceAlong = length;
                    if (sinAngle > 1e-3f) clearanceAlong = berth / sinAngle;
                }
                offset = Math.Max(offset, clearanceAlong);
            }
            return offset;
        }

        // collectWalls: the polygon's edges with nothing on the other side
        private static void CollectWalls(DtNavMeshQuery query, IDtQueryFilter filter, long polyRef, List<Wall> walls)
        {
            Span<RcSegmentVert> segments = stackalloc RcSegmentVert[MaxWallSegments];
            int count = 0;
            if (query.GetPolyWallSegments(polyRef, filter, segments, Span<long>.Empty, ref count, MaxWallSegments).Failed()) return;
            for (int i = 0; i < count; i++) walls.Add(new Wall { A = segments[i].vmin, B = segments[i].vmax });
        }

        // portalBetween: the edge of `from` whose link points at `to` (a tile-border link's part of it), or an
        // off-mesh link's connection point; both polygons' walls collected for shrinkPortal
        private static bool PortalBetween(DtNavMeshQuery query, IDtQueryFilter filter, long from, long to, out Portal portal, List<Wall> walls)
        {
            walls.Clear();
            portal = default;
            var mesh = query.GetAttachedNavMesh();
            if (mesh.GetTileAndPolyByRef(from, out var fromTile, out var fromPoly).Failed() ||
                mesh.GetTileAndPolyByRef(to, out var toTile, out var toPoly).Failed())
                return false;

            static bool PointPortal(DtMeshTile tile, DtPoly poly, long other, ref Portal p)
            {
                for (int link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                {
                    if (tile.links[link].refs != other) continue;
                    var vertex = Vertex(tile.data.verts, poly.verts[tile.links[link].edge]);
                    p.Left = vertex;
                    p.Right = vertex;
                    return true;
                }
                return false;
            }

            if (fromPoly.GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION) return PointPortal(fromTile, fromPoly, to, ref portal);
            if (toPoly.GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION) return PointPortal(toTile, toPoly, from, ref portal);

            DtLink found = null;
            for (int i = fromPoly.firstLink; i != DtDetour.DT_NULL_LINK; i = fromTile.links[i].next)
            {
                if (fromTile.links[i].refs == to)
                {
                    found = fromTile.links[i];
                    break;
                }
            }
            if (found == null) return false;

            portal.Left = Vertex(fromTile.data.verts, fromPoly.verts[found.edge]);
            portal.Right = Vertex(fromTile.data.verts, fromPoly.verts[(found.edge + 1) % fromPoly.vertCount]);

            // a tile-border link covers only part of the edge
            if (found.side != 0xff && (found.bmin != 0 || found.bmax != 255))
            {
                var a = portal.Left;
                var b = portal.Right;
                portal.Left = Lerp(a, b, found.bmin / 255.0f);
                portal.Right = Lerp(a, b, found.bmax / 255.0f);
            }

            CollectWalls(query, filter, from, walls);
            CollectWalls(query, filter, to, walls);
            return true;
        }

        // shrinkPortal: each end moved along the portal until it clears the walls meeting it by `berth`; a portal too
        // narrow for that collapses to the point splitting what each side asked for
        internal static void ShrinkPortal(ref Portal portal, List<Wall> walls, float berth)
        {
            float length = Dist2D(portal.Left, portal.Right);
            if (length < 1e-4f) return;

            float fromLeft = VertexOffset(portal.Left, portal.Right, walls, berth);
            float fromRight = VertexOffset(portal.Right, portal.Left, walls, berth);
            if (fromLeft + fromRight < length)
            {
                var left = Lerp(portal.Left, portal.Right, fromLeft / length);
                var right = Lerp(portal.Left, portal.Right, 1.0f - fromRight / length);
                portal.Left = left;
                portal.Right = right;
                return;
            }
            if (fromLeft + fromRight < 1e-4f) return;
            portal.Left = Lerp(portal.Left, portal.Right, fromLeft / (fromLeft + fromRight));
            portal.Right = portal.Left;
        }

        // funnel: Detour's string pull over the given portals (portal portalCount is the end itself); out takes
        // x, y, z triples, returns how many points
        internal static int Funnel(RcVec3f start, List<Portal> portals, RcVec3f end, float[] output, int maxPoints)
        {
            int count = 0;

            // append unless it repeats the last point; false once the buffer is full
            bool Emit(RcVec3f p)
            {
                if (count == 0 || !Equal(new RcVec3f(output[(count - 1) * 3], output[(count - 1) * 3 + 1], output[(count - 1) * 3 + 2]), p))
                {
                    if (count >= maxPoints) return false;
                    output[count * 3] = p.X;
                    output[count * 3 + 1] = p.Y;
                    output[count * 3 + 2] = p.Z;
                    ++count;
                }
                return count < maxPoints;
            }

            if (!Emit(start)) return count;

            int portalCount = portals.Count;
            var apex = start;
            var left = start;
            var right = start;
            int leftIndex = 0, rightIndex = 0;

            for (int i = 0; i <= portalCount; ++i)
            {
                var pl = end;
                var pr = end;
                if (i < portalCount)
                {
                    pl = portals[i].Left;
                    pr = portals[i].Right;
                    // standing on the first portal already
                    if (i == 0 && DistancePtSegSqr2D(apex, pl, pr) < 0.001f * 0.001f) continue;
                }

                if (TriArea2D(apex, right, pr) <= 0.0f)
                {
                    if (Equal(apex, right) || TriArea2D(apex, left, pr) > 0.0f)
                    {
                        right = pr;
                        rightIndex = i;
                    }
                    else
                    {
                        if (!Emit(left)) return count;
                        apex = left;
                        right = apex;
                        rightIndex = leftIndex;
                        i = leftIndex;
                        continue;
                    }
                }

                if (TriArea2D(apex, left, pl) >= 0.0f)
                {
                    if (Equal(apex, left) || TriArea2D(apex, right, pl) < 0.0f)
                    {
                        left = pl;
                        leftIndex = i;
                    }
                    else
                    {
                        if (!Emit(right)) return count;
                        apex = right;
                        left = apex;
                        leftIndex = rightIndex;
                        i = rightIndex;
                    }
                }
            }

            Emit(end);
            return count;
        }

        // pullString: both ends clamped to the corridor, every portal shrunk, then the funnel; 0 when the corridor
        // cannot be walked (the caller falls back to Detour)
        public static int PullString(DtNavMeshQuery query, IDtQueryFilter filter, long[] corridor, int corridorCount, RcVec3f start, RcVec3f end,
                                     float berth, float[] output, int maxPoints)
        {
            if (corridorCount == 0) return 0;
            if (query.ClosestPointOnPolyBoundary(corridor[0], start, out var startClamped).Failed() ||
                query.ClosestPointOnPolyBoundary(corridor[corridorCount - 1], end, out var endClamped).Failed())
                return 0;

            var walls = new List<Wall>();
            var portals = new List<Portal>(corridorCount);
            for (int i = 0; i + 1 < corridorCount; i++)
            {
                if (!PortalBetween(query, filter, corridor[i], corridor[i + 1], out var portal, walls))
                {
                    // the corridor breaks here, so end at the polygon reached, as Detour does
                    if (query.ClosestPointOnPolyBoundary(corridor[i], end, out endClamped).Failed()) return 0;
                    break;
                }
                ShrinkPortal(ref portal, walls, berth);
                portals.Add(portal);
            }
            return Funnel(startClamped, portals, endClamped, output, maxPoints);
        }
    }
}
