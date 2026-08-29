// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.AnimatingScene;

using System;

using RedBolt;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>
/// Moves a spacecraft repeatedly between two points.
/// </summary>
[Reflected]
public partial class SpacecraftFlight : LogicComponent
{
    /// <summary>
    /// Construct.
    /// </summary>
    public SpacecraftFlight()
    {
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    /// <summary>
    /// Configure the flight path.
    /// </summary>
    public void Configure(in Vector3f start, in Vector3f end, float duration, float phase)
    {
        _start = start;
        _end = end;
        _duration = duration;
        _progress = phase;
        Node!.SetPosition(_start * (1.0f - _progress) + _end * _progress);
        Node.LookAt(_end);
    }

    /// <summary>
    /// Advance along the flight path.
    /// </summary>
    public override void Update(float timeStep)
    {
        _progress += timeStep / _duration;
        _progress -= MathF.Floor(_progress);
        Node!.SetPosition(_start * (1.0f - _progress) + _end * _progress);
    }

    private Vector3f _start;
    private Vector3f _end;
    private float _duration = 1.0f;
    private float _progress;
}
