//
// Copyright (c) 2015 Xamarin Inc.
// Copyright (c) 2022 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Actions;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;

/// <summary>Move by 3D or 2D offset action. Target should have attribute "Position" of type Vector3, Vector2, IntVector2 or IntVector3.</summary>
[Reflected]
public partial class MoveBy : AttributeAction
{
    private sealed class MoveByVec3State : AttributeActionState
    {
        private Vector3f _positionDelta;
        private Vector3f _startPosition;
        private Vector3f _previousPosition;

        public MoveByVec3State(MoveBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _previousPosition = _startPosition = Get<Vector3f>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentPos = value.GetVector3();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos = _startPosition + _positionDelta * time;
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    private sealed class MoveByIntVec3State : AttributeActionState
    {
        private Vector3f _positionDelta;
        private Vector3i _startPosition;
        private Vector3i _previousPosition;

        public MoveByIntVec3State(MoveBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _previousPosition = _startPosition = Get<Vector3i>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentPos = value.GetIntVector3();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos = _startPosition + (_positionDelta * time).ToIntVector3();
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    private sealed class MoveByVec2State : AttributeActionState
    {
        private Vector2f _positionDelta;
        private Vector2f _startPosition;
        private Vector2f _previousPosition;

        public MoveByVec2State(MoveBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta.ToVector2();
            _previousPosition = _startPosition = Get<Vector2f>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentPos = value.GetVector2();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos = _startPosition + _positionDelta * time;
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    private sealed class MoveByIntVec2State : AttributeActionState
    {
        private Vector2f _positionDelta;
        private Vector2i _startPosition;
        private Vector2i _previousPosition;

        public MoveByIntVec2State(MoveBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta.ToVector2();
            _previousPosition = _startPosition = Get<Vector2i>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentPos = value.GetIntVector2();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos = _startPosition + (_positionDelta * time).ToIntVector2();
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    /// <summary>Construct.</summary>
    public MoveBy()
        : base(AttributeActionConstants.PositionAttribute)
    {
    }

    /// <summary>Set position delta.</summary>
    public void SetPositionDelta(Vector3f delta)
    {
        _delta = delta;
    }

    /// <summary>Get position delta.</summary>
    public Vector3f PositionDelta => _delta;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new MoveBy();
        result.SetDuration(Duration);
        result.SetPositionDelta(-_delta);
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "delta", ref _delta, Vector3f.Zero, ArchiveSerialization.SerializeValue);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        var attribute = GetAttribute(target);
        if (attribute is not null)
        {
            if (attribute.ValueType == typeof(Vector2f))
                return new MoveByVec2State(this, target, attribute);
            if (attribute.ValueType == typeof(Vector3f))
                return new MoveByVec3State(this, target, attribute);
            if (attribute.ValueType == typeof(Vector2i))
                return new MoveByIntVec2State(this, target, attribute);
            if (attribute.ValueType == typeof(Vector3i))
                return new MoveByIntVec3State(this, target, attribute);
            Log.GetLogger().LogError("Attribute {Attribute} is not of valid type.", AttributeName);
        }
        return base.StartAction(target);
    }

    private Vector3f _delta = Vector3f.Zero;
}

/// <summary>Move by 3D or 2D offset action. Target should have attribute "Position" of type Vector3, Vector2, IntVector2 or
/// IntVector3.</summary>
[Reflected]
public partial class MoveByQuadratic : MoveBy
{
    private sealed class MoveByVec3QuadraticState : AttributeActionState
    {
        private Vector3f _positionDelta;
        private Vector3f _startPosition;
        private Vector3f _previousPosition;
        private Vector3f _controlDelta;

        public MoveByVec3QuadraticState(MoveByQuadratic action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _controlDelta = action.ControlDelta;
            _previousPosition = _startPosition = Get<Vector3f>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var timeInverse = 1.0f - time;
            var currentPos = value.GetVector3();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos =
                _startPosition + _controlDelta * 2.0f * time * timeInverse + _positionDelta * time * time;
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    private sealed class MoveByIntVec3QuadraticState : AttributeActionState
    {
        private Vector3f _positionDelta;
        private Vector3f _controlDelta;
        private Vector3i _startPosition;
        private Vector3i _previousPosition;

        public MoveByIntVec3QuadraticState(MoveByQuadratic action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _controlDelta = action.ControlDelta;
            _previousPosition = _startPosition = Get<Vector3i>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var timeInverse = 1.0f - time;
            var currentPos = value.GetIntVector3();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos = _startPosition
                + (_controlDelta * 2.0f * time * timeInverse + _positionDelta * time * time).ToIntVector3();
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    private sealed class MoveByVec2QuadraticState : AttributeActionState
    {
        private Vector2f _positionDelta;
        private Vector2f _controlDelta;
        private Vector2f _startPosition;
        private Vector2f _previousPosition;

        public MoveByVec2QuadraticState(MoveByQuadratic action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta.ToVector2();
            _controlDelta = action.ControlDelta.ToVector2();
            _previousPosition = _startPosition = Get<Vector2f>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var timeInverse = 1.0f - time;
            var currentPos = value.GetVector2();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos =
                _startPosition + _controlDelta * 2.0f * time * timeInverse + _positionDelta * time * time;
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    private sealed class MoveByIntVec2QuadraticState : AttributeActionState
    {
        private Vector2f _positionDelta;
        private Vector2f _controlDelta;
        private Vector2i _startPosition;
        private Vector2i _previousPosition;

        public MoveByIntVec2QuadraticState(MoveByQuadratic action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta.ToVector2();
            _controlDelta = action.ControlDelta.ToVector2();
            _previousPosition = _startPosition = Get<Vector2i>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var timeInverse = 1.0f - time;
            var currentPos = value.GetIntVector2();
            var diff = currentPos - _previousPosition;
            _startPosition = _startPosition + diff;
            var newPos = _startPosition
                + (_controlDelta * 2.0f * time * timeInverse + _positionDelta * time * time).ToIntVector2();
            value.Dispose();
            value = new Variant(newPos);
            _previousPosition = newPos;
        }
    }

    /// <summary>Construct.</summary>
    public MoveByQuadratic()
    {
    }

    /// <summary>Set control point delta.</summary>
    public void SetControlDelta(Vector3f delta)
    {
        _control = delta;
    }

    /// <summary>Get control point delta.</summary>
    public Vector3f ControlDelta => _control;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new MoveByQuadratic();
        result.SetDuration(Duration);
        result.SetPositionDelta(-PositionDelta);
        result.SetControlDelta(-ControlDelta);
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "control", ref _control, Vector3f.Zero, ArchiveSerialization.SerializeValue);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        var attribute = GetAttribute(target);
        if (attribute is not null)
        {
            if (attribute.ValueType == typeof(Vector2f))
                return new MoveByVec2QuadraticState(this, target, attribute);
            if (attribute.ValueType == typeof(Vector3f))
                return new MoveByVec3QuadraticState(this, target, attribute);
            if (attribute.ValueType == typeof(Vector2i))
                return new MoveByIntVec2QuadraticState(this, target, attribute);
            if (attribute.ValueType == typeof(Vector3i))
                return new MoveByIntVec3QuadraticState(this, target, attribute);
            Log.GetLogger().LogError("Attribute {Attribute} is not of valid type.", AttributeName);
        }
        return base.StartAction(target);
    }

    private Vector3f _control = Vector3f.Zero;
}

/// <summary>Move instantly by 3D offset action. Target should have attribute "Position" of type Vector3 or IntVector3.</summary>
[Reflected]
public partial class JumpBy : AttributeAction
{
    private sealed class JumpByVec3State : AttributeActionState
    {
        private Vector3f _positionDelta;
        private bool _triggered;

        public JumpByVec3State(JumpBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _triggered = false;
        }

        protected override void Update(float time, ref Variant value)
        {
            if (_triggered)
                return;
            _triggered = true;
            var result = value.GetVector3() + _positionDelta;
            value.Dispose();
            value = new Variant(result);
        }
    }

    private sealed class JumpByIntVec3State : AttributeActionState
    {
        private Vector3f _positionDelta;
        private bool _triggered;

        public JumpByIntVec3State(JumpBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _triggered = false;
        }

        protected override void Update(float time, ref Variant value)
        {
            if (_triggered)
                return;
            _triggered = true;
            var result = value.GetIntVector3().ToVector3() + _positionDelta;
            value.Dispose();
            value = new Variant(result.ToIntVector3());
        }
    }

    private sealed class JumpByVec2State : AttributeActionState
    {
        private Vector3f _positionDelta;
        private bool _triggered;

        public JumpByVec2State(JumpBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _triggered = false;
        }

        protected override void Update(float time, ref Variant value)
        {
            if (_triggered)
                return;
            _triggered = true;
            var result = value.GetVector2() + _positionDelta.ToVector2();
            value.Dispose();
            value = new Variant(result);
        }
    }

    private sealed class JumpByIntVec2State : AttributeActionState
    {
        private Vector3f _positionDelta;
        private bool _triggered;

        public JumpByIntVec2State(JumpBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _positionDelta = action.PositionDelta;
            _triggered = false;
        }

        protected override void Update(float time, ref Variant value)
        {
            if (_triggered)
                return;
            _triggered = true;
            var result = value.GetIntVector2() + _positionDelta.ToIntVector2();
            value.Dispose();
            value = new Variant(result);
        }
    }

    /// <summary>Construct.</summary>
    public JumpBy()
    {
    }

    /// <summary>Set position delta.</summary>
    public void SetPositionDelta(Vector3f delta)
    {
        _delta = delta;
    }

    /// <summary>Get position delta.</summary>
    public Vector3f PositionDelta => _delta;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new JumpBy();
        result.SetDuration(Duration);
        result.SetPositionDelta(-_delta);
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "delta", ref _delta, Vector3f.Zero, ArchiveSerialization.SerializeValue);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        var attribute = GetAttribute(target);
        if (attribute is not null)
        {
            if (attribute.ValueType == typeof(Vector2f))
                return new JumpByVec2State(this, target, attribute);
            if (attribute.ValueType == typeof(Vector3f))
                return new JumpByVec3State(this, target, attribute);
            if (attribute.ValueType == typeof(Vector2i))
                return new JumpByIntVec2State(this, target, attribute);
            if (attribute.ValueType == typeof(Vector3i))
                return new JumpByIntVec3State(this, target, attribute);
            Log.GetLogger().LogError("Attribute {Attribute} is not of valid type.", AttributeName);
        }
        return base.StartAction(target);
    }

    private Vector3f _delta = Vector3f.Zero;
}

/// <summary>Scale by 3D offset action. Target should have attribute "Scale" of type Vector3.</summary>
[Reflected]
public partial class ScaleBy : AttributeAction
{
    private sealed class ScaleByState : AttributeActionState
    {
        private Vector3f _scaleDelta;
        private Vector3f _startScale;
        private Vector3f _previousScale;

        public ScaleByState(ScaleBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _scaleDelta = action.ScaleDelta;
            _previousScale = _startScale = Get<Vector3f>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentScale = value.GetVector3();
            var diff = currentScale / _previousScale;
            _startScale = _startScale * diff;
            var newScale = _startScale * Vector3f.One.Lerp(_scaleDelta, time);
            value.Dispose();
            value = new Variant(newScale);
            _previousScale = newScale;
        }
    }

    private sealed class ScaleByVec2State : AttributeActionState
    {
        private Vector2f _scaleDelta;
        private Vector2f _startScale;
        private Vector2f _previousScale;

        public ScaleByVec2State(ScaleBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _scaleDelta = action.ScaleDelta.ToVector2();
            _previousScale = _startScale = Get<Vector2f>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentScale = value.GetVector2();
            var diff = currentScale / _previousScale;
            _startScale = _startScale * diff;
            var newScale = _startScale * Vector2f.One.Lerp(_scaleDelta, time);
            value.Dispose();
            value = new Variant(newScale);
            _previousScale = newScale;
        }
    }

    /// <summary>Construct.</summary>
    public ScaleBy()
    {
    }

    /// <summary>Set scale delta.</summary>
    public void SetScaleDelta(Vector3f delta)
    {
        _delta = delta;
    }

    /// <summary>Get scale delta.</summary>
    public Vector3f ScaleDelta => _delta;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new ScaleBy();
        result.SetDuration(Duration);
        result.SetScaleDelta(new Vector3f(1.0f / _delta.X, 1.0f / _delta.Y, 1.0f / _delta.Z));
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "delta", ref _delta, Vector3f.One, ArchiveSerialization.SerializeValue);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        var attribute = GetAttribute(target);
        if (attribute is not null)
        {
            if (attribute.ValueType == typeof(Vector3f))
                return new ScaleByState(this, target, attribute);
            if (attribute.ValueType == typeof(Vector2f))
                return new ScaleByVec2State(this, target, attribute);
            Log.GetLogger().LogError("Attribute {Attribute} is not of valid type.", AttributeName);
        }
        return base.StartAction(target);
    }

    private Vector3f _delta = Vector3f.One;
}

/// <summary>Rotate by 3D delta action. Target should have attribute "Rotation" of type Quaternion.</summary>
[Reflected]
public partial class RotateBy : AttributeAction
{
    private sealed class RotateByState : AttributeActionState
    {
        private Quaternion _rotationDelta;
        private Quaternion _startRotation;
        private Quaternion _previousRotation;

        public RotateByState(RotateBy action, RedObject? target, AttributeInfo? attribute)
            : base(action, target, attribute)
        {
            _rotationDelta = action.RotationDelta;
            _previousRotation = _startRotation = Get<Quaternion>();
        }

        protected override void Update(float time, ref Variant value)
        {
            var currentRotation = value.GetQuaternion();
            var diff = _previousRotation.Inverse * currentRotation;
            _startRotation = _startRotation * diff;
            var newRotation = _startRotation * Quaternion.IDENTITY.Slerp(_rotationDelta, time);
            value.Dispose();
            value = new Variant(newRotation);
            _previousRotation = newRotation;
        }
    }

    /// <summary>Construct.</summary>
    public RotateBy()
    {
    }

    /// <summary>Set rotation delta.</summary>
    public void SetRotationDelta(Quaternion delta)
    {
        _delta = delta;
    }

    /// <summary>Get rotation delta.</summary>
    public Quaternion RotationDelta => _delta;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new RotateBy();
        result.SetDuration(Duration);
        result.SetRotationDelta(_delta.Inverse);
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "delta", ref _delta, Quaternion.IDENTITY, ArchiveSerialization.SerializeValue);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        var attribute = GetAttribute(target);
        if (attribute is not null)
        {
            if (attribute.ValueType == typeof(Quaternion))
                return new RotateByState(this, target, attribute);
            Log.GetLogger().LogError("Attribute {Attribute} is not of valid type.", AttributeName);
        }
        return base.StartAction(target);
    }

    private Quaternion _delta = Quaternion.IDENTITY;
}
