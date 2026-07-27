using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Heightmap terrain component.
/// </summary>
public class Terrain : Component
{
    private static readonly Vector3f DefaultSpacing = new(1.0f, 0.25f, 1.0f);
    private const uint MinLodLevels = 1;
    private const uint MaxLodLevelsConst = 4;
    private const int DefaultPatchSize = 32;
    private const int MinPatchSize = 4;
    private const int MaxPatchSize = 128;
    private const uint StitchNorth = 1;
    private const uint StitchSouth = 2;
    private const uint StitchWest = 4;
    private const uint StitchEast = 8;

    private static void GrowUpdateRegion(ref IntRect updateRegion, int x, int y)
    {
        if (updateRegion.Left < 0)
        {
            updateRegion = new IntRect(x, y, x, y);
        }
        else
        {
            if (x < updateRegion.Left)
                updateRegion = new IntRect(x, updateRegion.Top, updateRegion.Right, updateRegion.Bottom);
            if (x > updateRegion.Right)
                updateRegion = new IntRect(updateRegion.Left, updateRegion.Top, x, updateRegion.Bottom);
            if (y < updateRegion.Top)
                updateRegion = new IntRect(updateRegion.Left, y, updateRegion.Right, updateRegion.Bottom);
            if (y > updateRegion.Bottom)
                updateRegion = new IntRect(updateRegion.Left, updateRegion.Top, updateRegion.Right, y);
        }
    }

    private IndexBuffer? _indexBuffer = new();
    private Image? _heightMap;
    private float[]? _heightData;
    private float[]? _sourceHeightData;
    private Material? _material;
    private bool _bakeLightmap;
    private float _scaleInLightmap = 1.0f;
    private uint _lightmapIndex;
    private Vector4f _lightmapScaleOffset = new(1.0f, 1.0f, 0.0f, 0.0f);
    private readonly List<WeakReference<TerrainPatch>> _patches = [];
    private readonly List<(uint Start, uint Count)> _drawRanges = [];
    private WeakReference<Terrain>? _north;
    private WeakReference<Terrain>? _south;
    private WeakReference<Terrain>? _west;
    private WeakReference<Terrain>? _east;
    private Vector3f _spacing = DefaultSpacing;
    private Vector3f _lastSpacing = Vector3f.Zero;
    private Vector2f _patchWorldOrigin = Vector2f.Zero;
    private Vector2f _patchWorldSize = Vector2f.Zero;
    private Vector2i _numVertices = Vector2i.Zero;
    private Vector2i _lastNumVertices = Vector2i.Zero;
    private Vector2i _numPatches = Vector2i.Zero;
    private int _patchSize = DefaultPatchSize;
    private int _lastPatchSize;
    private uint _numLodLevels = 1;
    private uint _maxLodLevels = MaxLodLevelsConst;
    private uint _occlusionLodLevel = MathDefs.MaxUnsigned;
    private bool _smoothing;
    private bool _visible = true;
    private bool _castShadows;
    private bool _occluder;
    private bool _occludee = true;
    private uint _viewMask = Drawable.DefaultViewMask;
    private uint _lightMask = Drawable.DefaultLightMask;
    private uint _shadowMask = Drawable.DefaultShadowMask;
    private uint _zoneMask = Drawable.DefaultZoneMask;
    private float _drawDistance;
    private float _shadowDistance;
    private float _lodBias = 1.0f;
    private uint _northID;
    private uint _southID;
    private uint _westID;
    private uint _eastID;
    private bool _recreateTerrain;
    private bool _neighborsDirty;
    private bool _debugGeometry;

    /// <summary>Construct.</summary>
    public Terrain()
    {
        RegisterAttribute("Is Enabled", typeof(bool), self => ((Terrain)self).Enabled,
            (self, value) => ((Terrain)self).SetEnabled(value is bool enabled && enabled), true);
        _indexBuffer.SetShadowed(true);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _east = null;
        _west = null;
        _south = null;
        _north = null;
        _drawRanges.Clear();
        _patches.Clear();
        _material = null;
        _sourceHeightData = null;
        _heightData = null;
        _heightMap = null;
        _indexBuffer = null;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context) =>
        context.AddFactoryReflection<Terrain>(ObjectCategory.Category_Geometry);

    /// <inheritdoc />
    public override void ApplyAttributes()
    {
        if (_recreateTerrain)
            CreateGeometry();

        if (_neighborsDirty)
        {
            var scene = Scene;
            var north = scene?.GetNode(_northID);
            var south = scene?.GetNode(_southID);
            var west = scene?.GetNode(_westID);
            var east = scene?.GetNode(_eastID);
            var northTerrain = north?.GetComponent<Terrain>();
            var southTerrain = south?.GetComponent<Terrain>();
            var westTerrain = west?.GetComponent<Terrain>();
            var eastTerrain = east?.GetComponent<Terrain>();
            SetNeighbors(northTerrain, southTerrain, westTerrain, eastTerrain);
            _neighborsDirty = false;
        }
    }

    /// <inheritdoc />
    public override void OnSetEnabled()
    {
        var enabled = IsEnabledEffective;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetEnabled(enabled);
        }
    }

    /// <summary>Set patch quads per side. Must be a power of two.</summary>
    public void SetPatchSize(int size)
    {
        if (size < MinPatchSize || size > MaxPatchSize || !MathDefs.IsPowerOfTwo((uint)size))
            return;

        if (size != _patchSize)
        {
            _patchSize = size;
            CreateGeometry();
        }
    }

    /// <summary>Set vertex (XZ) and height (Y) spacing.</summary>
    public void SetSpacing(Vector3f spacing)
    {
        if (spacing != _spacing)
        {
            _spacing = spacing;
            CreateGeometry();
        }
    }

    /// <summary>Set maximum number of LOD levels for terrain patches. This can be between 1-4.</summary>
    public void SetMaxLodLevels(uint levels)
    {
        levels = (uint)Math.Clamp((int)levels, (int)MinLodLevels, (int)MaxLodLevelsConst);
        if (levels != _maxLodLevels)
        {
            _maxLodLevels = levels;
            _lastPatchSize = 0; // Force full recreate
            CreateGeometry();
        }
    }

    /// <summary>Set LOD level used for terrain patch occlusion.</summary>
    public void SetOcclusionLodLevel(uint level)
    {
        if (level != _occlusionLodLevel)
        {
            _occlusionLodLevel = level;
            _lastPatchSize = 0; // Force full recreate
            CreateGeometry();
        }
    }

    /// <summary>Set smoothing of heightmap.</summary>
    public void SetSmoothing(bool enable)
    {
        if (enable != _smoothing)
        {
            _smoothing = enable;
            CreateGeometry();
        }
    }

    /// <summary>Set heightmap image. Dimensions should be a power of two + 1.</summary>
    public bool SetHeightMap(Image? image) => SetHeightMapInternal(image, true);

    /// <summary>Set material.</summary>
    public void SetMaterial(Material? material)
    {
        _material = material;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetMaterial(material);
        }
    }


    /// <summary>Set north (positive Z) neighbor terrain.</summary>
    public void SetNorthNeighbor(Terrain? north)
    {
        Terrain? oldNorth = null;
        _north?.TryGetTarget(out oldNorth);
        if (ReferenceEquals(north, oldNorth))
            return;

        if (oldNorth?.Node is { } oldNode)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(oldNode);

        _north = north is null ? null : new WeakReference<Terrain>(north);
        if (north?.Node is { } node)
        {
            _northID = node.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(node, HandleNeighborTerrainCreated);
        }

        UpdateEdgePatchNeighbors();
    }

    /// <summary>Set south (negative Z) neighbor terrain.</summary>
    public void SetSouthNeighbor(Terrain? south)
    {
        Terrain? oldSouth = null;
        _south?.TryGetTarget(out oldSouth);
        if (ReferenceEquals(south, oldSouth))
            return;

        if (oldSouth?.Node is { } oldNode)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(oldNode);

        _south = south is null ? null : new WeakReference<Terrain>(south);
        if (south?.Node is { } node)
        {
            _southID = node.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(node, HandleNeighborTerrainCreated);
        }

        UpdateEdgePatchNeighbors();
    }

    /// <summary>Set west (negative X) neighbor terrain.</summary>
    public void SetWestNeighbor(Terrain? west)
    {
        Terrain? oldWest = null;
        _west?.TryGetTarget(out oldWest);
        if (ReferenceEquals(west, oldWest))
            return;

        if (oldWest?.Node is { } oldNode)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(oldNode);

        _west = west is null ? null : new WeakReference<Terrain>(west);
        if (west?.Node is { } node)
        {
            _westID = node.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(node, HandleNeighborTerrainCreated);
        }

        UpdateEdgePatchNeighbors();
    }

    /// <summary>Set east (positive X) neighbor terrain.</summary>
    public void SetEastNeighbor(Terrain? east)
    {
        Terrain? oldEast = null;
        _east?.TryGetTarget(out oldEast);
        if (ReferenceEquals(east, oldEast))
            return;

        if (oldEast?.Node is { } oldNode)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(oldNode);

        _east = east is null ? null : new WeakReference<Terrain>(east);
        if (east?.Node is { } node)
        {
            _eastID = node.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(node, HandleNeighborTerrainCreated);
        }

        UpdateEdgePatchNeighbors();
    }

    /// <summary>Set all neighbor terrains at once.</summary>
    public void SetNeighbors(Terrain? north, Terrain? south, Terrain? west, Terrain? east)
    {
        if (_north is not null && _north.TryGetTarget(out var oldNorth) && oldNorth.Node is { } n)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(n);
        if (_south is not null && _south.TryGetTarget(out var oldSouth) && oldSouth.Node is { } s)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(s);
        if (_west is not null && _west.TryGetTarget(out var oldWest) && oldWest.Node is { } w)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(w);
        if (_east is not null && _east.TryGetTarget(out var oldEast) && oldEast.Node is { } e)
            UnsubscribeFromEvent<DrawableEvents.TerrainCreatedEvent>(e);

        _north = north is null ? null : new WeakReference<Terrain>(north);
        if (north?.Node is { } nn)
        {
            _northID = nn.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(nn, HandleNeighborTerrainCreated);
        }
        _south = south is null ? null : new WeakReference<Terrain>(south);
        if (south?.Node is { } sn)
        {
            _southID = sn.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(sn, HandleNeighborTerrainCreated);
        }
        _west = west is null ? null : new WeakReference<Terrain>(west);
        if (west?.Node is { } wn)
        {
            _westID = wn.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(wn, HandleNeighborTerrainCreated);
        }
        _east = east is null ? null : new WeakReference<Terrain>(east);
        if (east?.Node is { } en)
        {
            _eastID = en.Id;
            SubscribeToEvent<DrawableEvents.TerrainCreatedEvent>(en, HandleNeighborTerrainCreated);
        }

        UpdateEdgePatchNeighbors();
    }

    public void SetDrawDistance(float distance)
    {
        _drawDistance = distance;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetDrawDistance(distance);
        }
    }

    public void SetShadowDistance(float distance)
    {
        _shadowDistance = distance;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetShadowDistance(distance);
        }
    }

    public void SetLodBias(float bias)
    {
        _lodBias = bias;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetLodBias(bias);
        }
    }

    public void SetViewMask(uint mask)
    {
        _viewMask = mask;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetViewMask(mask);
        }
    }

    public void SetLightMask(uint mask)
    {
        _lightMask = mask;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetLightMask(mask);
        }
    }

    public void SetShadowMask(uint mask)
    {
        _shadowMask = mask;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetShadowMask(mask);
        }
    }

    public void SetZoneMask(uint mask)
    {
        _zoneMask = mask;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetZoneMask(mask);
        }
    }

    public void SetCastShadows(bool enable)
    {
        _castShadows = enable;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetCastShadows(enable);
        }
    }

    public void SetOccluder(bool enable)
    {
        _occluder = enable;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetOccluder(enable);
        }
    }

    public void SetOccludee(bool enable)
    {
        _occludee = enable;
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.SetOccludee(enable);
        }
    }

    /// <summary>Enable drawing debug information. Set this before applying heightmap.</summary>
    public void SetEnableDebug(bool enable) => _debugGeometry = enable;

    /// <summary>Apply changes from the heightmap image.</summary>
    public void ApplyHeightMap()
    {
        if (_heightMap is not null)
            CreateGeometry();
    }

    public Image? GetHeightMap() => _heightMap;
    public Material? GetMaterial() => _material;

    public TerrainPatch? GetPatch(uint index)
    {
        return index < (uint)_patches.Count && _patches[(int)index].TryGetTarget(out var patch) ? patch : null;
    }

    public TerrainPatch? GetPatch(int x, int z)
    {
        if (x < 0 || x >= _numPatches.X || z < 0 || z >= _numPatches.Y)
            return null;
        return GetPatch((uint)(z * _numPatches.X + x));
    }

    public TerrainPatch? GetNeighborPatch(int x, int z)
    {
        if (z >= _numPatches.Y && _north is not null && _north.TryGetTarget(out var north))
            return north.GetPatch(x, z - _numPatches.Y);
        if (z < 0 && _south is not null && _south.TryGetTarget(out var south))
            return south.GetPatch(x, z + south.GetNumPatches().Y);
        if (x < 0 && _west is not null && _west.TryGetTarget(out var west))
            return west.GetPatch(x + west.GetNumPatches().X, z);
        if (x >= _numPatches.X && _east is not null && _east.TryGetTarget(out var east))
            return east.GetPatch(x - _numPatches.X, z);
        return GetPatch(x, z);
    }

    public float GetHeight(Vector3f worldPosition)
    {
        var node = TryGetNode();
        if (node is null)
            return 0.0f;

        var position = node.GetWorldTransform().Inverse * worldPosition;
        var xPos = (position.X - _patchWorldOrigin.X) / _spacing.X;
        var zPos = (position.Z - _patchWorldOrigin.Y) / _spacing.Z;
        var xFrac = MathDefs.Fract(xPos);
        var zFrac = MathDefs.Fract(zPos);
        float h1, h2, h3;

        if (xFrac + zFrac >= 1.0f)
        {
            h1 = GetRawHeight((int)xPos + 1, (int)zPos + 1);
            h2 = GetRawHeight((int)xPos, (int)zPos + 1);
            h3 = GetRawHeight((int)xPos + 1, (int)zPos);
            xFrac = 1.0f - xFrac;
            zFrac = 1.0f - zFrac;
        }
        else
        {
            h1 = GetRawHeight((int)xPos, (int)zPos);
            h2 = GetRawHeight((int)xPos + 1, (int)zPos);
            h3 = GetRawHeight((int)xPos, (int)zPos + 1);
        }

        var h = h1 * (1.0f - xFrac - zFrac) + h2 * xFrac + h3 * zFrac;
        /// \todo This assumes that the terrain scene node is upright
        return node.GetWorldScale().Y * h + node.GetWorldPosition().Y;
    }

    public Vector3f GetNormal(Vector3f worldPosition)
    {
        var node = TryGetNode();
        if (node is null)
            return Vector3f.Up;

        var position = node.GetWorldTransform().Inverse * worldPosition;
        var xPos = (position.X - _patchWorldOrigin.X) / _spacing.X;
        var zPos = (position.Z - _patchWorldOrigin.Y) / _spacing.Z;
        var xFrac = MathDefs.Fract(xPos);
        var zFrac = MathDefs.Fract(zPos);
        Vector3f n1, n2, n3;

        if (xFrac + zFrac >= 1.0f)
        {
            n1 = GetRawNormal((int)xPos + 1, (int)zPos + 1);
            n2 = GetRawNormal((int)xPos, (int)zPos + 1);
            n3 = GetRawNormal((int)xPos + 1, (int)zPos);
            xFrac = 1.0f - xFrac;
            zFrac = 1.0f - zFrac;
        }
        else
        {
            n1 = GetRawNormal((int)xPos, (int)zPos);
            n2 = GetRawNormal((int)xPos + 1, (int)zPos);
            n3 = GetRawNormal((int)xPos, (int)zPos + 1);
        }

        var n = (n1 * (1.0f - xFrac - zFrac) + n2 * xFrac + n3 * zFrac).Normalized;
        return node.GetWorldRotation() * n;
    }

    public Vector2i WorldToHeightMap(Vector3f worldPosition)
    {
        var node = TryGetNode();
        if (node is null)
            return Vector2i.Zero;

        var position = node.GetWorldTransform().Inverse * worldPosition;
        var xPos = MathDefs.RoundToInt((position.X - _patchWorldOrigin.X) / _spacing.X);
        var zPos = MathDefs.RoundToInt((position.Z - _patchWorldOrigin.Y) / _spacing.Z);
        xPos = Math.Clamp(xPos, 0, _numVertices.X - 1);
        zPos = Math.Clamp(zPos, 0, _numVertices.Y - 1);
        return new Vector2i(xPos, _numVertices.Y - 1 - zPos);
    }

    public Vector3f HeightMapToWorld(Vector2i pixelPosition)
    {
        var node = TryGetNode();
        if (node is null)
            return Vector3f.Zero;

        var pos = new Vector2i(pixelPosition.X, _numVertices.Y - 1 - pixelPosition.Y);
        var xPos = pos.X * _spacing.X + _patchWorldOrigin.X;
        var zPos = pos.Y * _spacing.Z + _patchWorldOrigin.Y;
        var lPos = new Vector3f(xPos, 0.0f, zPos);
        var wPos = node.GetWorldTransform() * lPos;
        wPos = new Vector3f(wPos.X, GetHeight(wPos), wPos.Z);
        return wPos;
    }

    public Vector2f HeightMapToUV(Vector2i pixelPosition)
    {
        var u = (float)pixelPosition.X / (_numVertices.X - 1);
        var v = (float)pixelPosition.Y / (_numVertices.Y - 1);
        return new Vector2f(u, v);
    }


    /// <summary>Regenerate patch geometry.</summary>
    public void CreatePatchGeometry(TerrainPatch patch)
    {
        var row = (uint)(_patchSize + 1);
        var vertexBuffer = patch.GetVertexBuffer();
        var geometry = patch.GetGeometry();
        var maxLodGeometry = patch.GetMaxLodGeometry();
        var occlusionGeometry = patch.GetOcclusionGeometry();

        vertexBuffer.SetDebugName($"Terrain patch at {patch.GetCoordinates()}");

        // Scale in lightmap is intentionally ignored here
        var vertexMask = VertexMask.Position | VertexMask.Normal | VertexMask.TexCoord1 | VertexMask.Tangent;
        if (_bakeLightmap)
            vertexMask |= VertexMask.TexCoord2;

        if (vertexBuffer.GetVertexCount() != row * row || vertexBuffer.GetElementMask() != vertexMask)
            vertexBuffer.SetSize(row * row, vertexMask);

        var vertexStride = VertexBuffer.GetVertexSize(vertexMask);
        var positionOnlySize = row * row * (uint)sizeof(float) * 3u;
        var cpuVertexData = new byte[positionOnlySize];
        var occlusionCpuVertexData = new byte[positionOnlySize];
        var floatBuffer = new float[row * row * (vertexStride / sizeof(float))];
        var posFloats = new float[row * row * 3];
        var occFloats = new float[row * row * 3];

        var box = new BoundingBox();
        var occlusionLevel = _occlusionLodLevel;
        if (occlusionLevel > _numLodLevels - 1)
            occlusionLevel = _numLodLevels - 1;

        {
            var coords = patch.GetCoordinates();
            var lodExpand = (1u << (int)occlusionLevel) - 1;
            var halfLodExpand = (1u << (int)occlusionLevel) / 2;

            var floatIndex = 0;
            var posIndex = 0;
            var occIndex = 0;

            for (uint z = 0; z <= (uint)_patchSize; ++z)
            {
                for (uint x = 0; x <= (uint)_patchSize; ++x)
                {
                    var xPos = coords.X * _patchSize + (int)x;
                    var zPos = coords.Y * _patchSize + (int)z;

                    // Position
                    var position = new Vector3f(x * _spacing.X, GetRawHeight(xPos, zPos), z * _spacing.Z);
                    floatBuffer[floatIndex++] = position.X;
                    floatBuffer[floatIndex++] = position.Y;
                    floatBuffer[floatIndex++] = position.Z;
                    posFloats[posIndex++] = position.X;
                    posFloats[posIndex++] = position.Y;
                    posFloats[posIndex++] = position.Z;

                    box.Merge(position);

                    // For vertices that are part of the occlusion LOD, calculate the minimum height in the neighborhood
                    var minHeight = position.Y;
                    if (halfLodExpand > 0 && (x & lodExpand) == 0 && (z & lodExpand) == 0)
                    {
                        var minX = Math.Max(xPos - (int)halfLodExpand, 0);
                        var maxX = Math.Min(xPos + (int)halfLodExpand, _numVertices.X - 1);
                        var minZ = Math.Max(zPos - (int)halfLodExpand, 0);
                        var maxZ = Math.Min(zPos + (int)halfLodExpand, _numVertices.Y - 1);
                        for (var nZ = minZ; nZ <= maxZ; ++nZ)
                        {
                            for (var nX = minX; nX <= maxX; ++nX)
                                minHeight = Math.Min(minHeight, GetRawHeight(nX, nZ));
                        }
                    }
                    occFloats[occIndex++] = position.X;
                    occFloats[occIndex++] = minHeight;
                    occFloats[occIndex++] = position.Z;

                    // Normal
                    var normal = GetRawNormal(xPos, zPos);
                    floatBuffer[floatIndex++] = normal.X;
                    floatBuffer[floatIndex++] = normal.Y;
                    floatBuffer[floatIndex++] = normal.Z;

                    // Texture coordinate(s)
                    var texCoord = HeightMapToUV(new Vector2i(xPos, _numVertices.Y - 1 - zPos));
                    floatBuffer[floatIndex++] = texCoord.X;
                    floatBuffer[floatIndex++] = texCoord.Y;

                    if (_bakeLightmap)
                    {
                        floatBuffer[floatIndex++] = texCoord.X;
                        floatBuffer[floatIndex++] = texCoord.Y;
                    }

                    // Tangent
                    var xyz = (Vector3f.Right - normal * normal.DotProduct(Vector3f.Right)).Normalized;
                    floatBuffer[floatIndex++] = xyz.X;
                    floatBuffer[floatIndex++] = xyz.Y;
                    floatBuffer[floatIndex++] = xyz.Z;
                    floatBuffer[floatIndex++] = 1.0f;
                }
            }

            var vertexBytes = MemoryMarshal.AsBytes(floatBuffer.AsSpan());
            var mapped = vertexBuffer.Map();
            if (!mapped.IsEmpty)
            {
                vertexBytes.CopyTo(mapped);
                vertexBuffer.Unmap();
                vertexBuffer.GetRawBuffer()?.ClearDataLost();
            }

            MemoryMarshal.AsBytes(posFloats.AsSpan()).CopyTo(cpuVertexData);
            MemoryMarshal.AsBytes(occFloats.AsSpan()).CopyTo(occlusionCpuVertexData);
        }

        patch.SetBoundingBox(box);

        if (_drawRanges.Count != 0)
        {
            var occlusionDrawRange = occlusionLevel << 4;

            geometry.SetIndexBuffer(_indexBuffer);
            geometry.SetDrawRange(PrimitiveType.TriangleList, _drawRanges[0].Start, _drawRanges[0].Count, false);
            geometry.SetRawVertexData(cpuVertexData, (uint)VertexMask.Position);
            maxLodGeometry.SetIndexBuffer(_indexBuffer);
            maxLodGeometry.SetDrawRange(PrimitiveType.TriangleList, _drawRanges[0].Start, _drawRanges[0].Count, false);
            maxLodGeometry.SetRawVertexData(cpuVertexData, (uint)VertexMask.Position);
            occlusionGeometry.SetIndexBuffer(_indexBuffer);
            occlusionGeometry.SetDrawRange(PrimitiveType.TriangleList, _drawRanges[(int)occlusionDrawRange].Start, _drawRanges[(int)occlusionDrawRange].Count, false);
            occlusionGeometry.SetRawVertexData(occlusionCpuVertexData, (uint)VertexMask.Position);
        }

        patch.ResetLod();
    }

    /// <summary>Update patch based on LOD and neighbor LOD.</summary>
    public void UpdatePatchLod(TerrainPatch patch)
    {
        var geometry = patch.GetGeometry();

        // All LOD levels except the coarsest have 16 versions for stitching
        var lodLevel = patch.GetLodLevel();
        var drawRangeIndex = lodLevel << 4;
        if (lodLevel < _numLodLevels - 1)
        {
            var north = patch.GetNorthPatch();
            var south = patch.GetSouthPatch();
            var west = patch.GetWestPatch();
            var east = patch.GetEastPatch();

            if (north is not null && north.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchNorth;
            if (south is not null && south.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchSouth;
            if (west is not null && west.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchWest;
            if (east is not null && east.GetLodLevel() > lodLevel)
                drawRangeIndex |= StitchEast;
        }

        if (drawRangeIndex < (uint)_drawRanges.Count)
        {
            var range = _drawRanges[(int)drawRangeIndex];
            geometry.SetDrawRange(PrimitiveType.TriangleList, range.Start, range.Count, false);
        }
    }

    public void SetMaterialAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        SetMaterial(cache.GetResourceOrNull<Material>(value.Name));
    }

    public void SetHeightMapAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var image = cache.GetResourceOrNull<Image>(value.Name);
        SetHeightMapInternal(image, false);
    }

    public void SetPatchSizeAttr(int value)
    {
        if (value < MinPatchSize || value > MaxPatchSize || !MathDefs.IsPowerOfTwo((uint)value))
            return;

        if (value != _patchSize)
        {
            _patchSize = value;
            _recreateTerrain = true;
        }
    }

    public void SetMaxLodLevelsAttr(uint value)
    {
        value = (uint)Math.Clamp((int)value, (int)MinLodLevels, (int)MaxLodLevelsConst);
        if (value != _maxLodLevels)
        {
            _maxLodLevels = value;
            _lastPatchSize = 0; // Force full recreate
            _recreateTerrain = true;
        }
    }

    public void SetOcclusionLodLevelAttr(uint value)
    {
        if (value != _occlusionLodLevel)
        {
            _occlusionLodLevel = value;
            _lastPatchSize = 0; // Force full recreate
            _recreateTerrain = true;
        }
    }

    public ResourceRef GetMaterialAttr() => Resource.GetResourceRef(_material, new StringHash(nameof(Material)));
    public ResourceRef GetHeightMapAttr() => Resource.GetResourceRef(_heightMap, new StringHash(nameof(Image)));

    public BoundingBox CalculateWorldBoundingBox()
    {
        var boundingBox = new BoundingBox();
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                boundingBox.Merge(patch.GetWorldBoundingBox());
        }
        return boundingBox;
    }

    public void SetBakeLightmap(bool bakeLightmap)
    {
        if (_bakeLightmap != bakeLightmap)
        {
            _bakeLightmap = bakeLightmap;
            _lastPatchSize = 0; // Force full recreate
            CreateGeometry();
        }
    }

    public bool GetBakeLightmap() => _bakeLightmap;
    public bool GetBakeLightmapEffective() => _bakeLightmap && _scaleInLightmap > 0.0f;

    public void SetScaleInLightmap(float scale)
    {
        _scaleInLightmap = scale;
        UpdatePatchesLightmaps();
    }

    public float GetScaleInLightmap() => _scaleInLightmap;

    public void SetLightmapIndex(uint idx)
    {
        _lightmapIndex = idx;
        UpdatePatchesLightmaps();
    }

    public uint GetLightmapIndex() => _lightmapIndex;

    public void SetLightmapScaleOffset(Vector4f scaleOffset)
    {
        _lightmapScaleOffset = scaleOffset;
        UpdatePatchesLightmaps();
    }

    public Vector4f GetLightmapScaleOffset() => _lightmapScaleOffset;


    /// <summary>Regenerate terrain geometry.</summary>
    private void CreateGeometry()
    {
        _recreateTerrain = false;

        var node = TryGetNode();
        if (node is null)
            return;

        var prevNumPatches = _patches.Count;

        // Determine number of LOD levels
        var lodSize = (uint)_patchSize;
        _numLodLevels = 1;
        while (lodSize > MinPatchSize && _numLodLevels < _maxLodLevels)
        {
            lodSize >>= 1;
            ++_numLodLevels;
        }

        // Determine total terrain size
        _patchWorldSize = new Vector2f(_spacing.X * _patchSize, _spacing.Z * _patchSize);
        var updateAll = false;

        if (_heightMap is not null)
        {
            _numPatches = new Vector2i((_heightMap.GetWidth() - 1) / _patchSize, (_heightMap.GetHeight() - 1) / _patchSize);
            _numVertices = new Vector2i(_numPatches.X * _patchSize + 1, _numPatches.Y * _patchSize + 1);
            _patchWorldOrigin = new Vector2f(
                -0.5f * _numPatches.X * _patchWorldSize.X,
                -0.5f * _numPatches.Y * _patchWorldSize.Y);
            if (_numVertices != _lastNumVertices || _lastSpacing != _spacing || _patchSize != _lastPatchSize)
                updateAll = true;
            var newDataSize = _numVertices.X * _numVertices.Y;

            // Create new height data if terrain size changed
            if (_heightData is null || updateAll)
                _heightData = new float[newDataSize];

            // Ensure that the source (unsmoothed) data exists if smoothing is active
            if (_smoothing && (_sourceHeightData is null || updateAll))
            {
                _sourceHeightData = new float[newDataSize];
                updateAll = true;
            }
            else if (!_smoothing)
                _sourceHeightData = null;
        }
        else
        {
            _numPatches = Vector2i.Zero;
            _numVertices = Vector2i.Zero;
            _patchWorldOrigin = Vector2f.Zero;
            _heightData = null;
            _sourceHeightData = null;
        }

        _lastNumVertices = _numVertices;
        _lastPatchSize = _patchSize;
        _lastSpacing = _spacing;

        // Remove old patch nodes which are not needed
        if (updateAll)
        {
            var oldPatchNodes = new List<Node>();
            node.GetChildrenWithComponent<TerrainPatch>(oldPatchNodes);
            foreach (var child in oldPatchNodes)
            {
                var nodeOk = false;
                var name = child.Name;
                if (name.StartsWith("Patch_", StringComparison.Ordinal))
                {
                    var coords = name.Substring(6).Split('_');
                    if (coords.Length == 2
                        && int.TryParse(coords[0], out var x)
                        && int.TryParse(coords[1], out var z)
                        && x < _numPatches.X && z < _numPatches.Y)
                    {
                        nodeOk = true;
                    }
                }

                if (!nodeOk)
                    node.RemoveChild(child);
            }
        }

        // Keep track of which patches actually need an update
        var dirtyPatches = new bool[Math.Max(0, _numPatches.X * _numPatches.Y)];
        for (var i = 0; i < dirtyPatches.Length; ++i)
            dirtyPatches[i] = updateAll;

        _patches.Clear();

        if (_heightMap is not null && _heightData is not null)
        {
            // Copy heightmap data
            var src = _heightMap.GetData();
            var dest = _smoothing ? _sourceHeightData! : _heightData;
            var imgComps = (int)_heightMap.GetComponents();
            var imgRow = _heightMap.GetWidth() * imgComps;
            var updateRegion = new IntRect(-1, -1, -1, -1);
            var destIndex = 0;

            if (imgComps == 1)
            {
                for (var z = 0; z < _numVertices.Y; ++z)
                {
                    for (var x = 0; x < _numVertices.X; ++x)
                    {
                        var newHeight = src[imgRow * (_numVertices.Y - 1 - z) + x] * _spacing.Y;

                        if (updateAll)
                            dest[destIndex] = newHeight;
                        else
                        {
                            if (dest[destIndex] != newHeight)
                            {
                                dest[destIndex] = newHeight;
                                GrowUpdateRegion(ref updateRegion, x, z);
                            }
                        }

                        ++destIndex;
                    }
                }
            }
            else
            {
                // If more than 1 component, use the green channel for more accuracy
                for (var z = 0; z < _numVertices.Y; ++z)
                {
                    for (var x = 0; x < _numVertices.X; ++x)
                    {
                        var baseIdx = imgRow * (_numVertices.Y - 1 - z) + imgComps * x;
                        var newHeight = (src[baseIdx] + src[baseIdx + 1] / 256.0f) * _spacing.Y;

                        if (updateAll)
                            dest[destIndex] = newHeight;
                        else
                        {
                            if (dest[destIndex] != newHeight)
                            {
                                dest[destIndex] = newHeight;
                                GrowUpdateRegion(ref updateRegion, x, z);
                            }
                        }

                        ++destIndex;
                    }
                }
            }

            // If updating a region of the heightmap, check which patches change
            if (!updateAll)
            {
                var lodExpand = 1 << (int)(_numLodLevels - 1);
                // Expand the right & bottom 1 pixel more, as patches share vertices at the edge
                updateRegion = new IntRect(
                    updateRegion.Left - lodExpand,
                    updateRegion.Top - lodExpand,
                    updateRegion.Right + lodExpand + 1,
                    updateRegion.Bottom + lodExpand + 1);

                var sX = Math.Max(updateRegion.Left / _patchSize, 0);
                var eX = Math.Min(updateRegion.Right / _patchSize, _numPatches.X - 1);
                var sY = Math.Max(updateRegion.Top / _patchSize, 0);
                var eY = Math.Min(updateRegion.Bottom / _patchSize, _numPatches.Y - 1);
                for (var y = sY; y <= eY; ++y)
                {
                    for (var x = sX; x <= eX; ++x)
                        dirtyPatches[y * _numPatches.X + x] = true;
                }
            }

            var enabled = IsEnabledEffective;

            // Create patches and set node transforms
            for (var z = 0; z < _numPatches.Y; ++z)
            {
                for (var x = 0; x < _numPatches.X; ++x)
                {
                    var nodeName = $"Patch_{x}_{z}";
                    var patchNode = node.GetChild(nodeName);

                    if (patchNode is null)
                    {
                        // Create the patch scene node as local and temporary so that it is not unnecessarily serialized
                        patchNode = node.CreateTemporaryChild(nodeName);
                    }

                    patchNode.SetPosition(new Vector3f(
                        _patchWorldOrigin.X + x * _patchWorldSize.X,
                        0.0f,
                        _patchWorldOrigin.Y + z * _patchWorldSize.Y));

                    var patch = patchNode.GetComponent<TerrainPatch>();
                    if (patch is null)
                    {
                        patch = patchNode.CreateComponent<TerrainPatch>();
                        patch.SetOwner(this);
                        patch.SetCoordinates(new Vector2i(x, z));
                        if (_debugGeometry)
                            patch.GetVertexBuffer().SetShadowed(true);

                        // Copy initial drawable parameters
                        patch.SetEnabled(enabled);
                        patch.SetMaterial(_material);
                        patch.SetDrawDistance(_drawDistance);
                        patch.SetShadowDistance(_shadowDistance);
                        patch.SetLodBias(_lodBias);
                        patch.SetViewMask(_viewMask);
                        patch.SetLightMask(_lightMask);
                        patch.SetShadowMask(_shadowMask);
                        patch.SetZoneMask(_zoneMask);
                        patch.SetCastShadows(_castShadows);
                        patch.SetOccluder(_occluder);
                        patch.SetOccludee(_occludee);
                    }

                    var bakeLightmap = GetBakeLightmapEffective();
                    patch.SetBakeLightmap(bakeLightmap);
                    patch.SetGlobalIlluminationType(
                        bakeLightmap ? GlobalIlluminationType.UseLightMap : GlobalIlluminationType.None);
                    patch.SetLightmapIndex(_lightmapIndex);
                    patch.SetLightmapScaleOffset(_lightmapScaleOffset);

                    _patches.Add(new WeakReference<TerrainPatch>(patch));
                }
            }

            // Create the shared index data
            if (updateAll)
                CreateIndexData();

            // Create vertex data for patches. First update smoothing to ensure normals are calculated correctly across patch borders
            if (_smoothing && _sourceHeightData is not null)
            {
                for (var i = 0; i < _patches.Count; ++i)
                {
                    if (!dirtyPatches[i])
                        continue;

                    if (!_patches[i].TryGetTarget(out var patch))
                        continue;
                    var coords = patch.GetCoordinates();
                    var startX = coords.X * _patchSize;
                    var endX = startX + _patchSize;
                    var startZ = coords.Y * _patchSize;
                    var endZ = startZ + _patchSize;

                    for (var zz = startZ; zz <= endZ; ++zz)
                    {
                        for (var xx = startX; xx <= endX; ++xx)
                        {
                            var smoothedHeight = (
                                GetSourceHeight(xx - 1, zz - 1) + GetSourceHeight(xx, zz - 1) * 2.0f + GetSourceHeight(xx + 1, zz - 1) +
                                GetSourceHeight(xx - 1, zz) * 2.0f + GetSourceHeight(xx, zz) * 4.0f + GetSourceHeight(xx + 1, zz) * 2.0f +
                                GetSourceHeight(xx - 1, zz + 1) + GetSourceHeight(xx, zz + 1) * 2.0f + GetSourceHeight(xx + 1, zz + 1)
                            ) / 16.0f;

                            _heightData[zz * _numVertices.X + xx] = smoothedHeight;
                        }
                    }
                }
            }

            for (var i = 0; i < _patches.Count; ++i)
            {
                if (!_patches[i].TryGetTarget(out var patch))
                    continue;

                if (dirtyPatches[i])
                {
                    CreatePatchGeometry(patch);
                    CalculateLodErrors(patch);
                }

                SetPatchNeighbors(patch);
            }
        }

        // Send event only if new geometry was generated, or the old was cleared
        if (_patches.Count != 0 || prevNumPatches != 0)
        {
            var eventData = new DrawableEvents.TerrainCreatedEvent { Node = node };
            node.SendEvent(ref eventData);
        }
    }


    /// <summary>Create index data shared by all patches.</summary>
    private void CreateIndexData()
    {
        var indices = new List<ushort>();
        _drawRanges.Clear();
        var row = (uint)(_patchSize + 1);

        /* Build index data for each LOD level. Each LOD level except the lowest can stitch to the next lower LOD from the edges:
           north, south, west, east, or any combination of them, requiring 16 different versions of each LOD level's index data

           Normal edge:     Stitched edge:
           +----+----+      +---------+
           |\   |\   |      |\       /|
           | \  | \  |      | \     / |
           |  \ |  \ |      |  \   /  |
           |   \|   \|      |   \ /   |
           +----+----+      +----+----+
        */
        for (uint i = 0; i < _numLodLevels; ++i)
        {
            var combinations = (i < _numLodLevels - 1) ? 16u : 1u;
            var skip = 1 << (int)i;

            for (uint j = 0; j < combinations; ++j)
            {
                var indexStart = (uint)indices.Count;

                var zStart = 0;
                var xStart = 0;
                var zEnd = _patchSize;
                var xEnd = _patchSize;

                if ((j & StitchNorth) != 0)
                    zEnd -= skip;
                if ((j & StitchSouth) != 0)
                    zStart += skip;
                if ((j & StitchWest) != 0)
                    xStart += skip;
                if ((j & StitchEast) != 0)
                    xEnd -= skip;

                // Build the main grid
                for (var z = zStart; z < zEnd; z += skip)
                {
                    for (var x = xStart; x < xEnd; x += skip)
                    {
                        indices.Add((ushort)((z + skip) * row + x));
                        indices.Add((ushort)(z * row + x + skip));
                        indices.Add((ushort)(z * row + x));
                        indices.Add((ushort)((z + skip) * row + x));
                        indices.Add((ushort)((z + skip) * row + x + skip));
                        indices.Add((ushort)(z * row + x + skip));
                    }
                }

                // Build the north edge
                if ((j & StitchNorth) != 0)
                {
                    var z = _patchSize - skip;
                    for (var x = 0; x < _patchSize; x += skip * 2)
                    {
                        if (x > 0 || (j & StitchWest) == 0)
                        {
                            indices.Add((ushort)((z + skip) * row + x));
                            indices.Add((ushort)(z * row + x + skip));
                            indices.Add((ushort)(z * row + x));
                        }
                        indices.Add((ushort)((z + skip) * row + x));
                        indices.Add((ushort)((z + skip) * row + x + 2 * skip));
                        indices.Add((ushort)(z * row + x + skip));
                        if (x < _patchSize - skip * 2 || (j & StitchEast) == 0)
                        {
                            indices.Add((ushort)((z + skip) * row + x + 2 * skip));
                            indices.Add((ushort)(z * row + x + 2 * skip));
                            indices.Add((ushort)(z * row + x + skip));
                        }
                    }
                }

                // Build the south edge
                if ((j & StitchSouth) != 0)
                {
                    var z = 0;
                    for (var x = 0; x < _patchSize; x += skip * 2)
                    {
                        if (x > 0 || (j & StitchWest) == 0)
                        {
                            indices.Add((ushort)((z + skip) * row + x));
                            indices.Add((ushort)((z + skip) * row + x + skip));
                            indices.Add((ushort)(z * row + x));
                        }
                        indices.Add((ushort)(z * row + x));
                        indices.Add((ushort)((z + skip) * row + x + skip));
                        indices.Add((ushort)(z * row + x + 2 * skip));
                        if (x < _patchSize - skip * 2 || (j & StitchEast) == 0)
                        {
                            indices.Add((ushort)((z + skip) * row + x + skip));
                            indices.Add((ushort)((z + skip) * row + x + 2 * skip));
                            indices.Add((ushort)(z * row + x + 2 * skip));
                        }
                    }
                }

                // Build the west edge
                if ((j & StitchWest) != 0)
                {
                    var x = 0;
                    for (var z = 0; z < _patchSize; z += skip * 2)
                    {
                        if (z > 0 || (j & StitchSouth) == 0)
                        {
                            indices.Add((ushort)(z * row + x));
                            indices.Add((ushort)((z + skip) * row + x + skip));
                            indices.Add((ushort)(z * row + x + skip));
                        }
                        indices.Add((ushort)((z + 2 * skip) * row + x));
                        indices.Add((ushort)((z + skip) * row + x + skip));
                        indices.Add((ushort)(z * row + x));
                        if (z < _patchSize - skip * 2 || (j & StitchNorth) == 0)
                        {
                            indices.Add((ushort)((z + 2 * skip) * row + x));
                            indices.Add((ushort)((z + 2 * skip) * row + x + skip));
                            indices.Add((ushort)((z + skip) * row + x + skip));
                        }
                    }
                }

                // Build the east edge
                if ((j & StitchEast) != 0)
                {
                    var x = _patchSize - skip;
                    for (var z = 0; z < _patchSize; z += skip * 2)
                    {
                        if (z > 0 || (j & StitchSouth) == 0)
                        {
                            indices.Add((ushort)(z * row + x));
                            indices.Add((ushort)((z + skip) * row + x));
                            indices.Add((ushort)(z * row + x + skip));
                        }
                        indices.Add((ushort)((z + skip) * row + x));
                        indices.Add((ushort)((z + 2 * skip) * row + x + skip));
                        indices.Add((ushort)(z * row + x + skip));
                        if (z < _patchSize - skip * 2 || (j & StitchNorth) == 0)
                        {
                            indices.Add((ushort)((z + skip) * row + x));
                            indices.Add((ushort)((z + 2 * skip) * row + x));
                            indices.Add((ushort)((z + 2 * skip) * row + x + skip));
                        }
                    }
                }

                _drawRanges.Add((indexStart, (uint)indices.Count - indexStart));
            }
        }

        _indexBuffer!.SetSize((uint)indices.Count, largeIndices: false);
        var bytes = new byte[indices.Count * 2];
        for (var i = 0; i < indices.Count; ++i)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), indices[i]);
        _indexBuffer.UpdateRange(bytes, 0, (uint)bytes.Length);
    }

    /// <summary>Return an uninterpolated terrain height value, clamping to edges.</summary>
    private float GetRawHeight(int x, int z)
    {
        if (_heightData is null)
            return 0.0f;

        x = Math.Clamp(x, 0, _numVertices.X - 1);
        z = Math.Clamp(z, 0, _numVertices.Y - 1);
        return _heightData[z * _numVertices.X + x];
    }

    /// <summary>Return a source terrain height value, clamping to edges. The source data is used for smoothing.</summary>
    private float GetSourceHeight(int x, int z)
    {
        if (_sourceHeightData is null)
            return 0.0f;

        x = Math.Clamp(x, 0, _numVertices.X - 1);
        z = Math.Clamp(z, 0, _numVertices.Y - 1);
        return _sourceHeightData[z * _numVertices.X + x];
    }

    /// <summary>Return interpolated height for a specific LOD level.</summary>
    private float GetLodHeight(int x, int z, uint lodLevel)
    {
        var offset = 1 << (int)lodLevel;
        var xFrac = (float)(x % offset) / offset;
        var zFrac = (float)(z % offset) / offset;
        float h1, h2, h3;

        if (xFrac + zFrac >= 1.0f)
        {
            h1 = GetRawHeight(x + offset, z + offset);
            h2 = GetRawHeight(x, z + offset);
            h3 = GetRawHeight(x + offset, z);
            xFrac = 1.0f - xFrac;
            zFrac = 1.0f - zFrac;
        }
        else
        {
            h1 = GetRawHeight(x, z);
            h2 = GetRawHeight(x + offset, z);
            h3 = GetRawHeight(x, z + offset);
        }

        return h1 * (1.0f - xFrac - zFrac) + h2 * xFrac + h3 * zFrac;
    }

    /// <summary>Get slope-based terrain normal at position.</summary>
    private Vector3f GetRawNormal(int x, int z)
    {
        var baseHeight = GetRawHeight(x, z);
        var nSlope = GetRawHeight(x, z - 1) - baseHeight;
        var neSlope = GetRawHeight(x + 1, z - 1) - baseHeight;
        var eSlope = GetRawHeight(x + 1, z) - baseHeight;
        var seSlope = GetRawHeight(x + 1, z + 1) - baseHeight;
        var sSlope = GetRawHeight(x, z + 1) - baseHeight;
        var swSlope = GetRawHeight(x - 1, z + 1) - baseHeight;
        var wSlope = GetRawHeight(x - 1, z) - baseHeight;
        var nwSlope = GetRawHeight(x - 1, z - 1) - baseHeight;
        var up = 0.5f * (_spacing.X + _spacing.Z);

        return (new Vector3f(0.0f, up, nSlope) +
                new Vector3f(-neSlope, up, neSlope) +
                new Vector3f(-eSlope, up, 0.0f) +
                new Vector3f(-seSlope, up, -seSlope) +
                new Vector3f(0.0f, up, -sSlope) +
                new Vector3f(swSlope, up, -swSlope) +
                new Vector3f(wSlope, up, 0.0f) +
                new Vector3f(nwSlope, up, nwSlope)).Normalized;
    }

    /// <summary>Calculate LOD errors for a patch.</summary>
    private void CalculateLodErrors(TerrainPatch patch)
    {
        var coords = patch.GetCoordinates();
        var lodErrors = patch.GetLodErrors();
        lodErrors.Clear();

        var xStart = coords.X * _patchSize;
        var zStart = coords.Y * _patchSize;
        var xEnd = xStart + _patchSize;
        var zEnd = zStart + _patchSize;

        for (uint i = 0; i < _numLodLevels; ++i)
        {
            var maxError = 0.0f;
            var divisor = 1 << (int)i;

            if (i > 0)
            {
                for (var z = zStart; z <= zEnd; ++z)
                {
                    for (var x = xStart; x <= xEnd; ++x)
                    {
                        if (x % divisor != 0 || z % divisor != 0)
                        {
                            var error = Math.Abs(GetLodHeight(x, z, i) - GetRawHeight(x, z));
                            maxError = Math.Max(error, maxError);
                        }
                    }
                }

                // Set error to be at least same as (half vertex spacing x LOD) to prevent horizontal stretches getting too inaccurate
                maxError = Math.Max(maxError, 0.25f * (_spacing.X + _spacing.Z) * (1 << (int)i));
            }

            lodErrors.Add(maxError);
        }
    }

    /// <summary>Set neighbors for a patch.</summary>
    private void SetPatchNeighbors(TerrainPatch? patch)
    {
        if (patch is null)
            return;

        var coords = patch.GetCoordinates();
        patch.SetNeighbors(
            GetNeighborPatch(coords.X, coords.Y + 1),
            GetNeighborPatch(coords.X, coords.Y - 1),
            GetNeighborPatch(coords.X - 1, coords.Y),
            GetNeighborPatch(coords.X + 1, coords.Y));
    }

    /// <summary>Set heightmap image and optionally recreate the geometry immediately.</summary>
    private bool SetHeightMapInternal(Image? image, bool recreateNow)
    {
        if (image is not null && image.IsCompressed())
        {
            Log.GetLogger().LogError("Can not use a compressed image as a terrain heightmap");
            return false;
        }

        // Unsubscribe from the reload event of previous image (if any), then subscribe to the new
        if (_heightMap is not null)
            UnsubscribeFromEvent<ResourceEvents.ReloadFinished>(_heightMap);
        if (image is not null)
            SubscribeToEvent<ResourceEvents.ReloadFinished>(image, HandleHeightMapReloadFinished);

        _heightMap = image;

        if (recreateNow)
            CreateGeometry();
        else
            _recreateTerrain = true;

        return true;
    }

    private void HandleHeightMapReloadFinished(ref ResourceEvents.ReloadFinished eventData) => CreateGeometry();

    private void HandleNeighborTerrainCreated(ref DrawableEvents.TerrainCreatedEvent eventData) =>
        UpdateEdgePatchNeighbors();

    /// <summary>Update edge patch neighbors when neighbor terrain(s) change or are recreated.</summary>
    private void UpdateEdgePatchNeighbors()
    {
        for (var x = 1; x < _numPatches.X - 1; ++x)
        {
            SetPatchNeighbors(GetPatch(x, 0));
            SetPatchNeighbors(GetPatch(x, _numPatches.Y - 1));
        }
        for (var z = 1; z < _numPatches.Y - 1; ++z)
        {
            SetPatchNeighbors(GetPatch(0, z));
            SetPatchNeighbors(GetPatch(_numPatches.X - 1, z));
        }

        SetPatchNeighbors(GetPatch(0, 0));
        SetPatchNeighbors(GetPatch(_numPatches.X - 1, 0));
        SetPatchNeighbors(GetPatch(0, _numPatches.Y - 1));
        SetPatchNeighbors(GetPatch(_numPatches.X - 1, _numPatches.Y - 1));
    }

    /// <summary>Update lightmap settings in patches.</summary>
    private void UpdatePatchesLightmaps()
    {
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (!_patches[i].TryGetTarget(out var patch))
                continue;
            var bakeLightmap = GetBakeLightmapEffective();
            patch.SetBakeLightmap(bakeLightmap);
            patch.SetGlobalIlluminationType(bakeLightmap ? GlobalIlluminationType.UseLightMap : GlobalIlluminationType.None);
            patch.SetLightmapIndex(_lightmapIndex);
            patch.SetLightmapScaleOffset(_lightmapScaleOffset);
        }
    }

    /// <inheritdoc />
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        for (var i = 0; i < _patches.Count; ++i)
        {
            if (_patches[i].TryGetTarget(out var patch))
                patch.DrawDebugGeometry(debug, depthTest);
        }
    }

    public int GetPatchSize() => _patchSize;
    public Vector3f GetSpacing() => _spacing;
    public Vector2i GetNumVertices() => _numVertices;
    public Vector2i GetNumPatches() => _numPatches;
    public uint GetMaxLodLevels() => _maxLodLevels;
    public uint GetOcclusionLodLevel() => _occlusionLodLevel;
    public bool GetSmoothing() => _smoothing;
    public Terrain? GetNorthNeighbor() => _north is not null && _north.TryGetTarget(out var terrain) ? terrain : null;
    public Terrain? GetSouthNeighbor() => _south is not null && _south.TryGetTarget(out var terrain) ? terrain : null;
    public Terrain? GetWestNeighbor() => _west is not null && _west.TryGetTarget(out var terrain) ? terrain : null;
    public Terrain? GetEastNeighbor() => _east is not null && _east.TryGetTarget(out var terrain) ? terrain : null;
    public float[]? GetHeightData() => _heightData;
    public float GetDrawDistance() => _drawDistance;
    public float GetShadowDistance() => _shadowDistance;
    public float GetLodBias() => _lodBias;
    public uint GetViewMask() => _viewMask;
    public uint GetLightMask() => _lightMask;
    public uint GetShadowMask() => _shadowMask;
    public uint GetZoneMask() => _zoneMask;
    public bool IsVisible() => _visible;
    public bool GetCastShadows() => _castShadows;
    public bool IsOccluder() => _occluder;
    public bool IsOccludee() => _occludee;
}
