using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Particle emitter shapes.</summary>
public enum EmitterType
{
    Sphere = 0,
    Box,
    SphereVolume,
    Cylinder,
    Ring
}

/// <summary>Color animation frame definition.</summary>
public struct ColorFrame
{
    /// <summary>Construct with default values.</summary>
    public ColorFrame()
    {
        Color = Color.White;
        Time = 0.0f;
    }

    /// <summary>Construct with a color and zero time.</summary>
    public ColorFrame(Color color)
    {
        Color = color;
        Time = 0.0f;
    }

    /// <summary>Construct from a color and time.</summary>
    public ColorFrame(Color color, float time)
    {
        Color = color;
        Time = time;
    }

    /// <summary>Return interpolated value with another color-time pair at the time specified.</summary>
    public readonly Color Interpolate(in ColorFrame next, float time)
    {
        var timeInterval = next.Time - Time;
        if (timeInterval > 0.0f)
        {
            var t = (time - Time) / timeInterval;
            return Color.Lerp(next.Color, t);
        }

        return next.Color;
    }

    /// <summary>Color.</summary>
    public Color Color;
    /// <summary>Time.</summary>
    public float Time;
}

/// <summary>Texture animation frame definition.</summary>
public struct TextureFrame : IEquatable<TextureFrame>
{
    /// <summary>Construct with default values.</summary>
    public TextureFrame()
    {
        Uv = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
        Time = 0.0f;
    }

    /// <summary>Equality operator.</summary>
    public readonly bool Equals(TextureFrame other) => Uv == other.Uv && MathDefs.Equals(Time, other.Time);

    public override bool Equals(object? obj) => obj is TextureFrame other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Uv, Time);

    public static bool operator ==(TextureFrame left, TextureFrame right) => left.Equals(right);

    public static bool operator !=(TextureFrame left, TextureFrame right) =>
        left.Uv != right.Uv && MathDefs.Equals(left.Time, right.Time);

    /// <summary>UV coordinates.</summary>
    public Rect Uv;
    /// <summary>Time.</summary>
    public float Time;
}

/// <summary>Particle effect definition.</summary>
public class ParticleEffect : Resource
{
    public const uint DefaultNumParticles = 10;

    private static readonly string[] EmitterTypeNames =
    [
        "Sphere",
        "Box",
        "SphereVolume",
        "Cylinder",
        "Ring"
    ];

    private static readonly Vector2f DefaultParticleSize = new(0.1f, 0.1f);
    private const float DefaultEmissionRate = 10.0f;
    private const float MinEmissionRate = 0.01f;
    private const float DefaultTimeToLive = 1.0f;
    private const float DefaultVelocity = 1.0f;
    private static readonly Vector3f DefaultDirectionMin = new(-1.0f, -1.0f, -1.0f);
    private static readonly Vector3f DefaultDirectionMax = new(1.0f, 1.0f, 1.0f);

    /// <summary>Construct.</summary>
    public ParticleEffect()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _loadMaterialName = string.Empty;
        _textureFrames.Clear();
        _colorFrames.Clear();
        _material = null;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<ParticleEffect>();
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        _loadMaterialName = string.Empty;

        using var file = new XMLFile();
        if (!file.Load(source))
        {
            Log.GetLogger().LogError("Load particle effect file failed");
            return false;
        }

        var rootElem = file.GetRoot();
        var success = Load(rootElem);
        if (success)
            SetMemoryUse(checked((int)source.Size));
        return success;
    }

    /// <summary>Finish resource loading. Always called from the main thread. Return true if successful.</summary>
    public override bool EndLoad()
    {
        // Apply the material now
        if (!string.IsNullOrEmpty(_loadMaterialName))
        {
            SetMaterial(GetSubsystem<ResourceCache>()!.GetResourceOrNull<Material>(_loadMaterialName));
            _loadMaterialName = string.Empty;
        }

        return true;
    }

    /// <summary>Save resource. Return true if successful.</summary>
    public override bool Save(Serializer dest)
    {
        using var xml = new XMLFile();
        var materialElem = xml.CreateRoot("particleeffect");
        Save(materialElem);
        return xml.Save(dest);
    }

    /// <summary>Save resource to XMLElement. Return true if successful.</summary>
    public bool Save(XMLElement dest)
    {
        if (dest.IsNull)
        {
            Log.GetLogger().LogError("Can not save particle effect to null XML element");
            return false;
        }

        var childElem = dest.CreateChild("material");
        childElem.SetAttribute("name", _material?.Name ?? string.Empty);

        childElem = dest.CreateChild("numparticles");
        childElem.SetInt("value", (int)_numParticles);

        childElem = dest.CreateChild("updateinvisible");
        childElem.SetBool("enable", _updateInvisible);

        childElem = dest.CreateChild("relative");
        childElem.SetBool("enable", _relative);

        childElem = dest.CreateChild("scaled");
        childElem.SetBool("enable", _scaled);

        childElem = dest.CreateChild("sorted");
        childElem.SetBool("enable", _sorted);

        childElem = dest.CreateChild("fixedscreensize");
        childElem.SetBool("enable", _fixedScreenSize);

        childElem = dest.CreateChild("animlodbias");
        childElem.SetFloat("value", _animationLodBias);

        childElem = dest.CreateChild("emittertype");
        childElem.SetAttribute("value", EmitterTypeNames[(int)_emitterType]);

        childElem = dest.CreateChild("emittersize");
        childElem.SetVector3("value", _emitterSize);

        childElem = dest.CreateChild("direction");
        childElem.SetVector3("min", _directionMin);
        childElem.SetVector3("max", _directionMax);

        childElem = dest.CreateChild("constantforce");
        childElem.SetVector3("value", _constantForce);

        childElem = dest.CreateChild("dampingforce");
        childElem.SetFloat("value", _dampingForce);

        childElem = dest.CreateChild("activetime");
        childElem.SetFloat("value", _activeTime);

        childElem = dest.CreateChild("inactivetime");
        childElem.SetFloat("value", _inactiveTime);

        childElem = dest.CreateChild("emissionrate");
        childElem.SetFloat("min", _emissionRateMin);
        childElem.SetFloat("max", _emissionRateMax);

        childElem = dest.CreateChild("particlesize");
        childElem.SetVector2("min", _sizeMin);
        childElem.SetVector2("max", _sizeMax);

        childElem = dest.CreateChild("timetolive");
        childElem.SetFloat("min", _timeToLiveMin);
        childElem.SetFloat("max", _timeToLiveMax);

        childElem = dest.CreateChild("velocity");
        childElem.SetFloat("min", _velocityMin);
        childElem.SetFloat("max", _velocityMax);

        childElem = dest.CreateChild("rotation");
        childElem.SetFloat("min", _rotationMin);
        childElem.SetFloat("max", _rotationMax);

        childElem = dest.CreateChild("rotationspeed");
        childElem.SetFloat("min", _rotationSpeedMin);
        childElem.SetFloat("max", _rotationSpeedMax);

        childElem = dest.CreateChild("sizedelta");
        childElem.SetFloat("add", _sizeAdd);
        childElem.SetFloat("mul", _sizeMul);

        childElem = dest.CreateChild("faceCameraMode");
        childElem.SetAttribute("value", BillboardSet.GetFaceCameraModeNames()[(int)_faceCameraMode]);

        if (_colorFrames.Count == 1)
        {
            childElem = dest.CreateChild("color");
            childElem.SetColor("value", _colorFrames[0].Color);
        }

        if (_colorFrames.Count > 1)
        {
            for (var i = 0; i < _colorFrames.Count; ++i)
            {
                childElem = dest.CreateChild("colorfade");
                childElem.SetColor("color", _colorFrames[i].Color);
                childElem.SetFloat("time", _colorFrames[i].Time);
            }
        }

        for (var i = 0; i < _textureFrames.Count; ++i)
        {
            childElem = dest.CreateChild("texanim");
            childElem.SetRect("uv", _textureFrames[i].Uv);
            childElem.SetFloat("time", _textureFrames[i].Time);
        }

        return true;
    }

    /// <summary>Load resource from XMLElement synchronously. Return true if successful.</summary>
    public bool Load(XMLElement source)
    {
        // Reset to defaults first so that missing parameters in case of a live reload behave as expected
        _material = null;
        _numParticles = DefaultNumParticles;
        _updateInvisible = false;
        _relative = true;
        _scaled = true;
        _sorted = false;
        _fixedScreenSize = false;
        _animationLodBias = 0.0f;
        _emitterType = EmitterType.Sphere;
        _emitterSize = Vector3f.Zero;
        _directionMin = DefaultDirectionMin;
        _directionMax = DefaultDirectionMax;
        _constantForce = Vector3f.Zero;
        _dampingForce = 0.0f;
        _activeTime = 0.0f;
        _inactiveTime = 0.0f;
        _emissionRateMin = DefaultEmissionRate;
        _emissionRateMax = DefaultEmissionRate;
        _sizeMin = DefaultParticleSize;
        _sizeMax = DefaultParticleSize;
        _timeToLiveMin = DefaultTimeToLive;
        _timeToLiveMax = DefaultTimeToLive;
        _velocityMin = DefaultVelocity;
        _velocityMax = DefaultVelocity;
        _rotationMin = 0.0f;
        _rotationMax = 0.0f;
        _rotationSpeedMin = 0.0f;
        _rotationSpeedMax = 0.0f;
        _sizeAdd = 0.0f;
        _sizeMul = 1.0f;
        _colorFrames.Clear();
        _textureFrames.Clear();
        _faceCameraMode = FaceCameraMode.RotateXyz;

        if (source.IsNull)
        {
            Log.GetLogger().LogError("Can not load particle effect from null XML element");
            return false;
        }

        if (source.HasChild("material"))
        {
            _loadMaterialName = source.GetChild("material").GetAttribute("name");
            // If async loading, can not GetResource() the material. But can do a background request for it
            if (AsyncLoadState == AsyncLoadState.Loading)
                GetSubsystem<ResourceCache>()!.BackgroundLoadResource<Material>(_loadMaterialName, true, this);
        }

        if (source.HasChild("numparticles"))
            SetNumParticles((uint)source.GetChild("numparticles").GetInt("value"));

        if (source.HasChild("updateinvisible"))
            _updateInvisible = source.GetChild("updateinvisible").GetBool("enable");

        if (source.HasChild("relative"))
            _relative = source.GetChild("relative").GetBool("enable");

        if (source.HasChild("scaled"))
            _scaled = source.GetChild("scaled").GetBool("enable");

        if (source.HasChild("sorted"))
            _sorted = source.GetChild("sorted").GetBool("enable");

        if (source.HasChild("fixedscreensize"))
            _fixedScreenSize = source.GetChild("fixedscreensize").GetBool("enable");

        if (source.HasChild("animlodbias"))
            SetAnimationLodBias(source.GetChild("animlodbias").GetFloat("value"));

        if (source.HasChild("emittertype"))
        {
            var type = source.GetChild("emittertype").GetAttributeLower("value");
            if (type == "point")
            {
                // Point emitter type is deprecated, handled as zero sized sphere
                _emitterType = EmitterType.Sphere;
                _emitterSize = Vector3f.Zero;
            }
            else
                _emitterType = (EmitterType)GetStringListIndex(type, EmitterTypeNames, (int)EmitterType.Sphere);
        }

        if (source.HasChild("emittersize"))
            _emitterSize = source.GetChild("emittersize").GetVector3("value");

        if (source.HasChild("emitterradius"))
        {
            var radius = source.GetChild("emitterradius").GetFloat("value");
            _emitterSize = new Vector3f(radius, radius, radius);
        }

        if (source.HasChild("direction"))
            GetVector3MinMax(source.GetChild("direction"), ref _directionMin, ref _directionMax);

        if (source.HasChild("constantforce"))
            _constantForce = source.GetChild("constantforce").GetVector3("value");

        if (source.HasChild("dampingforce"))
            _dampingForce = source.GetChild("dampingforce").GetFloat("value");

        if (source.HasChild("activetime"))
            _activeTime = source.GetChild("activetime").GetFloat("value");
        if (_activeTime < 0.0f)
            _activeTime = float.PositiveInfinity;

        if (source.HasChild("inactivetime"))
            _inactiveTime = source.GetChild("inactivetime").GetFloat("value");
        if (_inactiveTime < 0.0f)
            _inactiveTime = float.PositiveInfinity;

        if (source.HasChild("emissionrate"))
            GetFloatMinMax(source.GetChild("emissionrate"), ref _emissionRateMin, ref _emissionRateMax);

        if (source.HasChild("interval"))
        {
            float intervalMin = 0.0f;
            float intervalMax = 0.0f;
            GetFloatMinMax(source.GetChild("interval"), ref intervalMin, ref intervalMax);
            _emissionRateMax = 1.0f / intervalMin;
            _emissionRateMin = 1.0f / intervalMax;
        }

        if (source.HasChild("particlesize"))
            GetVector2MinMax(source.GetChild("particlesize"), ref _sizeMin, ref _sizeMax);

        if (source.HasChild("timetolive"))
            GetFloatMinMax(source.GetChild("timetolive"), ref _timeToLiveMin, ref _timeToLiveMax);

        if (source.HasChild("velocity"))
            GetFloatMinMax(source.GetChild("velocity"), ref _velocityMin, ref _velocityMax);

        if (source.HasChild("rotation"))
            GetFloatMinMax(source.GetChild("rotation"), ref _rotationMin, ref _rotationMax);

        if (source.HasChild("rotationspeed"))
            GetFloatMinMax(source.GetChild("rotationspeed"), ref _rotationSpeedMin, ref _rotationSpeedMax);

        if (source.HasChild("faceCameraMode"))
        {
            var type = source.GetChild("faceCameraMode").GetAttributeLower("value");
            _faceCameraMode = (FaceCameraMode)GetStringListIndex(type, BillboardSet.GetFaceCameraModeNames(), (int)FaceCameraMode.RotateXyz);
        }

        if (source.HasChild("sizedelta"))
        {
            var deltaElem = source.GetChild("sizedelta");
            if (deltaElem.HasAttribute("add"))
                _sizeAdd = deltaElem.GetFloat("add");
            if (deltaElem.HasAttribute("mul"))
                _sizeMul = deltaElem.GetFloat("mul");
        }

        if (source.HasChild("color"))
        {
            var colorFrame = new ColorFrame(source.GetChild("color").GetColor("value"));
            SetColorFrame(0, colorFrame);
        }

        if (source.HasChild("colorfade"))
        {
            var fades = new List<ColorFrame>();
            for (var colorFadeElem = source.GetChild("colorfade"); colorFadeElem.NotNull;
                 colorFadeElem = colorFadeElem.GetNext("colorfade"))
                fades.Add(new ColorFrame(colorFadeElem.GetColor("color"), colorFadeElem.GetFloat("time")));

            SetColorFrames(fades);
        }

        if (_colorFrames.Count == 0)
            _colorFrames.Add(new ColorFrame(Color.White));

        if (source.HasChild("texanim"))
        {
            var animations = new List<TextureFrame>();
            for (var animElem = source.GetChild("texanim"); animElem.NotNull; animElem = animElem.GetNext("texanim"))
            {
                var animation = new TextureFrame
                {
                    Uv = animElem.GetRect("uv"),
                    Time = animElem.GetFloat("time")
                };
                animations.Add(animation);
            }

            SetTextureFrames(animations);
        }

        return true;
    }

    public void SetMaterial(Material? material) => _material = material;
    public void SetNumParticles(uint num) => _numParticles = num;
    public void SetUpdateInvisible(bool enable) => _updateInvisible = enable;
    public void SetRelative(bool enable) => _relative = enable;
    public void SetScaled(bool enable) => _scaled = enable;
    public void SetSorted(bool enable) => _sorted = enable;
    public void SetFixedScreenSize(bool enable) => _fixedScreenSize = enable;
    public void SetAnimationLodBias(float lodBias) => _animationLodBias = lodBias;
    public void SetEmitterType(EmitterType type) => _emitterType = type;
    public void SetEmitterSize(in Vector3f size) => _emitterSize = size;
    public void SetMinDirection(in Vector3f direction) => _directionMin = direction;
    public void SetMaxDirection(in Vector3f direction) => _directionMax = direction;
    public void SetConstantForce(in Vector3f force) => _constantForce = force;
    public void SetDampingForce(float force) => _dampingForce = force;
    public void SetActiveTime(float time) => _activeTime = time;
    public void SetInactiveTime(float time) => _inactiveTime = time;
    public void SetMinEmissionRate(float rate) => _emissionRateMin = Math.Max(rate, MinEmissionRate);
    public void SetMaxEmissionRate(float rate) => _emissionRateMax = Math.Max(rate, MinEmissionRate);
    public void SetMinParticleSize(in Vector2f size) => _sizeMin = size;
    public void SetMaxParticleSize(in Vector2f size) => _sizeMax = size;
    public void SetMinTimeToLive(float time) => _timeToLiveMin = Math.Max(time, 0.0f);
    public void SetMaxTimeToLive(float time) => _timeToLiveMax = Math.Max(time, 0.0f);
    public void SetMinVelocity(float velocity) => _velocityMin = velocity;
    public void SetMaxVelocity(float velocity) => _velocityMax = velocity;
    public void SetMinRotation(float rotation) => _rotationMin = rotation;
    public void SetMaxRotation(float rotation) => _rotationMax = rotation;
    public void SetMinRotationSpeed(float speed) => _rotationSpeedMin = speed;
    public void SetMaxRotationSpeed(float speed) => _rotationSpeedMax = speed;
    public void SetSizeAdd(float sizeAdd) => _sizeAdd = sizeAdd;
    public void SetSizeMul(float sizeMul) => _sizeMul = sizeMul;

    /// <summary>Add a color frame sorted in the correct position based on time.</summary>
    public void AddColorTime(in Color color, float time)
    {
        var s = _colorFrames.Count;
        _colorFrames.Add(new ColorFrame());

        for (var i = 0; i < s; i++)
        {
            if (_colorFrames[i].Time > time)
            {
                for (var j = s; j > i; j--)
                    _colorFrames[j] = _colorFrames[j - 1];
                _colorFrames[i] = new ColorFrame(color, time);
                return;
            }
        }

        // highest time, add last:
        _colorFrames[s] = new ColorFrame(color, time);
    }

    public void AddColorFrame(in ColorFrame colorFrame) => AddColorTime(colorFrame.Color, colorFrame.Time);

    public void RemoveColorFrame(uint index)
    {
        var s = _colorFrames.Count;
        for (var i = (int)index; i < s - 1; i++)
            _colorFrames[i] = _colorFrames[i + 1];
        _colorFrames.RemoveAt(s - 1);
    }

    public void SetColorFrames(IReadOnlyList<ColorFrame> colorFrames)
    {
        if (ReferenceEquals(colorFrames, _colorFrames))
            return;

        _colorFrames.Clear();
        _colorFrames.AddRange(colorFrames);
    }

    public void SetColorFrame(uint index, in ColorFrame colorFrame)
    {
        while (_colorFrames.Count < index + 1)
            _colorFrames.Add(new ColorFrame());
        _colorFrames[(int)index] = colorFrame;
    }

    public void SetNumColorFrames(uint number)
    {
        if (_colorFrames.Count != number)
        {
            if (number < _colorFrames.Count)
                _colorFrames.RemoveRange((int)number, _colorFrames.Count - (int)number);
            else
            {
                while (_colorFrames.Count < number)
                    _colorFrames.Add(new ColorFrame());
            }
        }
    }

    public void SetFaceCameraMode(FaceCameraMode mode) => _faceCameraMode = mode;

    public void SortColorFrames()
    {
        var cf = new List<ColorFrame>(_colorFrames);
        _colorFrames.Clear();
        foreach (var frame in cf)
            AddColorFrame(frame);
    }

    public void AddTextureTime(in Rect uv, float time)
    {
        var s = _textureFrames.Count;
        _textureFrames.Add(new TextureFrame());

        for (var i = 0; i < s; i++)
        {
            if (_textureFrames[i].Time > time)
            {
                for (var j = s; j > i; j--)
                    _textureFrames[j] = _textureFrames[j - 1];
                _textureFrames[i] = new TextureFrame { Uv = uv, Time = time };
                return;
            }
        }

        // Highest time, add last
        _textureFrames[s] = new TextureFrame { Uv = uv, Time = time };
    }

    public void AddTextureFrame(in TextureFrame textureFrame) => AddTextureTime(textureFrame.Uv, textureFrame.Time);

    public void RemoveTextureFrame(uint index)
    {
        var s = _textureFrames.Count;
        for (var i = (int)index; i < s - 1; i++)
            _textureFrames[i] = _textureFrames[i + 1];
        _textureFrames.RemoveAt(s - 1);
    }

    public void SetTextureFrames(IReadOnlyList<TextureFrame> textureFrames)
    {
        if (ReferenceEquals(textureFrames, _textureFrames))
            return;

        _textureFrames.Clear();
        _textureFrames.AddRange(textureFrames);
    }

    public void SetTextureFrame(uint index, in TextureFrame textureFrame)
    {
        while (_textureFrames.Count < index + 1)
            _textureFrames.Add(new TextureFrame());
        _textureFrames[(int)index] = textureFrame;
    }

    public void SetNumTextureFrames(uint number)
    {
        if (_textureFrames.Count != number)
        {
            if (number < _textureFrames.Count)
                _textureFrames.RemoveRange((int)number, _textureFrames.Count - (int)number);
            else
            {
                while (_textureFrames.Count < number)
                    _textureFrames.Add(new TextureFrame());
            }
        }
    }

    public void SortTextureFrames()
    {
        var tf = new List<TextureFrame>(_textureFrames);
        _textureFrames.Clear();
        foreach (var frame in tf)
            AddTextureFrame(frame);
    }

    /// <summary>Clone the particle effect.</summary>
    public ParticleEffect Clone(string cloneName = "")
    {
        var ret = new ParticleEffect
        {
            _material = _material,
            _numParticles = _numParticles,
            _updateInvisible = _updateInvisible,
            _relative = _relative,
            _scaled = _scaled,
            _sorted = _sorted,
            _fixedScreenSize = _fixedScreenSize,
            _animationLodBias = _animationLodBias,
            _emitterType = _emitterType,
            _emitterSize = _emitterSize,
            _directionMin = _directionMin,
            _directionMax = _directionMax,
            _constantForce = _constantForce,
            _dampingForce = _dampingForce,
            _activeTime = _activeTime,
            _inactiveTime = _inactiveTime,
            _emissionRateMin = _emissionRateMin,
            _emissionRateMax = _emissionRateMax,
            _sizeMin = _sizeMin,
            _sizeMax = _sizeMax,
            _timeToLiveMin = _timeToLiveMin,
            _timeToLiveMax = _timeToLiveMax,
            _velocityMin = _velocityMin,
            _velocityMax = _velocityMax,
            _rotationMin = _rotationMin,
            _rotationMax = _rotationMax,
            _rotationSpeedMin = _rotationSpeedMin,
            _rotationSpeedMax = _rotationSpeedMax,
            _sizeAdd = _sizeAdd,
            _sizeMul = _sizeMul,
            _faceCameraMode = _faceCameraMode
        };
        ret.SetName(cloneName);
        ret._colorFrames.AddRange(_colorFrames);
        ret._textureFrames.AddRange(_textureFrames);
        /// \todo Zero if source was created programmatically
        ret.SetMemoryUse(GetMemoryUse());
        return ret;
    }

    public Material? GetMaterial() => _material;
    public uint GetNumParticles() => _numParticles;
    public bool GetUpdateInvisible() => _updateInvisible;
    public bool IsRelative() => _relative;
    public bool IsScaled() => _scaled;
    public bool IsSorted() => _sorted;
    public bool IsFixedScreenSize() => _fixedScreenSize;
    public float GetAnimationLodBias() => _animationLodBias;
    public EmitterType GetEmitterType() => _emitterType;
    public Vector3f GetEmitterSize() => _emitterSize;
    public Vector3f GetMinDirection() => _directionMin;
    public Vector3f GetMaxDirection() => _directionMax;
    public Vector3f GetConstantForce() => _constantForce;
    public float GetDampingForce() => _dampingForce;
    public float GetActiveTime() => _activeTime;
    public float GetInactiveTime() => _inactiveTime;
    public float GetMinEmissionRate() => _emissionRateMin;
    public float GetMaxEmissionRate() => _emissionRateMax;
    public Vector2f GetMinParticleSize() => _sizeMin;
    public Vector2f GetMaxParticleSize() => _sizeMax;
    public float GetMinTimeToLive() => _timeToLiveMin;
    public float GetMaxTimeToLive() => _timeToLiveMax;
    public float GetMinVelocity() => _velocityMin;
    public float GetMaxVelocity() => _velocityMax;
    public float GetMinRotation() => _rotationMin;
    public float GetMaxRotation() => _rotationMax;
    public float GetMinRotationSpeed() => _rotationSpeedMin;
    public float GetMaxRotationSpeed() => _rotationSpeedMax;
    public float GetSizeAdd() => _sizeAdd;
    public float GetSizeMul() => _sizeMul;
    public IReadOnlyList<ColorFrame> GetColorFrames() => _colorFrames;
    public uint GetNumColorFrames() => (uint)_colorFrames.Count;
    public ColorFrame? GetColorFrame(uint index) => index < (uint)_colorFrames.Count ? _colorFrames[(int)index] : null;
    public IReadOnlyList<TextureFrame> GetTextureFrames() => _textureFrames;
    public uint GetNumTextureFrames() => (uint)_textureFrames.Count;
    public TextureFrame? GetTextureFrame(uint index) => index < (uint)_textureFrames.Count ? _textureFrames[(int)index] : null;
    public FaceCameraMode GetFaceCameraMode() => _faceCameraMode;

    public Vector3f GetRandomDirection() => new(
        MathDefs.Lerp(_directionMin.X, _directionMax.X, MathDefs.Random(1.0f)),
        MathDefs.Lerp(_directionMin.Y, _directionMax.Y, MathDefs.Random(1.0f)),
        MathDefs.Lerp(_directionMin.Z, _directionMax.Z, MathDefs.Random(1.0f)));

    public Vector2f GetRandomSize() => _sizeMin.Lerp(_sizeMax, MathDefs.Random(1.0f));
    public float GetRandomVelocity() => MathDefs.Lerp(_velocityMin, _velocityMax, MathDefs.Random(1.0f));
    public float GetRandomTimeToLive() => MathDefs.Lerp(_timeToLiveMin, _timeToLiveMax, MathDefs.Random(1.0f));
    public float GetRandomRotationSpeed() => MathDefs.Lerp(_rotationSpeedMin, _rotationSpeedMax, MathDefs.Random(1.0f));
    public float GetRandomRotation() => MathDefs.Lerp(_rotationMin, _rotationMax, MathDefs.Random(1.0f));

    private void GetFloatMinMax(XMLElement element, ref float minValue, ref float maxValue)
    {
        if (element.IsNull)
            return;

        if (element.HasAttribute("value"))
            minValue = maxValue = element.GetFloat("value");

        if (element.HasAttribute("min") && element.HasAttribute("max"))
        {
            minValue = element.GetFloat("min");
            maxValue = element.GetFloat("max");
        }
    }

    private void GetVector2MinMax(XMLElement element, ref Vector2f minValue, ref Vector2f maxValue)
    {
        if (element.IsNull)
            return;

        if (element.HasAttribute("value"))
            minValue = maxValue = element.GetVector2("value");

        if (element.HasAttribute("min") && element.HasAttribute("max"))
        {
            minValue = element.GetVector2("min");
            maxValue = element.GetVector2("max");
        }
    }

    private void GetVector3MinMax(XMLElement element, ref Vector3f minValue, ref Vector3f maxValue)
    {
        if (element.IsNull)
            return;

        if (element.HasAttribute("value"))
            minValue = maxValue = element.GetVector3("value");

        if (element.HasAttribute("min") && element.HasAttribute("max"))
        {
            minValue = element.GetVector3("min");
            maxValue = element.GetVector3("max");
        }
    }

    private static int GetStringListIndex(string value, string[] strings, int defaultIndex)
    {
        for (var i = 0; i < strings.Length; ++i)
        {
            if (string.Equals(strings[i], value, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return defaultIndex;
    }

    /// <summary>Material.</summary>
    private Material? _material;
    /// <summary>Number of particles.</summary>
    private uint _numParticles = DefaultNumParticles;
    /// <summary>Update when invisible flag.</summary>
    private bool _updateInvisible;
    /// <summary>Billboards relative flag.</summary>
    private bool _relative = true;
    /// <summary>Scale affects billboard scale flag.</summary>
    private bool _scaled = true;
    /// <summary>Billboards sorted flag.</summary>
    private bool _sorted;
    /// <summary>Billboards fixed screen size flag.</summary>
    private bool _fixedScreenSize;
    /// <summary>Animation LOD bias.</summary>
    private float _animationLodBias;
    /// <summary>Emitter shape.</summary>
    private EmitterType _emitterType = EmitterType.Sphere;
    /// <summary>Emitter size.</summary>
    private Vector3f _emitterSize;
    /// <summary>Particle direction minimum.</summary>
    private Vector3f _directionMin = DefaultDirectionMin;
    /// <summary>Particle direction maximum.</summary>
    private Vector3f _directionMax = DefaultDirectionMax;
    /// <summary>Particle constant force.</summary>
    private Vector3f _constantForce;
    /// <summary>Particle velocity damping force.</summary>
    private float _dampingForce;
    /// <summary>Active period.</summary>
    private float _activeTime;
    /// <summary>Inactive period.</summary>
    private float _inactiveTime;
    /// <summary>Particles per second minimum.</summary>
    private float _emissionRateMin = DefaultEmissionRate;
    /// <summary>Particles per second maximum.</summary>
    private float _emissionRateMax = DefaultEmissionRate;
    /// <summary>Particle size minimum.</summary>
    private Vector2f _sizeMin = DefaultParticleSize;
    /// <summary>Particle size maximum.</summary>
    private Vector2f _sizeMax = DefaultParticleSize;
    /// <summary>Particle time to live minimum.</summary>
    private float _timeToLiveMin = DefaultTimeToLive;
    /// <summary>Particle time to live maximum.</summary>
    private float _timeToLiveMax = DefaultTimeToLive;
    /// <summary>Particle velocity minimum.</summary>
    private float _velocityMin = DefaultVelocity;
    /// <summary>Particle velocity maximum.</summary>
    private float _velocityMax = DefaultVelocity;
    /// <summary>Particle rotation angle minimum.</summary>
    private float _rotationMin;
    /// <summary>Particle rotation angle maximum.</summary>
    private float _rotationMax;
    /// <summary>Particle rotation speed minimum.</summary>
    private float _rotationSpeedMin;
    /// <summary>Particle rotation speed maximum.</summary>
    private float _rotationSpeedMax;
    /// <summary>Particle size additive parameter.</summary>
    private float _sizeAdd;
    /// <summary>Particle size multiplicative parameter.</summary>
    private float _sizeMul = 1.0f;
    /// <summary>Particle color animation frames.</summary>
    private readonly List<ColorFrame> _colorFrames = [];
    /// <summary>Texture animation frames.</summary>
    private readonly List<TextureFrame> _textureFrames = [];
    /// <summary>Material name acquired during BeginLoad().</summary>
    private string _loadMaterialName = string.Empty;
    /// <summary>Particle rotation mode in relation to the camera.</summary>
    private FaceCameraMode _faceCameraMode = FaceCameraMode.RotateXyz;
}
