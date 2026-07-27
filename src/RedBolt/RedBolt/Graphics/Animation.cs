//
// Copyright (c) 2008-2022 the Urho3D project.
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

using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>
/// Animation trigger point.
/// </summary>
public struct AnimationTriggerPoint : ICloneable, IDisposable
{
    /// <summary>
    /// Trigger time.
    /// </summary>
    public float Time;

    /// <summary>
    /// Trigger data.
    /// </summary>
    public Variant Data;

    /// <summary>
    /// Copy-construct the trigger point.
    /// </summary>
    public readonly AnimationTriggerPoint Clone()
    {
        return new AnimationTriggerPoint
        {
            Time = Time,
            Data = Data.Clone()
        };
    }

    object ICloneable.Clone() => Clone();

    /// <summary>
    /// Destruct.
    /// </summary>
    public void Dispose()
    {
        Data.Dispose();
        this = default;
    }
}

/// <summary>
/// Skeletal animation resource.
/// Don't use bone tracks and generic variant tracks with the same names.
// C#: ObjectRevisionTracker is not ported; revision tracking is inlined here.
public class Animation : ResourceWithMetadata
{
    /// <summary>
    /// Construct.
    /// </summary>
    public Animation()
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        for (var i = _triggers.Count - 1; i >= 0; --i)
        {
            var trigger = _triggers[i];
            trigger.Dispose();
        }
        _triggers.Clear();

        foreach (var track in _variantTracks.Values)
            track.Dispose();
        _variantTracks.Clear();
        foreach (var track in _tracks.Values)
            track.Dispose();
        _tracks.Clear();

        base.Dispose();
    }

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<Animation>();
        reflection.SetObjectFactory(static () => new Animation());
    }

    /// <summary>
    /// Reset resource state to default.
    /// </summary>
    public void ResetToDefault()
    {
        _animationName = string.Empty;
        _length = 0.0f;
        RemoveAllTracks();
        RemoveAllTriggers();
    }

    /// <summary>
    /// Load resource from XML file.
    /// </summary>
    public bool LoadXML(XMLElement source)
    {
        ResetToDefault();

        if (!source.HasAttribute("length"))
        {
            Log.GetLogger().LogError("Animation length is missing");
            return false;
        }

        _animationName = source.GetAttribute("name");
        _length = source.GetFloat("length");

        LoadTracksFromXML(source);
        LoadVariantTracksFromXML(source);
        LoadTriggersFromXML(source);
        LoadMetadataFromXML(source);

        return true;
    }

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ResetToDefault();

        var dataSize = source.Size;

        if (source.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            using var xmlFile = new XMLFile();
            if (!xmlFile.Load(source))
                return false;
            return LoadXML(xmlFile.GetRoot());
        }

        var input = source;
        var fileId = input.ReadFileID();
        if (fileId != "UANI" && fileId != "UAN2")
        {
            Log.GetLogger().LogError("{AnimationName} is not a valid animation file", source.Name);
            return false;
        }

        var version = fileId == "UAN2" ? input.ReadUInt() : LegacyVersion;

        _animationName = input.ReadString();
        _animationNameHash = new StringHash(_animationName);
        _length = input.ReadFloat();

        var trackCount = checked((int)input.ReadUInt());
        for (var i = 0; i < trackCount; ++i)
        {
            var newTrack = CreateTrack(input.ReadString());
            newTrack.ChannelMask = (AnimationChannel)input.ReadUByte();

            if (version >= ChannelWeightVersion)
            {
                newTrack.PositionWeight = input.ReadFloat();
                newTrack.RotationWeight = input.ReadFloat();
                newTrack.ScaleWeight = input.ReadFloat();
            }
            else if (version >= TrackWeightVersion)
            {
                var weight = input.ReadFloat();
                newTrack.PositionWeight = weight;
                newTrack.RotationWeight = weight;
                newTrack.ScaleWeight = weight;
            }

            var keyFrameCount = checked((int)input.ReadUInt());
            newTrack.KeyFrames.Clear();
            for (var j = 0; j < keyFrameCount; ++j)
            {
                var keyFrame = new AnimationKeyFrame
                {
                    Time = input.ReadFloat()
                };
                if ((newTrack.ChannelMask & AnimationChannel.Position) != 0)
                    keyFrame.Position = input.ReadVector3();
                if ((newTrack.ChannelMask & AnimationChannel.Rotation) != 0)
                    keyFrame.Rotation = input.ReadQuaternion();
                if ((newTrack.ChannelMask & AnimationChannel.Scale) != 0)
                    keyFrame.Scale = input.ReadVector3();
                newTrack.KeyFrames.Add(keyFrame);
            }
        }

        if (version >= VariantTrackVersion)
        {
            var variantTrackCount = checked((int)input.ReadUInt());
            for (var i = 0; i < variantTrackCount; ++i)
            {
                var newTrack = CreateVariantTrack(input.ReadString());
                var trackType = (VariantType)input.ReadUByte();
                newTrack.Interpolation = (KeyFrameInterpolation)input.ReadUByte();
                newTrack.SplineTension = input.ReadFloat();
                if (version >= TrackWeightVersion)
                    newTrack.Weight = input.ReadFloat();

                var keyFrameCount = checked((int)input.ReadUInt());
                newTrack.KeyFrames.Clear();
                newTrack.InTangents.Clear();
                newTrack.OutTangents.Clear();
                for (var j = 0; j < keyFrameCount; ++j)
                {
                    newTrack.KeyFrames.Add(new VariantCurvePoint
                    {
                        Time = input.ReadFloat(),
                        Value = input.ReadVariant(trackType)
                    });
                }

                if (newTrack.Interpolation == KeyFrameInterpolation.TangentSpline)
                {
                    for (var j = 0; j < keyFrameCount; ++j)
                    {
                        newTrack.InTangents.Add(input.ReadVariant(trackType));
                        newTrack.OutTangents.Add(input.ReadVariant(trackType));
                    }
                }

                newTrack.Commit();
            }
        }

        // Optionally read triggers from an XML file.
        var cache = GetSubsystem<ResourceCache>()!;
        var xmlName = Path.ChangeExtension(Name, ".xml");
        using var file = cache.GetTempResource<XMLFile>(xmlName, false);
        if (file is not null)
        {
            cache.StoreResourceDependency(this, xmlName);
            var root = file.GetRoot();
            LoadTracksFromXML(root);
            LoadVariantTracksFromXML(root);
            LoadTriggersFromXML(root);
            LoadMetadataFromXML(root);
        }

        SetMemoryUse(checked((int)dataSize));
        return true;
    }

    /// <summary>
    /// Save resource. Return true if successful.
    /// </summary>
    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        destination.WriteFileID("UAN2");
        destination.WriteUInt(CurrentVersion);
        destination.WriteString(_animationName);
        destination.WriteFloat(_length);

        destination.WriteUInt(checked((uint)_tracks.Count));
        foreach (var item in _tracks)
        {
            var track = item.Value;
            destination.WriteString(track.Name);
            destination.WriteUByte((byte)track.ChannelMask);
            destination.WriteFloat(track.PositionWeight);
            destination.WriteFloat(track.RotationWeight);
            destination.WriteFloat(track.ScaleWeight);
            destination.WriteUInt(checked((uint)track.KeyFrames.Count));

            foreach (var keyFrame in track.KeyFrames)
            {
                destination.WriteFloat(keyFrame.Time);
                if ((track.ChannelMask & AnimationChannel.Position) != 0)
                    destination.WriteVector3(keyFrame.Position);
                if ((track.ChannelMask & AnimationChannel.Rotation) != 0)
                    destination.WriteQuaternion(keyFrame.Rotation);
                if ((track.ChannelMask & AnimationChannel.Scale) != 0)
                    destination.WriteVector3(keyFrame.Scale);
            }
        }

        destination.WriteUInt(checked((uint)_variantTracks.Count));
        foreach (var item in _variantTracks)
        {
            var track = item.Value;
            var trackType = track.GetType();
            using var defaultValue = new Variant(trackType);

            destination.WriteString(track.Name);
            destination.WriteUByte((byte)trackType);
            destination.WriteUByte((byte)track.Interpolation);
            destination.WriteFloat(track.SplineTension);
            destination.WriteFloat(track.Weight);
            destination.WriteUInt(checked((uint)track.KeyFrames.Count));

            foreach (var keyFrame in track.KeyFrames)
            {
                destination.WriteFloat(keyFrame.Time);
                destination.WriteVariantData(keyFrame.Value.Type == trackType ? keyFrame.Value : defaultValue);
            }

            if (track.Interpolation == KeyFrameInterpolation.TangentSpline)
            {
                for (var i = 0; i < track.KeyFrames.Count; ++i)
                {
                    var inTangent = track.InTangents[i];
                    var outTangent = track.OutTangents[i];
                    destination.WriteVariantData(inTangent.Type == trackType ? inTangent : defaultValue);
                    destination.WriteVariantData(outTangent.Type == trackType ? outTangent : defaultValue);
                }
            }
        }

        // If triggers have been defined, write an XML file for them.
        if (_triggers.Count != 0 || HasMetadata())
        {
            var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
            var xmlName = Path.ChangeExtension(destination.GetName(), ".xml");
            using var destinationXmlFile = virtualFileSystem.OpenFile(new FileIdentifier(xmlName), FileMode.Write);
            if (destinationXmlFile is not null)
            {
                using var xml = new XMLFile();
                var root = xml.CreateRoot("animation");
                foreach (var trigger in _triggers)
                {
                    var triggerElement = root.CreateChild("trigger");
                    triggerElement.SetFloat("time", trigger.Time);
                    triggerElement.SetVariant(trigger.Data);
                }
                SaveMetadataToXML(root);
                xml.Save(destinationXmlFile);
            }
            else
            {
                Log.GetLogger().LogWarning("Can not save animation trigger data when not saving into a file");
            }
        }

        return true;
    }

    /// <summary>
    /// Set animation name.
    /// </summary>
    public void SetAnimationName(string name)
    {
        _animationName = name;
        _animationNameHash = new StringHash(name);
    }

    /// <summary>
    /// Set animation length.
    /// </summary>
    public void SetLength(float length)
    {
        _length = Math.Max(length, 0.0f);
    }

    /// <summary>
    /// Create and return a track by name. If track by same name already exists, returns the existing.
    /// </summary>
    public AnimationTrack CreateTrack(string name)
    {
        MarkRevisionUpdated();

        var nameHash = new StringHash(name);
        if (!_tracks.TryGetValue(nameHash, out var track))
        {
            track = new AnimationTrack
            {
                Name = name,
                NameHash = nameHash
            };
            _tracks.Add(nameHash, track);
        }

        return track;
    }

    /// <summary>
    /// Create and return generic variant track by name. If variant track by same name already exists, returns the existing.
    /// </summary>
    public VariantAnimationTrack CreateVariantTrack(string name)
    {
        MarkRevisionUpdated();

        var nameHash = new StringHash(name);
        if (!_variantTracks.TryGetValue(nameHash, out var track))
        {
            track = new VariantAnimationTrack
            {
                Name = name,
                NameHash = nameHash
            };
            _variantTracks.Add(nameHash, track);
        }

        return track;
    }

    /// <summary>
    /// Remove a track by name. Return true if was found and removed successfully. This is unsafe if the animation is currently used in playback.
    /// </summary>
    public bool RemoveTrack(string name)
    {
        MarkRevisionUpdated();

        var nameHash = new StringHash(name);
        var removed = _tracks.Remove(nameHash, out var animationTrack);
        animationTrack?.Dispose();
        if (_variantTracks.Remove(nameHash, out var variantTrack))
        {
            variantTrack.Dispose();
            removed = true;
        }
        return removed;
    }

    /// <summary>
    /// Remove all tracks. This is unsafe if the animation is currently used in playback.
    /// </summary>
    public void RemoveAllTracks()
    {
        MarkRevisionUpdated();
        foreach (var track in _tracks.Values)
            track.Dispose();
        _tracks.Clear();
        foreach (var track in _variantTracks.Values)
            track.Dispose();
        _variantTracks.Clear();
    }

    /// <summary>
    /// Set a trigger point at index.
    /// </summary>
    public void SetTrigger(int index, AnimationTriggerPoint trigger)
    {
        if (index == _triggers.Count)
            AddTrigger(trigger);
        else if (index >= 0 && index < _triggers.Count)
        {
            var replacement = trigger.Clone();
            var oldTrigger = _triggers[index];
            oldTrigger.Dispose();
            _triggers[index] = replacement;
            _triggers.Sort(static (lhs, rhs) => lhs.Time.CompareTo(rhs.Time));
        }
    }

    /// <summary>
    /// Add a trigger point.
    /// </summary>
    public void AddTrigger(AnimationTriggerPoint trigger)
    {
        _triggers.Add(trigger.Clone());
        _triggers.Sort(static (lhs, rhs) => lhs.Time.CompareTo(rhs.Time));
    }

    /// <summary>
    /// Add a trigger point.
    /// </summary>
    public void AddTrigger(float time, bool timeIsNormalized, Variant data)
    {
        var newTrigger = new AnimationTriggerPoint
        {
            Time = timeIsNormalized ? time * _length : time,
            Data = data.Clone()
        };
        _triggers.Add(newTrigger);
        _triggers.Sort(static (lhs, rhs) => lhs.Time.CompareTo(rhs.Time));
    }

    /// <summary>
    /// Remove a trigger point by index.
    /// </summary>
    public void RemoveTrigger(int index)
    {
        if (index >= 0 && index < _triggers.Count)
        {
            var trigger = _triggers[index];
            trigger.Dispose();
            _triggers.RemoveAt(index);
        }
    }

    /// <summary>
    /// Remove all trigger points.
    /// </summary>
    public void RemoveAllTriggers()
    {
        for (var i = _triggers.Count - 1; i >= 0; --i)
        {
            var trigger = _triggers[i];
            trigger.Dispose();
        }
        _triggers.Clear();
    }

    /// <summary>
    /// Resize trigger point vector.
    /// </summary>
    public void SetNumTriggers(int num)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(num);
        var count = num;
        if (count < _triggers.Count)
        {
            for (var i = _triggers.Count - 1; i >= count; --i)
            {
                var trigger = _triggers[i];
                trigger.Dispose();
            }
            _triggers.RemoveRange(count, _triggers.Count - count);
        }
        else
        {
            while (_triggers.Count < count)
                _triggers.Add(default);
        }
    }

    /// <summary>
    /// Clone the animation.
    /// </summary>
    public Animation Clone(string cloneName = "")
    {
        var ret = new Animation();
        ret.SetName(cloneName);
        ret.SetAbsoluteFileName(cloneName == Name ? AbsoluteFileName : string.Empty);
        ret.SetAnimationName(_animationName);
        ret._length = _length;

        foreach (var item in _tracks)
            ret._tracks.Add(item.Key, item.Value.Clone());

        foreach (var item in _variantTracks)
            ret._variantTracks.Add(item.Key, item.Value.Clone());

        foreach (var trigger in _triggers)
            ret._triggers.Add(trigger.Clone());
        ret.CopyMetadata(this);
        ret.SetMemoryUse(MemoryUse);
        return ret;
    }

    /// <summary>
    /// Return animation name.
    /// </summary>
    public string AnimationName => _animationName;

    /// <summary>
    /// Return animation name hash.
    /// </summary>
    public StringHash AnimationNameHash => _animationNameHash;

    /// <summary>
    /// Return animation length.
    /// </summary>
    public float Length => _length;

    /// <summary>
    /// Return all animation tracks.
    /// </summary>
    public IReadOnlyDictionary<StringHash, AnimationTrack> Tracks => _tracks;

    /// <summary>
    /// Return number of animation tracks.
    /// </summary>
    public int NumTracks => _tracks.Count;

    /// <summary>
    /// Return animation track by index.
    /// </summary>
    public AnimationTrack? GetTrack(int index)
    {
        if (index < 0 || index >= _tracks.Count)
            return null;

        foreach (var track in _tracks.Values)
        {
            if (index == 0)
                return track;
            --index;
        }

        return null;
    }

    /// <summary>
    /// Return animation track by name.
    /// </summary>
    public AnimationTrack? GetTrack(string name)
    {
        return GetTrack(new StringHash(name));
    }

    /// <summary>
    /// Return animation track by name hash.
    /// </summary>
    public AnimationTrack? GetTrack(StringHash nameHash)
    {
        return _tracks.TryGetValue(nameHash, out var track) ? track : null;
    }

    /// <summary>
    /// Return generic variant animation tracks.
    /// </summary>
    public IReadOnlyDictionary<StringHash, VariantAnimationTrack> VariantTracks => _variantTracks;

    /// <summary>
    /// Return number of generic variant animation tracks.
    /// </summary>
    public int NumVariantTracks => _variantTracks.Count;

    /// <summary>
    /// Return generic variant animation track by index.
    /// </summary>
    public VariantAnimationTrack? GetVariantTrack(int index)
    {
        if (index < 0 || index >= _variantTracks.Count)
            return null;

        foreach (var track in _variantTracks.Values)
        {
            if (index == 0)
                return track;
            --index;
        }

        return null;
    }

    /// <summary>
    /// Return generic variant animation track by name.
    /// </summary>
    public VariantAnimationTrack? GetVariantTrack(string name)
    {
        return GetVariantTrack(new StringHash(name));
    }

    /// <summary>
    /// Return generic variant animation track by name hash.
    /// </summary>
    public VariantAnimationTrack? GetVariantTrack(StringHash nameHash)
    {
        return _variantTracks.TryGetValue(nameHash, out var track) ? track : null;
    }

    /// <summary>
    /// Return animation trigger points.
    /// </summary>
    public IReadOnlyList<AnimationTriggerPoint> Triggers => _triggers;

    /// <summary>
    /// Return number of animation trigger points.
    /// </summary>
    public int NumTriggers => _triggers.Count;

    /// <summary>
    /// Return a trigger point by index.
    /// </summary>
    public ref AnimationTriggerPoint GetTrigger(int index)
    {
        if (index >= 0 && index < _triggers.Count)
            return ref CollectionsMarshal.AsSpan(_triggers)[index];
        return ref Unsafe.NullRef<AnimationTriggerPoint>();
    }

    /// <summary>
    /// Set all animation tracks.
    /// </summary>
    public void SetTracks(IReadOnlyList<AnimationTrack> tracks)
    {
        foreach (var track in _tracks.Values)
            track.Dispose();
        _tracks.Clear();
        foreach (var sourceTrack in tracks)
            _tracks[new StringHash(sourceTrack.Name)] = sourceTrack.Clone();
    }

    private void LoadTracksFromXML(XMLElement source)
    {
        for (var trackElement = source.GetChild("transform"); trackElement.NotNull; trackElement = trackElement.GetNext("transform"))
        {
            if (!trackElement.HasAttribute("name"))
            {
                Log.GetLogger().LogError("Animation track name is missing");
                continue;
            }

            var newTrack = CreateTrack(trackElement.GetAttribute("name"));
            if (trackElement.GetBool("position"))
                newTrack.ChannelMask |= AnimationChannel.Position;
            if (trackElement.GetBool("rotation"))
                newTrack.ChannelMask |= AnimationChannel.Rotation;
            if (trackElement.GetBool("scale"))
                newTrack.ChannelMask |= AnimationChannel.Scale;
            if (trackElement.HasAttribute("weight"))
            {
                var weight = trackElement.GetFloat("weight");
                newTrack.PositionWeight = weight;
                newTrack.RotationWeight = weight;
                newTrack.ScaleWeight = weight;
            }
            if (trackElement.HasAttribute("positionweight"))
                newTrack.PositionWeight = trackElement.GetFloat("positionweight");
            if (trackElement.HasAttribute("rotationweight"))
                newTrack.RotationWeight = trackElement.GetFloat("rotationweight");
            if (trackElement.HasAttribute("scaleweight"))
                newTrack.ScaleWeight = trackElement.GetFloat("scaleweight");

            for (var keyFrameElement = trackElement.GetChild("keyframe"); keyFrameElement.NotNull; keyFrameElement = keyFrameElement.GetNext("keyframe"))
            {
                var keyFrame = new AnimationKeyFrame
                {
                    Time = keyFrameElement.GetFloat("time")
                };
                if ((newTrack.ChannelMask & AnimationChannel.Position) != 0)
                    keyFrame.Position = keyFrameElement.GetVector3("position");
                if ((newTrack.ChannelMask & AnimationChannel.Rotation) != 0)
                    keyFrame.Rotation = keyFrameElement.GetQuaternion("rotation");
                if ((newTrack.ChannelMask & AnimationChannel.Scale) != 0)
                    keyFrame.Scale = keyFrameElement.GetVector3("scale");
                newTrack.KeyFrames.Add(keyFrame);
            }

            newTrack.SortKeyFrames();
        }
    }

    private void LoadVariantTracksFromXML(XMLElement source)
    {
        for (var trackElement = source.GetChild("variant"); trackElement.NotNull; trackElement = trackElement.GetNext("variant"))
        {
            if (!trackElement.HasAttribute("name"))
            {
                Log.GetLogger().LogError("Animation track name is missing");
                continue;
            }

            var newTrack = CreateVariantTrack(trackElement.GetAttribute("name"));
            var type = Variant.GetTypeFromName(trackElement.GetAttribute("type"));
            if (trackElement.HasAttribute("interpolation"))
            {
                var interpolation = trackElement.GetAttribute("interpolation");
                newTrack.Interpolation = interpolation switch
                {
                    "none" => KeyFrameInterpolation.None,
                    "linear" => KeyFrameInterpolation.Linear,
                    "spline" => KeyFrameInterpolation.TensionSpline,
                    "cubicspline" => KeyFrameInterpolation.TangentSpline,
                    _ => KeyFrameInterpolation.Linear
                };
            }
            if (trackElement.HasAttribute("tension"))
                newTrack.SplineTension = trackElement.GetFloat("tension");
            if (trackElement.HasAttribute("weight"))
                newTrack.Weight = trackElement.GetFloat("weight");

            for (var keyFrameElement = trackElement.GetChild("keyframe"); keyFrameElement.NotNull; keyFrameElement = keyFrameElement.GetNext("keyframe"))
            {
                newTrack.KeyFrames.Add(new VariantCurvePoint
                {
                    Time = keyFrameElement.GetFloat("time"),
                    Value = keyFrameElement.GetVariantValue(type)
                });

                if (newTrack.Interpolation == KeyFrameInterpolation.TangentSpline)
                {
                    newTrack.InTangents.Add(keyFrameElement.GetChild("intangent").GetVariantValue(type));
                    newTrack.OutTangents.Add(keyFrameElement.GetChild("outtangent").GetVariantValue(type));
                }
            }

            newTrack.SortKeyFrames();
            newTrack.Commit();
        }
    }

    private void LoadTriggersFromXML(XMLElement source)
    {
        for (var triggerElement = source.GetChild("trigger"); triggerElement.NotNull; triggerElement = triggerElement.GetNext("trigger"))
        {
            if (triggerElement.HasAttribute("normalizedtime"))
            {
                using var data = triggerElement.GetVariant();
                AddTrigger(triggerElement.GetFloat("normalizedtime"), true, data);
            }
            else if (triggerElement.HasAttribute("time"))
            {
                using var data = triggerElement.GetVariant();
                AddTrigger(triggerElement.GetFloat("time"), false, data);
            }
        }
    }

    private const uint LegacyVersion = 1;
    private const uint VariantTrackVersion = 2;
    private const uint TrackWeightVersion = 3;
    private const uint ChannelWeightVersion = 4;
    private const uint CurrentVersion = ChannelWeightVersion;

    private uint _revision = 1;
    private string _animationName = string.Empty;
    private StringHash _animationNameHash;
    private float _length;
    private readonly Dictionary<StringHash, AnimationTrack> _tracks = new();
    private readonly Dictionary<StringHash, VariantAnimationTrack> _variantTracks = new();
    private readonly List<AnimationTriggerPoint> _triggers = new();

    /// <summary>
    /// Return object revision.
    /// </summary>
    public uint GetRevision() => _revision;

    /// <summary>
    /// Mark object as changed.
    /// </summary>
    protected void MarkRevisionUpdated()
    {
        unchecked
        {
            ++_revision;
            if (_revision == 0)
                ++_revision;
        }
    }
}
