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

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

public interface ISimpleSettingsPageValues
{
    string UniqueName { get; }
    void SerializeInBlock(Archive archive);
    void RenderSettings();
}

/// <summary>Base class of an individual page with settings.</summary>
public abstract class SettingsPage : RedObject
{
    public SettingsPage()
    {
    }

    /// <summary>Return unique name of the page.</summary>
    public abstract string UniqueName { get; }

    /// <summary>Return whether the serialization is needed.</summary>
    public abstract bool IsSerializable { get; }

    /// <summary>Return whether to show "reset to default" button.</summary>
    public virtual bool CanResetToDefault => false;

    /// <summary>Serialization must be provided for settings page.</summary>
    public abstract override void SerializeInBlock(Archive archive);

    /// <summary>Render page with settings.</summary>
    public abstract void RenderSettings();

    /// <summary>Reset settings to default.</summary>
    public virtual void ResetToDefaults()
    {
    }

    /// <summary>Apply hotkeys for the page.</summary>
    public virtual void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
    }
}

/// <summary>Simple settings page that expects struct with methods.</summary>
public class SimpleSettingsPage<T> : SettingsPage
    where T : struct, ISimpleSettingsPageValues
{
    public SimpleSettingsPage()
    {
        _values = new T();
    }

    /// <summary>Implement SettingsPage.</summary>
    public override string UniqueName => _values.UniqueName;

    public override bool IsSerializable => true;

    public override bool CanResetToDefault => true;

    public override void SerializeInBlock(Archive archive) => _values.SerializeInBlock(archive);

    public override void RenderSettings() => _values.RenderSettings();

    public override void ResetToDefaults() => _values = new T();

    public override void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
    }

    public ref readonly T Values => ref _values;

    private T _values;
}

public class SettingsPageGroup
{
    public readonly SortedDictionary<string, SettingsPage> Pages = new(StringComparer.Ordinal);
    public readonly SortedDictionary<string, SettingsPageGroup> Children = new(StringComparer.Ordinal);
}

/// <summary>Class used to manage and serialize settings.</summary>
public class SettingsManager : RedObject
{
    public SettingsManager()
    {
    }

    /// <summary>Add new page with settings. Should have unique key.</summary>
    public void AddPage(SettingsPage page)
    {
        _pages.Add(page);

        var name = page.UniqueName;
        var (path, section) = GetPathAndSection(name);

        _sortedPages[name] = page;
        InsertPageInGroup(_rootGroup, path, page, section);
    }

    /// <summary>Serialize.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        foreach (var (key, page) in _sortedPages)
        {
            if (page.IsSerializable)
            {
                if (archive.IsInput && archive.IsUnorderedAccessSupportedInCurrentBlock
                    && !archive.HasElementOrBlock(key))
                {
                    continue;
                }

                using var block = archive.OpenUnorderedBlock(key);
                page.SerializeInBlock(archive);
            }
        }
    }

    public void LoadFile(string fileName)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.LoadFile(new FileIdentifier(fileName)))
            jsonFile.LoadObject("Settings", this);
    }

    public void SaveFile(string fileName)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.SaveObject("Settings", this))
            jsonFile.SaveFile(new FileIdentifier(fileName));
    }

    /// <summary>Find page by key.</summary>
    public SettingsPage? FindPage(string key)
    {
        return _sortedPages.TryGetValue(key, out var page) ? page : null;
    }

    /// <summary>Return sorted pages.</summary>
    public IReadOnlyDictionary<string, SettingsPage> SortedPages => _sortedPages;

    /// <summary>Return page tree for rendering.</summary>
    public SettingsPageGroup PageTree => _rootGroup;

    private void InsertPageInGroup(SettingsPageGroup parentGroup, string path, SettingsPage page, string section)
    {
        var dotIndex = path.IndexOf('.');
        var isLeaf = dotIndex == -1;
        var childName = isLeaf ? path : path[..dotIndex];

        if (!parentGroup.Children.TryGetValue(childName, out var childGroup))
        {
            childGroup = new SettingsPageGroup();
            parentGroup.Children.Add(childName, childGroup);
        }

        if (isLeaf)
        {
            if (!childGroup.Pages.ContainsKey(section))
                childGroup.Pages.Add(section, page);
        }
        else
            InsertPageInGroup(childGroup, path[(dotIndex + 1)..], page, section);
    }

    private static (string Path, string Section) GetPathAndSection(string uniqueName)
    {
        var colonIndex = uniqueName.IndexOf(':');
        if (colonIndex == -1)
            return (uniqueName, string.Empty);
        return (uniqueName[..colonIndex], uniqueName[(colonIndex + 1)..]);
    }

    private readonly List<SettingsPage> _pages = [];
    private readonly SortedDictionary<string, SettingsPage> _sortedPages = new(StringComparer.Ordinal);
    private readonly SettingsPageGroup _rootGroup = new();
}
