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

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RedBolt.Audio;
using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Resource;

public class SoundInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public SoundInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        _soundSource = new SoundSource();
        _soundSource.SetIgnoreSceneTimeScale(true);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        _soundName.Dispose();
        _soundSource.Dispose();
        _sounds.Clear();
        _resourceNames.Clear();
        if (_project.TryGetTarget(out var project))
            project.OnRequest -= OnProjectRequest;
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public void RenderContent()
    {
        for (var index = 0; index < _sounds.Count; ++index)
        {
            var sound = _sounds[index];
            _soundName.Set(sound.GetName());
            ImGui.PushID(_soundName.ContentBytes);
            try
            {
                if (index != 0)
                    ImGui.Separator();

                ImGui.Text(_soundName.ContentBytes);
                RenderSound(sound);
            }
            finally
            {
                ImGui.PopID();
            }
        }
    }

    public void RenderContextMenuItems()
    {
    }

    public void RenderMenu()
    {
    }

    public void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
    }

    private void OnProjectRequest(EditorTab? sender, ProjectRequest request)
    {
        if (request is not InspectResourceRequest inspectResourceRequest
            || inspectResourceRequest.Resources.Count == 0)
        {
            return;
        }

        var resources = inspectResourceRequest.Resources;

        var areAllSounds = resources.All(static descriptor => descriptor.HasObjectType<Sound>());
        if (!areAllSounds)
            return;

        request.QueueProcessCallback(() =>
        {
            var resourceNames = inspectResourceRequest.GetSortedResourceNames();
            if (!_resourceNames.SequenceEqual(resourceNames))
            {
                _resourceNames = resourceNames;
                InspectResources();
            }
            OnActivated?.Invoke();
        });
    }

    private void InspectResources()
    {
        var cache = GetSubsystem<ResourceCache>();

        _sounds.Clear();
        foreach (var resourceName in _resourceNames)
        {
            var sound = cache.GetResourceOrNull<Sound>(resourceName);
            if (sound is not null)
                _sounds.Add(sound);
        }
    }

    private void RenderSound(Sound sound)
    {
        var info = FormattableString.Invariant(
            $"Duration: {sound.GetLength()} s, Frequency: {(uint)sound.GetFrequency()} Hz\n");

        info += $"{(sound.IsStereo() ? "Stereo" : "Mono")}{(sound.IsSixteenBit() ? ", 16-bit" : string.Empty)}{(sound.IsCompressed() ? ", Compressed" : string.Empty)}\n";

        if (sound.IsLooped())
            info += FormattableString.Invariant($"Loop Start: {(uint)sound.GetRepeatOffset()}\n");

        ImGui.Text(Encoding.UTF8.GetBytes(info));

        if (ImGui.Button("\uf04b Play"u8))
            _soundSource.Play(sound);

        ImGui.SameLine();
        if (ImGui.Button("\uf04d Stop"u8))
            _soundSource.Stop();

        if (_soundSource.IsPlaying() && ReferenceEquals(_soundSource.GetSound(), sound))
        {
            ImGui.SameLine();

            var position = _soundSource.GetTimePosition();
            if (ImGui.SliderFloat("##time"u8, ref position, 0.0f, sound.GetLength()))
                _soundSource.Seek(Math.Max(Math.Min(position, sound.GetLength()), 0.0f));
            if (!_soundSource.IsPlaying())
                _soundSource.Seek(0);
        }
    }

    private readonly WeakReference<Project> _project;

    private List<string> _resourceNames = [];
    private readonly List<Sound> _sounds = [];
    private readonly SoundSource _soundSource;
    private readonly CString _soundName = new();
}
