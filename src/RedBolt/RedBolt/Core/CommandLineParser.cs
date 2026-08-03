namespace RedBolt.Core;

using SystemMath = global::System.Math;
using System.Text;

public sealed class CommandLineParser
{
    private readonly Dictionary<string, CommandLineOption> _optionsByAlias = new(StringComparer.Ordinal);
    private readonly List<CommandLineOption> _options = [];

    public CommandLineParser(string applicationName)
    {
        ApplicationName = string.IsNullOrWhiteSpace(applicationName) ? "application" : applicationName;
    }

    public string ApplicationName { get; }

    public void AddFlag(IEnumerable<string> aliases, string description, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        AddOptionCore(new FlagCommandLineOption(GetAliases(aliases), description, action));
    }

    public void AddOption(IEnumerable<string> aliases, string valueName, string description, Action<string> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentNullException.ThrowIfNull(action);
        AddOptionCore(new ValueCommandLineOption(GetAliases(aliases), description, valueName, action));
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
            var hasInlineValue = equalsIndex >= 0;
            var inlineValue = hasInlineValue ? argument[(equalsIndex + 1)..] : string.Empty;
            if (!_optionsByAlias.TryGetValue(optionName, out var option))
                return CommandLineParseResult.Failure($"Unknown option '{optionName}'.");

            try
            {
                if (option.RequiresValue)
                {
                    if (hasInlineValue)
                        option.Apply(inlineValue);
                    else if (index + 1 < args.Count)
                        option.Apply(args[++index]);
                    else
                        return CommandLineParseResult.Failure($"Option '{optionName}' requires a value.");
                }
                else if (hasInlineValue)
                {
                    return CommandLineParseResult.Failure($"Option '{optionName}' does not accept a value.");
                }
                else
                    option.Apply();
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

        var renderedOptions = new string[_options.Count + 1];
        var width = 0;
        for (var index = 0; index < _options.Count; ++index)
        {
            var renderedOption = _options[index].RenderSignature();
            renderedOptions[index] = renderedOption;
            width = SystemMath.Max(width, renderedOption.Length);
        }

        renderedOptions[^1] = "-h, --help";
        width = SystemMath.Max(width, renderedOptions[^1].Length);

        for (var index = 0; index < _options.Count; ++index)
            builder.Append("  ").Append(renderedOptions[index].PadRight(width)).Append("  ").AppendLine(_options[index].Description);

        builder.Append("  ").Append(renderedOptions[^1].PadRight(width)).Append("  ").AppendLine("Show this help message");
        return builder.ToString();
    }

    private static string[] GetAliases(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var aliasSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
                aliasSet.Add(alias);
        }

        if (aliasSet.Count == 0)
            throw new ArgumentException("At least one alias must be provided.", nameof(aliases));

        var aliasList = new string[aliasSet.Count];
        aliasSet.CopyTo(aliasList);
        return aliasList;
    }

    private void AddOptionCore(CommandLineOption option)
    {
        foreach (var alias in option.Aliases)
        {
            if (!_optionsByAlias.TryAdd(alias, option))
                throw new InvalidOperationException($"Command-line alias '{alias}' is already registered.");
        }

        _options.Add(option);
    }

    private abstract class CommandLineOption
    {
        protected CommandLineOption(string[] aliases, string description)
        {
            Aliases = aliases;
            Description = description;
        }

        public string[] Aliases { get; }

        public string Description { get; }

        public abstract bool RequiresValue { get; }

        public abstract void Apply();

        public abstract void Apply(string value);

        public abstract string RenderSignature();
    }

    private sealed class FlagCommandLineOption : CommandLineOption
    {
        private readonly Action _apply;

        public FlagCommandLineOption(string[] aliases, string description, Action apply)
            : base(aliases, description)
        {
            _apply = apply;
        }

        public override bool RequiresValue => false;

        public override void Apply()
        {
            _apply();
        }

        public override void Apply(string value)
        {
        }

        public override string RenderSignature()
        {
            return string.Join(", ", Aliases);
        }
    }

    private sealed class ValueCommandLineOption : CommandLineOption
    {
        private readonly Action<string> _apply;

        public ValueCommandLineOption(string[] aliases, string description, string valueName, Action<string> apply)
            : base(aliases, description)
        {
            ValueName = valueName;
            _apply = apply;
        }

        public string ValueName { get; }

        public override bool RequiresValue => true;

        public override void Apply()
        {
        }

        public override void Apply(string value)
        {
            _apply(value);
        }

        public override string RenderSignature()
        {
            var signature = string.Join(", ", Aliases);
            return $"{signature} <{ValueName}>";
        }
    }
}

public readonly record struct CommandLineParseResult(bool IsSuccess, bool IsHelpRequested, string ErrorMessage)
{
    public static CommandLineParseResult Success() => new(true, false, string.Empty);

    public static CommandLineParseResult HelpRequested() => new(true, true, string.Empty);

    public static CommandLineParseResult Failure(string errorMessage) => new(false, false, errorMessage);
}
