//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.RmlUI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using RedBolt.Audio;
using RedBolt.Bindings.RmlUi;
using RedBolt.Container;
using RedBolt.Core;
using CoreVariant = RedBolt.Core.Variant;
using EngineLog = RedBolt.Logging.Log;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Typed replacement for custom StringHash RmlUI events.</summary>
public struct RmlCustomEvent
{
    public StringHash EventType;
    public VariantMap Data;
}

internal static class RmlEventListeners
{
    private static readonly Regex SoundRegex = new(@"\A\s*(\d+%)?\s*(.+)\s*\z");

    private static List<string> ParsePipe(string str)
    {
        List<string> result = [string.Empty];

        var isEscaped = false;
        foreach (var ch in str)
        {
            if (ch == '\'')
                isEscaped = !isEscaped;
            if (!isEscaped && ch == ';')
                result.Add(string.Empty);
            else
                result[^1] += ch;
        }

        for (var i = result.Count - 1; i >= 0; --i)
        {
            result[i] = result[i].Trim();
            if (result[i].Length == 0)
                result.RemoveAt(i);
        }
        return result;
    }

    private static (string Resource, float Volume)? ParseSound(string str)
    {
        var match = SoundRegex.Match(str);
        if (!match.Success)
            return null;

        var resource = match.Groups[2].Value;
        var volume = match.Groups[1].Value.Length == 0
            ? 1.0f
            : int.Parse(match.Groups[1].Value.AsSpan(0, match.Groups[1].Value.Length - 1),
                CultureInfo.InvariantCulture) / 100.0f;
        return (resource, volume);
    }

    private static CoreVariant ToVariant(JSONValue json)
    {
        switch (json.GetValueType())
        {
            case JSONValueType.Null:
                return default;
            case JSONValueType.Bool:
                return new CoreVariant(json.GetBool());
            case JSONValueType.Number:
                return json.GetNumberType() switch
                {
                    JSONNumberType.NaN => new CoreVariant(float.NaN),
                    JSONNumberType.Int => new CoreVariant(json.GetInt()),
                    JSONNumberType.UInt => new CoreVariant(json.GetUInt()),
                    JSONNumberType.FloatDouble => new CoreVariant(json.GetFloat()),
                    _ => default
                };
            case JSONValueType.String:
                return new CoreVariant(json.GetString());
            case JSONValueType.Array:
            {
                using var array = new VariantVector();
                foreach (var item in json.GetArray())
                {
                    using var value = ToVariant(item);
                    array.Add(value);
                }
                return new CoreVariant(array);
            }
            case JSONValueType.Object:
            {
                using var map = new VariantMap();
                foreach (var item in json.GetObject())
                {
                    using var value = ToVariant(item.Value);
                    map[item.Key] = value;
                }
                return new CoreVariant(map);
            }
            default:
                return default;
        }
    }

    private static (string EventType, VariantMap EventData) ParseEvent(string str)
    {
        var pos = str.IndexOf('(');
        if (pos == -1)
            return (str.Trim(), new VariantMap());

        // TODO(ui): support event parameters (in JSON?)
        EngineLog.GetLogger().Warning("TODO: Event parameters are not supported yet");

        var eventName = str[..pos].Trim();
        return (eventName, new VariantMap());
    }

    public static EventListener? CreateSingleEventListener(string value, Element element)
    {
        const string SoundPrefix = "sound:";
        const string EventPrefix = "event:";

        if (value.StartsWith(SoundPrefix, StringComparison.Ordinal))
            return SoundEventListener.CreateInstancer(value[SoundPrefix.Length..], element);
        if (value.StartsWith(EventPrefix, StringComparison.Ordinal))
            return CustomEventListener.CreateInstancer(value[EventPrefix.Length..], element);

        EngineLog.GetLogger().Warning("Unknown event '{}'", value);
        return null;
    }

    public static EventListener? CreatePipeEventListener(string value, Element element)
    {
        var parsed = ParsePipe(value);
        List<EventListener> listeners = [];
        foreach (var str in parsed)
        {
            var listener = CreateSingleEventListener(str, element);
            if (listener is not null)
                listeners.Add(listener);
        }

        if (listeners.Count == 0)
            return null;
        if (listeners.Count == 1)
            return listeners[0];
        return new PipeEventListener(listeners);
    }

    public static EventListener? CreateSoundEventListener(string value, Element element)
    {
        var parsed = ParseSound(value);
        if (parsed is null)
        {
            EngineLog.GetLogger().Warning("Invalid syntax of sound event: '{}'", value);
            return null;
        }

        return new SoundEventListener(parsed.Value.Resource, parsed.Value.Volume);
    }

    public static EventListener CreateCustomEventListener(string value, Element element)
    {
        var (eventType, eventParams) = ParseEvent(value);
        return new CustomEventListener(eventType, eventParams);
    }
}

internal sealed class PipeEventListener : EventListener
{
    public static EventListener? CreateInstancer(string value, Element element)
    {
        return RmlEventListeners.CreatePipeEventListener(value, element);
    }

    public PipeEventListener(List<EventListener> listeners)
    {
        _listeners = listeners;
    }

    /// <summary>Implement Rml::EventListener.</summary>
    public override void ProcessEvent(Event @event)
    {
        foreach (var listener in _listeners)
            listener.ProcessEvent(@event);
    }

    /// <summary>Implement Rml::EventListener.</summary>
    public override void OnDetach(Element element)
    {
        foreach (var listener in _listeners)
            listener.Dispose();
        _listeners.Clear();
        RmlUI.ReleaseEventListener(this);
        Dispose();
    }

    private readonly List<EventListener> _listeners;
}

internal sealed class SoundEventListener : EventListener
{
    public static EventListener? CreateInstancer(string value, Element element)
    {
        return RmlEventListeners.CreateSoundEventListener(value, element);
    }

    public SoundEventListener(string soundResource, float volume)
    {
        _soundResource = soundResource;
        _volume = volume;
    }

    /// <summary>Implement Rml::EventListener.</summary>
    public override void ProcessEvent(Event @event)
    {
        if (_soundNode is null)
        {
            // At the time event listener is instantiated, element is not added to DOM, so it does not have rml context set and we have no means
            // to find Context. That is why initialization of these objects is delayed until first event invocation.
            var rmlContext = (RmlUI.RmlContext)@event.GetTargetElement().GetContext();
            var ui = rmlContext.GetOwnerSubsystem()!;
            _ = ui.Context;
            _soundNode = new Node();
            _soundPlayer = _soundNode.CreateComponent<SoundSource>();
            _soundPlayer.SetGain(_volume);
            _soundPlayer.SetIgnoreSceneTimeScale(true);
        }

        var cache = _soundNode.GetSubsystem<ResourceCache>();
        var sound = cache.GetResourceOrNull<Sound>(_soundResource);
        if (sound is not null)
            _soundPlayer!.Play(sound);
    }

    /// <summary>Implement Rml::EventListener.</summary>
    public override void OnDetach(Element element)
    {
        _soundNode?.Dispose();
        _soundNode = null;
        _soundPlayer = null;
        RmlUI.ReleaseEventListener(this);
        Dispose();
    }

    /// <summary>Sound resource.</summary>
    private readonly string _soundResource;
    /// <summary>Volume of the sound.</summary>
    private readonly float _volume = 1.0f;
    /// <summary>Node that contains SoundSource component.</summary>
    private Node? _soundNode;
    /// <summary>Sound player.</summary>
    private SoundSource? _soundPlayer;
}

internal sealed class CustomEventListener : EventListener
{
    public static EventListener CreateInstancer(string value, Element element)
    {
        return RmlEventListeners.CreateCustomEventListener(value, element);
    }

    public CustomEventListener(string eventType, VariantMap eventData)
    {
        _eventType = eventType;
        _eventData = eventData;
    }

    /// <summary>Implement Rml::EventListener.</summary>
    public override void ProcessEvent(Event @event)
    {
        var context = (RmlUI.RmlContext)@event.GetCurrentElement().GetContext();
        var ui = context.GetOwnerSubsystem()!;
        using var args = new VariantMap(_eventData);
        args["_Element"] = @event.GetCurrentElement().Handle;
        args["_Phase"] = (int)@event.GetPhase();
        args["_IsPropagating"] = @event.IsPropagating();
        var eventData = new RmlCustomEvent { EventType = _eventType, Data = args };
        ui.SendEvent(ref eventData);
    }

    /// <summary>Implement Rml::EventListener.</summary>
    public override void OnDetach(Element element)
    {
        _eventData.Dispose();
        RmlUI.ReleaseEventListener(this);
        Dispose();
    }

    private readonly StringHash _eventType;
    private readonly VariantMap _eventData;
}
