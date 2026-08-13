// Copyright (c) 2025-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.CSG;

using System;
using System.Collections.Generic;

using RedBolt.Graphics;
using RedBolt.Math;

public static partial class Csg
{
    private static bool IsCsgSimplifyDiagnosticsActive() => false;
    private static void CsgSimplifyDiagLog(string text)
    {
    }

    private sealed class SimplePolygonFailureDetails
    {
        public enum FailureType
        {
            None,
            DuplicateQuantizedVertex,
            SelfIntersection
        }

        public FailureType Type;

        // DuplicateQuantizedVertex
        public Vector3i DuplicateKey;
        public int DuplicateFirstIndex;
        public int DuplicateSecondIndex;

        // SelfIntersection
        public int EdgeA0;
        public int EdgeA1;
        public int EdgeB0;
        public int EdgeB1;
    }

    private sealed class PlaneGroup
    {
        public Plane Plane;
        public uint Shared;
        public List<CsgPolygon> Polys = [];
    }

    private readonly record struct EdgeRef(int PolyIndex, int EdgeStart);

    private readonly record struct CanceledEdge(uint From, uint To);

    private sealed class VertexGrid
    {
        private float _cellSize;
        private float _invCellSize;
        private readonly Dictionary<Vector3i, List<Vector3f>> _grid = [];

        public void Init(float cellSize)
        {
            _cellSize = cellSize;
            _invCellSize = 1.0f / cellSize;
        }

        public Vector3i Quantize(Vector3f v)
        {
            return new Vector3i(
                (int)MathF.Floor(v.X * _invCellSize),
                (int)MathF.Floor(v.Y * _invCellSize),
                (int)MathF.Floor(v.Z * _invCellSize));
        }

        public void Add(Vector3f v)
        {
            var key = Quantize(v);
            if (!_grid.TryGetValue(key, out var vertices))
            {
                vertices = [];
                _grid.Add(key, vertices);
            }
            vertices.Add(v);
        }

        private void AppendCellCandidates(Vector3i key, List<Vector3f> result)
        {
            if (_grid.TryGetValue(key, out var vertices))
                result.AddRange(vertices);
        }

        public void Query(Vector3f p0, Vector3f p1, List<Vector3f> result)
        {
            result.Clear();
            var min = new Vector3f(MathF.Min(p0.X, p1.X), MathF.Min(p0.Y, p1.Y), MathF.Min(p0.Z, p1.Z));
            var max = new Vector3f(MathF.Max(p0.X, p1.X), MathF.Max(p0.Y, p1.Y), MathF.Max(p0.Z, p1.Z));

            var minX = (int)MathF.Floor(min.X * _invCellSize);
            var minY = (int)MathF.Floor(min.Y * _invCellSize);
            var minZ = (int)MathF.Floor(min.Z * _invCellSize);
            var maxX = (int)MathF.Floor(max.X * _invCellSize);
            var maxY = (int)MathF.Floor(max.Y * _invCellSize);
            var maxZ = (int)MathF.Floor(max.Z * _invCellSize);

            if ((maxX - minX + 1) * (maxY - minY + 1) * (maxZ - minZ + 1) <= 8)
            {
                for (var x = minX; x <= maxX; ++x)
                for (var y = minY; y <= maxY; ++y)
                for (var z = minZ; z <= maxZ; ++z)
                    AppendCellCandidates(new Vector3i(x, y, z), result);
                return;
            }

            // Robust grid traversal: visit every grid cell intersected by the segment.
            var dir = p1 - p0;
            if (dir.LengthSquared < MathDefs.Epsilon * MathDefs.Epsilon)
                return;

            // Traverse in grid space where cell boundaries are integer coordinates.
            var g0 = p0 * _invCellSize;
            var g1 = p1 * _invCellSize;
            var gd = g1 - g0;

            var cell = new Vector3i(
                (int)MathF.Floor(g0.X),
                (int)MathF.Floor(g0.Y),
                (int)MathF.Floor(g0.Z));
            var endCell = new Vector3i(
                (int)MathF.Floor(g1.X),
                (int)MathF.Floor(g1.Y),
                (int)MathF.Floor(g1.Z));

            AppendCellCandidates(cell, result);

            const float inf = 1e30f;

            var stepX = gd.X > 0.0f ? 1 : gd.X < 0.0f ? -1 : 0;
            var stepY = gd.Y > 0.0f ? 1 : gd.Y < 0.0f ? -1 : 0;
            var stepZ = gd.Z > 0.0f ? 1 : gd.Z < 0.0f ? -1 : 0;

            var tDeltaX = stepX != 0 ? MathF.Abs(1.0f / gd.X) : inf;
            var tDeltaY = stepY != 0 ? MathF.Abs(1.0f / gd.Y) : inf;
            var tDeltaZ = stepZ != 0 ? MathF.Abs(1.0f / gd.Z) : inf;

            var nextBoundaryX = stepX > 0 ? cell.X + 1.0f - g0.X : g0.X - cell.X;
            var nextBoundaryY = stepY > 0 ? cell.Y + 1.0f - g0.Y : g0.Y - cell.Y;
            var nextBoundaryZ = stepZ > 0 ? cell.Z + 1.0f - g0.Z : g0.Z - cell.Z;

            var tMaxX = stepX != 0 ? nextBoundaryX * tDeltaX : inf;
            var tMaxY = stepY != 0 ? nextBoundaryY * tDeltaY : inf;
            var tMaxZ = stepZ != 0 ? nextBoundaryZ * tDeltaZ : inf;

            // Safety cap: segment traversal should be bounded by number of crossed cells.
            var maxSteps = 4 + Math.Abs(endCell.X - cell.X) + Math.Abs(endCell.Y - cell.Y)
                + Math.Abs(endCell.Z - cell.Z);
            for (var steps = 0; steps < maxSteps; ++steps)
            {
                if (cell == endCell)
                    break;

                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    cell.X += stepX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY <= tMaxZ)
                {
                    cell.Y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    cell.Z += stepZ;
                    tMaxZ += tDeltaZ;
                }

                AppendCellCandidates(cell, result);
            }
        }
    }

    private static bool IsDegeneratePolygon(CsgPolygon polygon) => polygon.Vertices.Count < 3;

    private static float Orient2(Vector2f a, Vector2f b, Vector2f c) => (b - a).CrossProduct(c - a);

    private static uint GetVertexId(Dictionary<Vector3i, uint> vertexId, Vector3i qp)
    {
        if (vertexId.TryGetValue(qp, out var existing))
            return existing;
        var id = (uint)vertexId.Count;
        vertexId[qp] = id;
        return id;
    }

    private static float ColinearAreaEpsilon(Vector2f a, Vector2f b, float eps)
    {
        // Orient2 is an area-like value (~|ab| * distance(p, ab)).
        // If we want to treat points within `eps` distance from the segment as colinear,
        // compare |orient| against eps * |ab|.
        return eps * MathF.Max((b - a).Length, 1e-6f);
    }

    private static bool OnSegment2(Vector2f a, Vector2f b, Vector2f p, float eps)
    {
        if (p.X < MathF.Min(a.X, b.X) - eps || p.X > MathF.Max(a.X, b.X) + eps)
            return false;
        if (p.Y < MathF.Min(a.Y, b.Y) - eps || p.Y > MathF.Max(a.Y, b.Y) + eps)
            return false;
        return MathF.Abs(Orient2(a, b, p)) <= ColinearAreaEpsilon(a, b, eps);
    }

    private static bool SegmentsIntersect2(Vector2f a0, Vector2f a1, Vector2f b0, Vector2f b1, float eps)
    {
        // Bounding box quick reject.
        var aMinX = MathF.Min(a0.X, a1.X);
        var aMaxX = MathF.Max(a0.X, a1.X);
        var aMinY = MathF.Min(a0.Y, a1.Y);
        var aMaxY = MathF.Max(a0.Y, a1.Y);
        var bMinX = MathF.Min(b0.X, b1.X);
        var bMaxX = MathF.Max(b0.X, b1.X);
        var bMinY = MathF.Min(b0.Y, b1.Y);
        var bMaxY = MathF.Max(b0.Y, b1.Y);

        if (aMaxX < bMinX - eps || bMaxX < aMinX - eps || aMaxY < bMinY - eps || bMaxY < aMinY - eps)
            return false;

        var o1 = Orient2(a0, a1, b0);
        var o2 = Orient2(a0, a1, b1);
        var o3 = Orient2(b0, b1, a0);
        var o4 = Orient2(b0, b1, a1);

        var colA = ColinearAreaEpsilon(a0, a1, eps);
        var colB = ColinearAreaEpsilon(b0, b1, eps);

        var o1z = MathF.Abs(o1) <= colA;
        var o2z = MathF.Abs(o2) <= colA;
        var o3z = MathF.Abs(o3) <= colB;
        var o4z = MathF.Abs(o4) <= colB;

        // General case
        if (!o1z && !o2z && !o3z && !o4z)
        {
            var ab = o1 > 0.0f != o2 > 0.0f;
            var ba = o3 > 0.0f != o4 > 0.0f;
            return ab && ba;
        }

        // Colinear / touching cases
        if (o1z && OnSegment2(a0, a1, b0, eps)) return true;
        if (o2z && OnSegment2(a0, a1, b1, eps)) return true;
        if (o3z && OnSegment2(b0, b1, a0, eps)) return true;
        if (o4z && OnSegment2(b0, b1, a1, eps)) return true;
        return false;
    }

    private static ulong PackDirectedEdgeKey(uint from, uint to) => (ulong)from << 32 | to;

    private static ulong PackUndirectedEdgeKey(uint a, uint b)
    {
        var lo = Math.Min(a, b);
        var hi = Math.Max(a, b);
        return (ulong)lo << 32 | hi;
    }

    private static ulong PackCellKey(int x, int y) => (ulong)(uint)x << 32 | (uint)y;

    private static float ChooseIntersectionGridCellSize(IReadOnlyList<Vector2f> p2, float intersectionEps)
    {
        if (p2.Count == 0)
            return 1.0f;

        var minP = p2[0];
        var maxP = p2[0];
        foreach (var p in p2)
        {
            minP.X = MathF.Min(minP.X, p.X);
            minP.Y = MathF.Min(minP.Y, p.Y);
            maxP.X = MathF.Max(maxP.X, p.X);
            maxP.Y = MathF.Max(maxP.Y, p.Y);
        }

        var ext = maxP - minP;
        var diag = ext.Length;
        var n = (float)p2.Count;

        // Heuristic: aim for ~sqrt(n) cells across the bbox.
        // Keep it conservative and stable to avoid missing boundary cases.
        var cell = diag > 0.0f && n > 1.0f ? diag / MathF.Sqrt(n) : 1.0f;
        cell = MathF.Max(cell, intersectionEps * 64.0f);
        return MathF.Max(cell, 1e-6f);
    }

    private static bool HasSelfIntersections2DGrid(IReadOnlyList<Vector2f> p2, float intersectionEps,
        SimplePolygonFailureDetails? details)
    {
        var n = p2.Count;
        if (n < 3)
            return true;

        // For small polygons, the O(N^2) check is fast and well-tested.
        if (n <= 24)
        {
            for (var i = 0; i < n; ++i)
            {
                var i1 = (i + 1) % n;
                for (var j = i + 1; j < n; ++j)
                {
                    var j1 = (j + 1) % n;
                    if (i == j || i1 == j || j1 == i)
                        continue;
                    if (i == 0 && j1 == 0)
                        continue;
                    if (SegmentsIntersect2(p2[i], p2[i1], p2[j], p2[j1], intersectionEps))
                    {
                        if (details is not null)
                        {
                            details.Type = SimplePolygonFailureDetails.FailureType.SelfIntersection;
                            details.EdgeA0 = i;
                            details.EdgeA1 = i1;
                            details.EdgeB0 = j;
                            details.EdgeB1 = j1;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        var cellSize = ChooseIntersectionGridCellSize(p2, intersectionEps);
        var invCell = 1.0f / cellSize;

        var cellToEdges = new Dictionary<ulong, List<int>>(n * 2);
        var comparedPairs = new HashSet<ulong>();

        for (var i = 0; i < n; ++i)
        {
            var i1 = (i + 1) % n;
            var a = p2[i];
            var b = p2[i1];

            // Expand AABB to keep quantization conservative around boundaries.
            var minXf = MathF.Min(a.X, b.X) - intersectionEps;
            var minYf = MathF.Min(a.Y, b.Y) - intersectionEps;
            var maxXf = MathF.Max(a.X, b.X) + intersectionEps;
            var maxYf = MathF.Max(a.Y, b.Y) + intersectionEps;

            var minX = (int)MathF.Floor(minXf * invCell);
            var minY = (int)MathF.Floor(minYf * invCell);
            var maxX = (int)MathF.Floor(maxXf * invCell);
            var maxY = (int)MathF.Floor(maxYf * invCell);

            for (var cx = minX; cx <= maxX; ++cx)
            {
                for (var cy = minY; cy <= maxY; ++cy)
                {
                    var cellKey = PackCellKey(cx, cy);
                    if (!cellToEdges.TryGetValue(cellKey, out var edges))
                    {
                        edges = [];
                        cellToEdges.Add(cellKey, edges);
                    }

                    foreach (var j in edges)
                    {
                        if (i == j)
                            continue;

                        var pairKey = PackUndirectedEdgeKey((uint)i, (uint)j);
                        if (!comparedPairs.Add(pairKey))
                            continue;

                        var j1 = (j + 1) % n;
                        if (i1 == j || j1 == i)
                            continue;
                        if (i == 0 && j1 == 0 || j == 0 && i1 == 0)
                            continue;

                        if (SegmentsIntersect2(p2[i], p2[i1], p2[j], p2[j1], intersectionEps))
                        {
                            if (details is not null)
                            {
                                details.Type = SimplePolygonFailureDetails.FailureType.SelfIntersection;
                                details.EdgeA0 = i;
                                details.EdgeA1 = i1;
                                details.EdgeB0 = j;
                                details.EdgeB1 = j1;
                            }
                            return true;
                        }
                    }

                    edges.Add(i);
                }
            }
        }

        return false;
    }

    private static bool IsSimplePolygonInPlane(CsgPolygon poly, float epsilon, Vector3f axisU, Vector3f axisV)
    {
        var n = poly.Vertices.Count;
        if (n < 3)
            return false;

        var eps = MathF.Max(epsilon, 1e-6f);
        var intersectionEps = MathF.Max(1e-9f, eps * 1e-5f);
        var invEps = 1.0f / eps;

        var unique = new Dictionary<Vector3i, int>(n * 2);
        for (var i = 0; i < n; ++i)
        {
            var qp = CsgCommon.QuantizePosition(poly.Vertices[i].GetPosition(), invEps);
            if (unique.ContainsKey(qp))
                return false;
            unique.Add(qp, i);
        }

        var p2 = new Vector2f[n];
        for (var i = 0; i < n; ++i)
        {
            var p = poly.Vertices[i].GetPosition();
            p2[i] = new Vector2f(p.DotProduct(axisU), p.DotProduct(axisV));
        }

        if (HasSelfIntersections2DGrid(p2, intersectionEps, null))
            return false;

        return true;
    }

    private static bool IsSimplePolygonInPlane(CsgPolygon poly, float epsilon)
    {
        CsgCommon.MakeAxesFromNormal(poly.Plane.Normal, out var axisU, out var axisV);
        return IsSimplePolygonInPlane(poly, epsilon, axisU, axisV);
    }

    private static bool IsSimplePolygonInPlane(CsgPolygon poly, float epsilon, Vector3f axisU, Vector3f axisV,
        SimplePolygonFailureDetails details)
    {
        var n = poly.Vertices.Count;
        if (n < 3)
            return false;

        var eps = MathF.Max(epsilon, 1e-6f);
        var intersectionEps = MathF.Max(1e-9f, eps * 1e-5f);
        var invEps = 1.0f / eps;

        var unique = new Dictionary<Vector3i, int>(n * 2);
        for (var i = 0; i < n; ++i)
        {
            var qp = CsgCommon.QuantizePosition(poly.Vertices[i].GetPosition(), invEps);
            if (unique.TryGetValue(qp, out var firstIndex))
            {
                details.Type = SimplePolygonFailureDetails.FailureType.DuplicateQuantizedVertex;
                details.DuplicateKey = qp;
                details.DuplicateFirstIndex = firstIndex;
                details.DuplicateSecondIndex = i;
                return false;
            }
            unique.Add(qp, i);
        }

        var p2 = new Vector2f[n];
        for (var i = 0; i < n; ++i)
        {
            var p = poly.Vertices[i].GetPosition();
            p2[i] = new Vector2f(p.DotProduct(axisU), p.DotProduct(axisV));
        }

        if (HasSelfIntersections2DGrid(p2, intersectionEps, details))
            return false;

        return true;
    }

    private static bool IsSimplePolygonInPlane(CsgPolygon poly, float epsilon,
        SimplePolygonFailureDetails details)
    {
        CsgCommon.MakeAxesFromNormal(poly.Plane.Normal, out var axisU, out var axisV);
        return IsSimplePolygonInPlane(poly, epsilon, axisU, axisV, details);
    }

    private static float PolygonAbsAreaInPlane(CsgPolygon poly, Vector3f axisU, Vector3f axisV)
    {
        var n = poly.Vertices.Count;
        if (n < 3)
            return 0.0f;

        double area2 = 0.0;
        for (var i = 0; i < n; ++i)
        {
            var p0 = poly.Vertices[i].GetPosition();
            var p1 = poly.Vertices[(i + 1) % n].GetPosition();
            var a = new Vector2f(p0.DotProduct(axisU), p0.DotProduct(axisV));
            var b = new Vector2f(p1.DotProduct(axisU), p1.DotProduct(axisV));
            area2 += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return MathF.Abs((float)(0.5 * area2));
    }

    private static float PolygonAbsAreaInPlane(CsgPolygon poly)
    {
        CsgCommon.MakeAxesFromNormal(poly.Plane.Normal, out var axisU, out var axisV);
        return PolygonAbsAreaInPlane(poly, axisU, axisV);
    }

    private static bool AreCoplanarOriented(Plane a, Plane b, float normalEps, float epsilon)
    {
        var lenSqA = a.Normal.LengthSquared;
        var lenSqB = b.Normal.LengthSquared;
        if (lenSqA <= 1e-20f || lenSqB <= 1e-20f)
            return false;

        var dot = a.Normal.DotProduct(b.Normal);
        if (dot <= 0.0f)
            return false;

        var cosThreshold = 1.0f - normalEps;
        var lhs = dot * dot;
        var rhs = cosThreshold * cosThreshold * lenSqA * lenSqB;
        if (lhs < rhs)
            return false;
        return MathF.Abs(a.D - b.D) <= epsilon;
    }

    private static uint GetVertexId(Dictionary<Vector3i, uint> vertexId, Vector3f p, float invEpsilon)
    {
        var qp = CsgCommon.QuantizePosition(p, invEpsilon);
        return GetVertexId(vertexId, qp);
    }

    private static bool RemoveOneRedundantVertex(CsgPolygon poly, float epsilon)
    {
        var eps = MathF.Max(epsilon, 1e-6f);
        var epsSq = eps * eps;

        var n = poly.Vertices.Count;
        if (n < 3)
            return false;

        for (var i = 0; i < n; ++i)
        {
            var prev = (i + n - 1) % n;
            var next = (i + 1) % n;

            var p0 = poly.Vertices[prev].GetPosition();
            var p1 = poly.Vertices[i].GetPosition();
            var p2 = poly.Vertices[next].GetPosition();

            if ((p1 - p0).LengthSquared <= epsSq)
            {
                poly.Vertices.RemoveAt(i);
                return true;
            }

            var line = p2 - p0;
            var lineLenSq = line.LengthSquared;
            if (lineLenSq <= epsSq)
            {
                poly.Vertices.RemoveAt(i);
                return true;
            }

            var lineLen = MathF.Sqrt(lineLenSq);
            var dist = (p1 - p0).CrossProduct(line).Length / lineLen;
            if (dist > eps)
                continue;

            var d1 = (p1 - p0).NormalizedOrDefault(Vector3f.Zero);
            var d2 = (p2 - p1).NormalizedOrDefault(Vector3f.Zero);
            if (d1.DotProduct(d2) <= 0.0f)
                continue;

            poly.Vertices.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static void CleanupPolygonVertices(CsgPolygon poly, float epsilon)
    {
        // Conservative local cleanup: remove near-duplicates and non-reflex colinear points.
        while (poly.Vertices.Count > 3 && RemoveOneRedundantVertex(poly, epsilon))
        {
        }
    }

    private static bool PopOutgoingEdge(Dictionary<uint, List<uint>> outgoing, uint from, uint to)
    {
        if (!outgoing.TryGetValue(from, out var vertices))
            return false;
        for (var i = 0; i < vertices.Count; ++i)
        {
            if (vertices[i] == to)
            {
                vertices[i] = vertices[^1];
                vertices.RemoveAt(vertices.Count - 1);
                if (vertices.Count == 0)
                    outgoing.Remove(from);
                return true;
            }
        }
        return false;
    }

    private static uint ChooseNextVertex(IReadOnlyList<uint> candidates, uint prev, uint curr,
        IReadOnlyList<Vector2f> pos2)
    {
        if (candidates.Count == 0)
            return uint.MaxValue;
        if (candidates.Count == 1)
            return candidates[0];

        var best = uint.MaxValue;
        var bestScore = -1e30f;
        var inDir = pos2[(int)curr] - pos2[(int)prev];
        var inLen = MathF.Max(inDir.Length, 1e-6f);

        foreach (var cand in candidates)
        {
            if (cand == prev)
                continue;
            var outDir = pos2[(int)cand] - pos2[(int)curr];
            var outLen = MathF.Max(outDir.Length, 1e-6f);
            var cross = inDir.CrossProduct(outDir) / (inLen * outLen);
            var dot = inDir.DotProduct(outDir) / (inLen * outLen);
            var score = cross * 2.0f + dot;
            if (score > bestScore)
            {
                bestScore = score;
                best = cand;
            }
        }

        if (best == uint.MaxValue)
            best = candidates[0];
        return best;
    }

    private static bool TryExtractSingleLoopFromEdges(IReadOnlyList<Vector2f> pos2,
        Dictionary<uint, List<uint>> outgoing, List<uint> outLoop)
    {
        outLoop.Clear();
        if (outgoing.Count == 0)
            return false;

        using var enumerator = outgoing.GetEnumerator();
        enumerator.MoveNext();
        var start = enumerator.Current.Key;
        if (enumerator.Current.Value.Count == 0)
            return false;
        var next = enumerator.Current.Value[^1];
        PopOutgoingEdge(outgoing, start, next);

        outLoop.Add(start);
        outLoop.Add(next);

        var prev = start;
        var curr = next;

        var maxSteps = pos2.Count * 4 + 16;
        for (var step = 0; step < maxSteps; ++step)
        {
            if (curr == start)
                break;

            if (!outgoing.TryGetValue(curr, out var candidates))
                return false;

            var chosen = ChooseNextVertex(candidates, prev, curr, pos2);
            if (chosen == uint.MaxValue)
                return false;

            if (!PopOutgoingEdge(outgoing, curr, chosen))
                return false;

            outLoop.Add(chosen);
            prev = curr;
            curr = chosen;
        }

        if (outLoop.Count < 4)
            return false;
        if (outLoop[^1] != start)
            return false;
        outLoop.RemoveAt(outLoop.Count - 1);
        return outLoop.Count >= 3;
    }

    private static void ProjectTo2D(CsgPolygon poly, Vector3f axisU, Vector3f axisV, List<Vector2f> output)
    {
        var n = poly.Vertices.Count;
        output.Clear();
        output.EnsureCapacity(n);
        for (var i = 0; i < n; ++i)
        {
            var p = poly.Vertices[i].GetPosition();
            output.Add(new Vector2f(p.DotProduct(axisU), p.DotProduct(axisV)));
        }
    }

    private static float SignedArea2D(IReadOnlyList<Vector2f> poly)
    {
        var n = poly.Count;
        if (n < 3)
            return 0.0f;
        double area2 = 0.0;
        for (var i = 0; i < n; ++i)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            area2 += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return (float)(0.5 * area2);
    }

    private static void RemoveConsecutiveDuplicateVertices(CsgPolygon poly, float epsilon)
    {
        var eps = MathF.Max(epsilon, 1e-6f);
        var epsSq = eps * eps;
        while (poly.Vertices.Count >= 2)
        {
            var changed = false;
            for (var i = 0; i < poly.Vertices.Count; ++i)
            {
                var i1 = (i + 1) % poly.Vertices.Count;
                var p0 = poly.Vertices[i].GetPosition();
                var p1 = poly.Vertices[i1].GetPosition();
                if ((p0 - p1).LengthSquared <= epsSq)
                {
                    poly.Vertices.RemoveAt(i1);
                    changed = true;
                    break;
                }
            }
            if (!changed)
                break;
            if (poly.Vertices.Count < 3)
                break;
        }
    }

    private readonly record struct SplitPoint(float T, Vector3f Pos);

    private static void SplitPolygonEdgesAtVertices(CsgPolygon poly, IReadOnlyList<ModelVertex> otherVertices,
        float epsilon)
    {
        if (poly.Vertices.Count < 3 || otherVertices.Count == 0)
            return;

        var eps = MathF.Max(epsilon, 1e-6f);
        var epsSq = eps * eps;
        var invEps = 1.0f / eps;

        CsgCommon.MakeAxesFromNormal(poly.Plane.Normal, out var axisU, out var axisV);

        var poly2 = new List<Vector2f>();
        ProjectTo2D(poly, axisU, axisV, poly2);

        var other2 = new Vector2f[otherVertices.Count];
        for (var i = 0; i < otherVertices.Count; ++i)
        {
            var p = otherVertices[i].GetPosition();
            other2[i] = new Vector2f(p.DotProduct(axisU), p.DotProduct(axisV));
        }

        var output = new List<ModelVertex>(poly.Vertices.Count + otherVertices.Count);

        var n = poly.Vertices.Count;
        for (var i = 0; i < n; ++i)
        {
            var i1 = (i + 1) % n;
            var a0 = poly2[i];
            var a1 = poly2[i1];
            var ab = a1 - a0;
            var abLenSq = ab.LengthSquared;

            output.Add(poly.Vertices[i].Clone());

            if (abLenSq <= 1e-20f)
                continue;

            var p0 = poly.Vertices[i].GetPosition();
            var p1 = poly.Vertices[i1].GetPosition();

            var splits = new List<SplitPoint>(8);
            var used = new HashSet<Vector3i>();

            for (var j = 0; j < otherVertices.Count; ++j)
            {
                var p = otherVertices[j].GetPosition();
                if ((p - p0).LengthSquared <= epsSq || (p - p1).LengthSquared <= epsSq)
                    continue;

                var q = other2[j];
                if (!OnSegment2(a0, a1, q, eps))
                    continue;

                var t = (q - a0).DotProduct(ab) / abLenSq;
                if (!(t > 0.0f && t < 1.0f))
                    continue;

                var key = CsgCommon.QuantizePosition(p, invEps);
                if (!used.Add(key))
                    continue;

                splits.Add(new SplitPoint(t, p));
            }

            if (splits.Count == 0)
                continue;

            splits.Sort(static (lhs, rhs) => lhs.T.CompareTo(rhs.T));

            foreach (var splitPoint in splits)
            {
                var newVertex = CsgCommon.LerpModelVertex(poly.Vertices[i], poly.Vertices[i1], splitPoint.T);
                newVertex.SetPosition(splitPoint.Pos);
                output.Add(newVertex);
            }
        }

        poly.Vertices = output;
        RemoveConsecutiveDuplicateVertices(poly, epsilon);
    }

    private static void SplitPolygonsMutuallyAtOverlaps(CsgPolygon a, CsgPolygon b, float epsilon)
    {
        SplitPolygonEdgesAtVertices(a, b.Vertices, epsilon);
        SplitPolygonEdgesAtVertices(b, a.Vertices, epsilon);
    }

    private sealed class BoundaryCancellationBuilder
    {
        private float _invEps;
        private readonly Dictionary<Vector3i, uint> _vertexId = [];
        public readonly List<ModelVertex> RepVertex = [];
        public readonly List<Vector3f> RepPos = [];

        public void Init(float invEps, int reserveVertices)
        {
            _invEps = invEps;
            _vertexId.Clear();
            _vertexId.EnsureCapacity(reserveVertices * 2);
            RepVertex.Clear();
            RepPos.Clear();
            RepVertex.EnsureCapacity(reserveVertices);
            RepPos.EnsureCapacity(reserveVertices);
        }

        public uint GetId(ModelVertex vertex)
        {
            var p = vertex.GetPosition();
            var qp = CsgCommon.QuantizePosition(p, _invEps);
            if (_vertexId.TryGetValue(qp, out var existing))
                return existing;
            var id = (uint)_vertexId.Count;
            _vertexId.Add(qp, id);
            RepVertex.Add(vertex.Clone());
            RepPos.Add(p);
            return id;
        }

        public void AddDirectedEdges(CsgPolygon poly, Dictionary<ulong, uint> directedCount)
        {
            var n = poly.Vertices.Count;
            for (var i = 0; i < n; ++i)
            {
                var i1 = (i + 1) % n;
                var id0 = GetId(poly.Vertices[i]);
                var id1 = GetId(poly.Vertices[i1]);
                if (id0 == id1)
                    continue;
                var key = PackDirectedEdgeKey(id0, id1);
                directedCount.TryGetValue(key, out var count);
                directedCount[key] = count + 1;
            }
        }
    }

    private static bool TryMergeByBoundaryCancellation(CsgPolygon a, CsgPolygon b, Vector3f axisU,
        Vector3f axisV, float normalEps, float epsilon, float epsSq, out CsgPolygon output)
    {
        output = new CsgPolygon();
        if (a.Shared != b.Shared)
            return false;

        var na = a.Plane.Normal.NormalizedOrDefault(Vector3f.Zero);
        var nb = b.Plane.Normal.NormalizedOrDefault(Vector3f.Zero);
        var planeDot = na == Vector3f.Zero || nb == Vector3f.Zero ? 0.0f : na.DotProduct(nb);
        var planeDDiff = MathF.Abs(a.Plane.D - b.Plane.D);
        if (planeDot < 1.0f - normalEps || planeDDiff > epsilon)
            return false;

        if (!IsSimplePolygonInPlane(a, epsilon, axisU, axisV)
            || !IsSimplePolygonInPlane(b, epsilon, axisU, axisV))
            return false;

        // Fallback path: make partial colinear overlaps explicit shared edges.
        var aSplit = a.Clone();
        var bSplit = b.Clone();
        SplitPolygonsMutuallyAtOverlaps(aSplit, bSplit, epsilon);

        var eps = MathF.Max(epsilon, 1e-6f);
        var invEps = 1.0f / eps;

        var builder = new BoundaryCancellationBuilder();
        builder.Init(invEps, a.Vertices.Count + b.Vertices.Count);

        var directedCount = new Dictionary<ulong, uint>((a.Vertices.Count + b.Vertices.Count) * 2);
        builder.AddDirectedEdges(aSplit, directedCount);
        builder.AddDirectedEdges(bSplit, directedCount);

        // Cancel opposite edges.
        var visitedUndirected = new HashSet<ulong>();
        var boundaryEdges = new List<CanceledEdge>(directedCount.Count);

        foreach (var pair in directedCount)
        {
            var key = pair.Key;
            var from = (uint)(key >> 32);
            var to = (uint)(key & 0xffffffffu);
            var undirected = PackUndirectedEdgeKey(from, to);
            if (!visitedUndirected.Add(undirected))
                continue;

            var reverse = PackDirectedEdgeKey(to, from);
            var countForward = pair.Value;
            directedCount.TryGetValue(reverse, out var countReverse);
            if (countForward == countReverse)
                continue;

            var keepForward = countForward > countReverse;
            var keepCount = keepForward ? countForward - countReverse : countReverse - countForward;
            var keepFrom = keepForward ? from : to;
            var keepTo = keepForward ? to : from;
            for (uint i = 0; i < keepCount; ++i)
                boundaryEdges.Add(new CanceledEdge(keepFrom, keepTo));
        }

        if (boundaryEdges.Count == 0)
            return false;

        // Build 2D positions for angle-based traversal.
        var pos2 = new List<Vector2f>(builder.RepPos.Count);
        foreach (var p in builder.RepPos)
            pos2.Add(new Vector2f(p.DotProduct(axisU), p.DotProduct(axisV)));

        var outgoing = new Dictionary<uint, List<uint>>(boundaryEdges.Count * 2);
        foreach (var edge in boundaryEdges)
        {
            if (!outgoing.TryGetValue(edge.From, out var vertices))
            {
                vertices = [];
                outgoing.Add(edge.From, vertices);
            }
            vertices.Add(edge.To);
        }

        var loopIds = new List<uint>();
        if (!TryExtractSingleLoopFromEdges(pos2, outgoing, loopIds))
            return false;
        if (outgoing.Count != 0)
            return false;

        output.Shared = a.Shared;
        output.Plane = a.Plane;
        output.Vertices.EnsureCapacity(loopIds.Count);
        foreach (var id in loopIds)
            output.Vertices.Add(builder.RepVertex[(int)id].Clone());

        CleanupPolygonVertices(output, epsilon);
        if (IsDegeneratePolygon(output))
            return false;

        var areaA = PolygonAbsAreaInPlane(a, axisU, axisV);
        var areaB = PolygonAbsAreaInPlane(b, axisU, axisV);
        var expected = areaA + areaB;
        var tolerance = expected * 1e-3f + epsilon * epsilon * 10.0f;

        var failure = new SimplePolygonFailureDetails();
        if (!IsSimplePolygonInPlane(output, epsilon, axisU, axisV, failure))
            return false;

        var areaOut = PolygonAbsAreaInPlane(output, axisU, axisV);
        if (!(MathF.Abs(areaOut - expected) <= tolerance))
            return false;

        // Normalize winding to keep signed area consistent with plane axes.
        var out2 = new List<Vector2f>();
        ProjectTo2D(output, axisU, axisV, out2);
        if (SignedArea2D(out2) < 0.0f)
            output.Vertices.Reverse();

        return true;
    }

    private static bool ExtractLoopByIndices(IReadOnlyList<ModelVertex> vertices, int begin, int endExclusive,
        CsgPolygon output)
    {
        output.Vertices.Clear();
        if (vertices.Count == 0)
            return false;

        var n = vertices.Count;
        begin %= n;
        endExclusive %= n;

        var i = begin;
        while (i != endExclusive)
        {
            output.Vertices.Add(vertices[i].Clone());
            i = (i + 1) % n;
            if (output.Vertices.Count > n + 1)
                return false;
        }
        return output.Vertices.Count >= 3;
    }

    private static bool ValidateRepairedLoop(CsgPolygon candidate, float epsilon, float expectedArea,
        float expectedTolerance, Vector3f axisU, Vector3f axisV)
    {
        CleanupPolygonVertices(candidate, epsilon);
        if (IsDegeneratePolygon(candidate))
            return false;
        if (!IsSimplePolygonInPlane(candidate, epsilon, axisU, axisV))
            return false;
        var area = PolygonAbsAreaInPlane(candidate, axisU, axisV);
        return MathF.Abs(area - expectedArea) <= expectedTolerance;
    }

    private static bool TryRepairDuplicateVertexLoop(CsgPolygon poly, SimplePolygonFailureDetails details,
        Vector3f axisU, Vector3f axisV, float epsilon, float expectedArea, float expectedTolerance)
    {
        if (details.Type != SimplePolygonFailureDetails.FailureType.DuplicateQuantizedVertex)
            return false;

        var n = poly.Vertices.Count;
        if (n < 4)
            return false;

        var i0 = Math.Min(details.DuplicateFirstIndex, details.DuplicateSecondIndex);
        var i1 = Math.Max(details.DuplicateFirstIndex, details.DuplicateSecondIndex);
        if (i0 >= n || i1 >= n || i0 == i1)
            return false;

        // Only attempt repair when the duplicate positions are extremely close.
        var repairEps = MathF.Max(1e-7f, epsilon * 1e-3f);
        var repairEpsSq = repairEps * repairEps;
        var p0 = poly.Vertices[i0].GetPosition();
        var p1 = poly.Vertices[i1].GetPosition();
        if ((p0 - p1).LengthSquared > repairEpsSq)
            return false;

        var loopA = new CsgPolygon
        {
            Shared = poly.Shared,
            Plane = poly.Plane
        };
        if (!ExtractLoopByIndices(poly.Vertices, i0, (i1 + 1) % n, loopA))
            loopA.Vertices.Clear();

        var loopB = new CsgPolygon
        {
            Shared = poly.Shared,
            Plane = poly.Plane
        };
        if (!ExtractLoopByIndices(poly.Vertices, i1, (i0 + 1) % n, loopB))
            loopB.Vertices.Clear();

        var aOk = ValidateRepairedLoop(loopA, epsilon, expectedArea, expectedTolerance, axisU, axisV);
        var bOk = ValidateRepairedLoop(loopB, epsilon, expectedArea, expectedTolerance, axisU, axisV);
        if (!aOk && !bOk)
            return false;

        if (aOk && bOk)
        {
            var areaA = PolygonAbsAreaInPlane(loopA);
            var areaB = PolygonAbsAreaInPlane(loopB);
            var selected = areaA >= areaB ? loopA : loopB;
            poly.Vertices = selected.Vertices;
            poly.Shared = selected.Shared;
            poly.Plane = selected.Plane;
        }
        else
        {
            var selected = aOk ? loopA : loopB;
            poly.Vertices = selected.Vertices;
            poly.Shared = selected.Shared;
            poly.Plane = selected.Plane;
        }

        return true;
    }

    private static bool TryMergeAtSharedEdge(CsgPolygon a, CsgPolygon b, int aEdgeStart, int bEdgeStart,
        out CsgPolygon output, float normalEps, float epsilon, float epsSq)
    {
        output = new CsgPolygon();
        if (a.Shared != b.Shared)
        {
            CsgSimplifyDiagLog("CSG simplify: merge reject (shared mismatch)");
            return false;
        }

        var na = a.Plane.Normal.NormalizedOrDefault(Vector3f.Zero);
        var nb = b.Plane.Normal.NormalizedOrDefault(Vector3f.Zero);
        var planeDot = na == Vector3f.Zero || nb == Vector3f.Zero ? 0.0f : na.DotProduct(nb);
        var planeDDiff = MathF.Abs(a.Plane.D - b.Plane.D);
        if (planeDot < 1.0f - normalEps || planeDDiff > epsilon)
        {
            CsgSimplifyDiagLog("CSG simplify: merge reject (plane mismatch)");
            return false;
        }

        CsgCommon.MakeAxesFromNormal(a.Plane.Normal, out var axisU, out var axisV);

        var aCount = a.Vertices.Count;
        var bCount = b.Vertices.Count;
        if (aCount < 3 || bCount < 3)
        {
            CsgSimplifyDiagLog("CSG simplify: merge reject (degenerate input)");
            return false;
        }

        var a0 = aEdgeStart;
        var a1 = (aEdgeStart + 1) % aCount;
        var b0 = bEdgeStart;
        var b1 = (bEdgeStart + 1) % bCount;

        var av0 = a.Vertices[a0].GetPosition();
        var av1 = a.Vertices[a1].GetPosition();
        var bv0 = b.Vertices[b0].GetPosition();
        var bv1 = b.Vertices[b1].GetPosition();

        var d01Sq = (av0 - bv1).LengthSquared;
        var d10Sq = (av1 - bv0).LengthSquared;
        if (d01Sq > epsSq || d10Sq > epsSq)
        {
            CsgSimplifyDiagLog("CSG simplify: merge reject (edge mismatch)");
            return false;
        }

        output.Shared = a.Shared;
        output.Plane = a.Plane;
        output.Vertices.EnsureCapacity(aCount + bCount - 2);

        for (var i = 0; i <= aEdgeStart; ++i)
            output.Vertices.Add(a.Vertices[i].Clone());

        for (var k = 0; k < bCount - 2; ++k)
        {
            var index = (bEdgeStart + 2 + k) % bCount;
            output.Vertices.Add(b.Vertices[index].Clone());
        }

        for (var i = aEdgeStart + 1; i < aCount; ++i)
            output.Vertices.Add(a.Vertices[i].Clone());

        CleanupPolygonVertices(output, epsilon);
        if (IsDegeneratePolygon(output))
        {
            CsgSimplifyDiagLog("CSG simplify: merge reject (degenerate output)");
            return false;
        }

        var areaA = PolygonAbsAreaInPlane(a, axisU, axisV);
        var areaB = PolygonAbsAreaInPlane(b, axisU, axisV);
        var expected = areaA + areaB;
        var tolerance = expected * 1e-3f + epsilon * epsilon * 10.0f;

        var failure = new SimplePolygonFailureDetails();
        if (!IsSimplePolygonInPlane(output, epsilon, axisU, axisV, failure))
        {
            if (!TryRepairDuplicateVertexLoop(output, failure, axisU, axisV, epsilon, expected, tolerance))
            {
                if (!TryMergeByBoundaryCancellation(a, b, axisU, axisV, normalEps, epsilon, epsSq,
                        out var canceled))
                {
                    CsgSimplifyDiagLog("CSG simplify: merge reject (non-simple polygon)");
                    return false;
                }
                output = canceled;
            }
        }

        var areaOut = PolygonAbsAreaInPlane(output, axisU, axisV);
        if (!(MathF.Abs(areaOut - expected) <= tolerance))
        {
            CsgSimplifyDiagLog("CSG simplify: merge reject (area mismatch)");
            return false;
        }

        return true;
    }

    private static bool TryMergeAtSharedEdge(CsgPolygon a, CsgPolygon b, int aEdgeStart, int bEdgeStart,
        out CsgPolygon output, Vector3f axisU, Vector3f axisV, float normalEps, float epsilon, float epsSq)
    {
        output = new CsgPolygon();
        if (a.Shared != b.Shared)
            return false;

        var na = a.Plane.Normal.NormalizedOrDefault(Vector3f.Zero);
        var nb = b.Plane.Normal.NormalizedOrDefault(Vector3f.Zero);
        var planeDot = na == Vector3f.Zero || nb == Vector3f.Zero ? 0.0f : na.DotProduct(nb);
        var planeDDiff = MathF.Abs(a.Plane.D - b.Plane.D);
        if (planeDot < 1.0f - normalEps || planeDDiff > epsilon)
            return false;

        var aCount = a.Vertices.Count;
        var bCount = b.Vertices.Count;
        if (aCount < 3 || bCount < 3)
            return false;

        var a0 = aEdgeStart;
        var a1 = (aEdgeStart + 1) % aCount;
        var b0 = bEdgeStart;
        var b1 = (bEdgeStart + 1) % bCount;

        var av0 = a.Vertices[a0].GetPosition();
        var av1 = a.Vertices[a1].GetPosition();
        var bv0 = b.Vertices[b0].GetPosition();
        var bv1 = b.Vertices[b1].GetPosition();

        var d01Sq = (av0 - bv1).LengthSquared;
        var d10Sq = (av1 - bv0).LengthSquared;
        if (d01Sq > epsSq || d10Sq > epsSq)
            return false;

        output.Shared = a.Shared;
        output.Plane = a.Plane;
        output.Vertices.EnsureCapacity(aCount + bCount - 2);

        for (var i = 0; i <= aEdgeStart; ++i)
            output.Vertices.Add(a.Vertices[i].Clone());

        for (var k = 0; k < bCount - 2; ++k)
        {
            var index = (bEdgeStart + 2 + k) % bCount;
            output.Vertices.Add(b.Vertices[index].Clone());
        }

        for (var i = aEdgeStart + 1; i < aCount; ++i)
            output.Vertices.Add(a.Vertices[i].Clone());

        CleanupPolygonVertices(output, epsilon);
        if (IsDegeneratePolygon(output))
            return false;

        var areaA = PolygonAbsAreaInPlane(a, axisU, axisV);
        var areaB = PolygonAbsAreaInPlane(b, axisU, axisV);
        var expected = areaA + areaB;
        var tolerance = expected * 1e-3f + epsilon * epsilon * 10.0f;

        var failure = new SimplePolygonFailureDetails();
        if (!IsSimplePolygonInPlane(output, epsilon, axisU, axisV, failure))
        {
            if (!TryRepairDuplicateVertexLoop(output, failure, axisU, axisV, epsilon, expected, tolerance))
            {
                if (!TryMergeByBoundaryCancellation(a, b, axisU, axisV, normalEps, epsilon, epsSq,
                        out var canceled))
                    return false;
                output = canceled;
            }
        }

        var areaOut = PolygonAbsAreaInPlane(output, axisU, axisV);
        if (!(MathF.Abs(areaOut - expected) <= tolerance))
            return false;

        return true;
    }

    private readonly record struct DirectedEdgeInfo(int EdgeStart, uint FromId, uint ToId, ulong Key);

    private static bool MergeByFullEdgesToFixpoint(List<CsgPolygon> group, float epsilon, float normalEps,
        float epsSq)
    {
        var mergedEver = false;

        group.RemoveAll(IsDegeneratePolygon);
        if (group.Count < 2)
            return false;

        // Cache plane basis once per coplanar group.
        CsgCommon.MakeAxesFromNormal(group[0].Plane.Normal, out var axisU, out var axisV);

        var invEpsilon = 1.0f / epsilon;

        static void RemoveEdgeRef(Dictionary<ulong, List<EdgeRef>> edgesByDirected, ulong key,
            int polyIndex, int edgeStart)
        {
            if (!edgesByDirected.TryGetValue(key, out var refs))
                return;
            for (var i = 0; i < refs.Count; ++i)
            {
                if (refs[i].PolyIndex == polyIndex && refs[i].EdgeStart == edgeStart)
                {
                    refs.RemoveAt(i);
                    break;
                }
            }
            if (refs.Count == 0)
                edgesByDirected.Remove(key);
        }

        var vertexId = new Dictionary<Vector3i, uint>(group.Count * 16);

        var edgesByPoly = new List<List<DirectedEdgeInfo>>(group.Count);
        for (var i = 0; i < group.Count; ++i)
            edgesByPoly.Add([]);

        var edgesByDirected = new Dictionary<ulong, List<EdgeRef>>(group.Count * 8);

        void RebuildPolyEdges(int polyIndex)
        {
            edgesByPoly[polyIndex].Clear();
            var poly = group[polyIndex];
            var n = poly.Vertices.Count;
            if (n < 3)
                return;

            edgesByPoly[polyIndex].EnsureCapacity(n);
            for (var edgeIndex = 0; edgeIndex < n; ++edgeIndex)
            {
                var p0 = poly.Vertices[edgeIndex].GetPosition();
                var p1 = poly.Vertices[(edgeIndex + 1) % n].GetPosition();
                var id0 = GetVertexId(vertexId, p0, invEpsilon);
                var id1 = GetVertexId(vertexId, p1, invEpsilon);
                if (id0 == id1)
                    continue;
                var key = PackDirectedEdgeKey(id0, id1);
                edgesByPoly[polyIndex].Add(new DirectedEdgeInfo(edgeIndex, id0, id1, key));
            }
        }

        void AddPolyToMap(int polyIndex)
        {
            foreach (var edge in edgesByPoly[polyIndex])
            {
                if (!edgesByDirected.TryGetValue(edge.Key, out var refs))
                {
                    refs = [];
                    edgesByDirected.Add(edge.Key, refs);
                }
                refs.Add(new EdgeRef(polyIndex, edge.EdgeStart));
                refs.Sort(static (lhs, rhs) => lhs.PolyIndex != rhs.PolyIndex
                    ? lhs.PolyIndex.CompareTo(rhs.PolyIndex)
                    : lhs.EdgeStart.CompareTo(rhs.EdgeStart));
            }
        }

        void RemovePolyFromMap(int polyIndex)
        {
            foreach (var edge in edgesByPoly[polyIndex])
                RemoveEdgeRef(edgesByDirected, edge.Key, polyIndex, edge.EdgeStart);
        }

        for (var polyIndex = 0; polyIndex < group.Count; ++polyIndex)
        {
            RebuildPolyEdges(polyIndex);
            AddPolyToMap(polyIndex);
        }

        var dirty = new byte[group.Count];
        Array.Fill(dirty, (byte)1);

        for (;;)
        {
            var mergedThisRound = false;

            for (var polyIndex = 0; polyIndex < group.Count; ++polyIndex)
            {
                if (dirty[polyIndex] == 0)
                    continue;
                dirty[polyIndex] = 0;

                if (IsDegeneratePolygon(group[polyIndex]))
                    continue;

                // Mirror the original deterministic behavior: scan polys/edges in order and perform the first valid merge.
                foreach (var edge in edgesByPoly[polyIndex])
                {
                    var reverseKey = PackDirectedEdgeKey(edge.ToId, edge.FromId);
                    if (!edgesByDirected.TryGetValue(reverseKey, out var refs))
                        continue;

                    // Copy refs because the merge path mutates edgesByDirected and may invalidate iterators/references.
                    var otherRefs = new List<EdgeRef>(refs);
                    foreach (var other in otherRefs)
                    {
                        var otherIndex = other.PolyIndex;
                        if (otherIndex == polyIndex)
                            continue;
                        if (otherIndex >= group.Count)
                            continue;
                        if (IsDegeneratePolygon(group[otherIndex]))
                            continue;

                        if (!TryMergeAtSharedEdge(group[polyIndex], group[otherIndex], edge.EdgeStart,
                                other.EdgeStart, out var merged, axisU, axisV, normalEps, epsilon, epsSq))
                            continue;

                        // Update maps incrementally.
                        RemovePolyFromMap(polyIndex);
                        RemovePolyFromMap(otherIndex);

                        group[polyIndex] = merged;
                        group[otherIndex].Vertices.Clear();

                        RebuildPolyEdges(polyIndex);
                        edgesByPoly[otherIndex].Clear();

                        AddPolyToMap(polyIndex);

                        mergedEver = true;
                        mergedThisRound = true;

                        // Conservative correctness: after merge, any polygon may become eligible (mirrors rebuild+rescan).
                        Array.Fill(dirty, (byte)1);
                        break;
                    }

                    if (mergedThisRound)
                        break;
                }

                if (mergedThisRound)
                    break;
            }

            if (!mergedThisRound)
                break;
        }

        group.RemoveAll(IsDegeneratePolygon);
        return mergedEver;
    }

    private static int ResolveTJunctions(List<CsgPolygon> polygons, float epsilon, out int outPasses)
    {
        var insertedTotal = 0;
        outPasses = 0;

        var boundingBox = new BoundingBox();
        foreach (var poly in polygons)
        foreach (var vertex in poly.Vertices)
            boundingBox.Merge(vertex.GetPosition());

        if (!boundingBox.Defined)
            return insertedTotal;

        var size = boundingBox.Size;
        var maxDim = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
        var cellSize = maxDim > 0.0f ? maxDim / 20.0f : 1.0f;
        cellSize = MathF.Max(cellSize, epsilon * 10.0f);

        var grid = new VertexGrid();
        grid.Init(cellSize);
        foreach (var poly in polygons)
        foreach (var vertex in poly.Vertices)
            grid.Add(vertex.GetPosition());

        var changed = true;
        var epsilonSq = epsilon * epsilon;
        var pass = 0;

        var candidates = new List<Vector3f>();

        var invEps = 1.0f / MathF.Max(epsilon, 1e-6f);

        bool RunPass(bool onlyOpenEdges)
        {
            var vertexId = new Dictionary<Vector3i, uint>(polygons.Count * 16);
            var undirectedCount = new Dictionary<ulong, uint>();
            if (onlyOpenEdges)
            {
                undirectedCount.EnsureCapacity(polygons.Count * 32);
                foreach (var poly in polygons)
                {
                    var n = poly.Vertices.Count;
                    if (n < 3)
                        continue;
                    for (var i = 0; i < n; ++i)
                    {
                        var i1 = (i + 1) % n;
                        var p0 = poly.Vertices[i].GetPosition();
                        var p1 = poly.Vertices[i1].GetPosition();
                        var id0 = GetVertexId(vertexId, p0, invEps);
                        var id1 = GetVertexId(vertexId, p1, invEps);
                        if (id0 == id1)
                            continue;
                        var key = PackUndirectedEdgeKey(id0, id1);
                        undirectedCount.TryGetValue(key, out var count);
                        undirectedCount[key] = count + 1;
                    }
                }
            }

            var changedLocal = false;
            ++pass;

            foreach (var poly in polygons)
            {
                if (poly.Vertices.Count == 0)
                    continue;

                for (var i = 0; i < poly.Vertices.Count; ++i)
                {
                    var i1 = (i + 1) % poly.Vertices.Count;
                    var p0 = poly.Vertices[i].GetPosition();
                    var p1 = poly.Vertices[i1].GetPosition();

                    if (onlyOpenEdges)
                    {
                        var id0 = GetVertexId(vertexId, p0, invEps);
                        var id1 = GetVertexId(vertexId, p1, invEps);
                        if (id0 == id1)
                            continue;
                        var undirected = PackUndirectedEdgeKey(id0, id1);
                        if (undirectedCount.TryGetValue(undirected, out var count) && count >= 2)
                            continue;
                    }

                    var edgeDir = p1 - p0;
                    var edgeLen = edgeDir.Length;
                    if (edgeLen < epsilon)
                        continue;

                    edgeDir /= edgeLen;
                    grid.Query(p0, p1, candidates);

                    var bestT = edgeLen;
                    var bestPos = default(Vector3f);
                    var foundSplit = false;

                    foreach (var pos in candidates)
                    {
                        var vector = pos - p0;
                        var t = vector.DotProduct(edgeDir);

                        if (t > epsilon && t < edgeLen - epsilon)
                        {
                            var projection = p0 + edgeDir * t;
                            if ((pos - projection).LengthSquared < epsilonSq)
                            {
                                if (t < bestT)
                                {
                                    bestT = t;
                                    bestPos = pos;
                                    foundSplit = true;
                                }
                            }
                        }
                    }

                    if (foundSplit)
                    {
                        var newVertex = CsgCommon.LerpModelVertex(poly.Vertices[i], poly.Vertices[i1],
                            bestT / edgeLen);
                        newVertex.SetPosition(bestPos);

                        poly.Vertices.Insert(i + 1, newVertex);
                        changedLocal = true;
                        ++insertedTotal;
                        --i; // Re-check the shortened edge.
                    }
                }
            }

            return changedLocal;
        }

        while (changed)
        {
            changed = RunPass(true);
            if (!changed)
                changed = RunPass(false);
        }

        outPasses = pass;
        return insertedTotal;
    }

    public static bool SimplifyPolygons(List<CsgPolygon> polygons, float epsilon)
    {
        // Assumptions:
        // - Input polygons may be in any order; we group by (shared_, oriented plane).
        // - Input is not pre-cleaned; only minimal-correct steps described in documentation are applied.
        var polygonsBefore = polygons.Count;

        if (polygons.Count == 0)
            return false;

        if (epsilon <= 0.0f)
            epsilon = CsgCommon.DefaultEpsilon;

        // Group-and-merge stage is only meaningful for 2+ polygons.
        if (polygons.Count < 2)
            return false;

        var normalEps = MathF.Max(1e-4f, epsilon * 0.1f);
        var epsSq = epsilon * epsilon;

        // Step 1: Group by (shared_, oriented plane). No plane canonicalization.
        var planeGroups = new List<PlaneGroup>(polygons.Count);

        foreach (var polygon in polygons)
        {
            var placed = false;
            foreach (var group in planeGroups)
            {
                if (group.Shared == polygon.Shared
                    && AreCoplanarOriented(group.Plane, polygon.Plane, normalEps, epsilon))
                {
                    group.Polys.Add(polygon);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                var group = new PlaneGroup
                {
                    Plane = polygon.Plane,
                    Shared = polygon.Shared
                };
                group.Polys.Add(polygon);
                planeGroups.Add(group);
            }
        }

        polygons.Clear();

        // Step 2: Per-group conservative merging.
        foreach (var planeGroup in planeGroups)
        {
            var group = planeGroup.Polys;
            if (group.Count == 0)
                continue;

            // Pass A: Merge polygons that share a full edge (reverse edge match) to a fixpoint.
            MergeByFullEdgesToFixpoint(group, epsilon, normalEps, epsSq);

            // Pass B: Resolve coplanar T-junctions, then merge again; repeat until no net change.
            if (group.Count > 1)
            {
                for (;;)
                {
                    var polysBefore = group.Count;
                    var vertsBefore = 0;
                    foreach (var polygon in group)
                        vertsBefore += polygon.Vertices.Count;

                    ResolveTJunctions(group, epsilon, out _);
                    MergeByFullEdgesToFixpoint(group, epsilon, normalEps, epsSq);

                    var polysAfter = group.Count;
                    var vertsAfter = 0;
                    foreach (var polygon in group)
                        vertsAfter += polygon.Vertices.Count;

                    if (polysAfter == polysBefore && vertsAfter == vertsBefore)
                        break;
                }
            }

            // Step 3: Emit non-degenerate polygons back to output.
            foreach (var polygon in group)
            {
                if (polygon.Vertices.Count >= 3)
                    polygons.Add(polygon);
            }
        }

        // Return true iff polygon count changed (vertex-only changes are not reported).
        return polygons.Count != polygonsBefore;
    }
}
