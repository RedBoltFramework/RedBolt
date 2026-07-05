using System.Text;

namespace RedBolt;

public sealed class CommandLineParser
{
    private readonly Dictionary<string, CommandLineOption> optionsByAlias = new(StringComparer.Ordinal);
    private readonly List<CommandLineOption> options = [];

    public CommandLineParser(string applicationName)
    {
        ApplicationName = string.IsNullOrWhiteSpace(applicationName) ? "application" : applicationName;
    }

    public string ApplicationName { get; }

    public void AddFlag(IEnumerable<string> aliases, string description, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        AddOptionCore(aliases, description, valueName: null, _ => action());
    }

    public void AddOption(IEnumerable<string> aliases, string valueName, string description, Action<string> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentNullException.ThrowIfNull(action);
        AddOptionCore(aliases, description, valueName, value => action(value!));
    }

    public CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        for (var index = 0; index < args.Count; ++index)
        {
            var argument = args[index];
            if (argument is "-h" or "--help")
                return CommandLineParseResult.HelpRequested();

            if (!argument.StartsWith("-", StringComparison.Ordinal))
                return CommandLineParseResult.Failure($"Unexpected positional argument '{argument}'.");

            var equalsIndex = argument.IndexOf('=');
            var optionName = equalsIndex >= 0 ? argument[..equalsIndex] : argument;
            var inlineValue = equalsIndex >= 0 ? argument[(equalsIndex + 1)..] : null;
            if (!optionsByAlias.TryGetValue(optionName, out var option))
                return CommandLineParseResult.Failure($"Unknown option '{optionName}'.");

            string? optionValue = null;
            if (option.RequiresValue)
            {
                if (inlineValue is not null)
                    optionValue = inlineValue;
                else if (index + 1 < args.Count)
                    optionValue = args[++index];
                else
                    return CommandLineParseResult.Failure($"Option '{optionName}' requires a value.");
            }
            else if (inlineValue is not null)
            {
                return CommandLineParseResult.Failure($"Option '{optionName}' does not accept a value.");
            }

            try
            {
                option.Apply(optionValue);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
            {
                return CommandLineParseResult.Failure(exception.Message);
            }
        }

        return CommandLineParseResult.Success();
    }

    public string GetUsage()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Usage: {ApplicationName} [options]");
        builder.AppendLine();
        builder.AppendLine("Options:");

        var renderedOptions = options
            .Select(static option => option.RenderSignature())
            .Append("-h, --help")
            .ToArray();
        var width = renderedOptions.Max(static text => text.Length);
        for (var index = 0; index < options.Count; ++index)
            builder.Append("  ").Append(renderedOptions[index].PadRight(width)).Append("  ").AppendLine(options[index].Description);

        builder.Append("  ").Append(renderedOptions[^1].PadRight(width)).Append("  ").AppendLine("Show this help message");
        return builder.ToString();
    }

    private void AddOptionCore(IEnumerable<string> aliases, string description, string? valueName, Action<string?> action)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(action);

        var aliasList = aliases
            .Where(static alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (aliasList.Length == 0)
            throw new ArgumentException("At least one alias must be provided.", nameof(aliases));

        var option = new CommandLineOption(aliasList, description, valueName, action);
        foreach (var alias in aliasList)
        {
            if (!optionsByAlias.TryAdd(alias, option))
                throw new InvalidOperationException($"Command-line alias '{alias}' is already registered.");
        }

        options.Add(option);
    }

    private sealed class CommandLineOption
    {
        private readonly Action<string?> apply;

        public CommandLineOption(string[] aliases, string description, string? valueName, Action<string?> apply)
        {
            Aliases = aliases;
            Description = description;
            ValueName = valueName;
            this.apply = apply;
        }

        public string[] Aliases { get; }

        public string Description { get; }

        public string? ValueName { get; }

        public bool RequiresValue => ValueName is not null;

        public void Apply(string? value)
        {
            apply(value);
        }

        public string RenderSignature()
        {
            var signature = string.Join(", ", Aliases);
            return ValueName is null ? signature : $"{signature} <{ValueName}>";
        }
    }
}

public readonly record struct CommandLineParseResult(bool IsSuccess, bool IsHelpRequested, string? ErrorMessage)
{
    public static CommandLineParseResult Success() => new(true, false, null);

    public static CommandLineParseResult HelpRequested() => new(true, true, null);

    public static CommandLineParseResult Failure(string errorMessage) => new(false, false, errorMessage);
}
