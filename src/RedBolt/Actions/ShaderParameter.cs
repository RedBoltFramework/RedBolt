//
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
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR rhs
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR rhsWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR rhs DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Actions;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;

/// <summary>Animate shader parameter.</summary>
[Reflected]
public partial class ShaderParameterAction : FiniteTimeAction
{
    /// <summary>Construct.</summary>
    public ShaderParameterAction()
    {
    }

    // Get shader parameter name.
    public void SetName(string parameter)
    {
        _name = parameter;
    }

    // Get shader parameter name.
    public string Name => _name;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeValue(archive, "name", ref _name);
    }

    private string _name = string.Empty;
}

/// <summary>Animate shader parameter from current value to another.</summary>
[Reflected]
public partial class ShaderParameterTo : ShaderParameterAction
{
    private sealed class ShaderParameterToState : FiniteTimeActionState
    {
        private Variant _from;
        private Variant _to;
        private readonly string _name;

        public ShaderParameterToState(ShaderParameterTo action, RedObject? target)
            : base(action, target)
        {
            _to = action.To.Clone();
            _name = action.Name;
            if (Target is Material material)
            {
                using var from = Variant.FromObject(material.GetShaderParameter(_name));
                _from = from.Clone();
                if (_from.Type != _to.Type)
                {
                    _from.Dispose();
                    _from = _to.Clone();
                }
            }
        }

        public override void Dispose()
        {
            _to.Dispose();
            _from.Dispose();
            base.Dispose();
        }

        public override void Update(float time)
        {
            if (Target is Material material)
            {
                using var value = _from.Lerp(_to, time);
                material.SetShaderParameter(_name, value);
            }
        }
    }

    /// <summary>Construct.</summary>
    public ShaderParameterTo()
    {
    }

    // Get "to" value.
    public void SetTo(Variant variant)
    {
        var value = variant.Clone();
        _to.Dispose();
        _to = value;
    }

    // Get "to" value.
    public ref readonly Variant To => ref _to;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "to", ref _to, Variant.Empty, ArchiveSerialization.SerializeValue);
    }

    public override void Dispose()
    {
        _to.Dispose();
        base.Dispose();
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new ShaderParameterToState(this, target);
    }

    private Variant _to;
}

/// <summary>Animate shader parameter from one value to another.</summary>
[Reflected]
public partial class ShaderParameterFromTo : ShaderParameterTo
{
    private sealed class ShaderParameterFromToState : FiniteTimeActionState
    {
        private Variant _from;
        private Variant _to;
        private readonly string _name;

        public ShaderParameterFromToState(ShaderParameterFromTo action, RedObject? target)
            : base(action, target)
        {
            _from = action.From.Clone();
            _to = action.To.Clone();
            _name = action.Name;
        }

        public override void Dispose()
        {
            _to.Dispose();
            _from.Dispose();
            base.Dispose();
        }

        public override void Update(float time)
        {
            if (Target is Material material)
            {
                using var value = _from.Lerp(_to, time);
                material.SetShaderParameter(_name, value);
            }
        }
    }

    /// <summary>Construct.</summary>
    public ShaderParameterFromTo()
    {
    }

    // Set "from" value.
    public void SetFrom(Variant variant)
    {
        var value = variant.Clone();
        _from.Dispose();
        _from = value;
    }

    // Get "from" value.
    public ref readonly Variant From => ref _from;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new ShaderParameterFromTo();
        result.SetDuration(Duration);
        result.SetName(Name);
        result.SetFrom(To);
        result.SetTo(_from);
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "from", ref _from, Variant.Empty, ArchiveSerialization.SerializeValue);
    }

    public override void Dispose()
    {
        _from.Dispose();
        base.Dispose();
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new ShaderParameterFromToState(this, target);
    }

    private Variant _from;
}
