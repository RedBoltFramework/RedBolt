namespace RedBolt;

/// <summary>One particle in the particle system.</summary>
public struct Particle
{
    /// <summary>Velocity.</summary>
    public Vector3f Velocity;
    /// <summary>Original billboard size.</summary>
    public Vector2f Size;
    /// <summary>Time elapsed from creation.</summary>
    public float Timer;
    /// <summary>Lifetime.</summary>
    public float TimeToLive;
    /// <summary>Size scaling value.</summary>
    public float Scale;
    /// <summary>Rotation speed.</summary>
    public float RotationSpeed;
    /// <summary>Current color animation index.</summary>
    public uint ColorIndex;
    /// <summary>Current texture animation index.</summary>
    public uint TexIndex;
}

/// <summary>Particle emitter component.</summary>
public class ParticleEmitter : BillboardSet
{
    private const uint MaxParticlesInFrame = 100;
    private static readonly string[] AutoRemoveModeNames = ["Disabled", "Component", "Node"];
    private static readonly string[] GlobalIlluminationTypeNames = ["None", "Use LightMap", "Blend Light Probes"];
    private static readonly string[] ReflectionModeNames = ["Zone", "Nearest Probe", "Blend Probes", "Blend Probes and Zone"];

    /// <summary>Construct.</summary>
    public ParticleEmitter()
    {
        SetNumParticles(ParticleEffect.DefaultNumParticles);

        RegisterAttribute("Is Enabled", typeof(bool), self => ((ParticleEmitter)self).Enabled,
            (self, value) => ((ParticleEmitter)self).SetEnabled(value is bool enabled && enabled), true);
        RegisterAttribute("Effect", typeof(ResourceRef), self => ((ParticleEmitter)self).GetEffectAttr(),
            (self, value) => ((ParticleEmitter)self).SetEffectAttr((ResourceRef)value!),
            new ResourceRef(new StringHash(nameof(ParticleEffect))));
        RegisterAttribute("Can Be Occluded", typeof(bool), self => ((ParticleEmitter)self).IsOccludee(),
            (self, value) => ((ParticleEmitter)self).SetOccludee(value is bool enabled && enabled), true);
        RegisterAttribute("Cast Shadows", typeof(bool), self => ((ParticleEmitter)self).GetCastShadows(),
            (self, value) => ((ParticleEmitter)self).SetCastShadows(value is bool enabled && enabled), false);
        RegisterAttribute("Draw Distance", typeof(float), self => ((ParticleEmitter)self).GetDrawDistance(),
            (self, value) => ((ParticleEmitter)self).SetDrawDistance((float)value!), 0.0f);
        RegisterAttribute("Shadow Distance", typeof(float), self => ((ParticleEmitter)self).GetShadowDistance(),
            (self, value) => ((ParticleEmitter)self).SetShadowDistance((float)value!), 0.0f);
        RegisterAttribute("Animation LOD Bias", typeof(float), self => ((ParticleEmitter)self).GetAnimationLodBias(),
            (self, value) => ((ParticleEmitter)self).SetAnimationLodBias((float)value!), 1.0f);
        RegisterAttribute("Is Emitting", typeof(bool), self => ((ParticleEmitter)self)._emitting,
            (self, value) => ((ParticleEmitter)self)._emitting = value is bool enabled && enabled, true);
        RegisterAttribute("Period Timer", typeof(float), self => ((ParticleEmitter)self)._periodTimer,
            (self, value) => ((ParticleEmitter)self)._periodTimer = (float)value!, 0.0f,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Emission Timer", typeof(float), self => ((ParticleEmitter)self)._emissionTimer,
            (self, value) => ((ParticleEmitter)self)._emissionTimer = (float)value!, 0.0f,
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Autoremove Mode", typeof(AutoRemoveMode), self => ((ParticleEmitter)self)._autoRemove,
            (self, value) => ((ParticleEmitter)self)._autoRemove = (AutoRemoveMode)value!, AutoRemoveMode.Disabled,
            enumNames: AutoRemoveModeNames);
        RegisterAttribute("View Mask", typeof(uint), self => ((ParticleEmitter)self).GetViewMask(),
            (self, value) => ((ParticleEmitter)self).SetViewMask((uint)value!), DefaultViewMask);
        RegisterAttribute("Light Mask", typeof(uint), self => ((ParticleEmitter)self).GetLightMask(),
            (self, value) => ((ParticleEmitter)self).SetLightMask((uint)value!), DefaultLightMask);
        RegisterAttribute("Shadow Mask", typeof(uint), self => ((ParticleEmitter)self).GetShadowMask(),
            (self, value) => ((ParticleEmitter)self).SetShadowMask((uint)value!), DefaultShadowMask);
        RegisterAttribute("Zone Mask", typeof(uint), self => ((ParticleEmitter)self).GetZoneMask(),
            (self, value) => ((ParticleEmitter)self).SetZoneMask((uint)value!), DefaultZoneMask);
        RegisterAttribute("Global Illumination", typeof(GlobalIlluminationType),
            self => ((ParticleEmitter)self).GetGlobalIlluminationType(),
            (self, value) => ((ParticleEmitter)self).SetGlobalIlluminationType((GlobalIlluminationType)value!),
            GlobalIlluminationType.None, enumNames: GlobalIlluminationTypeNames);
        RegisterAttribute("Reflection Mode", typeof(ReflectionMode), self => ((ParticleEmitter)self).GetReflectionMode(),
            (self, value) => ((ParticleEmitter)self).SetReflectionMode((ReflectionMode)value!),
            ReflectionMode.BlendProbesAndZone, enumNames: ReflectionModeNames);
        RegisterAttribute("Particles", typeof(VariantVector), self => ((ParticleEmitter)self).GetParticlesAttr(),
            (self, value) => ((ParticleEmitter)self).SetParticlesAttr((VariantVector)value!), new VariantVector(),
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Billboards", typeof(VariantVector), self => ((ParticleEmitter)self).GetParticleBillboardsAttr(),
            (self, value) => ((ParticleEmitter)self).SetBillboardsAttr((VariantVector)value!), new VariantVector(),
            AttributeMode.Default | AttributeMode.NoEdit);
        RegisterAttribute("Serialize Particles", typeof(bool), self => ((ParticleEmitter)self)._serializeParticles,
            (self, value) => ((ParticleEmitter)self)._serializeParticles = value is bool enabled && enabled, true);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _particles.Clear();
        if (_effect is not null)
            _effect.ReloadFinished -= HandleEffectReloadFinished;
        _effect = null;
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static new void RegisterObject(Context context)
    {
        context.AddFactoryReflection<ParticleEmitter>(ObjectCategory.Category_Geometry);
    }

    /// <summary>Handle enabled/disabled state change.</summary>
    public override void OnSetEnabled()
    {
        base.OnSetEnabled();

        var scene = Scene;
        if (scene is not null)
        {
            if (IsEnabledEffective)
                SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
            else
                UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>(scene);
        }
    }

    /// <summary>Update before octree reinsertion. Is called from a worker thread.</summary>
    public override void Update(CommonFrameInfo frame)
    {
        if (_effect is null)
            return;

        // Cancel update if has only moved but does not actually need to animate the particles
        if (!_needUpdate)
            return;

        // If there is an amount mismatch between particles and billboards, correct it
        if (_particles.Count != _billboards.Count)
            SetNumBillboards((uint)_particles.Count);

        var needCommit = false;

        // Check active/inactive period switching
        _periodTimer += _lastTimeStep;
        if (_emitting)
        {
            var activeTime = _effect.GetActiveTime();
            if (activeTime != 0.0f && _periodTimer >= activeTime)
            {
                _emitting = false;
                _periodTimer -= activeTime;
            }
        }
        else
        {
            var inactiveTime = _effect.GetInactiveTime();
            if (inactiveTime != 0.0f && _periodTimer >= inactiveTime)
            {
                _emitting = true;
                _sendFinishedEvent = true;
                _periodTimer -= inactiveTime;
            }
            // If emitter has an indefinite stop interval, keep period timer reset to allow restarting emission in the editor
            if (inactiveTime == 0.0f)
                _periodTimer = 0.0f;
        }

        // Check for emitting new particles
        if (_emitting)
        {
            _emissionTimer += _lastTimeStep;

            var intervalMin = 1.0f / _effect.GetMaxEmissionRate();
            var intervalMax = 1.0f / _effect.GetMinEmissionRate();

            // If emission timer has a longer delay than max. interval, clamp it
            if (_emissionTimer < -intervalMax)
                _emissionTimer = -intervalMax;

            var counter = MaxParticlesInFrame;

            while (_emissionTimer > 0.0f && counter != 0)
            {
                _emissionTimer -= MathDefs.Lerp(intervalMin, intervalMax, MathDefs.Random(1.0f));
                if (EmitNewParticle())
                {
                    --counter;
                    needCommit = true;
                }
                else
                    break;
            }
        }

        // Update existing particles
        var relativeConstantForce = Node.GetWorldRotation().Inverse * _effect.GetConstantForce();
        // If billboards are not relative, apply scaling to the position update
        var scaleVector = Vector3f.One;
        if (_scaled && !_relative)
            scaleVector = Node.GetWorldScale();

        for (var i = 0; i < _particles.Count; ++i)
        {
            var particle = _particles[i];
            var billboard = _billboards[i];

            if (billboard.Enabled)
            {
                needCommit = true;

                // Time to live
                if (particle.Timer >= particle.TimeToLive)
                {
                    billboard.Enabled = false;
                    _billboards[i] = billboard;
                    _particles[i] = particle;
                    continue;
                }
                particle.Timer += _lastTimeStep;

                // Velocity & position
                var constantForce = _effect.GetConstantForce();
                if (constantForce != Vector3f.Zero)
                {
                    if (_relative)
                        particle.Velocity += _lastTimeStep * relativeConstantForce;
                    else
                        particle.Velocity += _lastTimeStep * constantForce;
                }

                var dampingForce = _effect.GetDampingForce();
                if (dampingForce != 0.0f)
                {
                    var force = -dampingForce * particle.Velocity;
                    particle.Velocity += _lastTimeStep * force;
                }
                billboard.Position += _lastTimeStep * particle.Velocity * scaleVector;
                billboard.Direction = particle.Velocity.Normalized;

                // Rotation
                billboard.Rotation += _lastTimeStep * particle.RotationSpeed;

                // Scaling
                var sizeAdd = _effect.GetSizeAdd();
                var sizeMul = _effect.GetSizeMul();
                if (sizeAdd != 0.0f || sizeMul != 1.0f)
                {
                    particle.Scale += _lastTimeStep * sizeAdd;
                    if (particle.Scale < 0.0f)
                        particle.Scale = 0.0f;
                    if (sizeMul != 1.0f)
                        particle.Scale *= (_lastTimeStep * (sizeMul - 1.0f)) + 1.0f;
                    billboard.Size = particle.Size * particle.Scale;
                }

                // Color interpolation
                var index = particle.ColorIndex;
                var colorFrames = _effect.GetColorFrames();
                if (index < (uint)colorFrames.Count)
                {
                    if (index < (uint)colorFrames.Count - 1)
                    {
                        if (particle.Timer >= colorFrames[(int)index + 1].Time)
                            ++index;
                    }
                    if (index < (uint)colorFrames.Count - 1)
                        billboard.Color = colorFrames[(int)index].Interpolate(colorFrames[(int)index + 1], particle.Timer);
                    else
                        billboard.Color = colorFrames[(int)index].Color;
                }
                particle.ColorIndex = index;

                // Texture animation
                var texIndex = particle.TexIndex;
                var textureFrames = _effect.GetTextureFrames();
                if (textureFrames.Count != 0 && texIndex < (uint)textureFrames.Count - 1)
                {
                    if (particle.Timer >= textureFrames[(int)texIndex + 1].Time)
                    {
                        billboard.Uv = textureFrames[(int)texIndex + 1].Uv;
                        ++texIndex;
                    }
                }
                particle.TexIndex = texIndex;

                _particles[i] = particle;
                _billboards[i] = billboard;
            }
        }

        if (needCommit)
            Commit();

        _needUpdate = false;
    }

    /// <summary>Set particle effect.</summary>
    public void SetEffect(ParticleEffect? effect)
    {
        if (ReferenceEquals(effect, _effect))
            return;

        Reset();

        // Unsubscribe from the reload event of previous effect (if any), then subscribe to the new
        if (_effect is not null)
            _effect.ReloadFinished -= HandleEffectReloadFinished;

        _effect = effect;

        if (_effect is not null)
            _effect.ReloadFinished += HandleEffectReloadFinished;

        ApplyEffect();
    }

    /// <summary>Set maximum number of particles.</summary>
    public void SetNumParticles(uint num)
    {
        // Prevent negative value being assigned from the editor
        if (num > MathDefs.MaxInt)
            num = 0;

        while (_particles.Count < num)
            _particles.Add(default);
        if (_particles.Count > num)
            _particles.RemoveRange((int)num, _particles.Count - (int)num);
        SetNumBillboards(num);
    }

    /// <summary>Set whether should be emitting. If the state was changed, also resets the emission period timer.</summary>
    public void SetEmitting(bool enable)
    {
        if (enable != _emitting)
        {
            _emitting = enable;

            // If stopping emission now, and there are active particles, send finish event once they are gone
            _sendFinishedEvent = enable || CheckActiveParticles();
            _periodTimer = 0.0f;
            // Note: network update does not need to be marked as this is a file only attribute
        }
    }

    /// <summary>Set whether particles should be serialized. Default true, set false to reduce scene file size.</summary>
    public void SetSerializeParticles(bool enable)
    {
        _serializeParticles = enable;
        // Note: network update does not need to be marked as this is a file only attribute
    }

    /// <summary>Set to remove either the emitter component or its owner node from the scene automatically on particle effect completion. Disabled by default.</summary>
    public void SetAutoRemoveMode(AutoRemoveMode mode)
    {
        _autoRemove = mode;
    }

    /// <summary>Reset the emission period timer.</summary>
    public void ResetEmissionTimer()
    {
        _emissionTimer = 0.0f;
    }

    /// <summary>Remove all current particles.</summary>
    public void RemoveAllParticles()
    {
        for (var i = 0; i < _billboards.Count; ++i)
        {
            var billboard = _billboards[i];
            billboard.Enabled = false;
            _billboards[i] = billboard;
        }

        Commit();
    }

    /// <summary>Reset the particle emitter completely. Removes current particles, sets emitting state on, and resets the emission timer.</summary>
    public void Reset()
    {
        RemoveAllParticles();
        ResetEmissionTimer();
        SetEmitting(true);
    }

    /// <summary>Apply not continuously updated values such as the material, the number of particles and sorting mode from the particle effect. Call this if you change the effect programmatically.</summary>
    public void ApplyEffect()
    {
        if (_effect is null)
            return;

        SetMaterial(_effect.GetMaterial());
        SetNumParticles(_effect.GetNumParticles());
        SetRelative(_effect.IsRelative());
        SetScaled(_effect.IsScaled());
        SetSorted(_effect.IsSorted());
        SetFixedScreenSize(_effect.IsFixedScreenSize());
        SetAnimationLodBias(_effect.GetAnimationLodBias());
        SetFaceCameraMode(_effect.GetFaceCameraMode());
    }

    /// <summary>Return particle effect.</summary>
    public ParticleEffect? GetEffect() => _effect;

    /// <summary>Return maximum number of particles.</summary>
    public uint GetNumParticles() => (uint)_particles.Count;

    /// <summary>Return whether is currently emitting.</summary>
    public bool IsEmitting() => _emitting;

    /// <summary>Return whether particles are to be serialized.</summary>
    public bool GetSerializeParticles() => _serializeParticles;

    /// <summary>Return automatic removal mode on particle effect completion.</summary>
    public AutoRemoveMode GetAutoRemoveMode() => _autoRemove;

    /// <summary>Set particles effect attribute.</summary>
    public void SetEffectAttr(in ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        SetEffect(cache.GetResourceOrNull<ParticleEffect>(value.Name));
    }

    /// <summary>Set particles effect attribute.</summary>
    public ResourceRef GetEffectAttr() =>
        Resource.GetResourceRef(_effect, new StringHash(nameof(ParticleEffect)));

    /// <summary>Set particles attribute.</summary>
    public void SetParticlesAttr(VariantVector value)
    {
        var index = 0;
        SetNumParticles(index < value.Count ? value[index++].GetUInt() : 0);

        for (var i = 0; i < _particles.Count && index < value.Count; ++i)
        {
            var particle = _particles[i];
            particle.Velocity = value[index++].GetVector3();
            particle.Size = value[index++].GetVector2();
            particle.Timer = value[index++].GetFloat();
            particle.TimeToLive = value[index++].GetFloat();
            particle.Scale = value[index++].GetFloat();
            particle.RotationSpeed = value[index++].GetFloat();
            particle.ColorIndex = (uint)value[index++].GetInt();
            particle.TexIndex = (uint)value[index++].GetInt();
            _particles[i] = particle;
        }
    }

    /// <summary>Return particles attribute. Returns particle amount only if particles are not to be serialized.</summary>
    public VariantVector GetParticlesAttr()
    {
        var ret = new VariantVector();
        if (!_serializeParticles)
        {
            ret.Add(new Variant((int)_particles.Count));
            return ret;
        }

        ret.Capacity = _particles.Count * 8 + 1;
        ret.Add(new Variant((int)_particles.Count));
        foreach (var particle in _particles)
        {
            ret.Add(new Variant(particle.Velocity));
            ret.Add(new Variant(particle.Size));
            ret.Add(new Variant(particle.Timer));
            ret.Add(new Variant(particle.TimeToLive));
            ret.Add(new Variant(particle.Scale));
            ret.Add(new Variant(particle.RotationSpeed));
            ret.Add(new Variant((int)particle.ColorIndex));
            ret.Add(new Variant((int)particle.TexIndex));
        }
        return ret;
    }

    /// <summary>Return billboards attribute. Returns billboard amount only if particles are not to be serialized.</summary>
    public VariantVector GetParticleBillboardsAttr()
    {
        var ret = new VariantVector();
        if (!_serializeParticles)
        {
            ret.Add(new Variant((int)_billboards.Count));
            return ret;
        }

        ret.Capacity = _billboards.Count * 7 + 1;
        ret.Add(new Variant((int)_billboards.Count));

        foreach (var billboard in _billboards)
        {
            ret.Add(new Variant(billboard.Position));
            ret.Add(new Variant(billboard.Size));
            ret.Add(new Variant(new Vector4f(billboard.Uv.Min.X, billboard.Uv.Min.Y, billboard.Uv.Max.X, billboard.Uv.Max.Y)));
            ret.Add(new Variant(billboard.Color));
            ret.Add(new Variant(billboard.Rotation));
            ret.Add(new Variant(billboard.Direction));
            ret.Add(new Variant(billboard.Enabled));
        }

        return ret;
    }

    /// <summary>Handle scene being assigned.</summary>
    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (scene is not null && IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
        else if (scene is null)
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
    }

    /// <summary>Create a new particle. Return true if there was room.</summary>
    protected bool EmitNewParticle()
    {
        var index = GetFreeParticle();
        if (index == MathDefs.MaxUnsigned)
            return false;
        System.Diagnostics.Debug.Assert(index < (uint)_particles.Count);
        var particle = _particles[(int)index];
        var billboard = _billboards[(int)index];

        var startDir = _effect!.GetRandomDirection();
        startDir = startDir.Normalized;

        Vector3f startPos = default;
        switch (_effect.GetEmitterType())
        {
            case EmitterType.Sphere:
            {
                var dir = new Vector3f(
                    MathDefs.Random(2.0f) - 1.0f,
                    MathDefs.Random(2.0f) - 1.0f,
                    MathDefs.Random(2.0f) - 1.0f
                );
                dir = dir.Normalized;
                startPos = _effect.GetEmitterSize() * dir * 0.5f;
                break;
            }
            case EmitterType.Box:
            {
                var emitterSize = _effect.GetEmitterSize();
                startPos = new Vector3f(
                    MathDefs.Random(emitterSize.X) - emitterSize.X * 0.5f,
                    MathDefs.Random(emitterSize.Y) - emitterSize.Y * 0.5f,
                    MathDefs.Random(emitterSize.Z) - emitterSize.Z * 0.5f
                );
                break;
            }
            case EmitterType.SphereVolume:
            {
                var dir = new Vector3f(
                    MathDefs.Random(2.0f) - 1.0f,
                    MathDefs.Random(2.0f) - 1.0f,
                    MathDefs.Random(2.0f) - 1.0f
                );
                dir = dir.Normalized;
                startPos = _effect.GetEmitterSize() * dir * MathDefs.Pow(MathDefs.Random(), 1.0f / 3.0f) * 0.5f;
                break;
            }
            case EmitterType.Cylinder:
            {
                var angle = MathDefs.Random(360.0f);
                var radius = MathDefs.Sqrt(MathDefs.Random()) * 0.5f;
                startPos = new Vector3f(MathDefs.Cos(angle) * radius, MathDefs.Random() - 0.5f, MathDefs.Sin(angle) * radius) * _effect.GetEmitterSize();
                break;
            }
            case EmitterType.Ring:
            {
                var angle = MathDefs.Random(360.0f);
                startPos = new Vector3f(MathDefs.Cos(angle), MathDefs.Random(2.0f) - 1.0f, MathDefs.Sin(angle)) * _effect.GetEmitterSize() * 0.5f;
                break;
            }
        }

        particle.Size = _effect.GetRandomSize();
        particle.Timer = 0.0f;
        particle.TimeToLive = _effect.GetRandomTimeToLive();
        particle.Scale = 1.0f;
        particle.RotationSpeed = _effect.GetRandomRotationSpeed();
        particle.ColorIndex = 0;
        particle.TexIndex = 0;

        if (_faceCameraMode == FaceCameraMode.Direction)
        {
            startPos += startDir * particle.Size.Y;
        }

        if (!_relative)
        {
            startPos = Node.GetWorldTransform() * startPos;
            startDir = Node.GetWorldRotation() * startDir;
        }

        particle.Velocity = _effect.GetRandomVelocity() * startDir;

        billboard.Position = startPos;
        billboard.Size = particle.Size;
        var textureFrames = _effect.GetTextureFrames();
        billboard.Uv = textureFrames.Count != 0 ? textureFrames[0].Uv : Rect.Positive;
        billboard.Rotation = _effect.GetRandomRotation();
        var colorFrames = _effect.GetColorFrames();
        billboard.Color = colorFrames.Count != 0 ? colorFrames[0].Color : Color.White;
        billboard.Enabled = true;
        billboard.Direction = startDir;

        _particles[(int)index] = particle;
        _billboards[(int)index] = billboard;
        return true;
    }

    /// <summary>Return a free particle index.</summary>
    protected uint GetFreeParticle()
    {
        for (var i = 0; i < _billboards.Count; ++i)
        {
            if (!_billboards[i].Enabled)
                return (uint)i;
        }

        return MathDefs.MaxUnsigned;
    }

    /// <summary>Return whether has active particles.</summary>
    protected bool CheckActiveParticles()
    {
        for (var i = 0; i < _billboards.Count; ++i)
        {
            if (_billboards[i].Enabled)
                return true;
        }

        return false;
    }

    /// <summary>Handle scene post-update event.</summary>
    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData)
    {
        // Store scene's timestep and use it instead of global timestep, as time scale may be other than 1
        _lastTimeStep = eventData.TimeStep;

        // If no invisible update, check that the billboardset is in view (framenumber has changed)
        if ((_effect is not null && _effect.GetUpdateInvisible()) || _viewFrameNumber != _lastUpdateFrameNumber)
        {
            _lastUpdateFrameNumber = _viewFrameNumber;
            _needUpdate = true;
            MarkForUpdate();
        }

        // Send finished event only once all particles are gone
        if (Node is not null && !_emitting && _sendFinishedEvent && !CheckActiveParticles())
        {
            _sendFinishedEvent = false;

            var finished = new DrawableEvents.ParticleEffectFinishedEvent
            {
                Node = Node,
                Effect = _effect
            };
            Node.SendEvent(ref finished);

            DoAutoRemove(_autoRemove);
        }
    }

    /// <summary>Handle live reload of the particle effect.</summary>
    private void HandleEffectReloadFinished(Resource resource)
    {
        // When particle effect file is live-edited, remove existing particles and reapply the effect parameters
        Reset();
        ApplyEffect();
    }

    /// <summary>Particle effect.</summary>
    private ParticleEffect? _effect;
    /// <summary>Particles.</summary>
    private readonly List<Particle> _particles = [];
    /// <summary>Active/inactive period timer.</summary>
    private float _periodTimer;
    /// <summary>New particle emission timer.</summary>
    private float _emissionTimer;
    /// <summary>Last scene timestep.</summary>
    private float _lastTimeStep;
    /// <summary>Rendering framenumber on which was last updated.</summary>
    private uint _lastUpdateFrameNumber = MathDefs.MaxUnsigned;
    /// <summary>Currently emitting flag.</summary>
    private bool _emitting = true;
    /// <summary>Need update flag.</summary>
    private bool _needUpdate;
    /// <summary>Serialize particles flag.</summary>
    private bool _serializeParticles = true;
    /// <summary>Ready to send effect finish event flag.</summary>
    private bool _sendFinishedEvent = true;
    /// <summary>Automatic removal mode.</summary>
    private AutoRemoveMode _autoRemove = AutoRemoveMode.Disabled;
}
