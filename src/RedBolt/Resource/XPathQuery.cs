// C# wraps System.Xml.XPath. Implementation is C#-only.
using System.Globalization;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RedBolt;

public class XPathResultSet : IReadOnlyList<XMLElement>, ICloneable, IDisposable
{
    private IReadOnlyList<XElement>? _results;
    private XMLFile? _file;

    public XPathResultSet() : this([], null) { }

    internal XPathResultSet(IEnumerable<XElement> results, XMLFile? file = null)
        : this(results.ToArray(), file)
    {
    }

    private XPathResultSet(IReadOnlyList<XElement>? results, XMLFile? file)
    {
        _results = results;
        _file = file;
    }

    public object Clone() => new XPathResultSet(_results?.ToArray(), _file);

    public void Dispose()
    {
        _results = null;
        _file = null;
    }

    public XMLElement this[int index] => _results is not null && index >= 0 && index < _results.Count
        ? new XMLElement(_file, this, _results[index], index)
        : new XMLElement();

    public int Count => _results?.Count ?? 0;

    public XMLElement FirstResult() => this[0];

    public int Size() => Count;

    public bool Empty() => Count == 0;

    public IEnumerator<XMLElement> GetEnumerator()
    {
        for (var i = 0; i < Count; ++i)
            yield return this[i];
    }

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
