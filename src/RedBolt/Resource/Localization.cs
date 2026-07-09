namespace RedBolt;

public sealed class Localization
{
    private readonly List<string> _languages = [];
    private readonly Dictionary<string, Dictionary<StringHash, string>> _strings = new(StringComparer.Ordinal);
    private ResourceCache? _resources;
    private int _languageIndex = -1;

    public Localization(ResourceCache? resources = null)
    {
        this._resources = resources;
    }

    public void SetResourceCache(ResourceCache? resources)
    {
        this._resources = resources;
    }

    public ResourceCache? GetResourceCache() => _resources;

    public int GetNumLanguages() => _languages.Count;

    public int GetLanguageIndex() => _languageIndex;

    public int GetLanguageIndex(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        return _languages.FindIndex(existing => string.Equals(existing, language, StringComparison.Ordinal));
    }

    public string GetLanguage()
    {
        return _languageIndex >= 0 && _languageIndex < _languages.Count ? _languages[_languageIndex] : string.Empty;
    }

    public string GetLanguage(int index)
    {
        return index >= 0 && index < _languages.Count ? _languages[index] : string.Empty;
    }

    public void SetLanguage(int index)
    {
        if (index >= 0 && index < _languages.Count)
            _languageIndex = index;
    }

    public void SetLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var index = GetLanguageIndex(language);
        if (index >= 0)
            _languageIndex = index;
    }

    public string Get(string id, int index = -1)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        if (_languages.Count == 0)
            return id;

        var language = index >= 0 && index < _languages.Count ? _languages[index] : GetLanguage();
        if (string.IsNullOrEmpty(language))
            return id;

        return _strings.TryGetValue(language, out var translations) && translations.TryGetValue(new StringHash(id), out var translation) && !string.IsNullOrEmpty(translation)
            ? translation
            : id;
    }

    public void Reset()
    {
        _languages.Clear();
        _strings.Clear();
        _languageIndex = -1;
    }

    public void LoadJSONFile(string name, string language = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var jsonFile = _resources?.GetResource<JSONFile>(name);
        if (jsonFile is null)
            return;

        if (string.IsNullOrEmpty(language))
            LoadMultipleLanguageJSON(jsonFile.GetRoot());
        else
            LoadSingleLanguageJSON(jsonFile.GetRoot(), language);
    }

    public void LoadMultipleLanguageJSON(JSONValue source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var entry in source.GetObject())
        {
            if (string.IsNullOrEmpty(entry.Key) || !entry.Value.IsObject)
                continue;

            foreach (var translation in entry.Value.GetObject())
            {
                if (string.IsNullOrEmpty(translation.Key))
                    continue;

                var translatedString = translation.Value.GetString();
                if (string.IsNullOrEmpty(translatedString))
                    continue;

                StoreTranslation(entry.Key, translation.Key, translatedString);
            }
        }
    }

    public void LoadSingleLanguageJSON(JSONValue source, string language)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        foreach (var entry in source.GetObject())
        {
            if (string.IsNullOrEmpty(entry.Key) || !entry.Value.IsString)
                continue;

            var translatedString = entry.Value.GetString();
            if (!string.IsNullOrEmpty(translatedString))
                StoreTranslation(entry.Key, language, translatedString);
        }
    }

    private void StoreTranslation(string id, string language, string translatedString)
    {
        if (!_strings.TryGetValue(language, out var translations))
        {
            translations = new Dictionary<StringHash, string>();
            _strings.Add(language, translations);
        }

        translations[new StringHash(id)] = translatedString;
        if (!_languages.Contains(language, StringComparer.Ordinal))
            _languages.Add(language);
        if (_languageIndex < 0)
            _languageIndex = 0;
    }
}
