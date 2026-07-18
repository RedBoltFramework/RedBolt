# Associative Containers

Key-value and set containers. Same marshaller pattern as sequential containers — each type declares its own surface via `[[sharpcpp::managed_type(...)]]`.

## std::map → Std.Map

```cpp
template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.Map<{0}, {1}>",
    managed_base_type = "global::SharpCpp.Support.MapBase<{0}, {1}>",
    abi_type = "void*"
)]]
Marshaller<std::map<TKey, TValue, TCompare, TAllocator>>
{
    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static unsigned long long GetCountCore(const cpp_type& self) { return self.size(); }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static void ClearCore(cpp_type& self) { self.clear(); }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static bool ContainsKeyCore(const cpp_type& self, const TKey& key) { return self.find(key) != self.end(); }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static TValue GetValueCore(const cpp_type& self, const TKey& key) { return self.find(key)->second; }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void SetValueCore(cpp_type& self, const TKey& key, const TValue& value) { self[key] = value; }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static bool AddCore(cpp_type& self, const TKey& key, const TValue& value) { return self.emplace(key, value).second; }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static bool RemoveCore(cpp_type& self, const TKey& key) { return self.erase(key) != 0; }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static TKey GetKeyAtCore(const cpp_type& self, unsigned long long index);

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static TValue GetValueAtCore(const cpp_type& self, unsigned long long index);
};
```

### Pass direction variants

**Return:**
```cpp
std::map<std::string, int> GetScores() const;
```
```csharp
public Std.Map<string, int> Scores => new Std.Map<string, int>(__GetScores(_handle));
```

**Parameter — const reference (in):**
```cpp
void ProcessMap([[sharpcpp::in]] const std::map<std::string, int>& scores);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __ProcessMap(nint self, nint scores);
```

**Parameter — out:**
```cpp
void GetMap([[sharpcpp::out]] std::map<std::string, int>* outMap);
```
```csharp
[LibraryImport("libnative")]
internal static partial void __GetMap(nint self, out nint outMap);
```

### Key and value access

`ContainsKeyCore`, `GetValueCore`, `SetValueCore`, `AddCore`, `RemoveCore` — these `expose_member` declarations inject methods into the C# managed wrapper. They go through `ParameterMarshaller<TKey>` / `ReturnMarshaller<TValue>` for non-blittable key/value types.

`GetKeyAtCore` / `GetValueAtCore` provide indexed traversal. The `{0}` and `{1}` placeholders in `public_type` substitute key type name and value type name.

## std::unordered_map → Std.UnorderedMap

Same shape, different C++ container:

```cpp
template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.UnorderedMap<{0}, {1}>",
    managed_base_type = "global::SharpCpp.Support.UnorderedMapBase<{0}, {1}>",
    abi_type = "void*"
)]]
Marshaller<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> { ... };
```

Identical `expose_member` set. Same C# surface structure. O(1) average lookup instead of O(log n).

## Custom associative containers

Users can define their own container marshaller specializations with different surfaces:

```cpp
// FlatMap using sorted vector — different managed_base_type, different element access
template <typename TKey, typename TValue>
struct [[sharpcpp::managed_type(
    public_type = "FlatMap<{0}, {1}>",
    managed_base_type = "global::MyApp.Support.FlatMapBase<{0}, {1}>",
    abi_type = "void*"
)]]
Marshaller<MyFlatMap<TKey, TValue>> { ... };
```

The renderer does not know this is a map. It only sees `public_type`, `managed_base_type`, `abi_type`, and `expose_member` declarations.

---

← [[03-sequential-containers]] | [[05-optional-nullable]] →