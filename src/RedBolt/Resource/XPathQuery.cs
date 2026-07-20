using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace RedBolt;

/// <summary>XPath query result set.</summary>
public class XPathResultSet : IReadOnlyList<XMLElement>, ICloneable, IDisposable
{
    private IReadOnlyList<XElement>? _results;
    private XMLFile? _file;

    /// <summary>Construct empty.</summary>
    public XPathResultSet() : this(null, null) { }

    /// <summary>Construct from XML file and XPath node set.</summary>
    internal XPathResultSet(IEnumerable<XElement> results, XMLFile? file = null)
        : this(results.ToArray(), file)
    {
    }

    private XPathResultSet(IReadOnlyList<XElement>? results, XMLFile? file)
    {
        _results = results;
        _file = file;
    }

    /// <summary>Copy-construct.</summary>
    public object Clone() => new XPathResultSet(_results?.ToArray(), _file);

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        _results = null;
        _file = null;
    }

    /// <summary>Return the n-th result in the set.</summary>
    public XMLElement this[int index] => _results is not null && index >= 0 && index < _results.Count
        ? new XMLElement(_file, this, _results[index], index)
        : new XMLElement();

    public int Count => _results?.Count ?? 0;

    /// <summary>Return the first result in the set.</summary>
    public XMLElement FirstResult() => this[0];

    /// <summary>Return size of result set.</summary>
    public int Size() => Count;

    /// <summary>Return whether result set is empty.</summary>
    public bool Empty() => Count == 0;

    /// <summary>Return XPath node set.</summary>
    public IReadOnlyList<XElement>? GetXPathNodeSet() => _results;

    public IEnumerator<XMLElement> GetEnumerator()
    {
        for (var i = 0; i < Count; ++i)
            yield return this[i];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>XPath query.</summary>
public class XPathQuery : IDisposable
{
    /// <summary>Construct empty.</summary>
    public XPathQuery()
    {
    }

    /// <summary>Construct XPath query object with query string and variable string. The variable string format is "name1:type1,name2:type2,..." where type is one of "Bool", "Float", "String", "ResultSet".</summary>
    public XPathQuery(string queryString, string variableString = "")
    {
        SetQuery(queryString, variableString);
    }

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        _variables = null;
        _query = null;
        _queryString = string.Empty;
    }

    /// <summary>Bind query object with variable set.</summary>
    public void Bind()
    {
        // Delete previous query object and create a new one binding it with variable set
        var query = XPathExpression.Compile(_queryString);
        query.SetContext(_variables ?? new XPathVariableContext());
        _query = query;
    }

    /// <summary>Add/Set a bool variable. Return true if successful.</summary>
    public bool SetVariable(string name, bool value)
    {
        _variables ??= new XPathVariableContext();
        return _variables.Set(name, XPathResultType.Boolean, value);
    }

    /// <summary>Add/Set a float variable. Return true if successful.</summary>
    public bool SetVariable(string name, float value)
    {
        _variables ??= new XPathVariableContext();
        return _variables.Set(name, XPathResultType.Number, (double)value);
    }

    /// <summary>Add/Set a string variable. Return true if successful.</summary>
    public bool SetVariable(string name, string value)
    {
        _variables ??= new XPathVariableContext();
        return _variables.Set(name, XPathResultType.String, value);
    }

    /// <summary>Add/Set an XPath query result set variable. Return true if successful.</summary>
    public bool SetVariable(string name, XPathResultSet value)
    {
        _variables ??= new XPathVariableContext();
        var nodeSet = value.GetXPathNodeSet();
        if (nodeSet is null)
            return false;
        return _variables.Set(name, XPathResultType.NodeSet, nodeSet.ToArray());
    }

    /// <summary>Set XPath query string and variable string. The variable string format is "name1:type1,name2:type2,..." where type is one of "Bool", "Float", "String", "ResultSet".</summary>
    public bool SetQuery(string queryString, string variableString = "", bool bind = true)
    {
        if (variableString.Length != 0)
        {
            Clear();
            _variables = new XPathVariableContext();

            // Parse the variable string having format "name1:type1,name2:type2,..." where type is one of "Bool", "Float", "String", "ResultSet"
            var variables = variableString.Split(',');
            foreach (var variable in variables)
            {
                var tokens = variable.Trim().Split(':');
                if (tokens.Length != 2)
                    continue;

                var type = tokens[1] switch
                {
                    "Bool" => XPathResultType.Boolean,
                    "Float" => XPathResultType.Number,
                    "String" => XPathResultType.String,
                    "ResultSet" => XPathResultType.NodeSet,
                    _ => XPathResultType.Error
                };
                if (type == XPathResultType.Error)
                    return false;

                if (!_variables.Add(tokens[0], type))
                    return false;
            }
        }

        _queryString = queryString;

        if (bind)
            Bind();

        return true;
    }

    /// <summary>Clear by removing all variables and XPath query object.</summary>
    public void Clear()
    {
        _queryString = string.Empty;

        _variables = null;
        _query = null;
    }

    /// <summary>Evaluate XPath query and expecting a boolean return value.</summary>
    public bool EvaluateToBool(XMLElement element)
    {
        var value = EvaluateValue(element);
        return value switch
        {
            bool result => result,
            double result => result != 0.0 && !double.IsNaN(result),
            string result => result.Length != 0,
            XPathNodeIterator result => result.MoveNext(),
            _ => false
        };
    }

    /// <summary>Evaluate XPath query and expecting a float return value.</summary>
    public float EvaluateToFloat(XMLElement element) => (float)ToXPathNumber(EvaluateValue(element));

    /// <summary>Evaluate XPath query and expecting a string return value.</summary>
    public string EvaluateToString(XMLElement element)
    {
        var value = EvaluateValue(element);
        return value switch
        {
            bool result => result ? "true" : "false",
            double result => ToXPathString(result),
            string result => result,
            XPathNodeIterator result => result.MoveNext() ? result.Current?.Value ?? string.Empty : string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>Evaluate XPath query and expecting an XPath query result set as return value.</summary>
    /// <remarks>The XPathResultSet return value must be stored in a lhs variable to ensure the underlying result set is still valid while performing XPathResultSet.FirstResult(), indexing, and XMLElement.NextResult().</remarks>
    public XPathResultSet Evaluate(XMLElement element)
    {
        if (_query is null || element.GetFile() is null || element.Element is null)
            return new XPathResultSet();
        if (_query.ReturnType != XPathResultType.NodeSet)
            throw new XPathException("Expression does not evaluate to node set");

        var navigator = element.Element.CreateNavigator();
        var iterator = (XPathNodeIterator)navigator.Evaluate(_query);
        var results = new List<XElement>();
        while (iterator.MoveNext())
        {
            if (iterator.Current?.UnderlyingObject is XElement result)
                results.Add(result);
        }
        return new XPathResultSet(results, element.GetFile());
    }

    /// <summary>Return query string.</summary>
    public string Query => _queryString;

    /// <summary>Return compiled XPath query.</summary>
    public XPathExpression? GetXPathQuery() => _query;

    /// <summary>Return XPath variable set.</summary>
    public XsltContext? GetXPathVariableSet() => _variables;

    /// <summary>XPath query string.</summary>
    private string _queryString = string.Empty;
    /// <summary>Compiled XPath query.</summary>
    private XPathExpression? _query;
    /// <summary>XPath variable set.</summary>
    private XPathVariableContext? _variables;

    private object? EvaluateValue(XMLElement element)
    {
        if (_query is null || element.GetFile() is null || element.Element is null)
            return null;
        return element.Element.CreateNavigator().Evaluate(_query);
    }

    private static double ToXPathNumber(object? value)
    {
        return value switch
        {
            double result => result,
            bool result => result ? 1.0 : 0.0,
            string result => ParseXPathNumber(result),
            XPathNodeIterator result => result.MoveNext() ? ParseXPathNumber(result.Current?.Value ?? string.Empty) : double.NaN,
            _ => 0.0
        };
    }

    private static double ParseXPathNumber(string value)
    {
        var start = 0;
        while (start < value.Length && IsXPathWhitespace(value[start]))
            ++start;
        var end = value.Length;
        while (end > start && IsXPathWhitespace(value[end - 1]))
            --end;
        var text = value.AsSpan(start, end - start);
        if (text.IsEmpty || text[0] == '+')
            return double.NaN;
        return double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var result) ? result : double.NaN;
    }

    private static bool IsXPathWhitespace(char value) => value is ' ' or '\t' or '\r' or '\n';

    private static string ToXPathString(double value)
    {
        if (double.IsNaN(value))
            return "NaN";
        if (double.IsPositiveInfinity(value))
            return "Infinity";
        if (double.IsNegativeInfinity(value))
            return "-Infinity";
        if (value == 0.0)
            return "0";

        var scientific = Math.Abs(value).ToString("e15", CultureInfo.InvariantCulture);
        var exponentOffset = scientific.IndexOf('e');
        var exponent = int.Parse(scientific.AsSpan(exponentOffset + 1), CultureInfo.InvariantCulture) + 1;
        var mantissa = scientific.AsSpan(0, exponentOffset);
        Span<char> digits = stackalloc char[mantissa.Length - 1];
        digits[0] = mantissa[0];
        mantissa[2..].CopyTo(digits[1..]);
        var digitCount = digits.Length;
        while (digitCount > 1 && digits[digitCount - 1] == '0')
            --digitCount;

        var result = new StringBuilder(digitCount + Math.Abs(exponent) + 4);
        if (value < 0.0)
            result.Append('-');
        if (exponent <= 0)
            result.Append('0');
        else
        {
            var integerDigits = Math.Min(exponent, digitCount);
            result.Append(digits[..integerDigits]);
            result.Append('0', exponent - integerDigits);
        }

        if (exponent < digitCount)
        {
            result.Append('.');
            if (exponent < 0)
                result.Append('0', -exponent);
            result.Append(digits[Math.Max(exponent, 0)..digitCount]);
        }
        return result.ToString();
    }

    private sealed class XPathVariableContext : XsltContext
    {
        private readonly Dictionary<string, XPathVariable> _variables = new(StringComparer.Ordinal);

        public XPathVariableContext()
            : base(new NameTable())
        {
        }

        public override bool Whitespace => false;

        public bool Add(string name, XPathResultType type)
        {
            if (_variables.TryGetValue(name, out var variable))
                return variable.VariableType == type;
            _variables.Add(name, new XPathVariable(type));
            return true;
        }

        public bool Set(string name, XPathResultType type, object value)
        {
            if (!Add(name, type))
                return false;
            _variables[name].Value = value;
            return true;
        }

        public override int CompareDocument(string baseUri, string nextBaseUri) => 0;

        public override bool PreserveWhitespace(XPathNavigator node) => false;

        public override IXsltContextFunction ResolveFunction(string prefix, string name, XPathResultType[] argumentTypes) => null!;

        public override IXsltContextVariable ResolveVariable(string prefix, string name) =>
            prefix.Length == 0 && _variables.TryGetValue(name, out var variable) ? variable : null!;
    }

    private sealed class XPathVariable : IXsltContextVariable
    {
        public XPathVariable(XPathResultType type)
        {
            VariableType = type;
            Value = type switch
            {
                XPathResultType.Boolean => false,
                XPathResultType.Number => 0.0,
                XPathResultType.String => string.Empty,
                XPathResultType.NodeSet => Array.Empty<XElement>(),
                _ => string.Empty
            };
        }

        public bool IsLocal => false;

        public bool IsParam => true;

        public XPathResultType VariableType { get; }

        public object Value { get; set; }

        public object Evaluate(XsltContext xsltContext) => VariableType == XPathResultType.NodeSet
            ? new XElementXPathNodeIterator((IReadOnlyList<XElement>)Value)
            : Value;
    }

    private sealed class XElementXPathNodeIterator : XPathNodeIterator
    {
        private readonly IReadOnlyList<XElement> _elements;
        private int _position;

        public XElementXPathNodeIterator(IReadOnlyList<XElement> elements)
        {
            _elements = elements;
        }

        private XElementXPathNodeIterator(XElementXPathNodeIterator rhs)
        {
            _elements = rhs._elements;
            _position = rhs._position;
        }

        public override XPathNodeIterator Clone() => new XElementXPathNodeIterator(this);

        public override XPathNavigator? Current => _position > 0 && _position <= _elements.Count
            ? _elements[_position - 1].CreateNavigator()
            : null;

        public override int CurrentPosition => _position;

        public override bool MoveNext()
        {
            if (_position >= _elements.Count)
                return false;
            ++_position;
            return true;
        }
    }
}
