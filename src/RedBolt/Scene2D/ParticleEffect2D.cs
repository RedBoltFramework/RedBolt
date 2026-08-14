// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = RedBolt.Resource.Resource;

/// <summary>2D particle emitter types.</summary>
public enum EmitterType2D { Gravity, Radial }

/// <summary>2D particle effect resource in Particle Designer PEX format.</summary>
[Reflected]
public partial class ParticleEffect2D : ResourceType
{
    private static readonly int[] SourceBlendFunctions =
        [1, 1, 0x0306, 0x0302, 0x0302, 1, 0x0305, 1, 0x0302, 0x0302];
    private static readonly int[] DestinationBlendFunctions =
        [0, 1, 0, 0x0303, 1, 0x0303, 0x0304, 1, 1, 0x0303];

    private string _loadSpriteName = string.Empty;

    public Sprite2D? Sprite { get; set; }
    public Vector2f SourcePositionVariance { get; set; } = new(7.0f, 7.0f);
    public float Speed { get; set; } = 260.0f;
    public float SpeedVariance { get; set; } = 10.0f;
    public float ParticleLifeSpan { get; set; } = 1.0f;
    public float ParticleLifespanVariance { get; set; } = 0.7f;
    public float Angle { get; set; }
    public float AngleVariance { get; set; } = 360.0f;
    public Vector2f Gravity { get; set; }
    public float RadialAcceleration { get; set; } = -380.0f;
    public float TangentialAcceleration { get; set; } = -140.0f;
    public float RadialAccelVariance { get; set; }
    public float TangentialAccelVariance { get; set; }
    public Color StartColor { get; set; } = new(1.0f, 0.0f, 0.0f, 1.0f);
    public Color StartColorVariance { get; set; } = new(0.0f, 0.0f, 0.0f, 0.0f);
    public Color FinishColor { get; set; } = new(1.0f, 1.0f, 0.0f, 1.0f);
    public Color FinishColorVariance { get; set; } = new(0.0f, 0.0f, 0.0f, 0.0f);
    public int MaxParticles { get; set; } = 600;
    public float StartParticleSize { get; set; } = 60.0f;
    public float StartParticleSizeVariance { get; set; } = 40.0f;
    public float FinishParticleSize { get; set; } = 5.0f;
    public float FinishParticleSizeVariance { get; set; } = 5.0f;
    public float Duration { get; set; } = float.PositiveInfinity;
    public EmitterType2D EmitterType { get; set; }
    public float MaxRadius { get; set; } = 100.0f;
    public float MaxRadiusVariance { get; set; }
    public float MinRadius { get; set; }
    public float MinRadiusVariance { get; set; }
    public float RotatePerSecond { get; set; }
    public float RotatePerSecondVariance { get; set; }
    public BlendMode BlendMode { get; set; } = BlendMode.Alpha;
    public float RotationStart { get; set; }
    public float RotationStartVariance { get; set; }
    public float RotationEnd { get; set; }
    public float RotationEndVariance { get; set; }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(Name))
            SetName(source.Name);
        _loadSpriteName = string.Empty;

        using var xml = new XMLFile();
        if (!xml.Load(source) || xml.Document.Root is not { Name.LocalName: "particleEmitterConfig" } root)
            return false;

        var textureName = (string?)root.Element("texture")?.Attribute("name") ?? string.Empty;
        _loadSpriteName = GetParentPath(Name) + textureName;
        SourcePositionVariance = ReadVector2(root, "sourcePositionVariance");
        Speed = ReadFloat(root, "speed");
        SpeedVariance = ReadFloat(root, "speedVariance");
        ParticleLifeSpan = MathF.Max(0.01f, ReadFloat(root, "particleLifeSpan"));
        ParticleLifespanVariance = ReadFloat(root, "particleLifespanVariance");
        Angle = ReadFloat(root, "angle");
        AngleVariance = ReadFloat(root, "angleVariance");
        Gravity = ReadVector2(root, "gravity");
        RadialAcceleration = ReadFloat(root, "radialAcceleration");
        TangentialAcceleration = ReadFloat(root, "tangentialAcceleration");
        RadialAccelVariance = ReadFloat(root, "radialAccelVariance");
        TangentialAccelVariance = ReadFloat(root, "tangentialAccelVariance");
        StartColor = ReadColor(root, "startColor");
        StartColorVariance = ReadColor(root, "startColorVariance");
        FinishColor = ReadColor(root, "finishColor");
        FinishColorVariance = ReadColor(root, "finishColorVariance");
        MaxParticles = ReadInt(root, "maxParticles");
        StartParticleSize = ReadFloat(root, "startParticleSize");
        StartParticleSizeVariance = ReadFloat(root, "startParticleSizeVariance");
        FinishParticleSize = ReadFloat(root, "finishParticleSize");
        FinishParticleSizeVariance = ReadFloat(root, "FinishParticleSizeVariance");
        var duration = ReadFloat(root, "duration");
        Duration = duration > 0.0f ? duration : float.PositiveInfinity;
        EmitterType = (EmitterType2D)ReadInt(root, "emitterType");
        MaxRadius = ReadFloat(root, "maxRadius");
        MaxRadiusVariance = ReadFloat(root, "maxRadiusVariance");
        MinRadius = ReadFloat(root, "minRadius");
        MinRadiusVariance = ReadFloat(root, "minRadiusVariance");
        RotatePerSecond = ReadFloat(root, "rotatePerSecond");
        RotatePerSecondVariance = ReadFloat(root, "rotatePerSecondVariance");
        var sourceBlend = ReadInt(root, "blendFuncSource");
        var destinationBlend = ReadInt(root, "blendFuncDestination");
        BlendMode = BlendMode.Alpha;
        for (var i = 0; i < SourceBlendFunctions.Length; ++i)
        {
            if (sourceBlend == SourceBlendFunctions[i] && destinationBlend == DestinationBlendFunctions[i])
            {
                BlendMode = (BlendMode)i;
                break;
            }
        }
        RotationStart = ReadFloat(root, "rotationStart");
        RotationStartVariance = ReadFloat(root, "rotationStartVariance");
        RotationEnd = ReadFloat(root, "rotationEnd");
        RotationEndVariance = ReadFloat(root, "rotationEndVariance");
        SetMemoryUse(checked((int)source.Size));
        return true;
    }

    public override bool EndLoad()
    {
        if (!string.IsNullOrEmpty(_loadSpriteName))
            Sprite = GetSubsystem<ResourceCache>()?.GetResource<Sprite2D>(_loadSpriteName);
        _loadSpriteName = string.Empty;
        return true;
    }

    public override bool Save(Serializer destination)
    {
        if (Sprite is null)
            return false;
        using var xml = new XMLFile();
        var root = xml.CreateRoot("particleEmitterConfig");
        root.CreateChild("texture").SetAttribute("name", Path.GetFileName(Sprite.Name));
        WriteVector2(root, "sourcePosition", Vector2f.Zero);
        WriteVector2(root, "sourcePositionVariance", SourcePositionVariance);
        WriteFloat(root, "speed", Speed);
        WriteFloat(root, "speedVariance", SpeedVariance);
        WriteFloat(root, "particleLifeSpan", ParticleLifeSpan);
        WriteFloat(root, "particleLifespanVariance", ParticleLifespanVariance);
        WriteFloat(root, "angle", Angle);
        WriteFloat(root, "angleVariance", AngleVariance);
        WriteVector2(root, "gravity", Gravity);
        WriteFloat(root, "radialAcceleration", RadialAcceleration);
        WriteFloat(root, "tangentialAcceleration", TangentialAcceleration);
        WriteFloat(root, "radialAccelVariance", RadialAccelVariance);
        WriteFloat(root, "tangentialAccelVariance", TangentialAccelVariance);
        WriteColor(root, "startColor", StartColor);
        WriteColor(root, "startColorVariance", StartColorVariance);
        WriteColor(root, "finishColor", FinishColor);
        WriteColor(root, "finishColorVariance", FinishColorVariance);
        WriteInt(root, "maxParticles", MaxParticles);
        WriteFloat(root, "startParticleSize", StartParticleSize);
        WriteFloat(root, "startParticleSizeVariance", StartParticleSizeVariance);
        WriteFloat(root, "finishParticleSize", FinishParticleSize);
        WriteFloat(root, "FinishParticleSizeVariance", FinishParticleSizeVariance);
        WriteFloat(root, "duration", float.IsPositiveInfinity(Duration) ? -1.0f : Duration);
        WriteInt(root, "emitterType", (int)EmitterType);
        WriteFloat(root, "maxRadius", MaxRadius);
        WriteFloat(root, "maxRadiusVariance", MaxRadiusVariance);
        WriteFloat(root, "minRadius", MinRadius);
        WriteFloat(root, "minRadiusVariance", MinRadiusVariance);
        WriteFloat(root, "rotatePerSecond", RotatePerSecond);
        WriteFloat(root, "rotatePerSecondVariance", RotatePerSecondVariance);
        WriteInt(root, "blendFuncSource", SourceBlendFunctions[(int)BlendMode]);
        WriteInt(root, "blendFuncDestination", DestinationBlendFunctions[(int)BlendMode]);
        WriteFloat(root, "rotationStart", RotationStart);
        WriteFloat(root, "rotationStartVariance", RotationStartVariance);
        WriteFloat(root, "rotationEnd", RotationEnd);
        WriteFloat(root, "rotationEndVariance", RotationEndVariance);
        return xml.Save(destination);
    }

    public ParticleEffect2D Clone(string cloneName = "")
    {
        var clone = (ParticleEffect2D)MemberwiseClone();
        clone.SetName(cloneName);
        clone._loadSpriteName = string.Empty;
        return clone;
    }

    private static int ReadInt(global::System.Xml.Linq.XElement root, string name) =>
        int.TryParse((string?)root.Element(name)?.Attribute("value"), global::System.Globalization.NumberStyles.Integer,
            global::System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;
    private static float ReadFloat(global::System.Xml.Linq.XElement root, string name) =>
        float.TryParse((string?)root.Element(name)?.Attribute("value"), global::System.Globalization.NumberStyles.Float,
            global::System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0.0f;
    private static float ReadAttribute(global::System.Xml.Linq.XElement? element, string name) =>
        float.TryParse((string?)element?.Attribute(name), global::System.Globalization.NumberStyles.Float,
            global::System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0.0f;
    private static Vector2f ReadVector2(global::System.Xml.Linq.XElement root, string name)
    {
        var element = root.Element(name);
        return new Vector2f(ReadAttribute(element, "x"), ReadAttribute(element, "y"));
    }
    private static Color ReadColor(global::System.Xml.Linq.XElement root, string name)
    {
        var element = root.Element(name);
        return new Color(ReadAttribute(element, "red"), ReadAttribute(element, "green"),
            ReadAttribute(element, "blue"), ReadAttribute(element, "alpha"));
    }
    private static void WriteInt(XMLElement root, string name, int value) => root.CreateChild(name).SetInt("value", value);
    private static void WriteFloat(XMLElement root, string name, float value) => root.CreateChild(name).SetFloat("value", value);
    private static void WriteVector2(XMLElement root, string name, Vector2f value)
    {
        var child = root.CreateChild(name); child.SetFloat("x", value.X); child.SetFloat("y", value.Y);
    }
    private static void WriteColor(XMLElement root, string name, Color value)
    {
        var child = root.CreateChild(name);
        child.SetFloat("red", value.R); child.SetFloat("green", value.G);
        child.SetFloat("blue", value.B); child.SetFloat("alpha", value.A);
    }
    private static string GetParentPath(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..(separator + 1)];
    }
}
