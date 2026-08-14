// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D.Spriter;

using System.Globalization;
using System.Xml.Linq;

public enum ObjectType { Bone, Sprite }
public enum CurveType { Instant, Linear, Quadratic, Cubic }
public enum LoopMode { Default, ForceLooped, ForceClamped }

public sealed class SpriterData
{
    public int ScmlVersion { get; private set; }
    public string Generator { get; private set; } = string.Empty;
    public string GeneratorVersion { get; private set; } = string.Empty;
    public List<Folder> Folders { get; } = [];
    public List<Entity> Entities { get; } = [];

    public bool Load(ReadOnlySpan<byte> data)
    {
        Folders.Clear();
        Entities.Clear();
        try
        {
            using var stream = new MemoryStream(data.ToArray(), writable: false);
            var root = XDocument.Load(stream).Root;
            if (root?.Name.LocalName != "spriter_data")
                return false;
            ScmlVersion = AttrInt(root, "scml_version");
            Generator = Attr(root, "generator");
            GeneratorVersion = Attr(root, "generator_version");
            Folders.AddRange(root.Elements("folder").Select(ParseFolder));
            Entities.AddRange(root.Elements("entity").Select(ParseEntity));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Folder ParseFolder(XElement node)
    {
        var folder = new Folder { Id = AttrInt(node, "id"), Name = Attr(node, "name") };
        foreach (var fileNode in node.Elements("file"))
        {
            folder.Files.Add(new SpriterFile
            {
                Folder = folder,
                Id = AttrInt(fileNode, "id"),
                Name = Attr(fileNode, "name"),
                Width = AttrFloat(fileNode, "width"),
                Height = AttrFloat(fileNode, "height"),
                PivotX = AttrFloat(fileNode, "pivot_x", 0.0f),
                PivotY = AttrFloat(fileNode, "pivot_y", 1.0f),
            });
        }
        return folder;
    }

    private static Entity ParseEntity(XElement node)
    {
        var entity = new Entity { Id = AttrInt(node, "id"), Name = Attr(node, "name") };
        foreach (var mapNode in node.Elements("character_map"))
        {
            var map = new CharacterMap { Id = AttrInt(mapNode, "id"), Name = Attr(mapNode, "name") };
            foreach (var item in mapNode.Elements("map"))
            {
                map.Maps.Add(new MapInstruction
                {
                    Folder = AttrInt(item, "folder"), File = AttrInt(item, "file"),
                    TargetFolder = AttrInt(item, "target_folder", -1), TargetFile = AttrInt(item, "target_file", -1),
                });
            }
            entity.CharacterMaps.Add(map);
        }
        foreach (var animationNode in node.Elements("animation"))
            entity.Animations.Add(ParseAnimation(animationNode));
        return entity;
    }

    private static Animation ParseAnimation(XElement node)
    {
        var animation = new Animation
        {
            Id = AttrInt(node, "id"), Name = Attr(node, "name"),
            Length = AttrFloat(node, "length") * 0.001f, Looping = AttrBool(node, "looping", true),
        };
        var mainline = node.Element("mainline");
        if (mainline is not null)
        {
            foreach (var keyNode in mainline.Elements("key"))
            {
                var key = new MainlineKey { Id = AttrInt(keyNode, "id"), Time = AttrFloat(keyNode, "time") * 0.001f };
                key.BoneRefs.AddRange(keyNode.Elements("bone_ref").Select(ParseRef));
                key.ObjectRefs.AddRange(keyNode.Elements("object_ref").Select(ParseRef));
                animation.MainlineKeys.Add(key);
            }
        }
        foreach (var timelineNode in node.Elements("timeline"))
        {
            var typeName = Attr(timelineNode, "type", Attr(timelineNode, "object_type", "sprite"));
            var timeline = new Timeline
            {
                Id = AttrInt(timelineNode, "id"), Name = Attr(timelineNode, "name"),
                ObjectType = typeName == "bone" ? ObjectType.Bone : ObjectType.Sprite,
            };
            foreach (var keyNode in timelineNode.Elements("key"))
                timeline.Keys.Add(ParseTimelineKey(timeline, keyNode));
            animation.Timelines.Add(timeline);
        }
        return animation;
    }

    private static Ref ParseRef(XElement node) => new()
    {
        Id = AttrInt(node, "id"), Parent = AttrInt(node, "parent", -1), Timeline = AttrInt(node, "timeline"),
        Key = AttrInt(node, "key"), ZIndex = AttrInt(node, "z_index"),
    };

    private static SpatialTimelineKey ParseTimelineKey(Timeline timeline, XElement node)
    {
        var child = node.Element(timeline.ObjectType == ObjectType.Bone ? "bone" : "object") ?? node.Elements().First();
        SpatialTimelineKey key = timeline.ObjectType == ObjectType.Bone ? new BoneTimelineKey() : new SpriteTimelineKey();
        key.Timeline = timeline;
        key.Id = AttrInt(node, "id");
        key.Time = AttrFloat(node, "time") * 0.001f;
        key.CurveType = Attr(node, "curve_type", "linear") switch
        {
            "instant" => CurveType.Instant, "quadratic" => CurveType.Quadratic, "cubic" => CurveType.Cubic, _ => CurveType.Linear,
        };
        key.C1 = AttrFloat(node, "c1");
        key.C2 = AttrFloat(node, "c2");
        key.Info = new SpatialInfo(
            AttrFloat(child, "x"), AttrFloat(child, "y"), AttrFloat(child, "angle"),
            AttrFloat(child, "scale_x", 1.0f), AttrFloat(child, "scale_y", 1.0f),
            AttrFloat(child, "a", 1.0f), AttrInt(node, "spin", 1));
        if (key is BoneTimelineKey bone)
        {
            bone.Length = AttrFloat(child, "length", 200.0f);
            bone.Width = AttrFloat(child, "width", 10.0f);
        }
        else if (key is SpriteTimelineKey sprite)
        {
            sprite.FolderId = AttrInt(child, "folder", -1);
            sprite.FileId = AttrInt(child, "file", -1);
            sprite.UseDefaultPivot = child.Attribute("pivot_x") is null && child.Attribute("pivot_y") is null;
            sprite.PivotX = AttrFloat(child, "pivot_x");
            sprite.PivotY = AttrFloat(child, "pivot_y", 1.0f);
        }
        return key;
    }

    private static string Attr(XElement node, string name, string value = "") => node.Attribute(name)?.Value ?? value;
    private static int AttrInt(XElement node, string name, int value = 0) =>
        int.TryParse(Attr(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : value;
    private static float AttrFloat(XElement node, string name, float value = 0.0f) =>
        float.TryParse(Attr(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : value;
    private static bool AttrBool(XElement node, string name, bool value = false) =>
        bool.TryParse(Attr(node, name), out var result) ? result : value;
}

public sealed class Folder { public int Id; public string Name = string.Empty; public List<SpriterFile> Files { get; } = []; }
public sealed class SpriterFile
{
    public Folder Folder = null!; public int Id; public string Name = string.Empty;
    public float Width; public float Height; public float PivotX; public float PivotY;
}
public sealed class Entity
{
    public int Id; public string Name = string.Empty;
    public List<CharacterMap> CharacterMaps { get; } = []; public List<Animation> Animations { get; } = [];
}
public sealed class CharacterMap { public int Id; public string Name = string.Empty; public List<MapInstruction> Maps { get; } = []; }
public sealed class MapInstruction { public int Folder; public int File; public int TargetFolder; public int TargetFile; }
public sealed class Animation
{
    public int Id; public string Name = string.Empty; public float Length; public bool Looping;
    public List<MainlineKey> MainlineKeys { get; } = []; public List<Timeline> Timelines { get; } = [];
}
public sealed class MainlineKey
{
    public int Id; public float Time; public List<Ref> BoneRefs { get; } = []; public List<Ref> ObjectRefs { get; } = [];
}
public sealed class Ref { public int Id; public int Parent; public int Timeline; public int Key; public int ZIndex; }
public sealed class Timeline
{
    public int Id; public string Name = string.Empty; public ObjectType ObjectType; public List<SpatialTimelineKey> Keys { get; } = [];
}

public struct SpatialInfo
{
    public float X, Y, Angle, ScaleX, ScaleY, Alpha;
    public int Spin;
    public SpatialInfo(float x, float y, float angle, float scaleX, float scaleY, float alpha = 1, int spin = 1) =>
        (X, Y, Angle, ScaleX, ScaleY, Alpha, Spin) = (x, y, angle, scaleX, scaleY, alpha, spin);

    public readonly SpatialInfo UnmapFromParent(SpatialInfo parent)
    {
        var radians = parent.Angle * MathF.PI / 180.0f;
        var x = X * parent.ScaleX;
        var y = Y * parent.ScaleY;
        return new SpatialInfo(x * MathF.Cos(radians) - y * MathF.Sin(radians) + parent.X,
            x * MathF.Sin(radians) + y * MathF.Cos(radians) + parent.Y,
            Angle + parent.Angle, ScaleX * parent.ScaleX, ScaleY * parent.ScaleY, Alpha * parent.Alpha, Spin);
    }

    public void Interpolate(SpatialInfo other, float t)
    {
        X += (other.X - X) * t;
        Y += (other.Y - Y) * t;
        var otherAngle = other.Angle;
        if (Spin > 0 && otherAngle - Angle < 0) otherAngle += 360.0f;
        else if (Spin < 0 && otherAngle - Angle > 0) otherAngle -= 360.0f;
        Angle += (otherAngle - Angle) * t;
        ScaleX += (other.ScaleX - ScaleX) * t;
        ScaleY += (other.ScaleY - ScaleY) * t;
        Alpha += (other.Alpha - Alpha) * t;
    }
}

public class SpatialTimelineKey
{
    public Timeline Timeline = null!; public int Id; public float Time; public CurveType CurveType; public float C1, C2;
    public SpatialInfo Info;
    public virtual ObjectType ObjectType => Timeline.ObjectType;
    public virtual SpatialTimelineKey Clone() => (SpatialTimelineKey)MemberwiseClone();
    public virtual void Interpolate(SpatialTimelineKey other, float t) => Info.Interpolate(other.Info, t);
    public float GetTByCurveType(float currentTime, float nextTime)
    {
        if (CurveType == CurveType.Instant || nextTime == Time) return 0.0f;
        var t = (currentTime - Time) / (nextTime - Time);
        static float Linear(float a, float b, float x) => a + (b - a) * x;
        static float Quadratic(float a, float b, float c, float x) => Linear(Linear(a, b, x), Linear(b, c, x), x);
        return CurveType switch
        {
            CurveType.Quadratic => Quadratic(0, C1, 1, t),
            CurveType.Cubic => Linear(Quadratic(0, C1, C2, t), Quadratic(C1, C2, 1, t), t),
            _ => t,
        };
    }
}
public sealed class BoneTimelineKey : SpatialTimelineKey
{
    public float Length, Width;
    public override SpatialTimelineKey Clone() => (BoneTimelineKey)MemberwiseClone();
    public override void Interpolate(SpatialTimelineKey other, float t)
    {
        base.Interpolate(other, t);
        var bone = (BoneTimelineKey)other;
        Length += (bone.Length - Length) * t;
        Width += (bone.Width - Width) * t;
    }
}
public sealed class SpriteTimelineKey : SpatialTimelineKey
{
    public int FolderId, FileId; public bool UseDefaultPivot; public float PivotX, PivotY; public int ZIndex;
    public override SpatialTimelineKey Clone() => (SpriteTimelineKey)MemberwiseClone();
    public override void Interpolate(SpatialTimelineKey other, float t)
    {
        base.Interpolate(other, t);
        var sprite = (SpriteTimelineKey)other;
        PivotX += (sprite.PivotX - PivotX) * t;
        PivotY += (sprite.PivotY - PivotY) * t;
    }
}
