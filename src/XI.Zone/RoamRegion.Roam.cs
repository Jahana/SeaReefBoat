using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // roam_region.cpp's queries for roaming and walking home: closestPoint, distanceOutside, clampToRegion,
    // randomPoint / acceptPoint, randomPointAt with sampleStepDistance, and hasWalkableSurface (the check when the
    // zone's navmesh loads). With a navmesh a point takes the mesh's height and is dropped when the snap carries it
    // away or out, and randomPointAt walks the surface (moveAlongSurface); without one (a zone with no .nav) the
    // C++'s "no mesh" branch keeps the sample. closestPoint scans the ring edges (outer and holes) where the C++
    // scans its triangles' edges; the C++ notes that from outside a boundary edge always wins, so the answer is the
    // same.
    public sealed partial class RoamRegion
    {
        private const int RoamSampleAttempts = 8;     // kSampleAttempts
        private const float RoamMinStep = 2.0f;       // kMinStep: retail's 10th percentile leg
        private const float RoamEdgeMargin = 0.05f;   // kEdgeMargin: rest a little inside the edge
        private const double RoamStepSigma = 0.40;    // kStepSigma: legs are log-normal about RoamDistance
        private const float RoamStepCap = 2.0f;       // kStepCap: and end near twice it
        private const int RoamValidationSamples = 64; // kValidationSamples
        private const float RoamSnapTolerance = 2.5f; // kSnapTolerance

        private List<((float X, float Y, float Z) A, (float X, float Y, float Z) B)> roamEdges_;

        // edges_: every ring's edges, the outer ring and the holes
        private List<((float X, float Y, float Z) A, (float X, float Y, float Z) B)> RoamEdges()
        {
            if (roamEdges_ != null) return roamEdges_;
            var edges = new List<((float, float, float), (float, float, float))>();
            void Ring(List<(float X, float Y, float Z)> ring)
            {
                for (int k = 0; k < ring.Count; k++) edges.Add((ring[k], ring[(k + 1) % ring.Count]));
            }
            Ring(outer_);
            foreach (var hole in holes_) Ring(hole);
            return roamEdges_ = edges;
        }

        // closestPoint: the position itself when inside, else the nearest point on an edge (height lerped along it)
        public (float X, float Y, float Z) ClosestPoint(float x, float y, float z)
        {
            if (triangles_.Count == 0 || Contains(x, z)) return (x, y, z);
            var closest = (X: x, Y: y, Z: z);
            float closestDistance = float.MaxValue;
            foreach (var (a, b) in RoamEdges())
            {
                // dtDistancePtSegSqr2D, then dtVlerp
                float px = b.X - a.X, pz = b.Z - a.Z;
                float d = px * px + pz * pz;
                float t = px * (x - a.X) + pz * (z - a.Z);
                if (d > 0) t /= d;
                t = Math.Clamp(t, 0.0f, 1.0f);
                float dx = a.X + t * px - x, dz = a.Z + t * pz - z;
                float distance = dx * dx + dz * dz;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
                }
            }
            return closest;
        }

        // distanceOutside: the flat distance to closestPoint, 0 inside
        public float DistanceOutside(float x, float y, float z)
        {
            var closest = ClosestPoint(x, y, z);
            float dx = closest.X - x, dz = closest.Z - z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        // clampToRegion: how far a leg from `from` along the flat unit direction stays inside, a margin short of the
        // first ring edge it crosses; 0 from outside or heading straight out from the boundary
        public float ClampToRegion(float fromX, float fromZ, float dirX, float dirZ, float distance)
        {
            if (!Contains(fromX, fromZ)) return 0.0f;
            if (!Contains(fromX + dirX * RoamEdgeMargin, fromZ + dirZ * RoamEdgeMargin)) return 0.0f;
            float nearest = distance;
            foreach (var (a, b) in RoamEdges())
            {
                float ex = b.X - a.X, ez = b.Z - a.Z;
                float denom = dirX * ez - dirZ * ex;
                if (Math.Abs(denom) < 1e-6f) continue;
                float dx = a.X - fromX, dz = a.Z - fromZ;
                float t = (dx * ez - dz * ex) / denom;
                float u = (dx * dirZ - dz * dirX) / denom;
                if (t > 1e-4f && t < nearest && u >= 0.0f && u <= 1.0f) nearest = t;
            }
            if (nearest >= distance) return distance;
            return Math.Max(nearest - RoamEdgeMargin, 0.0f);
        }

        // hasWalkableSurface: one of 64 samples lands on the mesh
        public bool HasWalkableSurface(DetourNavMesh navMesh, Random rng) => RandomPoint(rng, navMesh, RoamValidationSamples) != null;

        // acceptPoint: the mesh's height over the authored one, unless the snap travelled more than 2.5 yalms or left
        // the region; with no mesh the geometry has the final say
        private (float X, float Y, float Z)? AcceptPoint((float X, float Y, float Z) point, DetourNavMesh navMesh)
        {
            if (navMesh == null) return point;
            if (navMesh.FindClosestValidPoint(point.X, point.Y, point.Z) is not { } snapped) return null;
            if (Math.Abs(snapped.X - point.X) > RoamSnapTolerance || Math.Abs(snapped.Z - point.Z) > RoamSnapTolerance || !Contains(snapped.X, snapped.Z))
                return null;
            return snapped;
        }

        // randomPoint: samples until one survives the mesh check
        public (float X, float Y, float Z)? RandomPoint(Random rng, DetourNavMesh navMesh = null, int attempts = RoamSampleAttempts)
        {
            if (triangles_.Count == 0) return null;
            for (int attempt = 0; attempt < attempts; attempt++)
                if (AcceptPoint(SamplePoint(rng), navMesh) is { } point) return point;
            return null;
        }

        // randomPointAt: a random heading, a log-normal step about `distance` cut short at the edge; with a mesh the
        // walk follows the surface, stopping at the first wall, and is retried when it ends outside or got nowhere;
        // with none the target at the start's height. Draws whose step falls under kMinStep are retried,
        // kSampleAttempts in all
        public (float X, float Y, float Z)? RandomPointAt(float fromX, float fromY, float fromZ, float distance, Random rng, DetourNavMesh navMesh = null)
        {
            if (triangles_.Count == 0) return null;
            for (int attempt = 0; attempt < RoamSampleAttempts; attempt++)
            {
                float angle = (float)(rng.NextDouble() * 2.0 * Math.PI);
                float dirX = MathF.Cos(angle), dirZ = MathF.Sin(angle);
                float step = ClampToRegion(fromX, fromZ, dirX, dirZ, RoamSampleStepDistance(distance, rng));
                if (step < RoamMinStep) continue;
                var target = (X: fromX + dirX * step, Y: fromY, Z: fromZ + dirZ * step);
                if (navMesh == null) return target;
                if (!navMesh.MoveAlongSurface(fromX, fromY, fromZ, target.X, target.Y, target.Z, out var reached)) continue;
                if (!Contains(reached.X, reached.Z)) continue;
                float dx = reached.X - fromX, dz = reached.Z - fromZ;
                if (MathF.Sqrt(dx * dx + dz * dz) < RoamMinStep) continue;
                return reached;
            }
            return null;
        }

        // sampleStepDistance: median * e^N(0, kStepSigma), capped at kStepCap times the median. xirand::GetNormalNumber
        // is std::normal_distribution; this draws the normal by Box-Muller from the same Random
        private static float RoamSampleStepDistance(float median, Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2) * RoamStepSigma;
            float drawn = (float)(median * Math.Exp(normal));
            return Math.Min(drawn, median * RoamStepCap);
        }
    }
}
