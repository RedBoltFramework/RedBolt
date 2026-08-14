// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.Scene2D;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Graphics2D;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using ResourceType = RedBolt.Resource.Resource;

/// <summary>One particle in a 2D particle system.</summary>
public struct Particle2D
{
    public float TimeToLive;
    public Vector3f Position;
    public float Size;
    public float SizeDelta;
    public float Rotation;
    public float RotationDelta;
    public Color Color;
    public Color ColorDelta;
    public Vector2f StartPosition;
    public Vector2f Velocity;
    public float RadialAcceleration;
    public float TangentialAcceleration;
    public float EmitRadius;
    public float EmitRadiusDelta;
    public float EmitRotation;
    public float EmitRotationDelta;
}

/// <summary>2D particle emitter component.</summary>
[Reflected(ObjectCategory.Category_Urho2D, InheritAttributesOrder = 1)]
public partial class ParticleEmitter2D : Drawable2D
{
    private ParticleEffect2D? _effect;
    private Sprite2D? _sprite;
    private BlendMode _blendMode = BlendMode.AddAlpha;
    private readonly List<Particle2D> _particles = [];
    private uint _maxParticles = 1;
    private float _emissionTime;
    private float _emitParticleTime;
    private bool _emitting = true;

    [Serialized("Particle Effect", Setter = nameof(SetParticleEffectAttr))]
    private ResourceRef _effectAttr = new(typeof(ParticleEffect2D).GetTypeId());
    [Serialized("Sprite ", Setter = nameof(SetSpriteAttr))]
    private ResourceRef _spriteAttr = new(typeof(Sprite2D).GetTypeId());

    public ParticleEmitter2D() => SourceBatches.Add(new SourceBatch2D { Owner = this });

    public ParticleEffect2D? Effect
    {
        get => _effect;
        set
        {
            if (ReferenceEquals(_effect, value))
                return;
            _effect = value;
            if (value is null)
                return;
            Sprite = value.Sprite;
            BlendMode = value.BlendMode;
            SetMaxParticles((uint)global::System.Math.Max(value.MaxParticles, 1));
            _emitParticleTime = 0.0f;
            _emissionTime = value.Duration;
        }
    }

    public Sprite2D? Sprite
    {
        get => _sprite;
        set { if (!ReferenceEquals(_sprite, value)) { _sprite = value; UpdateMaterial(); MarkSourceBatchesDirty(); } }
    }

    [Serialized("Blend Mode")]
    public BlendMode BlendMode
    {
        get => _blendMode;
        set { if (_blendMode != value) { _blendMode = value; UpdateMaterial(); MarkSourceBatchesDirty(); } }
    }

    [Serialized("Is Emitting")]
    public bool IsEmitting
    {
        get => _emitting;
        set { if (_emitting != value) { _emitting = value; _emitParticleTime = 0.0f; } }
    }

    public uint MaxParticles => _maxParticles;

    public void SetMaxParticles(uint maxParticles)
    {
        _maxParticles = global::System.Math.Max(maxParticles, 1u);
        if (_particles.Count > _maxParticles)
            _particles.RemoveRange((int)_maxParticles, _particles.Count - (int)_maxParticles);
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        if (Scene is null)
            return;
        if (IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(Scene, HandleScenePostUpdate);
        else
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
    }

    protected override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (previousScene is not null)
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
        base.OnSceneSet(previousScene, scene);
        if (scene is not null && IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
        UpdateMaterial();
    }

    protected override void OnDrawOrderChanged()
    {
        SourceBatches[0].DrawOrder = DrawOrder;
        MarkSourceBatchesDirty();
    }

    protected override void UpdateSourceBatches()
    {
        var vertices = SourceBatches[0].Vertices;
        vertices.Clear();
        if (_sprite is null || !_sprite.GetTextureRectangle(out var textureRect))
            return;

        var inverse = Node.GetWorldTransform().Inverse;
        foreach (var particle in _particles)
        {
            var angle = -particle.Rotation * MathF.PI / 180.0f;
            var c = MathF.Cos(angle);
            var s = MathF.Sin(angle);
            var add = (c + s) * particle.Size * 0.5f;
            var sub = (c - s) * particle.Size * 0.5f;
            var p0 = inverse * new Vector3f(particle.Position.X - sub, particle.Position.Y - add, particle.Position.Z);
            var p1 = inverse * new Vector3f(particle.Position.X - add, particle.Position.Y + sub, particle.Position.Z);
            var p2 = inverse * new Vector3f(particle.Position.X + sub, particle.Position.Y + add, particle.Position.Z);
            var p3 = inverse * new Vector3f(particle.Position.X + add, particle.Position.Y - sub, particle.Position.Z);
            var color = particle.Color.ToUInt();
            var v0 = new Vertex2D { Position = p0, Color = color, Uv = textureRect.Min };
            var v1 = new Vertex2D { Position = p1, Color = color, Uv = new Vector2f(textureRect.Min.X, textureRect.Max.Y) };
            var v2 = new Vertex2D { Position = p2, Color = color, Uv = textureRect.Max };
            var v3 = new Vertex2D { Position = p3, Color = color, Uv = new Vector2f(textureRect.Max.X, textureRect.Min.Y) };
            vertices.AddRange([v0, v1, v2, v0, v2, v3]);
        }
    }

    public override void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
        var offset = delta.ToVector3();
        for (var index = 0; index < _particles.Count; ++index)
        {
            var particle = _particles[index];
            particle.Position -= offset;
            particle.StartPosition.X -= offset.X;
            particle.StartPosition.Y -= offset.Y;
            _particles[index] = particle;
        }
        MarkSourceBatchesDirty();
    }

    private void UpdateMaterial() => SourceBatches[0].Material =
        _sprite?.Texture is { } texture && Renderer is { } renderer ? renderer.GetMaterial(texture, _blendMode) : null;

    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData)
    {
        var hadParticles = _particles.Count != 0;
        var wasEmitting = _emissionTime > 0.0f;
        UpdateParticles(eventData.TimeStep);
        if (wasEmitting && _emissionTime == 0.0f && Node is { } durationNode && _effect is { } durationEffect)
        {
            var durationEvent = new Urho2DEvents.ParticlesDurationEvent { Node = durationNode, Effect = durationEffect };
            durationNode.SendEvent(ref durationEvent);
        }
        if (hadParticles && _particles.Count == 0 && Node is { } endNode && _effect is { } endEffect)
        {
            var endEvent = new Urho2DEvents.ParticlesEndEvent { Node = endNode, Effect = endEffect };
            endNode.SendEvent(ref endEvent);
        }
    }

    private void UpdateParticles(float timeStep)
    {
        if (_effect is null || Node is null)
            return;
        var worldPosition = Node.GetWorldPosition();
        var worldScale = Node.GetWorldScale().X * Sprite2D.PixelSize;

        for (var index = _particles.Count - 1; index >= 0; --index)
        {
            var particle = _particles[index];
            if (particle.TimeToLive <= 0.0f)
            {
                _particles.RemoveAt(index);
                continue;
            }
            UpdateParticle(ref particle, timeStep, worldScale);
            _particles[index] = particle;
        }

        if (_emitting && _emissionTime > 0.0f)
        {
            var worldAngle = Node.GetWorldRotation().RollAngle;
            var timeBetweenParticles = _effect.ParticleLifeSpan / _maxParticles;
            _emitParticleTime += timeStep;
            while (_emitParticleTime > 0.0f)
            {
                if (EmitParticle(worldPosition, worldAngle, worldScale))
                {
                    var last = _particles[^1];
                    UpdateParticle(ref last, _emitParticleTime, worldScale);
                    _particles[^1] = last;
                }
                _emitParticleTime -= timeBetweenParticles;
            }
            if (!float.IsPositiveInfinity(_emissionTime))
                _emissionTime = MathF.Max(0.0f, _emissionTime - timeStep);
        }
        MarkSourceBatchesDirty();
    }

    private bool EmitParticle(Vector3f worldPosition, float worldAngle, float worldScale)
    {
        if (_effect is null || _particles.Count >= _maxParticles || _particles.Count >= _effect.MaxParticles)
            return false;
        var lifespan = _effect.ParticleLifeSpan + _effect.ParticleLifespanVariance * RandomSigned();
        if (lifespan <= 0.0f)
            return false;
        var inverseLifespan = 1.0f / lifespan;
        var particle = new Particle2D { TimeToLive = lifespan };
        particle.Position = new Vector3f(
            worldPosition.X + worldScale * _effect.SourcePositionVariance.X * RandomSigned(),
            worldPosition.Y + worldScale * _effect.SourcePositionVariance.Y * RandomSigned(), worldPosition.Z);
        particle.StartPosition = new Vector2f(worldPosition.X, worldPosition.Y);
        var angle = (worldAngle + _effect.Angle + _effect.AngleVariance * RandomSigned()) * MathF.PI / 180.0f;
        var speed = worldScale * (_effect.Speed + _effect.SpeedVariance * RandomSigned());
        particle.Velocity = new Vector2f(speed * MathF.Cos(angle), speed * MathF.Sin(angle));
        var maxRadius = MathF.Max(0.0f, worldScale * (_effect.MaxRadius + _effect.MaxRadiusVariance * RandomSigned()));
        var minRadius = MathF.Max(0.0f, worldScale * (_effect.MinRadius + _effect.MinRadiusVariance * RandomSigned()));
        particle.EmitRadius = maxRadius;
        particle.EmitRadiusDelta = (minRadius - maxRadius) * inverseLifespan;
        particle.EmitRotation = (worldAngle + _effect.Angle + _effect.AngleVariance * RandomSigned()) * MathF.PI / 180.0f;
        particle.EmitRotationDelta = (_effect.RotatePerSecond + _effect.RotatePerSecondVariance * RandomSigned()) * MathF.PI / 180.0f;
        particle.RadialAcceleration = worldScale * (_effect.RadialAcceleration + _effect.RadialAccelVariance * RandomSigned());
        particle.TangentialAcceleration = worldScale * (_effect.TangentialAcceleration + _effect.TangentialAccelVariance * RandomSigned());
        var startSize = worldScale * MathF.Max(0.1f, _effect.StartParticleSize + _effect.StartParticleSizeVariance * RandomSigned());
        var finishSize = worldScale * MathF.Max(0.1f, _effect.FinishParticleSize + _effect.FinishParticleSizeVariance * RandomSigned());
        particle.Size = startSize;
        particle.SizeDelta = (finishSize - startSize) * inverseLifespan;
        particle.Color = _effect.StartColor + _effect.StartColorVariance * RandomSigned();
        var endColor = _effect.FinishColor + _effect.FinishColorVariance * RandomSigned();
        particle.ColorDelta = (endColor - particle.Color) * inverseLifespan;
        particle.Rotation = worldAngle + _effect.RotationStart + _effect.RotationStartVariance * RandomSigned();
        var endRotation = worldAngle + _effect.RotationEnd + _effect.RotationEndVariance * RandomSigned();
        particle.RotationDelta = (endRotation - particle.Rotation) * inverseLifespan;
        _particles.Add(particle);
        return true;
    }

    private void UpdateParticle(ref Particle2D particle, float timeStep, float worldScale)
    {
        if (_effect is null)
            return;
        timeStep = MathF.Min(timeStep, particle.TimeToLive);
        particle.TimeToLive -= timeStep;
        if (_effect.EmitterType == EmitterType2D.Radial)
        {
            particle.EmitRotation += particle.EmitRotationDelta * timeStep;
            particle.EmitRadius += particle.EmitRadiusDelta * timeStep;
            particle.Position.X = particle.StartPosition.X - MathF.Cos(particle.EmitRotation) * particle.EmitRadius;
            particle.Position.Y = particle.StartPosition.Y + MathF.Sin(particle.EmitRotation) * particle.EmitRadius;
        }
        else
        {
            var distance = new Vector2f(particle.Position.X - particle.StartPosition.X,
                particle.Position.Y - particle.StartPosition.Y);
            var length = MathF.Max(distance.Length, 0.0001f);
            var radial = distance / length;
            var tangential = new Vector2f(-radial.Y, radial.X) * particle.TangentialAcceleration;
            radial *= particle.RadialAcceleration;
            particle.Velocity.X += (_effect.Gravity.X * worldScale + radial.X - tangential.X) * timeStep;
            particle.Velocity.Y -= (_effect.Gravity.Y * worldScale - radial.Y + tangential.Y) * timeStep;
            particle.Position.X += particle.Velocity.X * timeStep;
            particle.Position.Y += particle.Velocity.Y * timeStep;
        }
        particle.Size += particle.SizeDelta * timeStep;
        particle.Rotation += particle.RotationDelta * timeStep;
        particle.Color += particle.ColorDelta * timeStep;
    }

    private void SetParticleEffectAttr(ResourceRef value)
    {
        _effectAttr = value;
        Effect = GetSubsystem<ResourceCache>()?.GetResource<ParticleEffect2D>(value.Name);
    }
    public ResourceRef GetParticleEffectAttr() => ResourceType.GetResourceRef(_effect, typeof(ParticleEffect2D).GetTypeId());
    private void SetSpriteAttr(ResourceRef value) { _spriteAttr = value; Sprite = Sprite2D.LoadFromResourceRef(this, value); }
    public ResourceRef GetSpriteAttr() => Sprite2D.SaveToResourceRef(_sprite);
    private static float RandomSigned() => Random.Shared.NextSingle() * 2.0f - 1.0f;
}
