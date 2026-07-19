using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public partial class TetrahedralMesh
{
    /// <summary>Edge of tetrahedral mesh.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct TetrahedralMeshEdge
    {
        [InlineArray(2)]
        [StructLayout(LayoutKind.Sequential)]
        public struct UIntArray2
        {
            private uint _element0;
        }

        /// <summary>Indices. Always sorted.</summary>
        public UIntArray2 Indices;
        /// <summary>Degree of silver-ness. The more the worse.</summary>
        public float SilverScore;
        /// <summary>Tetrahedron.</summary>
        public uint TetIndex;
        /// <summary>How many times the edge is present in the tetrahedron.</summary>
        public uint Cardinality;

        /// <summary>Construct default.</summary>
        public TetrahedralMeshEdge()
        {
            Indices = default;
            SilverScore = 0.0f;
            TetIndex = uint.MaxValue;
            Cardinality = 1;
        }

        /// <summary>Construct valid.</summary>
        public TetrahedralMeshEdge(uint i0, uint i1, uint tetIndex, float score)
        {
            Indices = default;
            Indices[0] = i0;
            Indices[1] = i1;
            SilverScore = score;
            TetIndex = tetIndex;
            Cardinality = 1;
            if (Indices[0] > Indices[1])
                (Indices[0], Indices[1]) = (Indices[1], Indices[0]);
        }

        /// <summary>Compare for sorting.</summary>
        public static bool operator <(TetrahedralMeshEdge lhs, TetrahedralMeshEdge rhs)
        {
            if (lhs.Indices[0] != rhs.Indices[0])
                return lhs.Indices[0] < rhs.Indices[0];
            if (lhs.Indices[1] != rhs.Indices[1])
                return lhs.Indices[1] < rhs.Indices[1];
            return lhs.SilverScore < rhs.SilverScore;
        }

        public static bool operator >(TetrahedralMeshEdge lhs, TetrahedralMeshEdge rhs) => rhs < lhs;

        /// <summary>Compare edges.</summary>
        public readonly bool IsSameEdge(TetrahedralMeshEdge rhs)
        {
            return Indices[0] == rhs.Indices[0] && Indices[1] == rhs.Indices[1];
        }
    }

    /// <summary>Solve cubic equation x^3 + a*x^2 + b*x + c = 0.</summary>
    private static int SolveCubicEquation(double[] result, double a, double b, double c, double eps)
    {
        // Performance-critical code, don't use degree-based functions here
        var a2 = a * a;
        var q = (a2 - 3 * b) / 9;
        var r = (a * (2 * a2 - 9 * b) + 27 * c) / 54;
        var r2 = r * r;
        var q3 = q * q * q;
        if (r2 <= q3 + eps)
        {
            var t = r / Math.Sqrt(q3);
            if (t < -1)
                t = -1;
            if (t > 1)
                t = 1;
            t = Math.Acos(t);
            a /= 3;
            q = -2 * Math.Sqrt(q);
            result[0] = q * Math.Cos(t / 3) - a;
            result[1] = q * Math.Cos((t + Math.PI * 2) / 3) - a;
            result[2] = q * Math.Cos((t - Math.PI * 2) / 3) - a;
            return 3;
        }

        var A = -Math.Cbrt(Math.Abs(r) + Math.Sqrt(r2 - q3));
        if (r < 0)
            A = -A;
        var B = A == 0 ? 0 : q / A;

        a /= 3;
        result[0] = A + B - a;
        result[1] = -0.5 * (A + B) - a;
        result[2] = 0.5 * Math.Sqrt(3.0) * (A - B);
        if (Math.Abs(result[2]) < eps)
        {
            result[2] = result[1];
            return 2;
        }
        return 1;
    }

    /// <summary>Calculate most positive root of cubic equation x^3 + a*x^2 + b*x + c = 0.</summary>
    private static float SolveCubic(Vector3f abc)
    {
        var results = new double[3];
        var numRoots = SolveCubicEquation(results, abc.X, abc.Y, abc.Z, MathDefs.Epsilon);
        return (float)GetSmallestPositiveRoot(results, numRoots);
    }

    /// <summary>Calculate most positive root of quadratic or linear equation a*x^2 + b*x + c = 0.</summary>
    private static float SolveQuadratic(Vector3f abc)
    {
        var a = abc.X;
        var b = abc.Y;
        var c = abc.Z;
        if (MathF.Abs(a) < MathDefs.Epsilon)
            return -c / b;

        var discriminant = Math.Max(0.0f, b * b - 4 * a * c);
        var invA = 1.0f / (2 * a);
        var roots = new double[]
        {
            (-b + Math.Sqrt(discriminant)) * invA,
            (-b - Math.Sqrt(discriminant)) * invA
        };
        return (float)GetSmallestPositiveRoot(roots, roots.Length);
    }

    /// <summary>Pick smallest positive root.</summary>
    private static double GetSmallestPositiveRoot(double[] roots, int count)
    {
        var root = (double)MathDefs.LargeValue;
        for (var i = 0; i < count; ++i)
        {
            if (roots[i] > -MathDefs.LargeEpsilon && roots[i] < root)
                root = roots[i];
        }
        return Math.Max(0.0, root);
    }

    /// <summary>Calculate barycentric coordinates on triangle.</summary>
    private static Vector3f GetTriangleBarycentricCoords(Vector3f position, Vector3f p1, Vector3f p2, Vector3f p3)
    {
        var v12 = p2 - p1;
        var v13 = p3 - p1;
        var v0 = position - p1;
        var d00 = v12.DotProduct(v12);
        var d01 = v12.DotProduct(v13);
        var d11 = v13.DotProduct(v13);
        var d20 = v0.DotProduct(v12);
        var d21 = v0.DotProduct(v13);
        var denominator = d00 * d11 - d01 * d01;
        var v = (d11 * d20 - d01 * d21) / denominator;
        var w = (d00 * d21 - d01 * d20) / denominator;
        var u = 1.0f - v - w;
        return new Vector3f(u, v, w);
    }

    /// <summary>Find tetrahedron for given position. Ignore removed tetrahedrons. Return invalid index if cannot find.</summary>
    private uint FindTetrahedron(Vector3f position, List<bool> removed)
    {
        var firstNotRemoved = -1;
        for (var i = 0; i < removed.Count; ++i)
        {
            if (!removed[i])
            {
                firstNotRemoved = i;
                break;
            }
        }
        if (firstNotRemoved < 0)
            return uint.MaxValue;

        var maxIters = (uint)Tetrahedrons.Count;
        var tetIndex = (uint)firstNotRemoved;
        for (uint i = 0; i < maxIters; ++i)
        {
            // Found one
            var weights = GetInnerBarycentricCoords(tetIndex, position);
            if (weights.X >= 0.0f && weights.Y >= 0.0f && weights.Z >= 0.0f && weights.W >= 0.0f)
                break;

            if (weights.X < weights.Y && weights.X < weights.Z && weights.X < weights.W)
                tetIndex = Tetrahedrons[(int)tetIndex].Neighbors[0];
            else if (weights.Y < weights.Z && weights.Y < weights.W)
                tetIndex = Tetrahedrons[(int)tetIndex].Neighbors[1];
            else if (weights.Z < weights.W)
                tetIndex = Tetrahedrons[(int)tetIndex].Neighbors[2];
            else
                tetIndex = Tetrahedrons[(int)tetIndex].Neighbors[3];

            // Failed to find one
            if (tetIndex == uint.MaxValue)
                break;
        }
        return tetIndex;
    }

    /// <summary>Number of initial super-mesh vertices.</summary>
    private const uint NumSuperMeshVertices = 8;

    /// <summary>Create super-mesh for Delaunay triangulation.</summary>
    private void InitializeSuperMesh(BoundingBox boundingBox)
    {
        var offsets = new[]
        {
            new Vector3f(0.0f, 0.0f, 0.0f),
            new Vector3f(1.0f, 0.0f, 0.0f),
            new Vector3f(0.0f, 1.0f, 0.0f),
            new Vector3f(1.0f, 1.0f, 0.0f),
            new Vector3f(0.0f, 0.0f, 1.0f),
            new Vector3f(1.0f, 0.0f, 1.0f),
            new Vector3f(0.0f, 1.0f, 1.0f),
            new Vector3f(1.0f, 1.0f, 1.0f)
        };
        var indices = new uint[][]
        {
            [0, 1, 2, 4],
            [3, 1, 2, 7],
            [5, 1, 4, 7],
            [6, 2, 4, 7],
            [1, 2, 4, 7]
        };
        var neighbors = new uint[][]
        {
            [4, uint.MaxValue, uint.MaxValue, uint.MaxValue],
            [4, uint.MaxValue, uint.MaxValue, uint.MaxValue],
            [4, uint.MaxValue, uint.MaxValue, uint.MaxValue],
            [4, uint.MaxValue, uint.MaxValue, uint.MaxValue],
            [3, 2, 1, 0]
        };

        Vertices.Clear();
        for (var i = 0; i < NumSuperMeshVertices; ++i)
            Vertices.Add(boundingBox.Min + boundingBox.Size * offsets[i]);

        Tetrahedrons.Clear();
        for (var i = 0; i < indices.Length; ++i)
        {
            var tetrahedron = new Tetrahedron();
            for (var j = 0; j < 4; ++j)
            {
                tetrahedron.Indices[j] = indices[i][j];
                tetrahedron.Neighbors[j] = neighbors[i][j];
            }
            tetrahedron.CalculateInnerMatrix(Vertices);
            Tetrahedrons.Add(tetrahedron);
        }
    }

    /// <summary>Build tetrahedrons for given positions.</summary>
    private void BuildTetrahedrons(IReadOnlyList<Vector3f> positions)
    {
        // Initialize context
        var ctx = new DelaunayContext();
        for (var i = 0; i < Tetrahedrons.Count; ++i)
        {
            ctx.Circumspheres.Add(default);
            ctx.Removed.Add(false);
        }
        for (var i = 0; i < Tetrahedrons.Count; ++i)
            ctx.Circumspheres[i] = GetTetrahedronCircumsphere((uint)i);

        // Append vertices and initialize queue
        var startVertex = (uint)Vertices.Count;
        for (var i = 0; i < positions.Count; ++i)
            Vertices.Add(positions[i]);

        var verticesQueue = new List<uint>();
        for (var newVertexIndex = startVertex; newVertexIndex < Vertices.Count; ++newVertexIndex)
            verticesQueue.Add(newVertexIndex);

        // Triangulate
        var holeSurface = new TetrahedralMeshSurface();
        var removedTetrahedrons = new List<uint>();
        var postponedVertices = new List<uint>();
        while (verticesQueue.Count > 0)
        {
            // Process current bunch of vertices
            postponedVertices.Clear();
            for (var i = 0; i < verticesQueue.Count; ++i)
            {
                var newVertexIndex = verticesQueue[i];
                // Carve hole in the mesh
                if (!FindAndRemoveIntersected(ref ctx, Vertices[(int)newVertexIndex], ref holeSurface, removedTetrahedrons))
                {
                    postponedVertices.Add(newVertexIndex);
                    continue;
                }

                // Disconnect carved out tetrahedrons
                DisconnectRemovedTetrahedrons(removedTetrahedrons);

                // Allocate space for new tetrahedrons
                while (removedTetrahedrons.Count < holeSurface.Faces.Count)
                {
                    removedTetrahedrons.Add((uint)Tetrahedrons.Count);
                    Tetrahedrons.Add(new Tetrahedron());
                    ctx.Circumspheres.Add(default);
                    ctx.Removed.Add(true);
                }

                // Fill hole with tetrahedrons
                FillStarShapedHole(ref ctx, removedTetrahedrons, in holeSurface, newVertexIndex);
            }

            // Re-enqueue postponed vertices
            (postponedVertices, verticesQueue) = (verticesQueue, postponedVertices);

            // If all the vertices are postponed again, ignore them
            if (postponedVertices.Count == verticesQueue.Count)
            {
                Log.GetLogger().LogWarning(
                    "{Count} vertices are excluded from triangulation due to mathematical fluctuations",
                    postponedVertices.Count);
                break;
            }
        }

        // Dump failed attempts for debugging
        DebugHighlightEdges.Clear();
        for (var i = 0; i < verticesQueue.Count; ++i)
            FindAndRemoveIntersected(ref ctx, Vertices[(int)verticesQueue[i]], ref holeSurface, removedTetrahedrons, true);

        // Finalize triangulation
        DisconnectSuperMeshTetrahedrons(ctx.Removed);
        FilterMeshSurface(ctx.Removed);
        EnsureMeshConnectivity(ctx.Removed);
        RemoveMarkedTetrahedrons(ctx.Removed);
        RemoveSuperMeshVertices();
        UpdateIgnoredVertices();

        NumInnerTetrahedrons = (uint)Tetrahedrons.Count;
        Debug.Assert(IsAdjacencyValid(false));

        if (IgnoredVertices.Count > verticesQueue.Count)
        {
            Log.GetLogger().LogWarning(
                "Triangulation is incomplete because vertices are too sparse, {Count} vertices are ignored",
                verticesQueue.Count);
        }

        // Build the outer space and precompute matrices
        var hullSurface = new TetrahedralMeshSurface();
        BuildHullSurface(ref hullSurface);
        CalculateHullNormals(in hullSurface);
        BuildOuterTetrahedrons(in hullSurface);
        CalculateOuterMatrices();
    }

    /// <summary>Return whether the adjacency is valid.</summary>
    private bool IsAdjacencyValid(bool fullyConnected)
    {
        for (var tetIndex = 0; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                var neighborIndex = Tetrahedrons[tetIndex].Neighbors[faceIndex];
                if (neighborIndex != uint.MaxValue)
                {
                    var neighborTetrahedron = Tetrahedrons[(int)neighborIndex];
                    if (!neighborTetrahedron.HasNeighbor((uint)tetIndex))
                        return false;
                }
                else if (fullyConnected)
                    return false;
            }
        }

        return true;
    }

    /// <summary>Disconnect tetrahedron from mesh.</summary>
    private void DisconnectTetrahedron(uint tetIndex)
    {
        var tetrahedron = Tetrahedrons[(int)tetIndex];
        for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
        {
            var neighborIndex = tetrahedron.Neighbors[faceIndex];
            if (neighborIndex != uint.MaxValue)
            {
                var neighborTetrahedron = Tetrahedrons[(int)neighborIndex];
                var neighborFaceIndex = neighborTetrahedron.GetNeighborFaceIndex(tetIndex);
                Debug.Assert(neighborFaceIndex < 4);
                neighborTetrahedron.Neighbors[(int)neighborFaceIndex] = uint.MaxValue;
                Tetrahedrons[(int)neighborIndex] = neighborTetrahedron;
            }
        }
    }

    /// <summary>Data used for Delaunay triangulation.</summary>
    private struct DelaunayContext
    {
        /// <summary>Circumspheres of mesh tetrahedrons.</summary>
        public List<HighPrecisionSphere> Circumspheres;

        /// <summary>Whether the tetrahedron is removed.</summary>
        public List<bool> Removed;

        public DelaunayContext()
        {
            Circumspheres = [];
            Removed = [];
        }

        /// <summary>Tests if point is inside circumsphere of tetrahedron.</summary>
        public readonly bool IsInsideCircumsphere(uint tetIndex, Vector3f position)
        {
            return Circumspheres[(int)tetIndex].Distance(position) < MathDefs.LargeEpsilon;
        }
    }

    /// <summary>Find and remove tetrahedrons whose circumspheres intersect given point.</summary>
    private bool FindAndRemoveIntersected(ref DelaunayContext ctx, Vector3f position,
        ref TetrahedralMeshSurface holeSurface, List<uint> removedTetrahedrons, bool dumpErrors = false)
    {
        // Reset output
        holeSurface.Clear();
        removedTetrahedrons.Clear();

        // Find first tetrahedron to remove
        var firstTetIndex = FindTetrahedron(position, ctx.Removed);
        if (firstTetIndex == uint.MaxValue || !ctx.IsInsideCircumsphere(firstTetIndex, position))
        {
            Log.GetLogger().LogError("Cannot find tetrahedron to insert vertex at {Position}", position);
            return false;
        }

        removedTetrahedrons.Add(firstTetIndex);
        ctx.Removed[(int)firstTetIndex] = true;

        // Do breadth search to collect all bad tetrahedrons
        // Note: size may change during the loop
        for (var i = 0; i < removedTetrahedrons.Count; ++i)
        {
            var tetIndex = removedTetrahedrons[i];
            var tetrahedron = Tetrahedrons[(int)tetIndex];

            // Process neighbors
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                var neighborTetIndex = tetrahedron.Neighbors[faceIndex];

                // Outer surface is reached
                if (neighborTetIndex == uint.MaxValue)
                    continue;

                // Ignore already removed tetrahedrons
                if (ctx.Removed[(int)neighborTetIndex])
                    continue;

                // If circumsphere of neighbor tetrahedron contains the point, remove this neighbor and queue it.
                if (ctx.IsInsideCircumsphere(neighborTetIndex, position))
                {
                    removedTetrahedrons.Add(neighborTetIndex);
                    ctx.Removed[(int)neighborTetIndex] = true;
                }
            }
        }

        // Collect triangles of the hole surface
        for (var i = 0; i < removedTetrahedrons.Count; ++i)
        {
            var tetIndex = removedTetrahedrons[i];
            var tetrahedron = Tetrahedrons[(int)tetIndex];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                var neighborTetIndex = tetrahedron.Neighbors[faceIndex];

                // Outer surface is reached
                if (neighborTetIndex == uint.MaxValue)
                {
                    // Face of outer surface doesn't have underlying tetrahedron
                    holeSurface.Faces.Add(tetrahedron.GetTriangleFace((uint)faceIndex, uint.MaxValue, uint.MaxValue));
                    continue;
                }

                // If neighbor is not removed, add face
                if (!ctx.Removed[(int)neighborTetIndex])
                {
                    var neighborTetrahedron = Tetrahedrons[(int)neighborTetIndex];
                    var neighborFaceIndex = neighborTetrahedron.GetNeighborFaceIndex(tetIndex);
                    holeSurface.Faces.Add(neighborTetrahedron.GetTriangleFace(neighborFaceIndex,
                        neighborTetIndex, neighborFaceIndex));
                }
            }
        }

        // Verify that all hole triangles are faced in right direction
        var valid = true;
        var p0 = new Vector3d(position.X, position.Y, position.Z);
        for (var i = 0; i < holeSurface.Faces.Count; ++i)
        {
            var triangle = holeSurface.Faces[i];

            // Outer triangles are always oriented right
            if (triangle.TetIndex == uint.MaxValue)
                continue;

            // Normalize triangle orientation
            triangle.Normalize(Vertices);
            holeSurface.Faces[i] = triangle;

            var p1 = new Vector3d(Vertices[(int)triangle.Indices[0]].X, Vertices[(int)triangle.Indices[0]].Y, Vertices[(int)triangle.Indices[0]].Z);
            var p2 = new Vector3d(Vertices[(int)triangle.Indices[1]].X, Vertices[(int)triangle.Indices[1]].Y, Vertices[(int)triangle.Indices[1]].Z);
            var p3 = new Vector3d(Vertices[(int)triangle.Indices[2]].X, Vertices[(int)triangle.Indices[2]].Y, Vertices[(int)triangle.Indices[2]].Z);
            var normal = (p2 - p1).CrossProduct(p3 - p1);
            var distance = (p0 - p1).DotProduct(normal);

            // If coplanar or worse, cannot add new vertex
            if (distance < MathDefs.LargeEpsilon)
            {
                valid = false;
                break;
            }
        }

        // Try to initialize adjacency for surface
        if (valid && !holeSurface.CalculateAdjacency())
            valid = false;

        // Revert all changes if invalid or if dump error mode is on
        if (!valid || dumpErrors)
        {
            if (dumpErrors)
            {
                Debug.Assert(!valid);
                for (var i = 0; i < holeSurface.Faces.Count; ++i)
                {
                    var triangle = holeSurface.Faces[i];
                    var i0 = triangle.Indices[0];
                    var i1 = triangle.Indices[1];
                    var i2 = triangle.Indices[2];
                    DebugHighlightEdges.Add((i0, i1));
                    DebugHighlightEdges.Add((i1, i2));
                    DebugHighlightEdges.Add((i2, i0));
                }
            }

            for (var i = 0; i < removedTetrahedrons.Count; ++i)
                ctx.Removed[(int)removedTetrahedrons[i]] = false;

            removedTetrahedrons.Clear();
            holeSurface.Clear();
            return false;
        }

        if (!holeSurface.IsClosedSurface())
        {
            Log.GetLogger().LogError(
                "Surface of the carved hole in tetrahedral mesh is incomplete for vertex at {Position}",
                position);
            holeSurface.Clear();
            return false;
        }

        return true;
    }

    /// <summary>Disconnect removed tetrahedrons from the rest.</summary>
    private void DisconnectRemovedTetrahedrons(List<uint> removedTetrahedrons)
    {
        // Disconnect removed tetrahedrons
        for (var i = 0; i < removedTetrahedrons.Count; ++i)
        {
            var tetIndex = removedTetrahedrons[i];
            var tetrahedron = Tetrahedrons[(int)tetIndex];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                var neighborTetIndex = tetrahedron.Neighbors[faceIndex];
                if (neighborTetIndex != uint.MaxValue)
                {
                    var neighborTetrahedron = Tetrahedrons[(int)neighborTetIndex];
                    var neighborFaceIndex = neighborTetrahedron.GetNeighborFaceIndex(tetIndex);
                    tetrahedron.Neighbors[faceIndex] = uint.MaxValue;
                    neighborTetrahedron.Neighbors[(int)neighborFaceIndex] = uint.MaxValue;
                    Tetrahedrons[(int)neighborTetIndex] = neighborTetrahedron;
                }
            }
            Tetrahedrons[(int)tetIndex] = tetrahedron;
        }
    }

    /// <summary>Fill star-shaped hole with tetrahedrons connected to specified vertex.</summary>
    private void FillStarShapedHole(ref DelaunayContext ctx, List<uint> outputTetrahedrons,
        in TetrahedralMeshSurface holeSurface, uint centerIndex)
    {
        for (uint i = 0; i < holeSurface.Faces.Count; ++i)
        {
            var newTetIndex = outputTetrahedrons[(int)i];
            var tetrahedron = Tetrahedrons[(int)newTetIndex];
            var holeTriangle = holeSurface.Faces[(int)i];

            // Connect to newly added (or to be added) adjacent tetrahedrons filling the hole
            for (var j = 0; j < 3; ++j)
            {
                tetrahedron.Indices[j] = holeTriangle.Indices[j];
                tetrahedron.Neighbors[j] = outputTetrahedrons[(int)holeTriangle.Neighbors[j]];
            }

            // Connect to tetrahedron outside the hole
            tetrahedron.Indices[3] = centerIndex;
            tetrahedron.Neighbors[3] = holeTriangle.TetIndex;
            if (holeTriangle.TetIndex != uint.MaxValue)
            {
                var neighborTetrahedron = Tetrahedrons[(int)holeTriangle.TetIndex];
                Debug.Assert(neighborTetrahedron.Neighbors[(int)holeTriangle.TetFace] == uint.MaxValue);
                neighborTetrahedron.Neighbors[(int)holeTriangle.TetFace] = newTetIndex;
                Tetrahedrons[(int)holeTriangle.TetIndex] = neighborTetrahedron;
            }

            tetrahedron.CalculateInnerMatrix(Vertices);
            Tetrahedrons[(int)newTetIndex] = tetrahedron;
            ctx.Removed[(int)newTetIndex] = false;
            ctx.Circumspheres[(int)newTetIndex] = GetTetrahedronCircumsphere(newTetIndex);
        }
    }

    /// <summary>Mark super-mesh tetrahedrons in the to-be-removed array and disconnect all related adjacency.</summary>
    private void DisconnectSuperMeshTetrahedrons(List<bool> removed)
    {
        for (var tetIndex = 0; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            // Any tetrahedron containing super-vertex is to be removed
            var tetrahedron = Tetrahedrons[tetIndex];
            for (var j = 0; j < 4; ++j)
            {
                if (tetrahedron.Indices[j] < NumSuperMeshVertices)
                {
                    removed[tetIndex] = true;
                    break;
                }
            }

            if (!removed[tetIndex])
                continue;
            DisconnectTetrahedron((uint)tetIndex);
        }
    }

    /// <summary>Ensure mesh connectivity, remove disconnected parts.</summary>
    private void EnsureMeshConnectivity(List<bool> removed)
    {
        // Find first tetrahedron
        var firstTetIndex = 0;
        for (; firstTetIndex < Tetrahedrons.Count; ++firstTetIndex)
        {
            if (!removed[firstTetIndex])
                break;
        }

        // Mesh is empty
        if (firstTetIndex >= Tetrahedrons.Count)
            return;

        // Do breadth search to collect all tetrahedrons
        // Note: size may change during the loop
        var queue = new List<uint>();
        var visited = new List<bool>(Tetrahedrons.Count);
        for (var i = 0; i < Tetrahedrons.Count; ++i)
            visited.Add(false);

        queue.Add((uint)firstTetIndex);
        visited[firstTetIndex] = true;

        for (var i = 0; i < queue.Count; ++i)
        {
            var tetIndex = queue[i];
            var tetrahedron = Tetrahedrons[(int)tetIndex];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                var neighborTetIndex = tetrahedron.Neighbors[faceIndex];
                if (neighborTetIndex == uint.MaxValue)
                    continue;
                if (removed[(int)neighborTetIndex] || visited[(int)neighborTetIndex])
                    continue;

                queue.Add(neighborTetIndex);
                visited[(int)neighborTetIndex] = true;
            }
        }

        // Remove all non-visited tetrahedrons, no need to disconnect them
        for (var tetIndex = 0; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            if (!visited[tetIndex])
                removed[tetIndex] = true;
        }
    }

    /// <summary>Collect surface tetrahedrons and ensure that the surface doesn't have edge connections.</summary>
    private void FilterMeshSurface(List<bool> removed)
    {
        var surfaceEdges = new List<TetrahedralMeshEdge>();

        // Append edge to vector or update cardinality and score if already present.
        void AppendOrAccumulate(uint startIndex, TetrahedralMeshEdge edge)
        {
            // Try to update existing edges
            for (var i = (int)startIndex; i < surfaceEdges.Count; ++i)
            {
                var existingEdge = surfaceEdges[i];
                if (existingEdge.IsSameEdge(edge) && existingEdge.TetIndex == edge.TetIndex)
                {
                    ++existingEdge.Cardinality;
                    existingEdge.SilverScore = MathF.Max(existingEdge.SilverScore, edge.SilverScore);
                    surfaceEdges[i] = existingEdge;
                    return;
                }
            }

            // Append new edge
            surfaceEdges.Add(edge);
        }

        // Collect surface edges
        for (var tetIndex = 0; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            var tetrahedron = Tetrahedrons[tetIndex];
            if (removed[tetIndex])
                continue;

            var startIndex = (uint)surfaceEdges.Count;
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                if (tetrahedron.Neighbors[faceIndex] != uint.MaxValue)
                    continue;

                var triangle = tetrahedron.GetTriangleFace((uint)faceIndex, (uint)tetIndex, (uint)faceIndex);
                var score = triangle.CalculateScore(Vertices);
                AppendOrAccumulate(startIndex, new TetrahedralMeshEdge(triangle.Indices[0], triangle.Indices[1], (uint)tetIndex, score));
                AppendOrAccumulate(startIndex, new TetrahedralMeshEdge(triangle.Indices[1], triangle.Indices[2], (uint)tetIndex, score));
                AppendOrAccumulate(startIndex, new TetrahedralMeshEdge(triangle.Indices[2], triangle.Indices[0], (uint)tetIndex, score));
            }
        }

        // Remove extra tetrahedrons
        var removedTetrahedrons = new List<uint>();
        surfaceEdges.Sort((lhs, rhs) => lhs < rhs ? -1 : rhs < lhs ? 1 : 0);
        var firstEdge = 0;
        while (firstEdge < surfaceEdges.Count)
        {
            var lastEdge = firstEdge + 1;
            while (lastEdge < surfaceEdges.Count && surfaceEdges[firstEdge].IsSameEdge(surfaceEdges[lastEdge]))
                ++lastEdge;

            var totalCardinality = 0u;
            for (var i = firstEdge; i < lastEdge; ++i)
            {
                if (!removed[(int)surfaceEdges[i].TetIndex])
                    totalCardinality += surfaceEdges[i].Cardinality;
            }

            // Need to remove some tetrahedrons, going from back to front
            const uint maxCardinality = 2;
            if (totalCardinality > maxCardinality)
            {
                var remainingCardinality = totalCardinality;
                for (var i = lastEdge - 1; i > firstEdge; --i)
                {
                    var edge = surfaceEdges[i];
                    if (removed[(int)edge.TetIndex])
                        continue;

                    removed[(int)edge.TetIndex] = true;
                    removedTetrahedrons.Add(edge.TetIndex);
                    remainingCardinality -= edge.Cardinality;
                    if (remainingCardinality <= maxCardinality)
                        break;
                }
            }

            firstEdge = lastEdge;
        }

        // Disconnect removed tetrahedrons
        for (var i = 0; i < removedTetrahedrons.Count; ++i)
            DisconnectTetrahedron(removedTetrahedrons[i]);
    }

    /// <summary>Remove marked tetrahedrons from array.</summary>
    private void RemoveMarkedTetrahedrons(IReadOnlyList<bool> removed)
    {
        // Prepare for reconstruction
        var tetrahedronsCopy = new List<Tetrahedron>(Tetrahedrons);
        var oldToNewIndexMap = new uint[tetrahedronsCopy.Count];
        Tetrahedrons.Clear();

        // Rebuild array and create index
        for (var oldTetIndex = 0; oldTetIndex < tetrahedronsCopy.Count; ++oldTetIndex)
        {
            if (removed[oldTetIndex])
            {
                oldToNewIndexMap[oldTetIndex] = uint.MaxValue;
                continue;
            }

            oldToNewIndexMap[oldTetIndex] = (uint)Tetrahedrons.Count;
            Tetrahedrons.Add(tetrahedronsCopy[oldTetIndex]);
        }

        // Adjust neighbor indices
        for (var i = 0; i < Tetrahedrons.Count; ++i)
        {
            var tetrahedron = Tetrahedrons[i];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                var oldIndex = tetrahedron.Neighbors[faceIndex];
                if (oldIndex != uint.MaxValue)
                {
                    var newIndex = oldToNewIndexMap[(int)oldIndex];
                    Debug.Assert(newIndex != uint.MaxValue);
                    tetrahedron.Neighbors[faceIndex] = newIndex;
                }
            }
            Tetrahedrons[i] = tetrahedron;
        }
    }

    /// <summary>Remove super-mesh vertices.</summary>
    private void RemoveSuperMeshVertices()
    {
        var oldVertices = new List<Vector3f>(Vertices);
        for (var i = 0; i < Vertices.Count; ++i)
            Vertices[i] = oldVertices[(i + (int)NumSuperMeshVertices) % oldVertices.Count];

        for (var tetIndex = 0; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            var tetrahedron = Tetrahedrons[tetIndex];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
                tetrahedron.Indices[faceIndex] -= NumSuperMeshVertices;
            Tetrahedrons[tetIndex] = tetrahedron;
        }

        for (var i = 0; i < DebugHighlightEdges.Count; ++i)
        {
            var edge = DebugHighlightEdges[i];
            var first = edge.First < NumSuperMeshVertices
                ? edge.First + (uint)Vertices.Count - NumSuperMeshVertices
                : edge.First - NumSuperMeshVertices;
            var second = edge.Second < NumSuperMeshVertices
                ? edge.Second + (uint)Vertices.Count - NumSuperMeshVertices
                : edge.Second - NumSuperMeshVertices;
            DebugHighlightEdges[i] = (first, second);
        }
    }

    /// <summary>Update array of ignored vertices.</summary>
    private void UpdateIgnoredVertices()
    {
        var ignored = new bool[Vertices.Count];
        Array.Fill(ignored, true);
        for (var i = 0; i < Tetrahedrons.Count; ++i)
        {
            var tetrahedron = Tetrahedrons[i];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
                ignored[tetrahedron.Indices[faceIndex]] = false;
        }

        IgnoredVertices.Clear();
        for (var vertexIndex = 0; vertexIndex < Vertices.Count - (int)NumSuperMeshVertices; ++vertexIndex)
        {
            if (ignored[vertexIndex])
                IgnoredVertices.Add((uint)vertexIndex);
        }
    }

    /// <summary>Build hull surface.</summary>
    private void BuildHullSurface(ref TetrahedralMeshSurface hullSurface)
    {
        hullSurface.Clear();
        for (var tetIndex = 0; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            var tetrahedron = Tetrahedrons[tetIndex];
            for (var faceIndex = 0; faceIndex < 4; ++faceIndex)
            {
                if (tetrahedron.Neighbors[faceIndex] != uint.MaxValue)
                    continue;

                var hullTriangle = tetrahedron.GetTriangleFace((uint)faceIndex, (uint)tetIndex, (uint)faceIndex);
                hullSurface.Faces.Add(hullTriangle);
            }
        }

        hullSurface.CalculateAdjacency();
        Debug.Assert(hullSurface.IsClosedSurface());
        for (var i = 0; i < hullSurface.Faces.Count; ++i)
        {
            var hullTriangle = hullSurface.Faces[i];
            hullTriangle.Normalize(Vertices);
            hullSurface.Faces[i] = hullTriangle;
        }
    }

    /// <summary>Calculate hull normals.</summary>
    private void CalculateHullNormals(in TetrahedralMeshSurface hullSurface)
    {
        if (HullNormals.Count > Vertices.Count)
            HullNormals.RemoveRange(Vertices.Count, HullNormals.Count - Vertices.Count);
        while (HullNormals.Count < Vertices.Count)
            HullNormals.Add(Vector3f.Zero);

        for (var i = 0; i < hullSurface.Faces.Count; ++i)
        {
            var triangle = hullSurface.Faces[i];
            var p1 = Vertices[(int)triangle.Indices[0]];
            var p2 = Vertices[(int)triangle.Indices[1]];
            var p3 = Vertices[(int)triangle.Indices[2]];
            var normal = (p2 - p1).CrossProduct(p3 - p1);

            // Accumulate vertex normals
            for (var j = 0; j < 3; ++j)
                HullNormals[(int)triangle.Indices[j]] += normal;
        }

        // Normalize outputs
        for (var i = 0; i < HullNormals.Count; ++i)
        {
            if (HullNormals[i] != Vector3f.Zero)
            {
                var normal = HullNormals[i];
                normal.Normalize();
                HullNormals[i] = normal;
            }
        }
    }

    /// <summary>Build outer tetrahedrons.</summary>
    private void BuildOuterTetrahedrons(in TetrahedralMeshSurface hullSurface)
    {
        var targetCount = (int)NumInnerTetrahedrons + hullSurface.Faces.Count;
        while (Tetrahedrons.Count < targetCount)
            Tetrahedrons.Add(new Tetrahedron());
        if (Tetrahedrons.Count > targetCount)
            Tetrahedrons.RemoveRange(targetCount, Tetrahedrons.Count - targetCount);

        for (var tetIndex = (int)NumInnerTetrahedrons; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            var tetrahedron = Tetrahedrons[tetIndex];
            var hullTriangle = hullSurface.Faces[tetIndex - (int)NumInnerTetrahedrons];
            for (var faceIndex = 0; faceIndex < 3; ++faceIndex)
            {
                tetrahedron.Indices[faceIndex] = hullTriangle.Indices[faceIndex];
                tetrahedron.Neighbors[faceIndex] = NumInnerTetrahedrons + hullTriangle.Neighbors[faceIndex];
            }

            tetrahedron.Indices[3] = Tetrahedron.Infinity3;
            tetrahedron.Neighbors[3] = hullTriangle.TetIndex;
            var neighborTetrahedron = Tetrahedrons[(int)hullTriangle.TetIndex];
            neighborTetrahedron.Neighbors[(int)hullTriangle.TetFace] = (uint)tetIndex;
            Tetrahedrons[(int)hullTriangle.TetIndex] = neighborTetrahedron;
            Tetrahedrons[tetIndex] = tetrahedron;
        }

        Debug.Assert(IsAdjacencyValid(true));
    }

    /// <summary>Calculate matrices for outer tetrahedrons.</summary>
    private void CalculateOuterMatrices()
    {
        for (var tetIndex = (int)NumInnerTetrahedrons; tetIndex < Tetrahedrons.Count; ++tetIndex)
        {
            var tetrahedron = Tetrahedrons[tetIndex];
            var positions = new Vector3f[3];
            var normals = new Vector3f[3];
            for (var i = 0; i < 3; ++i)
            {
                positions[i] = Vertices[(int)tetrahedron.Indices[i]];
                normals[i] = HullNormals[(int)tetrahedron.Indices[i]];
            }

            var A = positions[0] - positions[2];
            var Ap = normals[0] - normals[2];
            var B = positions[1] - positions[2];
            var Bp = normals[1] - normals[2];
            var P2 = positions[2];
            var Cp = -normals[2];
            var m = tetrahedron.Matrix;

            m.M00 = +Ap.Y * Bp.Z - Ap.Z * Bp.Y;
            m.M01 = -Ap.X * Bp.Z + Ap.Z * Bp.X;
            m.M02 = +Ap.X * Bp.Y - Ap.Y * Bp.X;
            m.M03 = +A.X * Bp.Y * Cp.Z - A.Y * Bp.X * Cp.Z + Ap.X * B.Y * Cp.Z - Ap.Y * B.X * Cp.Z
                + A.Z * Bp.X * Cp.Y - A.Z * Bp.Y * Cp.X + Ap.Z * B.X * Cp.Y - Ap.Z * B.Y * Cp.X
                - A.X * Bp.Z * Cp.Y + A.Y * Bp.Z * Cp.X - Ap.X * B.Z * Cp.Y + Ap.Y * B.Z * Cp.X;
            m.M03 -= P2.X * m.M00 + P2.Y * m.M01 + P2.Z * m.M02;

            m.M10 = +Ap.Y * B.Z + A.Y * Bp.Z - Ap.Z * B.Y - A.Z * Bp.Y;
            m.M11 = -A.X * Bp.Z - Ap.X * B.Z + A.Z * Bp.X + Ap.Z * B.X;
            m.M12 = +A.X * Bp.Y - A.Y * Bp.X + Ap.X * B.Y - Ap.Y * B.X;
            m.M13 = +A.X * B.Y * Cp.Z - A.Y * B.X * Cp.Z - A.X * B.Z * Cp.Y + A.Y * B.Z * Cp.X
                + A.Z * B.X * Cp.Y - A.Z * B.Y * Cp.X;
            m.M13 -= P2.X * m.M10 + P2.Y * m.M11 + P2.Z * m.M12;

            m.M20 = -A.Z * B.Y + A.Y * B.Z;
            m.M21 = -A.X * B.Z + A.Z * B.X;
            m.M22 = +A.X * B.Y - A.Y * B.X;
            m.M23 = 0.0f;
            m.M23 -= P2.X * m.M20 + P2.Y * m.M21 + P2.Z * m.M22;

            var a = +Ap.X * Bp.Y * Cp.Z - Ap.Y * Bp.X * Cp.Z + Ap.Z * Bp.X * Cp.Y
                - Ap.Z * Bp.Y * Cp.X + Ap.Y * Bp.Z * Cp.X - Ap.X * Bp.Z * Cp.Y;

            if (MathF.Abs(a) > MathDefs.LargeEpsilon)
            {
                // d is not zero, so the polynomial at^3 + bt^2 + ct + d = 0 is actually cubic
                // and we can simplify to the monic form t^3 + pt^2 + qt + r = 0
                var scale = 1.0f / a;
                m.M00 *= scale;
                m.M01 *= scale;
                m.M02 *= scale;
                m.M03 *= scale;
                m.M10 *= scale;
                m.M11 *= scale;
                m.M12 *= scale;
                m.M13 *= scale;
                m.M20 *= scale;
                m.M21 *= scale;
                m.M22 *= scale;
                m.M23 *= scale;
            }
            else
            {
                // It's actually a quadratic or even linear equation
                tetrahedron.Indices[3] = Tetrahedron.Infinity2;
            }

            tetrahedron.Matrix = m;
            Tetrahedrons[tetIndex] = tetrahedron;
        }
    }

}
