using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedBolt;

public enum SceneResourceFormat
{
    Json,
    Xml
}

public sealed class SceneResource : Resource
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        WriteIndented = true
    };

    private static readonly Lazy<Dictionary<string, Type>> ComponentTypes = new(BuildComponentTypeMap);

    private SceneResourceFormat? _loadFormat;

    static SceneResource()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public SceneResource(string name)
        : base(name)
    {
        Scene = new Scene();
    }

    public Scene Scene { get; }

    public bool IsPrefab { get; private set; }

    public void SetPrefab(bool value)
    {
        IsPrefab = value;
    }

    public Scene GetScene() => Scene;

    public bool Save(Stream destination, SceneResourceFormat format, bool asPrefab = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var sceneData = SerializeScene(Scene);
        switch (format)
        {
            case SceneResourceFormat.Json:
            {
                JsonSerializer.Serialize(destination, sceneData, SerializerOptions);
                _loadFormat = SceneResourceFormat.Json;
                return true;
            }
            case SceneResourceFormat.Xml:
            {
                var xml = new XMLFile(Name);
                var root = xml.CreateRoot(GetXmlRootName());
                WriteSceneXml(root, sceneData);
                _loadFormat = SceneResourceFormat.Xml;
                return xml.Save(destination);
            }
            default:
                return false;
        }
    }

    public bool SaveFile(string fileName, SceneResourceFormat format, bool asPrefab = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var destination = File.Create(fileName);
        return Save(destination, format, asPrefab);
    }

    public override bool Save(Stream destination) => Save(destination, _loadFormat ?? SceneResourceFormat.Xml, IsPrefab);

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StreamReader(source, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        SerializedScene? sceneData;
        if (text.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal))
        {
            var xml = new XMLFile(Name);
            if (!xml.FromString(text))
                return false;
            sceneData = ReadSceneXml(xml.GetRoot(GetXmlRootName()));
            _loadFormat = SceneResourceFormat.Xml;
        }
        else
        {
            sceneData = JsonSerializer.Deserialize<SerializedScene>(text, SerializerOptions);
            _loadFormat = SceneResourceFormat.Json;
        }

        if (sceneData is null)
            return false;

        DeserializeScene(Scene, sceneData);
        return true;
    }

    public override bool EndLoad() => true;

    public static string GetXmlRootName() => "resource";

    private static Dictionary<string, Type> BuildComponentTypeMap()
    {
        var result = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in typeof(Component).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(Component).IsAssignableFrom(type))
                continue;

            result[type.Name] = type;
            if (!string.IsNullOrEmpty(type.FullName))
                result[type.FullName] = type;
            if (!string.IsNullOrEmpty(type.AssemblyQualifiedName))
                result[type.AssemblyQualifiedName] = type;
        }

        return result;
    }

    private static SerializedScene SerializeScene(Scene scene)
    {
        return new SerializedScene
        {
            UpdateEnabled = scene.IsUpdateEnabled(),
            TimeScale = scene.GetTimeScale(),
            ElapsedTime = scene.GetElapsedTime(),
            AsyncLoadingMs = scene.GetAsyncLoadingMs(),
            FileName = scene.GetFileName(),
            Root = SerializeNode(scene.Root)
        };
    }

    private static SerializedNode SerializeNode(Node node)
    {
        return new SerializedNode
        {
            Id = node.Id,
            Name = node.Name,
            Enabled = node.IsEnabledSelf,
            Temporary = node.IsTemporary,
            Position = node.Position,
            Rotation = node.Rotation,
            Scale = node.Scale,
            Tags = node.Tags.ToList(),
            Variables = node.Variables.Select(item => new SerializedVariable
            {
                Key = item.Key.Value,
                Value = SerializeValue(item.Value)
            }).ToList(),
            Components = node.Components.Select(SerializeComponent).ToList(),
            Children = node.Children.Select(SerializeNode).ToList()
        };
    }

    private static SerializedComponent SerializeComponent(Component component)
    {
        var serialized = new SerializedComponent
        {
            TypeName = component is UnknownComponent unknownComponent && !string.IsNullOrEmpty(unknownComponent.GetOriginalTypeName())
                ? unknownComponent.GetOriginalTypeName()
                : component.GetType().FullName ?? component.GetType().Name,
            Id = component.Id,
            Temporary = component.IsTemporary
        };

        foreach (var attribute in component.Attributes)
        {
            if (!attribute.ShouldSave)
                continue;

            serialized.Attributes.Add(new SerializedAttribute
            {
                Name = attribute.Name,
                Value = SerializeValue(component.GetAttribute(attribute.Name), attribute.ValueType)
            });
        }

        return serialized;
    }

    private static SerializedValue? SerializeValue(object? value, Type? declaredType = null)
    {
        if (value is null)
            return null;

        var type = value.GetType();
        if (declaredType is not null && declaredType != typeof(object) && !declaredType.IsInterface && !declaredType.IsAssignableFrom(type))
            type = declaredType;

        return new SerializedValue
        {
            TypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Json = JsonSerializer.Serialize(value, type, SerializerOptions)
        };
    }

    private static void DeserializeScene(Scene scene, SerializedScene sceneData)
    {
        scene.Clear();
        scene.SetUpdateEnabled(sceneData.UpdateEnabled);
        scene.SetTimeScale(sceneData.TimeScale);
        scene.SetElapsedTime(sceneData.ElapsedTime);
        scene.SetAsyncLoadingMs(sceneData.AsyncLoadingMs);
        scene.SetFileName(sceneData.FileName ?? string.Empty);
        DeserializeNode(scene.Root, sceneData.Root, isRoot: true);
    }

    private static void DeserializeNode(Node node, SerializedNode nodeData, bool isRoot = false)
    {
        node.SetName(nodeData.Name ?? string.Empty);
        node.SetEnabled(nodeData.Enabled);
        node.SetTemporary(nodeData.Temporary);
        node.SetTransform(nodeData.Position, nodeData.Rotation, nodeData.Scale);
        node.SetTags(nodeData.Tags);
        node.SetVariables(nodeData.Variables.Select(variable => new KeyValuePair<StringHash, object?>(new StringHash(variable.Key), DeserializeValue(variable.Value))));

        foreach (var componentData in nodeData.Components)
        {
            Component? component;
            if (!ComponentTypes.Value.TryGetValue(componentData.TypeName, out var type))
            {
                var unknown = node.CreateComponent<UnknownComponent>(componentData.Id);
                unknown.SetOriginalTypeName(componentData.TypeName);
                component = unknown;
            }
            else
            {
                component = node.CreateComponent(type, componentData.Id);
            }

            if (component is null)
                continue;

            component.SetTemporary(componentData.Temporary);
            foreach (var attribute in componentData.Attributes)
                component.SetAttribute(attribute.Name, DeserializeValue(attribute.Value));
            component.ApplyAttributes();
        }

        foreach (var childData in nodeData.Children)
        {
            var child = node.CreateChild(childData.Name ?? string.Empty, childData.Id, childData.Temporary);
            DeserializeNode(child, childData);
        }
    }

    private static object? DeserializeValue(SerializedValue? value)
    {
        if (value is null || string.IsNullOrEmpty(value.TypeName))
            return null;

        var type = Type.GetType(value.TypeName, throwOnError: false);
        if (type is null)
            return null;

        return JsonSerializer.Deserialize(value.Json ?? "null", type, SerializerOptions);
    }

    private static void WriteSceneXml(XMLElement root, SerializedScene scene)
    {
        root.SetBool("updateEnabled", scene.UpdateEnabled);
        root.SetFloat("timeScale", scene.TimeScale);
        root.SetFloat("elapsedTime", scene.ElapsedTime);
        root.SetInt("asyncLoadingMs", scene.AsyncLoadingMs);
        root.SetString("fileName", scene.FileName ?? string.Empty);
        WriteNodeXml(root.CreateChild("node"), scene.Root);
    }

    private static SerializedScene ReadSceneXml(XMLElement root)
    {
        return new SerializedScene
        {
            UpdateEnabled = !root.HasAttribute("updateEnabled") || root.GetBool("updateEnabled"),
            TimeScale = root.HasAttribute("timeScale") ? root.GetFloat("timeScale") : 1.0f,
            ElapsedTime = root.HasAttribute("elapsedTime") ? root.GetFloat("elapsedTime") : 0.0f,
            AsyncLoadingMs = root.HasAttribute("asyncLoadingMs") ? root.GetInt("asyncLoadingMs") : 5,
            FileName = root.GetString("fileName"),
            Root = ReadNodeXml(root.GetChild("node"))
        };
    }

    private static void WriteNodeXml(XMLElement element, SerializedNode node)
    {
        element.SetUInt("id", node.Id);
        element.SetString("name", node.Name ?? string.Empty);
        element.SetBool("enabled", node.Enabled);
        element.SetBool("temporary", node.Temporary);
        element.SetVector3("position", node.Position);
        element.SetQuaternion("rotation", node.Rotation);
        element.SetVector3("scale", node.Scale);

        var tagsElement = element.CreateChild("tags");
        foreach (var tag in node.Tags)
            tagsElement.CreateChild("tag").SetValue(tag);

        var variablesElement = element.CreateChild("variables");
        foreach (var variable in node.Variables)
        {
            var variableElement = variablesElement.CreateChild("var");
            variableElement.SetUInt("key", variable.Key);
            WriteValueXml(variableElement, variable.Value);
        }

        var componentsElement = element.CreateChild("components");
        foreach (var component in node.Components)
        {
            var componentElement = componentsElement.CreateChild("component");
            componentElement.SetString("type", component.TypeName);
            componentElement.SetUInt("id", component.Id);
            componentElement.SetBool("temporary", component.Temporary);
            foreach (var attribute in component.Attributes)
            {
                var attributeElement = componentElement.CreateChild("attribute");
                attributeElement.SetString("name", attribute.Name);
                WriteValueXml(attributeElement, attribute.Value);
            }
        }

        var childrenElement = element.CreateChild("children");
        foreach (var child in node.Children)
            WriteNodeXml(childrenElement.CreateChild("node"), child);
    }

    private static SerializedNode ReadNodeXml(XMLElement element)
    {
        var node = new SerializedNode
        {
            Id = element.GetUInt("id"),
            Name = element.GetString("name"),
            Enabled = !element.HasAttribute("enabled") || element.GetBool("enabled"),
            Temporary = element.HasAttribute("temporary") && element.GetBool("temporary"),
            Position = element.HasAttribute("position") ? element.GetVector3("position") : Vector3.Zero,
            Rotation = element.HasAttribute("rotation") ? element.GetQuaternion("rotation") : Quaternion.Identity,
            Scale = element.HasAttribute("scale") ? element.GetVector3("scale") : Vector3.One
        };

        var tagsElement = element.GetChild("tags");
        for (var tagElement = tagsElement.GetChild("tag"); tagElement.NotNull; tagElement = tagElement.GetNext("tag"))
            node.Tags.Add(tagElement.GetValue());

        var variablesElement = element.GetChild("variables");
        for (var variableElement = variablesElement.GetChild("var"); variableElement.NotNull; variableElement = variableElement.GetNext("var"))
        {
            node.Variables.Add(new SerializedVariable
            {
                Key = variableElement.GetUInt("key"),
                Value = ReadValueXml(variableElement)
            });
        }

        var componentsElement = element.GetChild("components");
        for (var componentElement = componentsElement.GetChild("component"); componentElement.NotNull; componentElement = componentElement.GetNext("component"))
        {
            var component = new SerializedComponent
            {
                TypeName = componentElement.GetString("type"),
                Id = componentElement.GetUInt("id"),
                Temporary = componentElement.HasAttribute("temporary") && componentElement.GetBool("temporary")
            };

            for (var attributeElement = componentElement.GetChild("attribute"); attributeElement.NotNull; attributeElement = attributeElement.GetNext("attribute"))
            {
                component.Attributes.Add(new SerializedAttribute
                {
                    Name = attributeElement.GetString("name"),
                    Value = ReadValueXml(attributeElement)
                });
            }

            node.Components.Add(component);
        }

        var childrenElement = element.GetChild("children");
        for (var childElement = childrenElement.GetChild("node"); childElement.NotNull; childElement = childElement.GetNext("node"))
            node.Children.Add(ReadNodeXml(childElement));

        return node;
    }

    private static void WriteValueXml(XMLElement element, SerializedValue? value)
    {
        if (value is null)
        {
            element.SetString("type", string.Empty);
            element.SetValue("null");
            return;
        }

        element.SetString("type", value.TypeName);
        element.SetValue(value.Json ?? "null");
    }

    private static SerializedValue? ReadValueXml(XMLElement element)
    {
        var typeName = element.GetString("type");
        if (string.IsNullOrEmpty(typeName))
            return null;

        return new SerializedValue
        {
            TypeName = typeName,
            Json = element.GetValue()
        };
    }

    private sealed class SerializedScene
    {
        public bool UpdateEnabled { get; set; }
        public float TimeScale { get; set; }
        public float ElapsedTime { get; set; }
        public int AsyncLoadingMs { get; set; }
        public string? FileName { get; set; }
        public SerializedNode Root { get; set; } = new();
    }

    private sealed class SerializedNode
    {
        public uint Id { get; set; }
        public string? Name { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Temporary { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.Identity;
        public Vector3 Scale { get; set; } = Vector3.One;
        public List<string> Tags { get; set; } = [];
        public List<SerializedVariable> Variables { get; set; } = [];
        public List<SerializedComponent> Components { get; set; } = [];
        public List<SerializedNode> Children { get; set; } = [];
    }

    private sealed class SerializedComponent
    {
        public string TypeName { get; set; } = string.Empty;
        public uint Id { get; set; }
        public bool Temporary { get; set; }
        public List<SerializedAttribute> Attributes { get; set; } = [];
    }

    private sealed class SerializedAttribute
    {
        public string Name { get; set; } = string.Empty;
        public SerializedValue? Value { get; set; }
    }

    private sealed class SerializedVariable
    {
        public uint Key { get; set; }
        public SerializedValue? Value { get; set; }
    }

    private sealed class SerializedValue
    {
        public string TypeName { get; set; } = string.Empty;
        public string Json { get; set; } = "null";
    }
}
