// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;

public static class CreateComponentMenuFunctions
{
    private sealed class TypeNameAndReflection : IDisposable
    {
        public TypeNameAndReflection(string typeName, ObjectReflection reflection)
        {
            TypeName = typeName;
            Label = new CString(typeName);
            Reflection = new WeakReference<ObjectReflection>(reflection);
        }

        public string TypeName { get; }

        public CString Label { get; }

        public WeakReference<ObjectReflection> Reflection { get; }

        public void Dispose()
        {
            Label.Dispose();
        }
    }

    private sealed class CategoryGroup : IDisposable
    {
        public CategoryGroup(string groupName = "")
        {
            _groupName = new CString(groupName);
            _ungroupedGroupName = new CString();
        }

        public CategoryGroup GetGroup(ReadOnlySpan<string> groups)
        {
            if (groups.IsEmpty)
                return this;

            if (!_children.TryGetValue(groups[0], out var child))
            {
                child = new CategoryGroup(groups[0]);
                _children.Add(groups[0], child);
            }
            return child.GetGroup(groups[1..]);
        }

        public bool IsEmpty => _types.Count == 0 && _children.Count == 0;

        public void FinalizeGroups()
        {
            _types.Sort(static (lhs, rhs) => string.CompareOrdinal(lhs.TypeName, rhs.TypeName));

            foreach (var child in _children.Values)
                child.FinalizeGroups();

            List<string>? emptyChildren = null;
            foreach (var (name, child) in _children)
            {
                if (child.IsEmpty)
                    (emptyChildren ??= []).Add(name);
            }
            if (emptyChildren is not null)
            {
                foreach (var name in emptyChildren)
                {
                    _children[name].Dispose();
                    _children.Remove(name);
                }
            }
        }

        public ObjectReflection? Render()
        {
            ObjectReflection? result = null;

            if (_ungroupedGroupName.ByteLength != 0 && _types.Count != 0
                && ImGui.BeginMenu(_ungroupedGroupName.ContentBytes))
            {
                RenderTypes(ref result);
                ImGui.EndMenu();
            }

            foreach (var group in _children.Values)
            {
                if (ImGui.BeginMenu(group._groupName.ContentBytes))
                {
                    var childResult = group.Render();
                    if (childResult is not null)
                        result = childResult;
                    ImGui.EndMenu();
                }
            }

            if (_ungroupedGroupName.ByteLength == 0)
                RenderTypes(ref result);

            return result;
        }

        public void Dispose()
        {
            _ungroupedGroupName.Dispose();
            _groupName.Dispose();
            foreach (var child in _children.Values)
                child.Dispose();
            _children.Clear();
            foreach (var type in _types)
                type.Dispose();
            _types.Clear();
        }

        public void AddType(string typeName, ObjectReflection reflection)
        {
            _types.Add(new TypeNameAndReflection(typeName, reflection));
        }

        public bool TryRemoveChild(string name, out CategoryGroup group)
        {
            if (_children.Remove(name, out var removed))
            {
                group = removed;
                return true;
            }

            group = null!;
            return false;
        }

        public void SetUngroupedGroupName(string name)
        {
            _ungroupedGroupName.Set(name);
        }

        private void RenderTypes(ref ObjectReflection? result)
        {
            foreach (var type in _types)
            {
                if (ImGui.MenuItem(type.Label.ContentBytes))
                {
                    type.Reflection.TryGetTarget(out result);
                }
            }
        }

        private readonly List<TypeNameAndReflection> _types = [];
        private readonly SortedDictionary<string, CategoryGroup> _children = new(StringComparer.Ordinal);
        private readonly CString _groupName;
        private readonly CString _ungroupedGroupName;
    }

    private static void ExtractSpecialGroups(
        List<CategoryGroup> groups, string prefix, ReadOnlySpan<string> specialCategories)
    {
        foreach (var categoryName in specialCategories)
        {
            var groupName = categoryName[prefix.Length..];

            var commonGroups = groups[0];
            if (!commonGroups.TryRemoveChild(groupName, out var specialGroup))
                continue;

            specialGroup.SetUngroupedGroupName($"[{groupName}]");
            groups.Add(specialGroup);
        }
    }

    private static List<CategoryGroup> CreateCategoryGroups(
        Context context, string prefix, ReadOnlySpan<string> specialCategories)
    {
        var result = new List<CategoryGroup>();
        var commonGroups = new CategoryGroup();
        result.Add(commonGroups);

        var typesByCategory = context.GetObjectCategories();
        foreach (var (category, typeList) in typesByCategory)
        {
            if (!category.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var categoryGroups = category[prefix.Length..].Split('/');
            var group = commonGroups.GetGroup(categoryGroups);

            foreach (var typeHash in typeList)
            {
                var reflection = context.GetReflection(typeHash);
                if (reflection.HasObjectFactory())
                    group.AddType(reflection.ClassName, reflection);
            }
        }

        commonGroups.FinalizeGroups();

        ExtractSpecialGroups(result, prefix, specialCategories);
        return result;
    }

    private static IReadOnlyList<CategoryGroup> GetOrCreateCategoryGroups(
        Context context, string prefix, ReadOnlySpan<string> specialCategories)
    {
        var typesByCategory = context.GetObjectCategories();
        var currentHash = MakeHash(typesByCategory);
        if (currentHash == 0)
            currentHash = 1;

        if (currentHash != _cacheHash)
        {
            _cacheHash = currentHash;
            foreach (var group in _cacheGroups)
                group.Dispose();
            _cacheGroups = CreateCategoryGroups(context, prefix, specialCategories);
        }

        return _cacheGroups;
    }

    /// <summary>Helper function to render "create component menu".</summary>
    public static ObjectReflection? RenderCreateComponentMenu()
    {
        var groups = GetOrCreateCategoryGroups(Context.Instance, Prefix, SpecialCategories);

        ObjectReflection? result = null;
        for (var index = 0; index < groups.Count; ++index)
        {
            if (index != 0)
                ImGui.Separator();

            var groupResult = groups[index].Render();
            if (groupResult is not null)
                result = groupResult;
        }

        return result;
    }

    private static uint MakeHash(IReadOnlyDictionary<string, List<StringHash>> typesByCategory)
    {
        var hash = 0u;
        foreach (var (category, typeList) in typesByCategory)
        {
            CombineHash(ref hash, StringHash.Calculate(category));
            foreach (var type in typeList)
                CombineHash(ref hash, type.ToHash());
        }
        return hash;
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }

    private static uint _cacheHash;
    private static List<CategoryGroup> _cacheGroups = [];
    private const string Prefix = "Component/";
    private static readonly string[] SpecialCategories = [ObjectCategory.Category_Plugin, ObjectCategory.Category_User];
}
