// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Audio;

using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Sound source component with 3D position attenuation.</summary>
[Reflected(ObjectCategory.Category_Audio)]
[RemoveSerialized("Attenuation")]
[RemoveSerialized("Panning")]
public sealed partial class SoundSource3D : SoundSource
{
    [Serialized("Near Distance")]
    private float _nearDistance;
    [Serialized("Far Distance")]
    private float _farDistance = 100.0f;
    [Serialized("Inner Angle")]
    private float _innerAngle = 360.0f;
    [Serialized("Outer Angle")]
    private float _outerAngle = 360.0f;
    [Serialized("Rolloff Factor")]
    private float _rolloffFactor = 2.0f;

    public SoundSource3D()
    {
        Attenuation = 0.0f;
    }


    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || TryGetNode() is null || !IsEnabledEffective)
            return;

        var worldPosition = Node.GetWorldPosition();
        var worldRotation = Node.GetWorldRotation();
        var innerColor = new Color(1.0f, 0.5f, 1.0f);
        var outerColor = new Color(1.0f, 0.0f, 1.0f);

        if (_innerAngle < 360.0f && _outerAngle > 0.0f)
        {
            var rotation = worldRotation * new Quaternion(Vector3f.Up, Vector3f.Forward);
            debug.AddSphereSector(new Sphere(worldPosition, _nearDistance), rotation, _innerAngle, false, innerColor, depthTest);
            debug.AddSphereSector(new Sphere(worldPosition, _nearDistance), rotation, _outerAngle, false, outerColor, depthTest);
            debug.AddSphereSector(new Sphere(worldPosition, _farDistance), rotation, _innerAngle, true, innerColor, depthTest);
            debug.AddSphereSector(new Sphere(worldPosition, _farDistance), rotation, _outerAngle, true, outerColor, depthTest);
        }
        else
        {
            debug.AddSphere(new Sphere(worldPosition, _nearDistance), innerColor, depthTest);
            debug.AddSphere(new Sphere(worldPosition, _farDistance), outerColor, depthTest);
        }
    }

    public override void Update(float timeStep)
    {
        CalculateAttenuation();
        base.Update(timeStep);
    }

    public void SetDistanceAttenuation(float nearDistance, float farDistance, float rolloffFactor)
    {
        _nearDistance = SystemMath.Max(nearDistance, 0.0f);
        _farDistance = SystemMath.Max(farDistance, 0.0f);
        _rolloffFactor = SystemMath.Max(rolloffFactor, 0.1f);
    }

    public void SetAngleAttenuation(float innerAngle, float outerAngle)
    {
        _innerAngle = MathDefs.Clamp(innerAngle, 0.0f, 360.0f);
        _outerAngle = MathDefs.Clamp(outerAngle, 0.0f, 360.0f);
    }

    public void SetNearDistance(float distance) { _nearDistance = SystemMath.Max(distance, 0.0f); }
    public void SetFarDistance(float distance) { _farDistance = SystemMath.Max(distance, 0.0f); }
    public void SetInnerAngle(float angle) { _innerAngle = MathDefs.Clamp(angle, 0.0f, 360.0f); }
    public void SetOuterAngle(float angle) { _outerAngle = MathDefs.Clamp(angle, 0.0f, 360.0f); }
    public void SetRolloffFactor(float factor) { _rolloffFactor = SystemMath.Max(factor, 0.1f); }

    public float GetNearDistance() => _nearDistance;
    public float GetFarDistance() => _farDistance;
    public float GetInnerAngle() => _innerAngle;
    public float GetOuterAngle() => _outerAngle;
    public float RollAngleoffFactor() => _rolloffFactor;

    public void CalculateAttenuation()
    {
        if (Audio == null)
            return;

        if (TryGetNode() is null)
        {
            Attenuation = 0.0f;
            return;
        }

        var listener = Audio.GetListener();
        if (listener == null || !listener.IsEnabledEffective || (listener.Scene != null && listener.Scene != Scene))
        {
            Attenuation = 0.0f;
            return;
        }

        var listenerNode = listener.Node;

        var interval = _farDistance - _nearDistance;

        var worldPosition = Node.GetWorldPosition();
        var listenerWorldPos = listenerNode.GetWorldPosition();
        var listenerWorldRot = listenerNode.GetWorldRotation();
        var relativePos = listenerWorldRot.Inverse * (worldPosition - listenerWorldPos);
        var distance = relativePos.Length;

        if (interval > 0.0f)
            Attenuation = MathF.Pow(1.0f - MathDefs.Clamp(distance - _nearDistance, 0.0f, interval) / interval, _rolloffFactor);
        else
            Attenuation = distance <= _nearDistance ? 1.0f : 0.0f;

        var normalizedRelativePos = distance > 0.0f ? relativePos / distance : Vector3f.Zero;
        Panning = normalizedRelativePos.X;
        Reach = normalizedRelativePos.Z;

        if (_innerAngle < 360.0f && _outerAngle > 0.0f)
        {
            var nodeWorldRot = Node.GetWorldRotation();
            var listenerRelativePos = nodeWorldRot.Inverse * (listenerWorldPos - worldPosition);
            var listenerDist = listenerRelativePos.Length;
            if (listenerDist > 0.0f)
            {
                var listenerDir = listenerRelativePos / listenerDist;
                var listenerDot = Vector3f.Forward.DotProduct(listenerDir);
                var listenerAngle = MathF.Acos(listenerDot) * MathDefs.RadToDeg * 2.0f;
                var angleInterval = MathF.Max(_outerAngle - _innerAngle, 0.0f);
                float angleAttenuation;

                if (angleInterval > 0.0f)
                    angleAttenuation = listenerAngle > _innerAngle
                        ? MathF.Pow(1.0f - MathDefs.Clamp(listenerAngle - _innerAngle, 0.0f, angleInterval) / angleInterval, _rolloffFactor)
                        : 1.0f;
                else
                    angleAttenuation = listenerAngle <= _innerAngle ? 1.0f : 0.0f;

                Attenuation *= angleAttenuation;
            }
        }
    }


}
