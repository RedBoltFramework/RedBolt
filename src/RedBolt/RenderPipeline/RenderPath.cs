using System.Globalization;

namespace RedBolt;

public sealed class RenderPath : Resource
{
    private readonly List<RenderPass> passes = [];
    private RenderPassTraits traits;

    public RenderPath(string name)
        : base(name)
    {
    }

    public IReadOnlyList<RenderPass> GetPasses() => passes;

    public RenderPassTraits GetAggregatedPassTraits() => traits;

    public void CollectParameters(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var pass in passes)
            pass.CollectParameters(parameters);
    }

    public void InitializeView(RenderPipelineView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        foreach (var pass in passes)
            pass.InitializeView(view);
    }

    public void UpdateParameters(RenderPipelineSettings settings, IReadOnlyList<RenderPassToggle> enabledPasses, IReadOnlyDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(enabledPasses);
        ArgumentNullException.ThrowIfNull(parameters);

        if (enabledPasses.Count != passes.Count)
            throw new ArgumentException("Enabled passes count must match render path pass count.", nameof(enabledPasses));

        for (var i = 0; i < passes.Count; ++i)
        {
            var pass = passes[i];
            pass.SetEnabled(enabledPasses[i].Enabled);
            pass.UpdateParameters(settings, parameters);
        }

        traits = default;
        foreach (var pass in passes)
        {
            var passTraits = pass.GetTraits();
            traits = new RenderPassTraits(
                traits.NeedReadWriteColorBuffer || passTraits.NeedReadWriteColorBuffer,
                traits.NeedBilinearColorSampler || passTraits.NeedBilinearColorSampler);
        }
    }

    public void Update(SharedRenderPassState sharedState)
    {
        ArgumentNullException.ThrowIfNull(sharedState);

        foreach (var pass in passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Update(sharedState);
        }
    }

    public void Render(SharedRenderPassState sharedState)
    {
        ArgumentNullException.ThrowIfNull(sharedState);

        foreach (var pass in passes)
        {
            if (pass.IsEnabledEffectively())
                pass.Render(sharedState);
        }
    }

    public RenderPath Clone()
    {
        var clone = new RenderPath(Name);
        foreach (var pass in passes)
            clone.AddPass(pass.Clone());
        return clone;
    }

    public IReadOnlyList<RenderPassToggle> RepairEnabledRenderPasses(IEnumerable<RenderPassToggle> sourcePasses)
    {
        ArgumentNullException.ThrowIfNull(sourcePasses);
        var sourceList = sourcePasses.ToArray();

        if (sourceList.Length == passes.Count)
        {
            var matches = true;
            for (var i = 0; i < passes.Count; ++i)
            {
                if (!string.Equals(passes[i].GetPassName(), sourceList[i].Name, StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return sourceList;
        }

        var result = new List<RenderPassToggle>(passes.Count);
        foreach (var pass in passes)
        {
            var match = sourceList.FirstOrDefault(staticToggle => string.Equals(staticToggle.Name, pass.GetPassName(), StringComparison.Ordinal));
            var enabled = match.Name is not null ? match.Enabled : pass.IsEnabledByDefault();
            result.Add(new RenderPassToggle(pass.GetPassName(), enabled));
        }
        return result;
    }

    public void AddPass(RenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        passes.Add(pass);
    }

    public bool RemovePass(RenderPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return passes.Remove(pass);
    }

    public bool ReorderPass(RenderPass pass, uint index)
    {
        ArgumentNullException.ThrowIfNull(pass);

        if (index >= passes.Count)
            return false;

        var oldIndex = passes.IndexOf(pass);
        if (oldIndex < 0)
            return false;

        passes.RemoveAt(oldIndex);
        passes.Insert((int)index, pass);
        return true;
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var xml = new XMLFile($"{Name}#xml");
        if (!xml.BeginLoad(source))
            return false;

        var root = xml.GetRoot("renderpath");
        if (root.IsNull)
            return false;

        passes.Clear();
        for (var passElement = root.GetChild("pass"); passElement.NotNull; passElement = passElement.GetNext("pass"))
        {
            var passName = passElement.GetAttribute("name");
            if (string.IsNullOrWhiteSpace(passName))
                return false;

            var pass = new SerializedRenderPass(passName);
            if (passElement.HasAttribute("enabled"))
                pass.SetEnabledByDefault(passElement.GetBool("enabled"));
            if (passElement.HasAttribute("comment"))
                pass.SetComment(passElement.GetAttribute("comment"));

            for (var parameterElement = passElement.GetChild("parameter"); parameterElement.NotNull; parameterElement = parameterElement.GetNext("parameter"))
            {
                var parameterName = parameterElement.GetAttribute("name");
                if (string.IsNullOrWhiteSpace(parameterName))
                    return false;

                var parameterType = ParseVariantType(parameterElement.GetAttribute("type"));
                if (parameterType == VariantType.None)
                    return false;

                var parameterValue = parameterElement.HasAttribute("value")
                    ? parameterElement.GetAttribute("value")
                    : parameterElement.GetValue();
                pass.SetParameter(parameterName, VariantTypeUtility.Deserialize(parameterValue, parameterType));
            }

            AddPass(pass);
        }

        MemoryUse = (uint)(passes.Count * 256);
        return true;
    }

    public override bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var xml = new XMLFile(Name);
        var root = xml.CreateRoot("renderpath");
        foreach (var pass in passes)
        {
            var passElement = root.CreateChild("pass");
            passElement.SetAttribute("name", pass.GetPassName());
            passElement.SetBool("enabled", pass.IsEnabledByDefault());
            if (!string.IsNullOrWhiteSpace(pass.GetComment()))
                passElement.SetAttribute("comment", pass.GetComment());

            if (pass is not SerializedRenderPass serializedPass)
                continue;

            foreach (var parameter in serializedPass.Parameters)
            {
                var type = VariantTypeUtility.GetVariantType(parameter.Value);
                if (type == VariantType.None)
                    continue;

                var parameterElement = passElement.CreateChild("parameter");
                parameterElement.SetAttribute("name", parameter.Key);
                parameterElement.SetAttribute("type", type.ToString());
                parameterElement.SetAttribute("value", VariantTypeUtility.Serialize(parameter.Value, type));
            }
        }

        return xml.Save(destination);
    }

    private static VariantType ParseVariantType(string value)
    {
        return Enum.TryParse<VariantType>(value, ignoreCase: true, out var type) ? type : VariantType.None;
    }

    private sealed class SerializedRenderPass : RenderPass
    {
        private readonly Dictionary<string, object?> parameters = new(StringComparer.Ordinal);

        public SerializedRenderPass(string passName)
            : base(passName)
        {
        }

        public IReadOnlyDictionary<string, object?> Parameters => parameters;

        public void SetParameter(string name, object? value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            parameters[name] = value;
        }

        public override void CollectParameters(IDictionary<string, object?> target)
        {
            ArgumentNullException.ThrowIfNull(target);

            foreach (var parameter in parameters)
                DeclareParameter(parameter.Key, parameter.Value, target);
        }

        public override RenderPass Clone()
        {
            var clone = new SerializedRenderPass(GetPassName());
            clone.SetEnabledByDefault(IsEnabledByDefault());
            clone.SetComment(GetComment());
            foreach (var parameter in parameters)
                clone.SetParameter(parameter.Key, parameter.Value);
            return clone;
        }
    }
}
