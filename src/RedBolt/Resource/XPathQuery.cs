// C++: no direct C++ counterpart. XPathQuery is only forward-declared in XMLElement.h:48.
// C# wraps System.Xml.XPath. Implementation is C#-only.
using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RedBolt;

public sealed class XPathResultSet : IReadOnlyList<XMLElement>
{
    private readonly IReadOnlyList<XMLElement> _results;

    public XPathResultSet() : this([]) { }

    internal XPathResultSet(IEnumerable<XElement> results, XMLFile? file = null)
        : this(results.Select(element => new XMLElement(file, element)).ToArray())
    {
    }

    private XPathResultSet(IReadOnlyList<XMLElement> results) => _results = results;

    public XMLElement this[int index] => index >= 0 && index < _results.Count ? _results[index] : new XMLElement(null, null);

    public int Count => _results.Count;

    public XMLElement FirstResult() => this[0];

    public uint Size() => (uint)Count;

    public bool Empty() => Count == 0;

    public IEnumerator<XMLElement> GetEnumerator() => _results.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class XPathQuery
{
    private readonly Dictionary<string, object> _variables = new(StringComparer.Ordinal);

    public XPathQuery(string queryString = "", string variableString = "") => SetQuery(queryString, variableString);

    public string Query { get; private set; } = string.Empty;

    public void Bind() { }

    public bool SetVariable(string name, bool value) => SetVariableCore(name, value);

    public bool SetVariable(string name, float value) => SetVariableCore(name, value);

    public bool SetVariable(string name, string value) => SetVariableCore(name, value);

    public bool SetVariable(string name, XPathResultSet value) => SetVariableCore(name, value);

    public bool SetQuery(string queryString, string variableString = "", bool bind = true)
    {
        Query = queryString ?? string.Empty;
        _variables.Clear();
        return Query.Length != 0;
    }

    public void Clear()
    {
        Query = string.Empty;
        _variables.Clear();
    }

    public bool EvaluateToBool(XMLElement element) => Convert.ToBoolean(EvaluateScalar(element), CultureInfo.InvariantCulture);

    public float EvaluateToFloat(XMLElement element) => Convert.ToSingle(EvaluateScalar(element), CultureInfo.InvariantCulture);

    public string EvaluateToString(XMLElement element) => Convert.ToString(EvaluateScalar(element), CultureInfo.InvariantCulture) ?? string.Empty;

    public XPathResultSet Evaluate(XMLElement element) => element.SelectPrepared(this);

    public string GetQuery() => Query;

    internal string GetExpandedQuery()
    {
        var query = Query;
        foreach (var (name, value) in _variables)
        {
            var text = value switch
            {
                bool flag => flag ? "true()" : "false()",
                string str => $"'{str.Replace("'", "&apos;", StringComparison.Ordinal)}'",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
            query = query.Replace("$" + name, text, StringComparison.Ordinal);
        }
        return query;
    }

    private object EvaluateScalar(XMLElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.Element?.XPathEvaluate(GetExpandedQuery()) ?? string.Empty;
    }

    private bool SetVariableCore(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        _variables[name.TrimStart('$')] = value;
        return true;
    }
}
