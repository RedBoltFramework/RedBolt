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
