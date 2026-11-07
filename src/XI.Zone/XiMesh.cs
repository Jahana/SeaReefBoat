using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;

namespace XI.Zone
{
    // enums/terrain_type.h
    public enum TerrainType : byte
    {
        Object = 0, Path = 1, Grass = 2, Sand = 3, Snow = 4, Stone = 5, Metal = 6, Wood = 7,
        ShallowWater = 8, DeepWater = 9, Unknown = 10, None = 0xFF,
    }

    // ximesh_structs.h's CellHit: what floor is under a point
    public readonly struct CellHit
    {
        public readonly TerrainType Type;
        public readonly byte MapId;
        public readonly bool Roofed, Barrier;
        public readonly float Y; // the floor's world height at the point (Y is negative-up)

        public CellHit(TerrainType type, byte mapId, bool roofed, bool barrier, float y)
        {
            Type = type;
            MapId = mapId;
            Roofed = roofed;
            Barrier = barrier;
            Y = y;
        }
    }

    // RayHitInfo: where a ray first met geometry (object space)
    public readonly struct RayHitInfo
    {
        public readonly Vector3 Intersection;
        public readonly float DistanceSq;
        public readonly int Placement;
        public readonly TerrainType Type;
        public readonly bool Barrier;

        public RayHitInfo(Vector3 intersection, float distanceSq, int placement, TerrainType type, bool barrier)
        {
            Intersection = intersection;
            DistanceSq = distanceSq;
            Placement = placement;
            Type = type;
            Barrier = barrier;
        }
    }

    // ximesh/ximesh_impl.cpp: a zone's collision mesh (ximeshes/<name>.ximesh, zlib). After decompression a 20-byte
    // header (grid width / height, the block and placement sections, their counts, wideSearch), a cell offset table,
    // and per cell its (block, placement) entries; a block is local-space triangles with a material and barrier bit
    // each, a placement a 3x3 column-major rotation and translation with its map id and roof flag. The grid's cells are
    // 4 yalms, centred on the origin. query finds the floor under a point, rayIntersect any geometry between two
    // (transparent barriers skipped by default), getPositionInfo the nearest hit along a vertical ray.
    // Coordinates are the game's own (no axis flip, Y negative-up).
    public sealed class XiMesh
    {
        private const float CellSize = 4.0f;
        private const int CullMinTriangles = 1;
        private const int MaxDecompressedSize = 64 * 1024 * 1024;
        private const float RayTriEpsilon = 0.0000001f, ParallelEpsilon = 1e-6f, ZeroDirEpsilon = 1e-8f,
            LargeReciprocal = 1e30f, FloorEpsilon = 0.01f, AabbPad = 0.1f;

        private sealed class Block
        {
            public float[] Vertices;    // x, y, z interleaved
            public ushort[] Indices;    // 3 per triangle
            public byte[] Metas;        // material:4 barrier:1 per triangle
            public bool HasBarriers;
            public Vector3 AabbMin, AabbMax;
        }

        private sealed class Placement
        {
            public readonly float[] Rotation = new float[9];
            public readonly float[] Translation = new float[3];
            public byte MapId;
            public bool Roofed;
            public float YMin = float.MaxValue, YMax = float.MinValue;
        }

        // TransformationMatrix: rows 0-2 the 3x3, row 3 the translation
        private sealed class Matrix43
        {
            public readonly float[,] E = new float[4, 3];
        }

        private ushort gridWidth_, gridHeight_;
        private uint wideSearch_;
        private readonly List<Block> blocks_ = new List<Block>();
        private readonly List<Placement> placements_ = new List<Placement>();
        private readonly List<(ushort Block, ushort Placement)> entries_ = new List<(ushort, ushort)>();
        private (int Offset, int Count)[] cells_;
        private (float Min, float Max)[] cellRanges_;
        private Matrix43[] w2os_;
        private bool[] placementFlips_;

        public ushort GridWidth => gridWidth_;
        public ushort GridHeight => gridHeight_;
        public int BlockCount => blocks_.Count;
        public int PlacementCount => placements_.Count;

        // XiMeshImpl(filename): throws when the file cannot be read or parsed
        public static XiMesh Load(string filename)
        {
            var mesh = new XiMesh();
            string error = mesh.LoadFile(filename);
            if (error != null) throw new InvalidDataException($"Failed to load {filename}: {error}");
            return mesh;
        }

        private static byte[] Inflate(byte[] raw)
        {
            using var input = new MemoryStream(raw);
            using var z = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var chunk = new byte[32 * 1024];
            int n;
            while ((n = z.Read(chunk, 0, chunk.Length)) > 0)
            {
                output.Write(chunk, 0, n);
                if (output.Length > MaxDecompressedSize) return null;
            }
            return output.ToArray();
        }

        private static ushort U16(byte[] b, long off) => off + 2 > b.Length ? (ushort)0 : BitConverter.ToUInt16(b, (int)off);
        private static uint U32(byte[] b, long off) => off + 4 > b.Length ? 0u : BitConverter.ToUInt32(b, (int)off);
        private static uint RoundUp4(uint v) => (v + 3) & ~3u;

        private string LoadFile(string filename)
        {
            byte[] raw;
            try { raw = File.ReadAllBytes(filename); }
            catch (Exception e) { return $"read failed ({e.Message})"; }
            byte[] buf;
            try { buf = Inflate(raw); }
            catch (InvalidDataException) { buf = null; }
            if (buf == null || buf.Length == 0) return "zlib decompression failed";
            if (buf.Length < 20) return "file too small";

            gridWidth_ = U16(buf, 0);
            gridHeight_ = U16(buf, 2);
            wideSearch_ = U32(buf, 16);
            if (gridWidth_ == 0 || gridHeight_ == 0) return $"invalid grid {gridWidth_}x{gridHeight_}";
            int cellCount = gridWidth_ * gridHeight_;
            cells_ = new (int, int)[cellCount];

            var blockCache = new Dictionary<uint, ushort>();
            var placeCache = new Dictionary<uint, ushort>();

            ushort? Block(uint at)
            {
                if (blockCache.TryGetValue(at, out var idx)) return idx;
                ushort vertexCount = U16(buf, at), triangleCount = U16(buf, at + 2), barrierFlag = U16(buf, at + 4);
                uint vertexBytes = vertexCount * 12u, indexBytes = triangleCount * 6u, metaBytes = triangleCount;
                uint vertexOffset = at + 8, indexOffset = RoundUp4(vertexOffset + vertexBytes), metaOffset = RoundUp4(indexOffset + indexBytes);
                if (vertexOffset + vertexBytes > buf.Length || indexOffset + indexBytes > buf.Length || metaOffset + metaBytes > buf.Length)
                    return null;
                var block = new Block
                {
                    HasBarriers = barrierFlag > 0,
                    Vertices = new float[vertexCount * 3],
                    Indices = new ushort[triangleCount * 3],
                    Metas = new byte[metaBytes],
                };
                Buffer.BlockCopy(buf, (int)vertexOffset, block.Vertices, 0, (int)vertexBytes);
                Buffer.BlockCopy(buf, (int)indexOffset, block.Indices, 0, (int)indexBytes);
                Buffer.BlockCopy(buf, (int)metaOffset, block.Metas, 0, (int)metaBytes);
                // the local-space bounds, padded, for the ray broad-phase
                if (vertexCount > 0)
                {
                    var mn = new Vector3(block.Vertices[0], block.Vertices[1], block.Vertices[2]);
                    var mx = mn;
                    for (int vi = 1; vi < vertexCount; vi++)
                    {
                        var v = new Vector3(block.Vertices[vi * 3], block.Vertices[vi * 3 + 1], block.Vertices[vi * 3 + 2]);
                        mn = Vector3.Min(mn, v);
                        mx = Vector3.Max(mx, v);
                    }
                    block.AabbMin = mn - new Vector3(AabbPad);
                    block.AabbMax = mx + new Vector3(AabbPad);
                }
                idx = (ushort)blocks_.Count;
                blockCache[at] = idx;
                blocks_.Add(block);
                return idx;
            }

            ushort? PlacementAt(uint at)
            {
                if (placeCache.TryGetValue(at, out var idx)) return idx;
                uint flags = U32(buf, at);
                // PlacementFlags: padding0:2 roofed:1 mapIdLow:3 padding1:20 mapIdHigh:2 padding2:4
                var p = new Placement
                {
                    Roofed = (flags >> 2 & 1) != 0,
                    MapId = (byte)((flags >> 26 & 3) << 3 | flags >> 3 & 7),
                };
                if (at + 4 + 48 > buf.Length) return null;
                Buffer.BlockCopy(buf, (int)at + 4, p.Rotation, 0, 36);
                Buffer.BlockCopy(buf, (int)at + 40, p.Translation, 0, 12);
                idx = (ushort)placements_.Count;
                placeCache[at] = idx;
                placements_.Add(p);
                return idx;
            }

            for (int cell = 0; cell < cellCount; cell++)
            {
                uint cellOffset = U32(buf, 20 + cell * 4L);
                if (cellOffset == 0 || cellOffset + 6 > buf.Length) continue;
                ushort entryCount = U16(buf, cellOffset + 4);
                cells_[cell] = (entries_.Count, 0);
                for (int e = 0; e < entryCount; e++)
                {
                    long entryOffset = cellOffset + 6 + e * 8L;
                    if (entryOffset + 8 > buf.Length) break;
                    var blockIdx = Block(U32(buf, entryOffset));
                    var placementIdx = PlacementAt(U32(buf, entryOffset + 4));
                    if (blockIdx == null || placementIdx == null) return "corrupt block/placement data";
                    entries_.Add((blockIdx.Value, placementIdx.Value));
                    cells_[cell].Count++;
                }
            }

            // each placement's world Y bounds (query culling)
            for (int cell = 0; cell < cellCount; cell++)
            {
                var (offset, count) = cells_[cell];
                for (int r = 0; r < count; r++)
                {
                    var (b, pi) = entries_[offset + r];
                    var block = blocks_[b];
                    var place = placements_[pi];
                    for (int v = 0; v < block.Vertices.Length; v += 3)
                    {
                        float y = Transform(place, block.Vertices, v).Y;
                        place.YMin = Math.Min(place.YMin, y);
                        place.YMax = Math.Max(place.YMax, y);
                    }
                }
            }

            // world->object matrices, winding flips, each cell's Y range (ray queries)
            w2os_ = new Matrix43[placements_.Count];
            placementFlips_ = new bool[placements_.Count];
            for (int i = 0; i < placements_.Count; i++)
            {
                var o2w = BuildO2W(placements_[i]);
                w2os_[i] = Inverted(o2w);
                placementFlips_[i] = Determinant(o2w) > 0.0f;
            }
            cellRanges_ = new (float, float)[cellCount];
            for (int cell = 0; cell < cellCount; cell++)
            {
                float min = float.MaxValue, max = float.MinValue;
                var (offset, count) = cells_[cell];
                for (int r = 0; r < count; r++)
                {
                    var place = placements_[entries_[offset + r].Placement];
                    min = Math.Min(min, place.YMin);
                    max = Math.Max(max, place.YMax);
                }
                cellRanges_[cell] = (min, max);
            }
            return null;
        }

        // local to world space
        private static Vector3 Transform(Placement p, float[] v, int at)
        {
            float[] r = p.Rotation, t = p.Translation;
            float x = v[at], y = v[at + 1], z = v[at + 2];
            return new Vector3(r[0] * x + r[3] * y + r[6] * z + t[0],
                               r[1] * x + r[4] * y + r[7] * z + t[1],
                               r[2] * x + r[5] * y + r[8] * z + t[2]);
        }

        private static Matrix43 BuildO2W(Placement p)
        {
            var m = new Matrix43();
            for (int i = 0; i < 9; i++) m.E[i / 3, i % 3] = p.Rotation[i];
            for (int i = 0; i < 3; i++) m.E[3, i] = p.Translation[i];
            return m;
        }

        private static float Determinant(Matrix43 m)
        {
            var el = m.E;
            return el[0, 0] * el[1, 1] * el[2, 2]
                   + el[0, 1] * el[1, 2] * el[2, 0]
                   + el[0, 2] * el[1, 0] * el[2, 1]
                   - el[0, 0] * el[1, 2] * el[2, 1]
                   - el[0, 1] * el[1, 0] * el[2, 2]
                   - el[0, 2] * el[1, 1] * el[2, 0];
        }

        // TransformationMatrix::getInverted (a singular one stays zero)
        private static Matrix43 Inverted(Matrix43 m)
        {
            var el = m.E;
            var inv = new Matrix43();
            var o = inv.E;
            float t11 = -el[1, 2] * el[2, 1] + el[1, 1] * el[2, 2];
            float t12 = el[0, 2] * el[2, 1] - el[0, 1] * el[2, 2];
            float t13 = -el[0, 2] * el[1, 1] + el[0, 1] * el[1, 2];
            float det = el[0, 0] * t11 + el[1, 0] * t12 + el[2, 0] * t13;
            if (det == 0) return inv;
            float invDet = 1 / det;
            o[0, 0] = t11 * invDet;
            o[1, 0] = (el[1, 2] * el[2, 0] - el[1, 0] * el[2, 2]) * invDet;
            o[2, 0] = (-el[1, 1] * el[2, 0] + el[1, 0] * el[2, 1]) * invDet;
            o[3, 0] = (el[1, 2] * el[2, 1] * el[3, 0] - el[1, 1] * el[2, 2] * el[3, 0] - el[1, 2] * el[2, 0] * el[3, 1] + el[1, 0] * el[2, 2] * el[3, 1] + el[1, 1] * el[2, 0] * el[3, 2] - el[1, 0] * el[2, 1] * el[3, 2]) * invDet;
            o[0, 1] = t12 * invDet;
            o[1, 1] = (-el[0, 2] * el[2, 0] + el[0, 0] * el[2, 2]) * invDet;
            o[2, 1] = (el[0, 1] * el[2, 0] - el[0, 0] * el[2, 1]) * invDet;
            o[3, 1] = (el[0, 1] * el[2, 2] * el[3, 0] - el[0, 2] * el[2, 1] * el[3, 0] + el[0, 2] * el[2, 0] * el[3, 1] - el[0, 0] * el[2, 2] * el[3, 1] - el[0, 1] * el[2, 0] * el[3, 2] + el[0, 0] * el[2, 1] * el[3, 2]) * invDet;
            o[0, 2] = t13 * invDet;
            o[1, 2] = (el[0, 2] * el[1, 0] - el[0, 0] * el[1, 2]) * invDet;
            o[2, 2] = (-el[0, 1] * el[1, 0] + el[0, 0] * el[1, 1]) * invDet;
            o[3, 2] = (el[0, 2] * el[1, 1] * el[3, 0] - el[0, 1] * el[1, 2] * el[3, 0] - el[0, 2] * el[1, 0] * el[3, 1] + el[0, 0] * el[1, 2] * el[3, 1] + el[0, 1] * el[1, 0] * el[3, 2] - el[0, 0] * el[1, 1] * el[3, 2]) * invDet;
            return inv;
        }

        private static Vector3 Apply(Matrix43 m, Vector3 v)
        {
            var el = m.E;
            return new Vector3(el[0, 0] * v.X + el[1, 0] * v.Y + el[2, 0] * v.Z + el[3, 0],
                               el[0, 1] * v.X + el[1, 1] * v.Y + el[2, 1] * v.Z + el[3, 1],
                               el[0, 2] * v.X + el[1, 2] * v.Y + el[2, 2] * v.Z + el[3, 2]);
        }

        private static Vector3 ApplyDirection(Matrix43 m, Vector3 d)
        {
            var el = m.E;
            return new Vector3(el[0, 0] * d.X + el[1, 0] * d.Y + el[2, 0] * d.Z,
                               el[0, 1] * d.X + el[1, 1] * d.Y + el[2, 1] * d.Z,
                               el[0, 2] * d.X + el[1, 2] * d.Y + el[2, 2] * d.Z);
        }

        private static Vector3 VertexAt(Block b, ushort i) => new Vector3(b.Vertices[i * 3], b.Vertices[i * 3 + 1], b.Vertices[i * 3 + 2]);

        // the slab test: does the object-space segment (t in [0, 1]) overlap the box?
        private static bool SegmentHitsAabb(Vector3 o, Vector3 invD, Vector3 bmin, Vector3 bmax)
        {
            float tx1 = (bmin.X - o.X) * invD.X, tx2 = (bmax.X - o.X) * invD.X;
            float tmin = Math.Min(tx1, tx2), tmax = Math.Max(tx1, tx2);
            float ty1 = (bmin.Y - o.Y) * invD.Y, ty2 = (bmax.Y - o.Y) * invD.Y;
            tmin = Math.Max(tmin, Math.Min(ty1, ty2));
            tmax = Math.Min(tmax, Math.Max(ty1, ty2));
            float tz1 = (bmin.Z - o.Z) * invD.Z, tz2 = (bmax.Z - o.Z) * invD.Z;
            tmin = Math.Max(tmin, Math.Min(tz1, tz2));
            tmax = Math.Min(tmax, Math.Max(tz1, tz2));
            return tmax >= Math.Max(tmin, 0.0f) && tmin <= 1.0f;
        }

        private static Vector3 ReciprocalDir(Vector3 d) => new Vector3(
            Math.Abs(d.X) > ZeroDirEpsilon ? 1.0f / d.X : LargeReciprocal,
            Math.Abs(d.Y) > ZeroDirEpsilon ? 1.0f / d.Y : LargeReciprocal,
            Math.Abs(d.Z) > ZeroDirEpsilon ? 1.0f / d.Z : LargeReciprocal);

        // Moller-Trumbore over the segment origin + vector * t, t in (eps, 1]
        private static Vector3? RayIntersectTriangle(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 origin, Vector3 vector)
        {
            var edge1 = v2 - v1;
            var edge2 = v3 - v1;
            var h = Vector3.Cross(vector, edge2);
            float a = Vector3.Dot(edge1, h);
            if (a > -RayTriEpsilon && a < RayTriEpsilon) return null;
            float f = 1.0f / a;
            var s = origin - v1;
            float u = f * Vector3.Dot(s, h);
            if (u < 0.0f || u > 1.0f) return null;
            var q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(vector, q);
            if (v < 0.0f || u + v > 1.0f) return null;
            float t = f * Vector3.Dot(edge2, q);
            if (t > RayTriEpsilon && t <= 1.0f) return origin + vector * t;
            return null;
        }

        // dtClosestHeightPointTriangle
        private static bool ClosestHeightPointTriangle(float px, float pz, Vector3 a, Vector3 b, Vector3 c, out float h)
        {
            h = 0;
            const float Eps = 1e-6f;
            float v0x = c.X - a.X, v0y = c.Y - a.Y, v0z = c.Z - a.Z;
            float v1x = b.X - a.X, v1y = b.Y - a.Y, v1z = b.Z - a.Z;
            float v2x = px - a.X, v2z = pz - a.Z;
            float denom = v0x * v1z - v0z * v1x;
            if (MathF.Abs(denom) < Eps) return false;
            float u = v1z * v2x - v1x * v2z;
            float v = v0x * v2z - v0z * v2x;
            if (denom < 0)
            {
                denom = -denom;
                u = -u;
                v = -v;
            }
            if (u >= 0.0f && v >= 0.0f && u + v <= denom)
            {
                h = a.Y + (v0y * u + v1y * v) / denom;
                return true;
            }
            return false;
        }

        // world position to the cell grid (4 yalms a cell, the origin at the grid's middle)
        private (int X, int Z) WorldToCell(float x, float z) =>
            ((int)MathF.Floor(x / CellSize) + gridWidth_ / 2, (int)MathF.Floor(z / CellSize) + gridHeight_ / 2);

        // query: the triangle under (x, z) closest above y, from the point's cell or else its nearest neighbour's
        public CellHit? Query(float x, float y, float z)
        {
            var (cx, cz) = WorldToCell(x, z);
            CellHit? SearchCell(int cellX, int cellZ)
            {
                if (cellX < 0 || cellX >= gridWidth_ || cellZ < 0 || cellZ >= gridHeight_) return null;
                var (offset, count) = cells_[cellZ * gridWidth_ + cellX];
                if (count == 0) return null;
                CellHit? best = null;
                for (int r = 0; r < count; r++)
                {
                    var (bi, pi) = entries_[offset + r];
                    var block = blocks_[bi];
                    var place = placements_[pi];
                    // a placement entirely above the point
                    if (place.YMax < y - FloorEpsilon) continue;
                    for (int tri = 0; tri < block.Metas.Length; tri++)
                    {
                        var w0 = Transform(place, block.Vertices, block.Indices[tri * 3] * 3);
                        var w1 = Transform(place, block.Vertices, block.Indices[tri * 3 + 1] * 3);
                        var w2 = Transform(place, block.Vertices, block.Indices[tri * 3 + 2] * 3);
                        if (!ClosestHeightPointTriangle(x, z, w0, w1, w2, out float triY)) continue;
                        // the closest triangle above the point (Y is negative-up)
                        if (triY >= y - FloorEpsilon && (best == null || triY < best.Value.Y))
                        {
                            byte meta = block.Metas[tri];
                            best = new CellHit((TerrainType)(meta & 0xF), place.MapId, place.Roofed, (meta >> 4 & 1) != 0, triY);
                        }
                    }
                }
                return best;
            }

            if (SearchCell(cx, cz) is { } hit) return hit;
            CellHit? nearest = null;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (SearchCell(cx + dx, cz + dz) is { } h && (nearest == null || h.Y < nearest.Value.Y)) nearest = h;
                }
            return nearest;
        }

        public TerrainType GetTerrainAt(float x, float y, float z) => Query(x, y, z)?.Type ?? TerrainType.None;

        public byte GetFloorId(float x, float y, float z) => Query(x, y, z)?.MapId ?? 0;

        // rayIntersect: geometry between start and end? The cells around the start, then a grid walk toward the end
        // checking the next row or column of cells as it crosses each line.
        public bool RayIntersect(Vector3 start, Vector3 end, bool ignoreTransparentBarriers = true)
        {
            int cellSearchDiff = wideSearch_ > 0 ? 2 : 1;
            var (col, row) = WorldToCell(start.X, start.Z);
            var yRange = (Math.Min(start.Y, end.Y), Math.Max(start.Y, end.Y));
            int row1 = Math.Max(0, row - cellSearchDiff), row2 = Math.Min(gridHeight_ - 1, row + cellSearchDiff);
            int col1 = Math.Max(0, col - cellSearchDiff), col2 = Math.Min(gridWidth_ - 1, col + cellSearchDiff);
            for (int r = row1; r <= row2; r++)
                for (int c = col1; c <= col2; c++)
                    if (RayIntersectCell(start, end, yRange, r * gridWidth_ + c, ignoreTransparentBarriers, null)) return true;

            var (endCol, endRow) = WorldToCell(end.X, end.Z);
            float dx = end.X - start.X, dz = end.Z - start.Z;
            if (Math.Abs(dx) < ParallelEpsilon && Math.Abs(dz) < ParallelEpsilon) return false;
            int stepCol = dx > 0 ? 1 : -1, stepRow = dz > 0 ? 1 : -1;
            float deltaX = Math.Abs(dx) < ParallelEpsilon ? float.PositiveInfinity : Math.Abs(CellSize / dx);
            float deltaZ = Math.Abs(dz) < ParallelEpsilon ? float.PositiveInfinity : Math.Abs(CellSize / dz);
            float nextX, nextZ;
            if (dx > 0) nextX = ((col + 1) * CellSize - gridWidth_ * (CellSize / 2.0f) - start.X) / dx;
            else if (dx < 0) nextX = (col * CellSize - gridWidth_ * (CellSize / 2.0f) - start.X) / dx;
            else nextX = float.PositiveInfinity;
            if (dz > 0) nextZ = ((row + 1) * CellSize - gridHeight_ * (CellSize / 2.0f) - start.Z) / dz;
            else if (dz < 0) nextZ = (row * CellSize - gridHeight_ * (CellSize / 2.0f) - start.Z) / dz;
            else nextZ = float.PositiveInfinity;

            float currentDistance = 0.0f;
            int maxSteps = gridWidth_ + gridHeight_ + 2, steps = 0;
            while ((col != endCol || row != endRow) && currentDistance < 1.0f)
            {
                if (++steps > maxSteps) break;
                // to whichever grid line is closer
                if (nextX < nextZ)
                {
                    currentDistance = nextX;
                    nextX += deltaX;
                    col += stepCol;
                    row1 = Math.Max(0, row - cellSearchDiff);
                    row2 = Math.Min(gridHeight_ - 1, row + cellSearchDiff);
                    col1 = col2 = Math.Clamp(col + stepCol, 0, gridWidth_ - 1);
                }
                else
                {
                    currentDistance = nextZ;
                    nextZ += deltaZ;
                    row += stepRow;
                    row1 = row2 = Math.Clamp(row + stepRow, 0, gridHeight_ - 1);
                    col1 = Math.Max(0, col - cellSearchDiff);
                    col2 = Math.Min(gridWidth_ - 1, col + cellSearchDiff);
                }
                if (currentDistance <= 1.0f)
                    for (int r = row1; r <= row2; r++)
                        for (int c = col1; c <= col2; c++)
                            if (RayIntersectCell(start, end, yRange, r * gridWidth_ + c, ignoreTransparentBarriers, null)) return true;
            }
            return false;
        }

        // getPositionInfo: the nearest hit along the vertical ray from position.y + start to position.y + start + end
        public RayHitInfo? GetPositionInfo(Vector3 position, float yStart, float yEnd, bool ignoreTransparentBarriers = true)
        {
            int cellSearchDiff = wideSearch_ > 0 ? 2 : 1;
            var start = new Vector3(position.X, position.Y + yStart, position.Z);
            var end = new Vector3(position.X, position.Y + yStart + yEnd, position.Z);
            var (col, row) = WorldToCell(start.X, start.Z);
            var yRange = (Math.Min(start.Y, end.Y), Math.Max(start.Y, end.Y));
            int row1 = Math.Max(0, row - cellSearchDiff), row2 = Math.Min(gridHeight_ - 1, row + cellSearchDiff);
            int col1 = Math.Max(0, col - cellSearchDiff), col2 = Math.Min(gridWidth_ - 1, col + cellSearchDiff);
            var closest = new RayHitInfo?[1];
            for (int r = row1; r <= row2; r++)
                for (int c = col1; c <= col2; c++)
                    RayIntersectCell(start, end, yRange, r * gridWidth_ + c, ignoreTransparentBarriers, closest);
            return closest[0];
        }

        // rayIntersectCell, or with `closest` rayIntersectCellHitInfo (every triangle, keeping the nearest hit)
        private bool RayIntersectCell(Vector3 start, Vector3 end, (float Min, float Max) yRange, int cellIdx, bool ignoreTransparentBarriers, RayHitInfo?[] closest)
        {
            if (cellIdx < 0 || cellIdx >= cells_.Length) return false;
            var (offset, count) = cells_[cellIdx];
            if (count == 0) return false;
            var cRange = cellRanges_[cellIdx];
            if (yRange.Min > cRange.Max || yRange.Max < cRange.Min) return false;
            var worldDiff = end - start;
            int lastPlacement = -1;
            Vector3 oStart = default, oDiff = default, oInvDiff = default;
            int v1Off = 0, v3Off = 2;
            for (int r = 0; r < count; r++)
            {
                var (bi, pi) = entries_[offset + r];
                var block = blocks_[bi];
                if (block.HasBarriers && ignoreTransparentBarriers) continue;
                var place = placements_[pi];
                if (yRange.Min > place.YMax || yRange.Max < place.YMin) continue;
                if (pi != lastPlacement)
                {
                    lastPlacement = pi;
                    oStart = Apply(w2os_[pi], start);
                    oDiff = ApplyDirection(w2os_[pi], worldDiff);
                    oInvDiff = ReciprocalDir(oDiff);
                    bool flip = placementFlips_[pi];
                    v1Off = flip ? 2 : 0;
                    v3Off = flip ? 0 : 2;
                }
                int triCount = block.Metas.Length;
                // broad-phase: the whole block missed
                if (triCount > CullMinTriangles && !SegmentHitsAabb(oStart, oInvDiff, block.AabbMin, block.AabbMax)) continue;
                for (int tri = 0; tri < triCount; tri++)
                {
                    int bse = tri * 3;
                    var hit = RayIntersectTriangle(VertexAt(block, block.Indices[bse + v1Off]), VertexAt(block, block.Indices[bse + 1]),
                        VertexAt(block, block.Indices[bse + v3Off]), oStart, oDiff);
                    if (hit == null) continue;
                    if (closest == null) return true;
                    float distanceSq = (oStart - hit.Value).LengthSquared();
                    if (closest[0] == null || closest[0].Value.DistanceSq > distanceSq)
                    {
                        byte meta = block.Metas[tri];
                        closest[0] = new RayHitInfo(hit.Value, distanceSq, pi, (TerrainType)(meta & 0xF), (meta >> 4 & 1) != 0);
                    }
                }
            }
            return false;
        }
    }

    // los_cache.h: an entity's last 8 line-of-sight answers for 2.5 s, keyed by zone and both ends on a 2-yalm grid,
    // most recently used first
    public sealed class LineOfSightCache
    {
        private const int Capacity = 8;
        private const long TtlMs = 2500;
        private const float YalmsPerCell = 2.0f;

        private readonly (long Key1, long Key2, bool Result, long Expiry)[] entries_ = new (long, long, bool, long)[Capacity];
        private int size_;

        private static (long, long) MakeKey(Vector3 src, Vector3 dst, ushort zone)
        {
            static long Q(float v) => (ushort)(short)(v / YalmsPerCell);
            return ((long)zone << 48 | Q(src.X) << 32 | Q(src.Y) << 16 | Q(src.Z), Q(dst.X) << 32 | Q(dst.Y) << 16 | Q(dst.Z));
        }

        public bool? Get(Vector3 src, Vector3 dst, ushort zone, long nowMs)
        {
            var key = MakeKey(src, dst, zone);
            for (int i = 0; i < size_; i++)
            {
                if ((entries_[i].Key1, entries_[i].Key2) != key) continue;
                if (nowMs >= entries_[i].Expiry) return null;
                bool result = entries_[i].Result;
                MoveToFront(i);
                return result;
            }
            return null;
        }

        public void Put(Vector3 src, Vector3 dst, ushort zone, bool result, long nowMs)
        {
            var key = MakeKey(src, dst, zone);
            long expiry = nowMs + TtlMs;
            for (int i = 0; i < size_; i++)
            {
                if ((entries_[i].Key1, entries_[i].Key2) != key) continue;
                entries_[i].Result = result;
                entries_[i].Expiry = expiry;
                MoveToFront(i);
                return;
            }
            if (size_ < Capacity) size_++;
            // shift down, dropping the least recent when full
            for (int i = size_ - 1; i > 0; i--) entries_[i] = entries_[i - 1];
            entries_[0] = (key.Item1, key.Item2, result, expiry);
        }

        private void MoveToFront(int i)
        {
            if (i == 0) return;
            var tmp = entries_[i];
            for (int j = i; j > 0; j--) entries_[j] = entries_[j - 1];
            entries_[0] = tmp;
        }
    }
}
