// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>Utility to import models.</summary>
internal sealed class GLTFModelImporter
{
    public GLTFModelImporter(GLTFImporterBase importerBase, GLTFBufferReader bufferReader,
        GLTFHierarchyAnalyzer hierarchyAnalyzer, GLTFMaterialImporter materialImporter)
    {
        _base = importerBase;
        _model = _base.GetModel();
        _bufferReader = bufferReader;
        _hierarchyAnalyzer = hierarchyAnalyzer;
        _materialImporter = materialImporter;
        InitializeModels();
        if (_base.GetSettings().CombineLODs)
            CombineLODs();
        CookModels();
    }

    public void SaveResources()
    {
        foreach (var model in _models)
            _base.SaveResource(model.Model!);
    }

    public Model GetModel(int meshIndex, int skinIndex) => GetImportedModel(meshIndex, skinIndex).Model!;
    public ResourceRefList GetModelMaterials(int meshIndex, int skinIndex) => GetImportedModel(meshIndex, skinIndex).Materials;

    private sealed class ImportedModel
    {
        public string MeshName = string.Empty;
        public int? Skin;
        public string BaseMeshName = string.Empty;
        public float? LodDistance;
        public ModelView ModelView = null!;
        public Model? Model;
        public ResourceRefList Materials;
        public bool AlreadyProcessedAsLOD;
    }

    private void InitializeModels()
    {
        foreach (var pair in _hierarchyAnalyzer.GetUniqueMeshSkinPairs())
        {
            var sourceMesh = _model.Meshes[pair.Mesh];
            var importedModel = new ImportedModel { MeshName = sourceMesh.Name.ToString(), Skin = pair.Skin };
            _models.Add(importedModel);
            (importedModel.BaseMeshName, importedModel.LodDistance) = ParseLodDistance(importedModel.MeshName);
            importedModel.ModelView = ImportModelView(sourceMesh, _hierarchyAnalyzer.GetSkinBones(pair.Skin));
        }
    }

    private void CombineLODs()
    {
        foreach (var importedModel in _models)
        {
            if (importedModel.LodDistance is null || importedModel.AlreadyProcessedAsLOD)
                continue;
            var lods = FindLods(importedModel);
            Debug.Assert(lods.Count != 0);
            var finalModelView = lods[0].ModelView;
            var finalGeometries = finalModelView.GetGeometries();
            // Set LOD distance for the first LOD
            for (var geometryIndex = 0; geometryIndex < finalGeometries.Count; ++geometryIndex)
            {
                var geometryView = finalGeometries[geometryIndex];
                if (geometryView.Lods.Count == 0)
                    continue;
                if (geometryView.Lods.Count > 1)
                    geometryView.Lods.RemoveRange(1, geometryView.Lods.Count - 1);
                var lod = geometryView.Lods[0];
                lod.LodDistance = lods[0].LodDistance!.Value;
                geometryView.Lods[0] = lod;
                finalGeometries[geometryIndex] = geometryView;
            }
            // Append other LODs
            // TODO: Handle materials more gracefully
            for (var lodIndex = 1; lodIndex < lods.Count; ++lodIndex)
            {
                var otherGeometries = lods[lodIndex].ModelView.GetGeometries();
                var lodDistance = lods[lodIndex].LodDistance!.Value;
                while (finalGeometries.Count < otherGeometries.Count)
                    finalGeometries.Add(new GeometryView());
                for (var geometryIndex = 0; geometryIndex < otherGeometries.Count; ++geometryIndex)
                {
                    var geometryView = finalGeometries[geometryIndex];
                    var lod = otherGeometries[geometryIndex].Lods[0];
                    lod.LodDistance = lodDistance;
                    geometryView.Lods.Add(lod);
                    finalGeometries[geometryIndex] = geometryView;
                }
            }
            foreach (var otherImportedModel in lods)
            {
                otherImportedModel.ModelView = finalModelView;
                otherImportedModel.AlreadyProcessedAsLOD = true;
            }
        }
    }

    private void CookModels()
    {
        var viewToModel = new Dictionary<ModelView, Model>();
        foreach (var importedModel in _models)
        {
            importedModel.Materials = importedModel.ModelView.ExportMaterialList();
            if (!viewToModel.TryGetValue(importedModel.ModelView, out var model))
            {
                var modelName = _base.GetResourceName(importedModel.BaseMeshName, "Models/", "Model", ".mdl");
                importedModel.ModelView.SetName(modelName);
                _base.GetCallback().OnModelLoaded(importedModel.ModelView);
                model = importedModel.ModelView.ExportModel();
                _base.AddToResourceCache(model);
                _modelsToSave.Add(model);
                viewToModel.Add(importedModel.ModelView, model);
            }
            importedModel.Model = model;
        }
    }

    private List<ImportedModel> FindLods(ImportedModel importedModel)
    {
        var lods = new SortedDictionary<float, ImportedModel>();
        foreach (var otherModel in _models)
        {
            if (otherModel.BaseMeshName != importedModel.BaseMeshName || otherModel.LodDistance is null ||
                otherModel.Skin != importedModel.Skin)
                continue;
            if (!lods.TryAdd(otherModel.LodDistance.Value, otherModel))
                Log.GetLogger().Error("Multiple LODs with the same distance {} for model {}", otherModel.LodDistance, otherModel.MeshName);
        }
        return lods.Values.ToList();
    }

    private static (string, float?) ParseLodDistance(string name)
    {
        var match = Regex.Match(name, @"^(.*)_LOD(\d+(\.\d+)?)$");
        return match.Success ? (match.Groups[1].Value, float.Parse(match.Groups[2].Value)) : (name, null);
    }

    private ImportedModel GetImportedModel(int meshIndex, int skinIndex) =>
        _models[_hierarchyAnalyzer.GetUniqueMeshSkin(meshIndex, skinIndex)];

    private ModelView ImportModelView(Tg.Mesh sourceMesh, List<BoneView> bones)
    {
        var modelView = new ModelView();
        modelView.SetBones(bones);
        var numMorphWeights = checked((int)sourceMesh.Weights.Count);
        for (var morphIndex = 0; morphIndex < numMorphWeights; ++morphIndex)
            modelView.SetMorph((uint)morphIndex, new ModelMorphView { Name = string.Empty, InitialWeight = (float)sourceMesh.Weights[morphIndex] });
        var geometries = modelView.GetGeometries();
        for (var geometryIndex = 0; geometryIndex < sourceMesh.Primitives.Count; ++geometryIndex)
        {
            var geometryView = new GeometryView();
            var geometryLodView = new GeometryLODView();
            geometryView.Lods.Add(geometryLodView);
            geometries.Add(geometryView);
            var primitive = sourceMesh.Primitives[geometryIndex];
            geometryLodView.PrimitiveType = GetPrimitiveType(primitive.Mode);
            if (primitive.Attributes.Count == 0)
                throw new InvalidOperationException($"No attributes in primitive #{geometryIndex} in mesh '{sourceMesh.Name}'.");
            var numVertices = checked((int)_model.Accessors[primitive.Attributes.First().Value].Count);
            geometryLodView.Vertices.AddRange(Enumerable.Range(0, numVertices).Select(_ => new ModelVertex()));
            foreach (var attribute in primitive.Attributes)
                ReadVertexData(ref geometryLodView.VertexFormat, geometryLodView.Vertices, attribute.Key.ToString(),
                    _model.Accessors[attribute.Value]);
            if (primitive.Indices >= 0)
            {
                _base.CheckAccessor(primitive.Indices);
                geometryLodView.Indices.AddRange(_bufferReader.ReadAccessorChecked<uint>(_model.Accessors[primitive.Indices]));
            }
            else
                geometryLodView.Indices.AddRange(Enumerable.Range(0, numVertices).Select(index => (uint)index));
            // Manually connect line loop to convert it to line strip
            if (primitive.Mode == GLTFConstants.ModeLineLoop)
                geometryLodView.Indices.Add(0);
            if (primitive.Material >= 0)
                geometryView.Material = _materialImporter.GetMaterial(primitive.Material, GetMaterialVariant(geometryLodView)).GetName();
            if (numMorphWeights > 0 && primitive.Targets.Count != numMorphWeights)
                throw new InvalidOperationException($"Primitive #{geometryIndex} in mesh '{sourceMesh.Name}' has incorrect number of morph weights.");
            for (var morphIndex = 0; morphIndex < primitive.Targets.Count; ++morphIndex)
                geometryLodView.Morphs[(uint)morphIndex] = ReadVertexMorphs(primitive.Targets[morphIndex], numVertices);
            geometryView.Lods[0] = geometryLodView;
            geometries[geometryIndex] = geometryView;
        }
        var transform = _hierarchyAnalyzer.GetInlineTransform();
        if (transform.MirrorX) modelView.MirrorGeometriesX();
        if (transform.Scale != 1.0f) modelView.ScaleGeometries(transform.Scale);
        modelView.CalculateMissingNormals(true);
        modelView.CalculateMissingTangents();
        modelView.RecalculateBoneBoundingBoxes();
        modelView.RepairBoneWeights();
        modelView.Normalize();
        return modelView;
    }

    private static GLTFMaterialImporter.MaterialVariant GetMaterialVariant(GeometryLODView lodView)
    {
        if (lodView.IsTriangleGeometry() || lodView.VertexFormat.Tangent != ModelVertexFormat.Undefined)
            return GLTFMaterialImporter.MaterialVariant.LitNormalMapMaterial;
        if (lodView.VertexFormat.Normal != ModelVertexFormat.Undefined)
            return GLTFMaterialImporter.MaterialVariant.LitMaterial;
        return GLTFMaterialImporter.MaterialVariant.UnlitMaterial;
    }

    private static PrimitiveType GetPrimitiveType(int primitiveMode) => primitiveMode switch
    {
        GLTFConstants.ModePoints => PrimitiveType.PointList,
        GLTFConstants.ModeLine => PrimitiveType.LineList,
        GLTFConstants.ModeLineLoop or GLTFConstants.ModeLineStrip => PrimitiveType.LineStrip,
        GLTFConstants.ModeTriangles => PrimitiveType.TriangleList,
        GLTFConstants.ModeTriangleStrip => PrimitiveType.TriangleStrip,
        GLTFConstants.ModeTriangleFan => PrimitiveType.TriangleFan,
        _ => throw new InvalidOperationException($"Unknown primitive type #{primitiveMode}")
    };

    private void ReadVertexData(ref ModelVertexFormat vertexFormat, List<ModelVertex> vertices,
        string semantics, Tg.Accessor accessor)
    {
        var parsedSemantics = semantics.Split('_');
        var semanticsName = parsedSemantics[0];
        var semanticsIndex = parsedSemantics.Length > 1 ? int.Parse(parsedSemantics[1]) : 0;
        if (semanticsName == "POSITION" && semanticsIndex == 0)
        {
            if (accessor.Type != GLTFConstants.TypeVec3) throw new InvalidOperationException("Unexpected type of vertex position");
            vertexFormat.Position = VertexElementType.Vector3;
            var values = _bufferReader.ReadVector3Accessor(accessor);
            for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.SetPosition(values[i]); vertices[i] = vertex; }
        }
        else if (semanticsName == "NORMAL" && semanticsIndex == 0)
        {
            if (accessor.Type != GLTFConstants.TypeVec3) throw new InvalidOperationException("Unexpected type of vertex normal");
            vertexFormat.Normal = VertexElementType.Vector3;
            var values = _bufferReader.ReadVector3Accessor(accessor);
            for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.SetNormal(values[i].Normalized); vertices[i] = vertex; }
        }
        else if (semanticsName == "TANGENT" && semanticsIndex == 0)
        {
            if (accessor.Type != GLTFConstants.TypeVec4) throw new InvalidOperationException("Unexpected type of vertex tangent");
            vertexFormat.Tangent = VertexElementType.Vector4;
            var values = _bufferReader.ReadVector4Accessor(accessor);
            for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.Tangent = values[i]; vertices[i] = vertex; }
        }
        else if (semanticsName == "TEXCOORD" && semanticsIndex < ModelVertex.MaxUVs)
        {
            if (accessor.Type != GLTFConstants.TypeVec2) throw new InvalidOperationException("Unexpected type of vertex uv");
            vertexFormat.Uv[semanticsIndex] = VertexElementType.Vector2;
            var values = _bufferReader.ReadVector2Accessor(accessor);
            for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.Uv[semanticsIndex] = values[i].ToVector4(); vertices[i] = vertex; }
        }
        else if (semanticsName == "COLOR" && semanticsIndex < ModelVertex.MaxColors)
        {
            if (accessor.Type != GLTFConstants.TypeVec3 && accessor.Type != GLTFConstants.TypeVec4)
                throw new InvalidOperationException("Unexpected type of vertex color");
            if (accessor.Type == GLTFConstants.TypeVec3)
            {
                vertexFormat.Color[semanticsIndex] = VertexElementType.Vector3;
                var values = _bufferReader.ReadVector3Accessor(accessor);
                for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.Color[semanticsIndex] = values[i].ToVector4(1); vertices[i] = vertex; }
            }
            else
            {
                vertexFormat.Color[semanticsIndex] = VertexElementType.Vector4;
                var values = _bufferReader.ReadVector4Accessor(accessor);
                for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.Color[semanticsIndex] = values[i]; vertices[i] = vertex; }
            }
        }
        else if (semanticsName == "JOINTS" && semanticsIndex == 0)
        {
            if (accessor.Type != GLTFConstants.TypeVec4) throw new InvalidOperationException("Unexpected type of skin joints");
            vertexFormat.BlendIndices = VertexElementType.UByte4;
            var values = _bufferReader.ReadVector4Accessor(accessor);
            for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.BlendIndices = values[i]; vertices[i] = vertex; }
        }
        else if (semanticsName == "WEIGHTS" && semanticsIndex == 0)
        {
            if (accessor.Type != GLTFConstants.TypeVec4) throw new InvalidOperationException("Unexpected type of skin weights");
            vertexFormat.BlendWeights = VertexElementType.UByte4Norm;
            var values = _bufferReader.ReadVector4Accessor(accessor);
            for (var i = 0; i < values.Count; ++i) { var vertex = vertices[i]; vertex.BlendWeights = values[i]; vertices[i] = vertex; }
        }
    }

    private List<ModelVertexMorph> ReadVertexMorphs(Std.Map<Std.String, int> accessors, int numVertices)
    {
        var positionDeltas = Enumerable.Repeat(Vector3f.Zero, numVertices).ToList();
        var normalDeltas = Enumerable.Repeat(Vector3f.Zero, numVertices).ToList();
        var tangentDeltas = Enumerable.Repeat(Vector3f.Zero, numVertices).ToList();
        if (accessors.TryGetValue("POSITION", out var positionAccessor))
        {
            _base.CheckAccessor(positionAccessor);
            positionDeltas = _bufferReader.ReadVector3Accessor(_model.Accessors[positionAccessor]);
        }
        if (accessors.TryGetValue("NORMAL", out var normalAccessor))
        {
            _base.CheckAccessor(normalAccessor);
            normalDeltas = _bufferReader.ReadVector3Accessor(_model.Accessors[normalAccessor]);
        }
        if (accessors.TryGetValue("TANGENT", out var tangentAccessor))
        {
            _base.CheckAccessor(tangentAccessor);
            tangentDeltas = _bufferReader.ReadVector3Accessor(_model.Accessors[tangentAccessor]);
        }
        if (numVertices != positionDeltas.Count || numVertices != normalDeltas.Count || numVertices != tangentDeltas.Count)
            throw new InvalidOperationException("Morph target has inconsistent sizes of accessors");
        var vertexMorphs = new List<ModelVertexMorph>(numVertices);
        for (var i = 0; i < numVertices; ++i)
            vertexMorphs.Add(new ModelVertexMorph { Index = (uint)i, PositionDelta = positionDeltas[i], NormalDelta = normalDeltas[i], TangentDelta = tangentDeltas[i] });
        return vertexMorphs;
    }

    private readonly GLTFImporterBase _base;
    private readonly Tg.Model _model;
    private readonly GLTFBufferReader _bufferReader;
    private readonly GLTFHierarchyAnalyzer _hierarchyAnalyzer;
    private readonly GLTFMaterialImporter _materialImporter;
    private readonly List<ImportedModel> _models = [];
    private readonly List<Model> _modelsToSave = [];
}
