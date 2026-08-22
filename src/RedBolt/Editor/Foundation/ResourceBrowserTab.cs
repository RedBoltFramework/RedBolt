// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SystemFile = global::System.IO.File;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.SystemUI;
using RedBolt.Utility;

public class ResourceBrowserTab : EditorTab
{
    private static readonly EditorHotkey HotkeyDelete =
        new EditorHotkey("ResourceBrowserTab.Delete").Press(Key.Delete);
    private static readonly EditorHotkey HotkeyRename =
        new EditorHotkey("ResourceBrowserTab.Rename").Press(Key.F2);
    private static readonly EditorHotkey HotkeyRevealInExplorer =
        new EditorHotkey("ResourceBrowserTab.RevealInExplorer").Alt().Shift().Press(Key.R);

    private static ReadOnlySpan<byte> ContextMenuId => "ResourceBrowserTab_PopupDirectory"u8;
    private const string SatelliteDirectoryExtension = ".d";

    private static string? TryAdjustPathOnRename(
        string path, string oldResourceName, string newResourceName)
    {
        if (path.StartsWith(oldResourceName, StringComparison.Ordinal))
        {
            var pathSuffix = path[oldResourceName.Length..];
            if (pathSuffix.Length == 0 || pathSuffix[0] == '/')
                return newResourceName + pathSuffix;
        }
        return null;
    }

    private static bool IsPayloadMovable(ResourceDragDropPayload payload)
    {
        foreach (var descriptor in payload.Resources)
        {
            if (descriptor.IsAutomatic)
                return false;
        }
        return true;
    }

    public sealed class Selection : ICloneable, IEquatable<Selection>
    {
        public int SelectedRoot = 1;
        public string SelectedLeftPath = string.Empty;
        public HashSet<string> SelectedRightPaths = new(StringComparer.Ordinal);

        public Selection()
        {
        }

        private Selection(Selection rhs)
        {
            CopyFrom(rhs);
        }

        public Selection Clone() => new(this);

        object ICloneable.Clone() => Clone();

        public bool Equals(Selection? rhs)
        {
            return rhs is not null
                && SelectedRoot == rhs.SelectedRoot
                && SelectedLeftPath == rhs.SelectedLeftPath
                && SelectedRightPaths.SetEquals(rhs.SelectedRightPaths);
        }

        public override bool Equals(object? obj) => obj is Selection rhs && Equals(rhs);

        public override int GetHashCode() => HashCode.Combine(SelectedRoot, SelectedLeftPath);

        public static bool operator ==(Selection? lhs, Selection? rhs)
        {
            return ReferenceEquals(lhs, rhs) || (lhs is not null && lhs.Equals(rhs));
        }

        public static bool operator !=(Selection? lhs, Selection? rhs) => !(lhs == rhs);

        public void CopyFrom(Selection rhs)
        {
            SelectedRoot = rhs.SelectedRoot;
            SelectedLeftPath = rhs.SelectedLeftPath;
            SelectedRightPaths.Clear();
            foreach (var path in rhs.SelectedRightPaths)
                SelectedRightPaths.Add(path);
        }
    }

    public ResourceBrowserTab()
        : base("Resources", "96c69b8e-ee83-43de-885c-8a51cef65d59",
            EditorTabFlag.OpenByDefault, EditorTabPlacement.DockBottom)
    {
        _projectInitializedCallback = _ => RefreshContents();
        _checkFileNameCallback = CheckFileName;
        _createEntryPayloadCallback = CreateEntryPayload;
        _createSelectionPayloadCallback = CreatePayloadFromRightSelection;

        InitializeRoots();
        InitializeDefaultFactories();
        InitializeHotkeys();

        var project = GetProject();
        project.OnInitialized += _projectInitializedCallback;
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        var project = GetProject();
        project.OnRequest -= OnProjectRequest;
        project.OnInitialized -= _projectInitializedCallback;

        ClearCachedEntryData();
        _tempEntryList.Clear();
        _create.Factory = null;
        for (var index = _factories.Count - 1; index >= 0; --index)
            _factories[index].Dispose();
        _factories.Clear();
        for (var index = _roots.Count - 1; index >= 0; --index)
            _roots[index].Dispose();
        _roots.Clear();
        _delete.Dispose();
        _rename.Dispose();
        _create.Dispose();
        _dialogText.Dispose();
        _textBuffer.Dispose();
        _hotkeyLabel.Dispose();
        _dragEntry = null;
        base.Dispose();
    }

    public void AddFactory(ResourceFactory factory)
    {
        _factories.Add(factory);
        _sortFactories = true;
    }

    public Selection GetSelection()
    {
        var selection = new Selection();
        CopyCurrentSelectionTo(selection);
        return selection;
    }

    public void SetSelection(Selection selection)
    {
        SelectLeftPanel(selection.SelectedLeftPath, selection.SelectedRoot);
        _right.SelectedPaths.Clear();
        foreach (var path in selection.SelectedRightPaths)
            _right.SelectedPaths.Add(path);
        _right.LastSelectedPath = GetFirstOrEmpty(_right.SelectedPaths);
        OnSelectionChanged();
        ScrollToSelection();
    }

    public void RenameOrMove(string oldFileName, string newFileName,
        string oldResourceName, string newResourceName, bool suppressUndo = false)
    {
        if (oldFileName == newFileName)
            return;

        var oldRootIndex = GetRootIndex(oldFileName) ?? 0;
        var newRootIndex = GetRootIndex(newFileName) ?? 0;

        var isFile = SystemFile.Exists(oldFileName);

        var renamed = TryRename(oldFileName, newFileName, isFile);
        if (renamed)
        {
            // Show tooltip if waiting for refresh
            _waitingForUpdate = true;

            // Keep selection on dragged element
            AdjustSelectionOnRename(oldRootIndex, oldResourceName, newRootIndex, newResourceName);

            // If file is moved and there's directory in cache with the same name, remove it
            if (isFile)
                CleanupResourceCache(oldResourceName);

            if (!suppressUndo)
            {
                var undoManager = GetUndoManager();
                var action = new RenameResourceAction(
                    this, oldFileName, newFileName, oldResourceName, newResourceName);
                undoManager.PushAction(action);
            }
        }
    }

    /// <summary>Commands.</summary>
    public void DeleteSelected()
    {
        // For right panel delete all the items in the selection
        if (!_cursor.IsLeftPanel)
            BeginRightSelectionDelete();
        else if (GetSelectedEntryForCursor() is { } entry)
            BeginEntryDelete(entry);
    }

    public void RenameSelected()
    {
        if (GetSelectedEntryForCursor() is { } entry)
            BeginEntryRename(entry);
    }

    public void RevealInExplorerSelected()
    {
        if (GetSelectedEntryForCursor() is { } entry)
            RevealInExplorer(entry.AbsolutePath);
    }

    public void OpenSelected()
    {
        if (GetSelectedEntryForCursor() is { } entry)
            OpenEntryInEditor(entry);
    }

    /// <summary>Implement EditorTab.</summary>
    public override void RenderContent()
    {
        CopyCurrentSelectionTo(_oldSelectionSnapshot);

        foreach (var root in _roots)
            root.Reflection.Update();

        if (_waitingForUpdate
            && ImGui.IsWindowHovered(ImGuiHoveredFlags.ImGuiHoveredFlagsRootAndChildWindows))
        {
            ImGui.SetTooltip("Waiting for update..."u8);
        }

        var workaroundPadding = new Vector2f(0.0f, -5.0f);
        if (ImGui.BeginTable(
                "##ResourceBrowserTab"u8, 2, ImGuiTableFlags.ImGuiTableFlagsResizable))
        {
            ImGui.TableSetupColumn(
                "Left"u8, ImGuiTableColumnFlags.ImGuiTableColumnFlagsWidthStretch, 0.35f);
            ImGui.TableSetupColumn(
                "Right"u8, ImGuiTableColumnFlags.ImGuiTableColumnFlagsWidthStretch, 0.65f);

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            if (ImGui.BeginChild("##DirectoryTree"u8, ImGui.GetContentRegionAvail() + workaroundPadding))
            {
                foreach (var root in _roots)
                    RenderDirectoryTree(root.Reflection.Root, root.NameBytes.ContentBytes);
                _left.ScrollToSelection = false;
            }
            ImGui.EndChild();

            ImGui.TableSetColumnIndex(1);
            if (ImGui.BeginChild("##DirectoryContent"u8, ImGui.GetContentRegionAvail() + workaroundPadding))
            {
                RenderDirectoryContent();
                _right.ScrollToSelection = false;
            }
            ImGui.EndChild();

            ImGui.EndTable();
        }

        RenderDialogs();

        if (_selectionDirty)
        {
            _selectionDirty = false;
            CopyCurrentSelectionTo(_newSelectionSnapshot);
            if (_newSelectionSnapshot != _oldSelectionSnapshot)
            {
                var undoManager = GetUndoManager();
                var action = new ChangeResourceSelectionAction(
                    this, _oldSelectionSnapshot, _newSelectionSnapshot);
                undoManager.PushAction(action);
            }
        }
    }

    public override void RenderContextMenuItems()
    {
        var entry = GetSelectedEntryForCursor();
        entry ??= _roots[_defaultRoot].Reflection.Root;

        RenderEntryContextMenuItems(entry);
    }

    public override bool IsUndoSupported() => true;

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        base.WriteIniSettings(output);

        var selectedRightPaths = string.Join(';', _right.SelectedPaths);

        IniHelpers.WriteIntToIni(output, "SelectedRoot", _left.SelectedRoot);
        IniHelpers.WriteStringToIni(output, "SelectedLeftPath", _left.SelectedPath);
        IniHelpers.WriteStringToIni(output, "LastSelectedRightPath", _right.LastSelectedPath);
        IniHelpers.WriteStringToIni(output, "SelectedRightPaths", selectedRightPaths);
    }

    public override void ReadIniSettings(string line)
    {
        base.ReadIniSettings(line);

        var selectedRoot = IniHelpers.ReadIntFromIni(line, "SelectedRoot");
        if (selectedRoot.HasValue)
            _left.SelectedRoot = selectedRoot.Value;

        var selectedLeftPath = IniHelpers.ReadStringFromIni(line, "SelectedLeftPath");
        if (selectedLeftPath is not null)
            SelectLeftPanel(selectedLeftPath);

        var lastSelectedRightPath = IniHelpers.ReadStringFromIni(line, "LastSelectedRightPath");
        if (lastSelectedRightPath is not null)
            SelectRightPanel(lastSelectedRightPath);

        var selectedRightPaths = IniHelpers.ReadStringFromIni(line, "SelectedRightPaths");
        if (selectedRightPaths is not null)
        {
            var selectedPaths = selectedRightPaths.Split(';', StringSplitOptions.RemoveEmptyEntries);
            _right.SelectedPaths.Clear();
            foreach (var path in selectedPaths)
                _right.SelectedPaths.Add(path);
            OnSelectionChanged();
        }
    }

    private readonly struct EntryReference : IComparable<EntryReference>
    {
        public EntryReference(int rootIndex, string resourcePath)
        {
            RootIndex = rootIndex;
            ResourcePath = resourcePath;
        }

        public int RootIndex { get; }
        public string ResourcePath { get; }

        public int CompareTo(EntryReference rhs)
        {
            var rootComparison = RootIndex.CompareTo(rhs.RootIndex);
            return rootComparison != 0
                ? rootComparison
                : string.CompareOrdinal(ResourcePath, rhs.ResourcePath);
        }
    }

    private sealed class ResourceRoot : IDisposable
    {
        public string Name = string.Empty;
        public readonly CString NameBytes = new();
        public bool OpenByDefault;
        public bool SupportCompositeFiles;
        public readonly List<string> WatchedDirectories = [];
        public string ActiveDirectory = string.Empty;

        public FileSystemReflection Reflection = null!;
        public Action<FileSystemReflection>? ListUpdatedCallback;

        public void Dispose()
        {
            if (ListUpdatedCallback is not null)
                Reflection.OnListUpdated -= ListUpdatedCallback;
            Reflection.Dispose();
            NameBytes.Dispose();
            WatchedDirectories.Clear();
        }
    }

    /// <summary>Per-entry cache for optimization.</summary>
    private sealed class CachedEntryData : IDisposable
    {
        public readonly CString ResourceName = new();
        public readonly CString TreeDisplayName = new();
        public readonly CString SimpleDisplayName = new();
        public readonly CString CompositeDisplayName = new();
        public bool IsFileNameIgnored;

        public void Dispose()
        {
            CompositeDisplayName.Dispose();
            SimpleDisplayName.Dispose();
            TreeDisplayName.Dispose();
            ResourceName.Dispose();
        }
    }

    private void InitializeRoots()
    {
        var project = GetProject();

        {
            var root = new ResourceRoot
            {
                Name = "CoreData",
                ActiveDirectory = project.CoreDataPath
            };
            root.NameBytes.Set(root.Name);
            root.WatchedDirectories.Add(project.CoreDataPath);
            _roots.Add(root);
        }

        {
            var root = new ResourceRoot
            {
                Name = "Data",
                ActiveDirectory = project.DataPath,
                OpenByDefault = true,
                SupportCompositeFiles = true
            };
            root.NameBytes.Set(root.Name);
            root.WatchedDirectories.Add(project.DataPath);
            root.WatchedDirectories.Add(project.CachePath);
            _roots.Add(root);
        }

        var knownDirectories = new HashSet<string>(StringComparer.Ordinal)
        {
            project.CoreDataPath,
            project.DataPath
        };
        foreach (var resourceDirectory in project.ResourceDirectories)
        {
            if (!knownDirectories.Add(resourceDirectory))
                continue;

            var displayName = Path.GetFileName(RemoveTrailingSlash(resourceDirectory));
            if (displayName.Length == 0)
                displayName = resourceDirectory;
            var root = new ResourceRoot
            {
                // Equal leaf names need distinct ImGui identifiers.
                Name = $"{displayName}###{resourceDirectory}",
                ActiveDirectory = resourceDirectory
            };
            root.NameBytes.Set(root.Name);
            root.WatchedDirectories.Add(resourceDirectory);
            _roots.Add(root);
        }

        _defaultRoot = 1;

        foreach (var root in _roots)
        {
            root.Reflection = new FileSystemReflection(root.WatchedDirectories);
            root.ListUpdatedCallback = _ => RefreshContents();
            root.Reflection.OnListUpdated += root.ListUpdatedCallback;
        }
    }

    private void InitializeDefaultFactories()
    {
        AddFactory(new SimpleResourceFactory(
            int.MinValue, $"{Icons.Folder} Folder", "New Folder",
            static (fileName, _) => Directory.CreateDirectory(fileName)));
    }

    private void InitializeHotkeys()
    {
        BindHotkey(HotkeyDelete, DeleteSelected);
        BindHotkey(HotkeyRename, RenameSelected);
        BindHotkey(HotkeyRevealInExplorer, RevealInExplorerSelected);
    }

    private void OnProjectRequest(EditorTab? sender, ProjectRequest request)
    {
        if (ReferenceEquals(sender, this))
            return;

        if (request is OpenResourceRequest openResourceRequest)
        {
            var descriptor = openResourceRequest.Resource;
            if (FindLeftPanelEntry(descriptor.ResourceName) is { } entry)
            {
                SelectLeftPanel(entry.ResourceName);
                if (!descriptor.IsDirectory)
                    SelectRightPanel(descriptor.ResourceName);
                ScrollToSelection();
            }
        }
        else if (request is CreateResourceRequest createResourceRequest)
        {
            if (_create.Factory is null)
            {
                if (GetCurrentFolderEntry() is { } entry)
                    BeginEntryCreate(entry, createResourceRequest.Factory);
            }
        }
    }

    private FileSystemEntry? FindLeftPanelEntry(string resourceName)
    {
        var root = _roots[_left.SelectedRoot];
        var entry = root.Reflection.FindEntry(resourceName);
        while (entry is not null && (entry.IsFile || IsEntryFromCache(entry)))
            entry = entry.Parent;
        return entry;
    }

    private CachedEntryData GetCachedEntryData(FileSystemEntry entry)
    {
        if (_cachedEntryData.TryGetValue(entry, out var cached))
            return cached;

        var result = new CachedEntryData();
        result.ResourceName.Set(entry.ResourceName);
        result.TreeDisplayName.Set(entry.LocalName);
        result.SimpleDisplayName.FormatInPlace($"{GetEntryIcon(entry, false)} {entry.LocalName}");
        result.CompositeDisplayName.FormatInPlace($"{GetEntryIcon(entry, true)} {entry.LocalName}");
        result.IsFileNameIgnored = GetProject().IsFileNameIgnored(entry.LocalName);
        _cachedEntryData[entry] = result;
        return result;
    }

    /// <summary>Render left panel.</summary>
    private void RenderDirectoryTree(FileSystemEntry entry, ReadOnlySpan<byte> displayedName)
    {
        var project = GetProject();
        if (IsFileNameIgnored(entry, project, entry.LocalName))
            return;

        using var guard = new IdScopeGuard(displayedName);

        var rootIndex = GetRootIndex(entry);
        var root = _roots[rootIndex];

        // Open tree node if child is selected
        if (_left.ScrollToSelection && rootIndex == _left.SelectedRoot
            && _left.SelectedPath.StartsWith(entry.ResourceName, StringComparison.Ordinal))
        {
            if (_left.SelectedPath != entry.ResourceName)
                ImGui.SetNextItemOpen(true);
            ImGui.SetScrollHereY();
        }

        // Render the element itself
        var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnArrow
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnDoubleClick
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanFullWidth;

        if (IsLeafDirectory(entry))
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;
        if (entry.ResourceName == _left.SelectedPath && rootIndex == _left.SelectedRoot)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSelected;
        if (entry.ResourceName.Length == 0 && root.OpenByDefault)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen;

        var isOpen = ImGui.TreeNodeEx(displayedName, flags);

        // Process clicking
        var isContextMenuOpen = ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonRight);
        if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
            SelectLeftPanel(entry.ResourceName, rootIndex);

        // Process drag&drop from this element
        if (ImGui.BeginDragDropSource())
        {
            BeginEntryDrag(entry);
            ImGui.EndDragDropSource();
        }

        // Process drag&drop to this element
        if (ImGui.BeginDragDropTarget())
        {
            DropPayloadToFolder(entry);
            ImGui.EndDragDropTarget();
        }

        // Render children
        if (isOpen)
        {
            foreach (var childEntry in entry.Children)
            {
                if (IsNormalDirectory(childEntry))
                {
                    var cached = GetCachedEntryData(childEntry);
                    RenderDirectoryTree(childEntry, cached.TreeDisplayName.ContentBytes);
                }
            }
            ImGui.TreePop();
        }

        if (isContextMenuOpen)
            ImGui.OpenPopup(ContextMenuId);

        // Render context menu and popups
        RenderEntryContextMenu(entry);
    }

    /// <summary>Render right panel.</summary>
    private void RenderDirectoryContent()
    {
        var root = _roots[_left.SelectedRoot];
        var entry = root.Reflection.FindEntry(_left.SelectedPath);
        if (entry is null)
            return;

        RenderCreateButton(entry);

        if (entry.ResourceName.Length != 0)
            RenderDirectoryUp(entry);

        foreach (var childEntry in entry.Children)
        {
            if (!childEntry.IsFile)
                RenderDirectoryContentEntry(childEntry);
        }

        foreach (var childEntry in entry.Children)
        {
            if (childEntry.IsFile)
                RenderDirectoryContentEntry(childEntry);
        }
    }

    private static readonly CString _directoryUpLabel = new($"{Icons.FolderOpen} [..]");

    private void RenderDirectoryUp(FileSystemEntry entry)
    {
        using var guard = new IdScopeGuard(".."u8);

        // Render the element itself
        var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnDoubleClick
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanFullWidth
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;

        var isOpen = ImGui.TreeNodeEx(_directoryUpLabel, flags);

        if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            var separatorPosition = _left.SelectedPath.LastIndexOf('/');
            var newSelection = separatorPosition >= 0
                ? _left.SelectedPath[..separatorPosition]
                : string.Empty;
            SelectLeftPanel(newSelection);
            ScrollToSelection();
        }

        if (isOpen)
            ImGui.TreePop();

        // Process drag&drop to this element
        if (ImGui.BeginDragDropTarget())
        {
            Debug.Assert(entry.Parent is not null);
            DropPayloadToFolder(entry.Parent!);
            ImGui.EndDragDropTarget();
        }
    }

    private void RenderDirectoryContentEntry(FileSystemEntry entry)
    {
        var project = GetProject();
        if (IsFileNameIgnored(entry, project, entry.LocalName))
            return;

        var cached = GetCachedEntryData(entry);
        using var guard = new IdScopeGuard(cached.TreeDisplayName.ContentBytes);

        var (isCompositeFile, satelliteDirectory) = IsCompositeFile(entry);
        var isNormalDirectory = IsNormalDirectory(entry);
        var isNormalFile = !entry.IsDirectory && !isCompositeFile;
        var isSelected = IsRightSelected(entry.ResourceName);

        if (!isNormalDirectory && !isNormalFile && !isCompositeFile)
            return;

        // Scroll to selection if requested
        if (_right.ScrollToSelection && isSelected)
            ImGui.SetScrollHereY();

        // Render the element itself
        var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnArrow
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanFullWidth;
        if (isSelected)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSelected;
        if (!isCompositeFile)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;

        var isOpen = ImGui.TreeNodeEx(GetDisplayName(entry, isCompositeFile), flags);
        var isContextMenuOpen = ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonRight);
        var toggleSelection = IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl);

        if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft) && ImGui.IsItemToggledOpen())
            _ignoreNextMouseRelease = true;

        if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            if (isNormalDirectory)
            {
                SelectLeftPanel(entry.ResourceName);
                ScrollToSelection();
            }
            else if (isNormalFile || isCompositeFile)
            {
                ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
                OpenEntryInEditor(entry);
            }
        }
        else if (ImGui.IsItemHovered()
            && ImGui.IsMouseReleased(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && !ImGui.IsMouseDragPastThreshold(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            if (_ignoreNextMouseRelease)
                _ignoreNextMouseRelease = false;
            else
                ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
        }
        else if (isContextMenuOpen)
        {
            if (!IsRightSelected(entry.ResourceName))
            {
                _suppressInspector = true;
                ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
                _suppressInspector = false;
            }
        }

        // Process drag&drop from this element
        if (ImGui.BeginDragDropSource())
        {
            if (!IsRightSelected(entry.ResourceName))
            {
                _suppressInspector = true;
                ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
                _suppressInspector = false;
            }

            BeginRightSelectionDrag();
            ImGui.EndDragDropSource();
        }

        // Process drag&drop to this element only if directory
        if (isNormalDirectory && ImGui.BeginDragDropTarget())
        {
            DropPayloadToFolder(entry);
            ImGui.EndDragDropTarget();
        }

        // Render children if any
        if (isOpen)
        {
            if (isCompositeFile)
                RenderCompositeFile(entry, satelliteDirectory);
            ImGui.TreePop();
        }

        if (isContextMenuOpen)
            ImGui.OpenPopup(ContextMenuId);

        // Render context menu and popups
        RenderEntryContextMenu(entry);
    }

    private void RenderCompositeFile(FileSystemEntry entry, FileSystemEntry? satelliteDirectory)
    {
        _tempEntryList.Clear();
        CollectCompositeFiles(entry, entry);
        if (satelliteDirectory is not null)
            CollectCompositeFiles(satelliteDirectory, satelliteDirectory);

        _tempEntryList.Sort(static (lhs, rhs) =>
        {
            if (lhs.LocalName == rhs.LocalName)
                return 0;
            return FileSystemEntry.ComparePathFilesFirst(lhs.LocalName, rhs.LocalName) ? -1 : 1;
        });

        foreach (var item in _tempEntryList)
            RenderCompositeFileEntry(item.Entry, item.LocalName);
    }

    private void CollectCompositeFiles(FileSystemEntry entry, FileSystemEntry root)
    {
        if (!ReferenceEquals(entry, root) && entry.IsFile)
        {
            var localResourceName = entry.ResourceName[(root.ResourceName.Length + 1)..];
            _tempEntryList.Add(new TempEntry(entry, localResourceName));
        }

        if (!entry.IsDirectory)
            return;

        foreach (var child in entry.Children)
            CollectCompositeFiles(child, root);
    }

    private void RenderCompositeFileEntry(FileSystemEntry entry, string localResourceName)
    {
        var project = GetProject();
        if (IsFileNameIgnored(entry, project, localResourceName))
            return;

        var cached = GetCachedEntryData(entry);
        using var guard = new IdScopeGuard(cached.ResourceName.ContentBytes);

        // Render the element itself
        var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnDoubleClick
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanFullWidth
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;
        if (IsRightSelected(entry.ResourceName))
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSelected;

        // TODO: Cache this string
        _textBuffer.FormatInPlace($"{GetEntryIcon(entry, false)} {localResourceName}");

        var isOpen = ImGui.TreeNodeEx(_textBuffer.ContentBytes, flags);
        var isContextMenuOpen = ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonRight);
        var toggleSelection = IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl);

        if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
            OpenEntryInEditor(entry);
        }
        else if (ImGui.IsItemHovered()
            && ImGui.IsMouseReleased(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && !ImGui.IsMouseDragPastThreshold(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
        }
        else if (isContextMenuOpen)
        {
            if (!IsRightSelected(entry.ResourceName))
            {
                _suppressInspector = true;
                ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
                _suppressInspector = false;
            }
        }

        // Process drag&drop from this element
        if (ImGui.BeginDragDropSource())
        {
            if (!IsRightSelected(entry.ResourceName))
            {
                _suppressInspector = true;
                ChangeRightPanelSelection(entry.ResourceName, toggleSelection);
                _suppressInspector = false;
            }

            BeginRightSelectionDrag();
            ImGui.EndDragDropSource();
        }

        if (isOpen)
            ImGui.TreePop();

        if (isContextMenuOpen)
            ImGui.OpenPopup(ContextMenuId);

        // Render context menu and popups
        RenderEntryContextMenu(entry);
    }

    private static readonly CString _createButtonLabel = new($"{Icons.SquarePlus} Create...");

    private void RenderCreateButton(FileSystemEntry entry)
    {
        ImGui.Indent();
        if (ImGui.Button(_createButtonLabel))
            ImGui.OpenPopup("##CreateButtonPopup"u8);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Create new file or directory"u8);
        ImGui.Unindent();

        if (ImGui.BeginPopup("##CreateButtonPopup"u8))
        {
            var createPending = RenderEntryCreateContextMenu(entry);
            if (createPending.HasValue && createPending.Value < _factories.Count)
                BeginEntryCreate(entry, _factories[createPending.Value]);
            ImGui.EndPopup();
        }
    }

    /// <summary>Common rendering.</summary>
    private void RenderDialogs()
    {
        if (_delete.OpenPending)
            ImGui.OpenPopup(_delete.PopupTitleBytes.ContentBytes);

        if (_rename.OpenPending)
            ImGui.OpenPopup(_rename.PopupTitleBytes.ContentBytes);

        if (_create.OpenPending)
            ImGui.OpenPopup(_create.PopupTitleBytes.ContentBytes);

        RenderRenameDialog();
        RenderDeleteDialog();
        RenderCreateDialog();
    }

    private void RenderEntryContextMenu(FileSystemEntry entry)
    {
        if (ImGui.BeginPopup(ContextMenuId))
        {
            RenderEntryContextMenuItems(entry);
            ImGui.EndPopup();
        }
    }

    private void RenderEntryContextMenuItems(FileSystemEntry entry)
    {
        var needSeparator = false;

        var root = GetRoot(entry);

        if (!entry.IsFile && !IsEntryFromCache(entry))
        {
            needSeparator = true;
            if (ImGui.BeginMenu("Create"u8))
            {
                var index = RenderEntryCreateContextMenu(entry);
                if (index.HasValue)
                    BeginEntryCreate(entry, _factories[index.Value]);
                ImGui.EndMenu();
            }
        }

        if (needSeparator)
            ImGui.Separator();

        if (ImGui.MenuItem("Open"u8) && entry.ResourceName.Length != 0)
            OpenEntryInEditor(entry);

        _hotkeyLabel.Set(GetHotkeyLabel(HotkeyRevealInExplorer));
        if (ImGui.MenuItem("Reveal in Explorer"u8, _hotkeyLabel.ContentBytes))
        {
            if (entry.ResourceName.Length == 0)
                RevealInExplorer(root.ActiveDirectory);
            else
                RevealInExplorer(entry.AbsolutePath);
        }

        if (ImGui.MenuItem("Copy Absolute Path"u8))
        {
            _textBuffer.Set(entry.AbsolutePath);
            ImGui.SetClipboardText(_textBuffer.ContentBytes);
        }

        if (ImGui.MenuItem("Copy Resource Name"u8))
        {
            _textBuffer.Set(entry.ResourceName);
            ImGui.SetClipboardText(_textBuffer.ContentBytes);
        }

        ImGui.Separator();

        var isEditable = entry.ResourceName.Length != 0 && !IsEntryFromCache(entry);
        ImGui.BeginDisabled(!isEditable);
        _hotkeyLabel.Set(GetHotkeyLabel(HotkeyRename));
        if (ImGui.MenuItem("Rename"u8, _hotkeyLabel.ContentBytes))
            BeginEntryRename(entry);

        _hotkeyLabel.Set(GetHotkeyLabel(HotkeyDelete));
        if (ImGui.MenuItem("Delete"u8, _hotkeyLabel.ContentBytes))
            BeginEntryDelete(entry);
        ImGui.EndDisabled();
    }

    private int? RenderEntryCreateContextMenu(FileSystemEntry entry)
    {
        int? result = null;

        if (_sortFactories)
        {
            _factories.Sort(static (lhs, rhs) =>
            {
                var groupComparison = lhs.Group.CompareTo(rhs.Group);
                return groupComparison != 0
                    ? groupComparison
                    : string.CompareOrdinal(lhs.Title, rhs.Title);
            });
            _sortFactories = false;
        }

        int? previousGroup = null;
        for (var index = 0; index < _factories.Count; ++index)
        {
            using var guard = new IdScopeGuard(index);
            var factory = _factories[index];

            if (previousGroup.HasValue && previousGroup.Value != factory.Group)
                ImGui.Separator();
            previousGroup = factory.Group;

            var isEnabled = factory.IsEnabled(entry);
            ImGui.BeginDisabled(!isEnabled);
            _textBuffer.Set(factory.Title);
            if (ImGui.MenuItem(_textBuffer.ContentBytes))
                result = index;
            ImGui.EndDisabled();
        }

        return result;
    }

    private static readonly CString _renameLabel = new($"{Icons.Check} Rename");
    private static readonly CString _renameCancelLabel = new($"{Icons.Ban} Cancel");

    private void RenderRenameDialog()
    {
        var entry = GetEntry(_rename.EntryRef);
        if (entry?.Parent is null)
            return;

        if (!ImGui.BeginPopupModal(
                _rename.PopupTitleBytes.ContentBytes,
                flags: ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize))
        {
            return;
        }

        var justOpened = _rename.OpenPending;
        _rename.OpenPending = false;

        var (isEnabled, extraLine) = IsFileNameAvailable(
            entry.Parent, entry.LocalName, _rename.InputBuffer);
        _ = extraLine;
        // The native format string has one placeholder and intentionally ignores extraLine.
        _dialogText.FormatInPlace($"Would you like to rename '{entry.AbsolutePath}'?\n");
        ImGui.TextUnformatted(_dialogText.ContentBytes);

        if (justOpened)
            ImGui.SetKeyboardFocusHere();
        var done = Widgets.InputText(
            "##Rename"u8,
            ref _rename.InputBuffer,
            ImGuiInputTextFlags.ImGuiInputTextFlagsAutoSelectAll
                | ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue);

        ImGui.BeginDisabled(!isEnabled);
        if (ImGui.Button(_renameLabel) || (isEnabled && done))
        {
            if (_rename.InputBuffer != entry.LocalName)
                RenameEntry(entry, _rename.InputBuffer);
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(_renameCancelLabel) || IsKeyPressed(Key.Escape))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private static readonly CString _deleteLabel = new($"{Icons.Check} Delete");
    private static readonly CString _deleteCancelLabel = new($"{Icons.Ban} Cancel");

    private void RenderDeleteDialog()
    {
        var entries = GetEntries(_delete.EntryRefs);
        if (entries.Count == 0)
            return;

        if (!ImGui.BeginPopupModal(
                _delete.PopupTitleBytes.ContentBytes,
                flags: ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize))
        {
            return;
        }

        //var justOpened = _delete.OpenPending;
        _delete.OpenPending = false;

        _dialogBuilder.Clear();
        if (entries.Count == 1)
        {
            _dialogBuilder.Append("Are you sure you want to PERMANENTLY delete '");
            _dialogBuilder.Append(entries[0].AbsolutePath);
            _dialogBuilder.Append("'?\n");
        }
        else
        {
            _dialogBuilder.Append("Are you sure you want to PERMANENTLY delete ");
            _dialogBuilder.Append(entries.Count);
            _dialogBuilder.Append(" items?\n\n");
            var maxItems = Math.Min(10, entries.Count);
            for (var index = 0; index < maxItems; ++index)
            {
                _dialogBuilder.Append("* ");
                _dialogBuilder.Append(entries[index].AbsolutePath);
                _dialogBuilder.Append('\n');
            }
            if (maxItems < entries.Count)
            {
                _dialogBuilder.Append("(and ");
                _dialogBuilder.Append(entries.Count - maxItems);
                _dialogBuilder.Append(" more items)\n");
            }
            _dialogBuilder.Append('\n');
        }
        _dialogBuilder.Append(Icons.TriangleExclamation);
        _dialogBuilder.Append(" This action cannot be undone!");
        _dialogText.Set(_dialogBuilder.ToString());
        ImGui.TextUnformatted(_dialogText.ContentBytes);

        if (ImGui.Button(_deleteLabel) || IsKeyPressed(Key.Return))
        {
            foreach (var entry in entries)
            {
                if (entry.Parent is not null)
                    DeleteEntry(entry);
            }
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();

        if (ImGui.Button(_deleteCancelLabel) || IsKeyPressed(Key.Escape))
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private static readonly CString _createLabel = new($"{Icons.Check} Create");
    private static readonly CString _createCancelLabel = new($"{Icons.Ban} Cancel");

    private void RenderCreateDialog()
    {
        var parentEntry = GetEntry(_create.ParentEntryRef);
        if (parentEntry is null || _create.Factory is null)
            return;

        if (!ImGui.BeginPopupModal(
                _create.PopupTitleBytes.ContentBytes,
                flags: ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize))
        {
            return;
        }

        var justOpened = _create.OpenPending;
        _create.OpenPending = false;

        if (justOpened)
        {
            var baseFilePath = parentEntry.AbsolutePath.Length == 0
                ? GetRoot(parentEntry).ActiveDirectory
                : parentEntry.AbsolutePath + "/";
            var baseResourcePath = parentEntry.ResourceName.Length == 0
                ? string.Empty
                : parentEntry.ResourceName + "/";

            _create.Factory.Open(baseFilePath, baseResourcePath);
        }

        var canCommit = false;
        var shouldCommit = false;
        _create.Factory.Render(_checkFileNameCallback, ref canCommit, ref shouldCommit);

        ImGui.BeginDisabled(!canCommit);
        if (ImGui.Button(_createLabel) || (canCommit && shouldCommit))
        {
            _create.Factory.CommitAndClose();
            _create.Reset();
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();

        if (ImGui.Button(_createCancelLabel) || IsKeyPressed(Key.Escape))
        {
            _create.Factory.DiscardAndClose();
            _create.Reset();
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    /// <summary>Drag&amp;drop handling.</summary>
    private void AddEntryToPayload(ResourceDragDropPayload payload, FileSystemEntry entry)
    {
        var project = GetProject();
        var descriptor = project.GetResourceDescriptor(entry.ResourceName, entry.AbsolutePath);
        payload.Resources.Add(descriptor);
    }

    private ResourceDragDropPayload CreatePayloadFromEntry(FileSystemEntry entry)
    {
        var payload = new ResourceDragDropPayload();
        AddEntryToPayload(payload, entry);
        return payload;
    }

    private ResourceDragDropPayload CreatePayloadFromRightSelection()
    {
        var payload = new ResourceDragDropPayload();

        foreach (var resourcePath in _right.SelectedPaths)
        {
            var entry = GetEntry(new EntryReference(_left.SelectedRoot, resourcePath));
            if (entry is not null)
            {
                AddEntryToPayload(payload, entry);
                var (_, satelliteDirectory) = IsCompositeFile(entry);
                if (satelliteDirectory is not null && !IsEntryFromCache(satelliteDirectory))
                    AddEntryToPayload(payload, satelliteDirectory);
            }
        }

        // Last selected resource is the first in the payload
        var lastSelectedIndex = payload.Resources.FindIndex(
            descriptor => descriptor.ResourceName == _right.LastSelectedPath);
        if (lastSelectedIndex >= 0)
        {
            (payload.Resources[0], payload.Resources[lastSelectedIndex]) =
                (payload.Resources[lastSelectedIndex], payload.Resources[0]);
        }

        return payload;
    }

    private DragDropPayload CreateEntryPayload()
    {
        Debug.Assert(_dragEntry is not null);
        return CreatePayloadFromEntry(_dragEntry!);
    }

    private DragDropPayload CreateSelectionPayload() => CreatePayloadFromRightSelection();

    private void BeginEntryDrag(FileSystemEntry entry)
    {
        _dragEntry = entry;
        DragDropPayload.UpdateSource(_createEntryPayloadCallback);
    }

    private void BeginRightSelectionDrag()
    {
        DragDropPayload.UpdateSource(_createSelectionPayloadCallback);
    }

    private void DropPayloadToFolder(FileSystemEntry entry)
    {
        var root = GetRoot(entry);
        var payload = DragDropPayload.Get() as ResourceDragDropPayload;
        if (payload is not null && IsPayloadMovable(payload))
        {
            using var acceptedPayload = ImGui.AcceptDragDropPayload("DragDropPayload"u8);
            if (!acceptedPayload.IsNull)
            {
                var separator = entry.ResourceName.Length == 0 ? string.Empty : "/";
                foreach (var descriptor in payload.Resources)
                {
                    var newResourceName = entry.ResourceName + separator + descriptor.LocalName;
                    var newFileName = root.ActiveDirectory + newResourceName;
                    RenameOrMove(
                        descriptor.FileName, newFileName, descriptor.ResourceName, newResourceName);
                }
            }
        }
    }

    /// <summary>Utility functions.</summary>
    private ReadOnlySpan<byte> GetDisplayName(FileSystemEntry entry, bool isCompositeFile)
    {
        var cachedData = GetCachedEntryData(entry);
        return isCompositeFile
            ? cachedData.CompositeDisplayName.ContentBytes
            : cachedData.SimpleDisplayName.ContentBytes;
    }

    private bool IsFileNameIgnored(FileSystemEntry entry, Project project, string name)
    {
        _ = project;
        _ = name;
        var cachedData = GetCachedEntryData(entry);
        return cachedData.IsFileNameIgnored;
    }

    private static string GetEntryIcon(FileSystemEntry entry, bool isCompositeFile)
    {
        if (isCompositeFile)
            return Icons.FileZipper;
        if (!entry.IsFile)
            return Icons.Folder;
        if (!entry.IsDirectory)
            return Icons.File;
        return Icons.CircleQuestion;
    }

    private int GetRootIndex(FileSystemEntry entry)
    {
        var owner = entry.Owner;
        for (var index = 0; index < _roots.Count; ++index)
        {
            if (ReferenceEquals(_roots[index].Reflection, owner))
                return index;
        }
        return 0;
    }

    private ResourceRoot GetRoot(FileSystemEntry entry) => _roots[GetRootIndex(entry)];

    private static bool IsEntryFromCache(FileSystemEntry entry) => entry.DirectoryIndex > 0;

    private EntryReference GetReference(FileSystemEntry entry)
    {
        return new EntryReference(GetRootIndex(entry), entry.ResourceName);
    }

    private FileSystemEntry? GetEntry(EntryReference entryReference)
    {
        if (entryReference.RootIndex < _roots.Count)
        {
            var resourcePath = entryReference.ResourcePath ?? string.Empty;
            return _roots[entryReference.RootIndex].Reflection.FindEntry(resourcePath);
        }
        return null;
    }

    private FileSystemEntry? GetSelectedEntryForCursor()
    {
        return GetEntry(new EntryReference(_left.SelectedRoot, _cursor.SelectedPath));
    }

    private FileSystemEntry? GetCurrentFolderEntry()
    {
        return GetEntry(new EntryReference(_left.SelectedRoot, _left.SelectedPath));
    }

    private (bool IsValid, string ExtraLine) IsFileNameValid(string name)
    {
        var isEmptyName = name.Length == 0;
        var isInvalidName = GetSanitizedName(name) != name;

        if (isInvalidName)
            return (false, $"{Icons.TriangleExclamation} Name contains forbidden characters");
        if (isEmptyName)
            return (false, $"{Icons.TriangleExclamation} Name must not be empty");
        return (true, $"{Icons.CircleCheck} Name is OK");
    }

    private (bool IsValid, string ExtraLine) IsFileNameAvailable(
        FileSystemEntry parentEntry, string oldName, string newName)
    {
        var isUsedName = newName != oldName && parentEntry.FindChild(newName) is not null;
        if (isUsedName)
            return (false, $"{Icons.TriangleExclamation} File or directory with this name already exists");

        return IsFileNameValid(newName);
    }

    private List<FileSystemEntry> GetEntries(List<EntryReference> entryReferences)
    {
        _ = entryReferences;
        _entryList.Clear();
        // Keep native behavior: this method intentionally reads delete_.entryRefs_.
        foreach (var entryReference in _delete.EntryRefs)
        {
            var entry = GetEntry(entryReference);
            if (entry is not null)
                _entryList.Add(entry);
        }
        return _entryList;
    }

    private int? GetRootIndex(string fileName)
    {
        for (var rootIndex = 0; rootIndex < _roots.Count; ++rootIndex)
        {
            var root = _roots[rootIndex];
            foreach (var rootPath in root.WatchedDirectories)
            {
                if (fileName.StartsWith(rootPath, StringComparison.Ordinal))
                    return rootIndex;
            }
        }
        return null;
    }

    private static bool IsNormalDirectory(FileSystemEntry entry)
    {
        if (entry.IsFile)
            return false;
        if (entry.Parent is not null
            && entry.ResourceName.EndsWith(SatelliteDirectoryExtension, StringComparison.Ordinal))
        {
            var primaryFileName = entry.LocalName[..^SatelliteDirectoryExtension.Length];
            var primaryFileEntry = entry.Parent.FindChild(primaryFileName);
            if (primaryFileEntry?.IsFile == true)
                return false;
        }
        return true;
    }

    private static bool IsLeafDirectory(FileSystemEntry entry)
    {
        foreach (var childEntry in entry.Children)
        {
            if (IsNormalDirectory(childEntry))
                return false;
        }
        return true;
    }

    private (bool IsCompositeFile, FileSystemEntry? SatelliteDirectory) IsCompositeFile(
        FileSystemEntry entry)
    {
        var root = GetRoot(entry);
        if (!root.SupportCompositeFiles || !entry.IsFile)
            return (false, null);

        FileSystemEntry? satelliteDirectoryEntry = null;
        if (entry.Parent is not null)
        {
            var otherEntry = entry.Parent.FindChild(entry.LocalName + SatelliteDirectoryExtension);
            if (otherEntry?.IsDirectory == true)
                satelliteDirectoryEntry = otherEntry;
        }

        var isCompositeFile = satelliteDirectoryEntry is not null || entry.IsDirectory;
        return (isCompositeFile, satelliteDirectoryEntry);
    }

    private (bool IsValid, string ExtraLine) CheckFileName(string filePath, string fileName)
    {
        var fullFileName = AddTrailingSlash(filePath) + fileName;
        if (SystemFile.Exists(fullFileName) || Directory.Exists(fullFileName))
        {
            return (false, $"{Icons.TriangleExclamation} File or directory with this name already exists");
        }

        return IsFileNameValid(fileName);
    }

    /// <summary>Manage selection.</summary>
    private bool IsRightSelected(string path) => _right.SelectedPaths.Contains(path);

    private void SelectLeftPanel(string path, int? rootIndex = null)
    {
        var newPath = RemoveTrailingSlash(path);
        var newRoot = rootIndex ?? _left.SelectedRoot;

        if (newPath == _left.SelectedPath && newRoot == _left.SelectedRoot)
            return;

        _left.SelectedPath = RemoveTrailingSlash(path);
        _left.SelectedRoot = rootIndex ?? _left.SelectedRoot;

        _right.LastSelectedPath = string.Empty;
        _right.SelectedPaths.Clear();

        _cursor.SelectedPath = path;
        _cursor.IsLeftPanel = true;

        OnSelectionChanged();
    }

    private void SelectRightPanel(string path, bool clearSelection = true)
    {
        _right.LastSelectedPath = RemoveTrailingSlash(path);

        if (clearSelection)
            _right.SelectedPaths.Clear();
        if (_right.LastSelectedPath.Length != 0)
        {
            _right.SelectedPaths.Add(_right.LastSelectedPath);
            _cursor.SelectedPath = _right.LastSelectedPath;
            _cursor.IsLeftPanel = false;
        }

        OnSelectionChanged();
    }

    private void DeselectRightPanel(string path)
    {
        _right.SelectedPaths.Remove(path);

        if (_right.LastSelectedPath == path)
            _right.LastSelectedPath = GetFirstOrEmpty(_right.SelectedPaths);

        if (_cursor.SelectedPath == path)
            _cursor.SelectedPath = _right.LastSelectedPath;

        OnSelectionChanged(true);
    }

    private void ChangeRightPanelSelection(string path, bool toggleSelection)
    {
        if (toggleSelection && IsRightSelected(path))
            DeselectRightPanel(path);
        else
            SelectRightPanel(path, !toggleSelection);
    }

    private void AdjustSelectionOnRename(
        int oldRootIndex, string oldResourceName, int newRootIndex, string newResourceName)
    {
        if (_left.SelectedRoot != oldRootIndex)
            return;

        // Cache results because following calls may change values
        var lastSelectedRightPath = _right.LastSelectedPath;
        _selectedPathList.Clear();
        _selectedPathList.AddRange(_right.SelectedPaths);

        if (TryAdjustPathOnRename(_left.SelectedPath, oldResourceName, newResourceName) is { } newLeftPath)
            SelectLeftPanel(newLeftPath, newRootIndex);

        if (TryAdjustPathOnRename(lastSelectedRightPath, oldResourceName, newResourceName) is { } newLastPath)
            SelectRightPanel(newLastPath);

        for (var index = 0; index < _selectedPathList.Count; ++index)
        {
            if (TryAdjustPathOnRename(
                    _selectedPathList[index], oldResourceName, newResourceName) is { } newPath)
            {
                _selectedPathList[index] = newPath;
            }
        }
        _right.SelectedPaths.Clear();
        foreach (var path in _selectedPathList)
            _right.SelectedPaths.Add(path);

        ScrollToSelection();
    }

    private void ScrollToSelection()
    {
        _left.ScrollToSelection = true;
        _right.ScrollToSelection = true;
    }

    private void OnSelectionChanged(bool sendEmptyEvent = false)
    {
        _selectionDirty = true;
        if (!_suppressInspector && (sendEmptyEvent || _right.SelectedPaths.Count != 0))
        {
            var project = GetProject();
            var request = new InspectResourceRequest(_right.SelectedPaths);
            project.ProcessRequest(request, this);
        }
    }

    private void CopyCurrentSelectionTo(Selection selection)
    {
        selection.SelectedRoot = _left.SelectedRoot;
        selection.SelectedLeftPath = _left.SelectedPath;
        selection.SelectedRightPaths.Clear();
        foreach (var path in _right.SelectedPaths)
            selection.SelectedRightPaths.Add(path);
    }

    private static string GetFirstOrEmpty(HashSet<string> values)
    {
        foreach (var value in values)
            return value;
        return string.Empty;
    }

    /// <summary>Manipulation utilities and helpers.</summary>
    private void BeginEntryDelete(FileSystemEntry entry)
    {
        _delete.EntryRefs.Clear();
        _delete.EntryRefs.Add(GetReference(entry));
        _delete.PopupTitle = $"Delete '{entry.LocalName}'?##DeleteDialog";
        _delete.PopupTitleBytes.Set(_delete.PopupTitle);
        _delete.OpenPending = true;
    }

    private void BeginRightSelectionDelete()
    {
        _delete.EntryRefs.Clear();
        foreach (var resourcePath in _right.SelectedPaths)
            _delete.EntryRefs.Add(new EntryReference(_left.SelectedRoot, resourcePath));
        _delete.EntryRefs.Sort();

        _delete.PopupTitle = $"Delete {_right.SelectedPaths.Count} items?##DeleteDialog";
        _delete.PopupTitleBytes.Set(_delete.PopupTitle);
        _delete.OpenPending = true;
    }

    private void BeginEntryRename(FileSystemEntry entry)
    {
        _rename.EntryRef = GetReference(entry);
        _rename.PopupTitle = $"Rename '{entry.LocalName}'?##RenameDialog";
        _rename.PopupTitleBytes.Set(_rename.PopupTitle);
        _rename.InputBuffer = entry.LocalName;
        _rename.OpenPending = true;
    }

    private void BeginEntryCreate(FileSystemEntry entry, ResourceFactory factory)
    {
        if (entry.IsFile)
            return;

        _create.ParentEntryRef = GetReference(entry);
        _create.PopupTitle = $"Create {factory.Title}...##CreateDialog";
        _create.PopupTitleBytes.Set(_create.PopupTitle);
        _create.Factory = factory;
        _create.OpenPending = true;
    }

    private void RefreshContents()
    {
        ScrollToSelection();
        _waitingForUpdate = false;
        ClearCachedEntryData();
    }

    private static void RevealInExplorer(string path)
    {
        var startInfo = new ProcessStartInfo { UseShellExecute = false };
#if REDBOLT_PLATFORM_WINDOWS
        startInfo.FileName = "explorer.exe";
        startInfo.ArgumentList.Add("/select,");
        startInfo.ArgumentList.Add(path);
#else
        {
            startInfo.FileName = "xdg-open";
            startInfo.ArgumentList.Add(Directory.Exists(path) ? path : GetPath(path));
        }
#endif
        Process.Start(startInfo)?.Dispose();
    }

    private void RenameEntry(FileSystemEntry entry, string newName)
    {
        var newFileName = GetPath(entry.AbsolutePath) + newName;
        var newResourceName = GetPath(entry.ResourceName) + newName;
        RenameOrMove(entry.AbsolutePath, newFileName, entry.ResourceName, newResourceName);

        var (_, satelliteDirectory) = IsCompositeFile(entry);
        if (satelliteDirectory is not null && !IsEntryFromCache(satelliteDirectory))
            RenameEntry(satelliteDirectory, newName + SatelliteDirectoryExtension);
    }

    private void DeleteEntry(FileSystemEntry entry)
    {
        DeselectRightPanel(entry.ResourceName);

        var isFile = SystemFile.Exists(entry.AbsolutePath);
        var deleted = TryDelete(entry.AbsolutePath, isFile);

        if (deleted)
        {
            _waitingForUpdate = true;

            if (isFile)
                CleanupResourceCache(entry.ResourceName);
        }

        var (_, satelliteDirectory) = IsCompositeFile(entry);
        if (satelliteDirectory is not null && !IsEntryFromCache(satelliteDirectory))
            DeleteEntry(satelliteDirectory);
    }

    private void CleanupResourceCache(string resourceName)
    {
        var matchingDirectoryInCache = GetProject().CachePath + resourceName;
        if (Directory.Exists(matchingDirectoryInCache))
            Directory.Delete(matchingDirectoryInCache, true);
    }

    private void OpenEntryInEditor(FileSystemEntry entry)
    {
        var project = GetProject();
        var absolutePath = entry.AbsolutePath;

        var request = new OpenResourceRequest(entry.ResourceName);
        request.QueueProcessCallback(() => SystemOpen(absolutePath), int.MinValue);
        project.ProcessRequest(request, this);
    }

    private void ClearCachedEntryData()
    {
        foreach (var data in _cachedEntryData.Values)
            data.Dispose();
        _cachedEntryData.Clear();
    }

    private static bool TryRename(string oldFileName, string newFileName, bool isFile)
    {
        try
        {
            if (isFile)
                SystemFile.Move(oldFileName, newFileName);
            else
                Directory.Move(oldFileName, newFileName);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryDelete(string path, bool isFile)
    {
        try
        {
            if (isFile)
                SystemFile.Delete(path);
            else
                Directory.Delete(path, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return false;
        }
    }

    private static void SystemOpen(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
    }

    private static string GetPath(string value)
    {
        var separatorPosition = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        return separatorPosition >= 0 ? value[..(separatorPosition + 1)] : string.Empty;
    }

    private static string AddTrailingSlash(string value)
    {
        var result = value.Trim().Replace('\\', '/');
        if (result.Length != 0 && result[^1] != '/')
            result += '/';
        return result;
    }

    private static string RemoveTrailingSlash(string value)
    {
        var result = value.Trim().Replace('\\', '/');
        if (result.Length != 0 && result[^1] == '/')
            result = result[..^1];
        return result;
    }

    private static string GetSanitizedName(string value)
    {
        const string forbiddenCharacters = "<>:\"/\\|?*";
        StringBuilder? result = null;
        for (var index = 0; index < value.Length; ++index)
        {
            var character = value[index];
            if (character < 31 || forbiddenCharacters.Contains(character))
            {
                result ??= new StringBuilder(value);
                result[index] = character < 31 ? ' ' : '_';
            }
        }
        return result?.ToString() ?? value;
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private static bool IsKeyPressed(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyPressed(imguiKey);
    }

    private sealed class LeftPanel
    {
        public int SelectedRoot = 1;
        public string SelectedPath = string.Empty;
        public bool ScrollToSelection;
    }

    private sealed class RightPanel
    {
        public string LastSelectedPath = string.Empty;
        public readonly HashSet<string> SelectedPaths = new(StringComparer.Ordinal);
        public bool ScrollToSelection;
    }

    private sealed class CursorForHotkeys
    {
        public string SelectedPath = string.Empty;
        public bool IsLeftPanel;
    }

    private sealed class RenameDialog : IDisposable
    {
        public EntryReference EntryRef;
        public string PopupTitle = string.Empty;
        public readonly CString PopupTitleBytes = new();
        public string InputBuffer = string.Empty;
        public bool OpenPending;

        public void Dispose()
        {
            PopupTitleBytes.Dispose();
        }
    }

    private sealed class DeleteDialog : IDisposable
    {
        public readonly List<EntryReference> EntryRefs = [];
        public string PopupTitle = string.Empty;
        public readonly CString PopupTitleBytes = new();
        public bool OpenPending;

        public void Dispose()
        {
            EntryRefs.Clear();
            PopupTitleBytes.Dispose();
        }
    }

    private sealed class CreateDialog : IDisposable
    {
        public EntryReference ParentEntryRef;
        public string PopupTitle = string.Empty;
        public readonly CString PopupTitleBytes = new();
        public ResourceFactory? Factory;
        public bool OpenPending;

        public void Reset()
        {
            ParentEntryRef = default;
            PopupTitle = string.Empty;
            PopupTitleBytes.Clear();
            Factory = null;
            OpenPending = false;
        }

        public void Dispose()
        {
            Factory = null;
            PopupTitleBytes.Dispose();
        }
    }

    private readonly struct TempEntry
    {
        public TempEntry(FileSystemEntry entry, string localName)
        {
            Entry = entry;
            LocalName = localName;
        }

        public FileSystemEntry Entry { get; }
        public string LocalName { get; }
    }

    private readonly List<ResourceRoot> _roots = [];
    private int _defaultRoot;
    private bool _waitingForUpdate;

    private readonly List<ResourceFactory> _factories = [];
    private bool _sortFactories = true;

    private bool _suppressInspector;
    private bool _ignoreNextMouseRelease;

    private readonly LeftPanel _left = new();
    private readonly RightPanel _right = new();
    private readonly CursorForHotkeys _cursor = new();
    private readonly RenameDialog _rename = new();
    private readonly DeleteDialog _delete = new();
    private readonly CreateDialog _create = new();

    private readonly List<TempEntry> _tempEntryList = [];
    private bool _selectionDirty;

    private readonly Dictionary<FileSystemEntry, CachedEntryData> _cachedEntryData = [];

    private readonly List<FileSystemEntry> _entryList = [];
    private readonly List<string> _selectedPathList = [];
    private readonly Selection _oldSelectionSnapshot = new();
    private readonly Selection _newSelectionSnapshot = new();
    private readonly CString _hotkeyLabel = new();
    private readonly CString _textBuffer = new();
    private readonly CString _dialogText = new();
    private readonly StringBuilder _dialogBuilder = new();

    private readonly Action<Project> _projectInitializedCallback;
    private readonly ResourceFactory.FileNameChecker _checkFileNameCallback;
    private readonly DragDropPayload.CreateCallback _createEntryPayloadCallback;
    private readonly DragDropPayload.CreateCallback _createSelectionPayloadCallback;
    private FileSystemEntry? _dragEntry;
}

public class ChangeResourceSelectionAction : EditorAction
{
    public ChangeResourceSelectionAction(ResourceBrowserTab tab,
        ResourceBrowserTab.Selection oldSelection, ResourceBrowserTab.Selection newSelection)
    {
        _tab = new WeakReference<ResourceBrowserTab>(tab);
        _oldSelection = oldSelection.Clone();
        _newSelection = newSelection.Clone();
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool IsTransparent() => true;

    public override void Redo()
    {
        if (_tab.TryGetTarget(out var tab))
            tab.SetSelection(_newSelection);
    }

    public override void Undo()
    {
        if (_tab.TryGetTarget(out var tab))
            tab.SetSelection(_oldSelection);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeResourceSelectionAction otherAction)
            return false;

        _newSelection.CopyFrom(otherAction._newSelection);
        return true;
    }

    private readonly WeakReference<ResourceBrowserTab> _tab;
    private readonly ResourceBrowserTab.Selection _oldSelection;
    private readonly ResourceBrowserTab.Selection _newSelection;
}

public class RenameResourceAction : EditorAction
{
    public RenameResourceAction(ResourceBrowserTab tab,
        string oldFileName, string newFileName, string oldResourceName, string newResourceName)
    {
        _tab = new WeakReference<ResourceBrowserTab>(tab);
        _oldFileName = oldFileName;
        _newFileName = newFileName;
        _oldResourceName = oldResourceName;
        _newResourceName = newResourceName;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanRedo() => CanRenameTo(_newFileName);

    public override void Redo()
    {
        _tab.TryGetTarget(out var tab);
        tab!.RenameOrMove(
            _oldFileName, _newFileName, _oldResourceName, _newResourceName, true);
    }

    public override bool CanUndo() => CanRenameTo(_oldFileName);

    public override void Undo()
    {
        _tab.TryGetTarget(out var tab);
        tab!.RenameOrMove(
            _newFileName, _oldFileName, _newResourceName, _oldResourceName, true);
    }

    private bool CanRenameTo(string fileName)
    {
        return _tab.TryGetTarget(out _)
            && !SystemFile.Exists(fileName)
            && !Directory.Exists(fileName);
    }

    private readonly WeakReference<ResourceBrowserTab> _tab;
    private readonly string _oldFileName;
    private readonly string _newFileName;
    private readonly string _oldResourceName;
    private readonly string _newResourceName;
}
