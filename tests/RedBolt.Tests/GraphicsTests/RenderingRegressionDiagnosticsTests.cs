using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderingRegressionDiagnosticsTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderingRegressionDiagnosticsTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Rbfx_mushroom_reports_projected_front_face_winding()
    {
        RbfxTestData.AddResourceDirs(Resources);
        try
        {
            var model = Resources.GetResource<Model>("Models/Mushroom.mdl");
            var geometry = model.GetGeometry(0);
            Assert.NotNull(geometry);

            var vertexBuffer = geometry!.GetVertexBuffer(0);
            var indexBuffer = geometry.GetIndexBuffer();
            Assert.NotNull(vertexBuffer);
            Assert.NotNull(indexBuffer);

            var scene = new Scene();
            var modelNode = scene.CreateChild("Mushroom");

            var cameraNode = scene.CreateChild("Camera");
            cameraNode.SetPosition(new Vector3(0.0f, 2.0f, -6.0f));
            var camera = cameraNode.CreateComponent<Camera>();
            camera.SetAspectRatio(960.0f / 540.0f);

            var diagnostic = DiagnoseProjectedFrontFaceWinding(modelNode, cameraNode, camera, geometry, vertexBuffer!, indexBuffer!);
            Assert.True(
                diagnostic.ExaminedTriangles > 0,
                $"Expected to find a projected front-facing mushroom triangle. {diagnostic}");
            Assert.Equal("CW", diagnostic.FrontFaceWinding);
        }
        finally
        {
            RbfxTestData.RemoveResourceDirs(Resources);
        }
    }

    private static WindingDiagnostic DiagnoseProjectedFrontFaceWinding(
        Node modelNode,
        Node cameraNode,
        Camera camera,
        Geometry geometry,
        VertexBuffer vertexBuffer,
        IndexBuffer indexBuffer)
    {
        var viewProj = camera.GetGPUViewProj();
        var indices = indexBuffer.GetUnpackedData(geometry.GetIndexStart(), geometry.GetIndexCount());
        var positions = ReadPositions(vertexBuffer);

        var result = new WindingDiagnostic();
        foreach (var triangle in EnumerateTriangles(indices))
        {
            var p0 = modelNode.LocalToWorld(positions[(int)triangle.I0]);
            var p1 = modelNode.LocalToWorld(positions[(int)triangle.I1]);
            var p2 = modelNode.LocalToWorld(positions[(int)triangle.I2]);

            var centroid = (p0 + p1 + p2) / 3.0f;
            var toCamera = (cameraNode.WorldPosition - centroid).NormalizedOrDefault;
            var normal = (p1 - p0).CrossProduct(p2 - p0).NormalizedOrDefault;
            var worldFacing = normal.LengthSquared > MathDefs.Epsilon && normal.DotProduct(toCamera) > 0.0f;
            if (worldFacing)
                ++result.FrontFacingInWorld;

            var clip0 = viewProj * new Vector4(p0, 1.0f);
            var clip1 = viewProj * new Vector4(p1, 1.0f);
            var clip2 = viewProj * new Vector4(p2, 1.0f);
            var clipVisible = clip0.W > MathDefs.Epsilon && clip1.W > MathDefs.Epsilon && clip2.W > MathDefs.Epsilon;
            if (clipVisible)
                ++result.ProjectedInFront;

            result.FirstTriangleDiagnostics ??=
                $"firstTriangle: normal={normal}, toCamera={toCamera}, " +
                $"dot={normal.DotProduct(toCamera).ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                $"clipW=({clip0.W.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                $"{clip1.W.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                $"{clip2.W.ToString(System.Globalization.CultureInfo.InvariantCulture)})";

            if (!worldFacing || !clipVisible)
                continue;

            var ndc0 = new Vector2(clip0.X / clip0.W, clip0.Y / clip0.W);
            var ndc1 = new Vector2(clip1.X / clip1.W, clip1.Y / clip1.W);
            var ndc2 = new Vector2(clip2.X / clip2.W, clip2.Y / clip2.W);
            var signedArea = (ndc1.X - ndc0.X) * (ndc2.Y - ndc0.Y) - (ndc1.Y - ndc0.Y) * (ndc2.X - ndc0.X);

            result.ExaminedTriangles = 1;
            result.FrontFaceWinding = signedArea >= 0.0f ? "CCW" : "CW";
            result.SignedArea = signedArea;
            return result;
        }

        return result;
    }

    private static Vector3[] ReadPositions(VertexBuffer vertexBuffer)
    {
        var positionOffset = vertexBuffer.GetElementOffset(VertexElementSemantic.Position);
        Assert.NotEqual(uint.MaxValue, positionOffset);

        var rawData = vertexBuffer.GetData().ToArray();
        var vertexSize = checked((int)vertexBuffer.GetVertexSize());
        var vertexCount = checked((int)vertexBuffer.GetVertexCount());
        var positions = new Vector3[vertexCount];
        for (var i = 0; i < vertexCount; ++i)
        {
            var offset = checked(i * vertexSize + (int)positionOffset);
            positions[i] = new Vector3(
                BitConverter.ToSingle(rawData, offset + 0),
                BitConverter.ToSingle(rawData, offset + 4),
                BitConverter.ToSingle(rawData, offset + 8));
        }

        return positions;
    }

    private static IEnumerable<(uint I0, uint I1, uint I2)> EnumerateTriangles(IReadOnlyList<uint> indices)
    {
        for (var i = 0; i + 2 < indices.Count; i += 3)
            yield return (indices[i], indices[i + 1], indices[i + 2]);
    }

    private sealed class WindingDiagnostic
    {
        public int ExaminedTriangles { get; set; }
        public int FrontFacingInWorld { get; set; }
        public int ProjectedInFront { get; set; }
        public string? FrontFaceWinding { get; set; }
        public float SignedArea { get; set; }
        public string? FirstTriangleDiagnostics { get; set; }

        public override string ToString()
        {
            return $"frontFacingInWorld={FrontFacingInWorld}, projectedInFront={ProjectedInFront}, " +
                   $"frontFaceWinding={FrontFaceWinding ?? "n/a"}, " +
                   $"signedArea={SignedArea.ToString(System.Globalization.CultureInfo.InvariantCulture)}, " +
                   $"{FirstTriangleDiagnostics}";
        }
    }
}
