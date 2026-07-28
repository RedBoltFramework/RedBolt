namespace RedBolt.Tests;

using RedBolt;
using RedBolt.Core;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_returns_help_for_help_alias()
    {
        var parser = new CommandLineParser("RedBolt.Tests");

        var result = parser.Parse(["--help"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsHelpRequested);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void Parse_applies_registered_flags_and_options()
    {
        var parser = new CommandLineParser("RedBolt.Tests");
        var flagTriggered = false;
        var optionValue = "unset";

        parser.AddFlag(["--flag"], "Enable flag", () => flagTriggered = true);
        parser.AddOption(["--name"], "value", "Set value", value => optionValue = value);

        var result = parser.Parse(["--flag", "--name="]);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsHelpRequested);
        Assert.True(flagTriggered);
        Assert.Equal(string.Empty, optionValue);
    }

    [Fact]
    public void Parse_reports_value_mismatches_and_usage_lists_help()
    {
        var parser = new CommandLineParser("RedBolt.Tests");
        parser.AddFlag(["--flag"], "Enable flag", () => { });
        parser.AddOption(["--name"], "value", "Set value", _ => { });

        var missingValue = parser.Parse(["--name"]);
        var unexpectedValue = parser.Parse(["--flag=true"]);
        var usage = parser.GetUsage();

        Assert.False(missingValue.IsSuccess);
        Assert.Equal("Option '--name' requires a value.", missingValue.ErrorMessage);
        Assert.False(unexpectedValue.IsSuccess);
        Assert.Equal("Option '--flag' does not accept a value.", unexpectedValue.ErrorMessage);
        Assert.Contains("Usage: RedBolt.Tests [options]", usage);
        Assert.Contains("--name <value>", usage);
        Assert.Contains("-h, --help", usage);
    }
}
