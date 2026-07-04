namespace RedBolt;

public sealed class Localization
{
    private readonly List<string> languages = [];
    private readonly Dictionary<string, Dictionary<StringHash, string>> strings = new(StringComparer.Ordinal);
    private ResourceCache? resources;
    private int languageIndex = -1;

    public Localization(ResourceCache? resources = null)
    {
        this.resources = resources;
    }

    public void SetResourceCache(ResourceCache? resources)
    {
        this.resources = resources;
    }

    public ResourceCache? GetResourceCache() => resources;

    public int GetNumLanguages() => languages.Count;

    public int GetLanguageIndex() => languageIndex;

    public int GetLanguageIndex(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        return languages.FindIndex(existing => string.Equals(existing, language, StringComparison.Ordinal));
    }

    public string GetLanguage()
    {
        return languageIndex >= 0 && languageIndex < languages.Count ? languages[languageIndex] : string.Empty;
    }

    public string GetLanguage(int index)
    {
        return index >= 0 && index < languages.Count ? languages[index] : string.Empty;
    }

    public void SetLanguage(int index)
    {
        if (index >= 0 && index < languages.Count)
            languageIndex = index;
    }

    public void SetLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var index = GetLanguageIndex(language);
        if (index >= 0)
            languageIndex = index;
    }

    public string Get(string id, int index = -1)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        if (languages.Count == 0)
            return id;

        var language = index >= 0 && index < languages.Count ? languages[index] : GetLanguage();
        if (string.IsNullOrEmpty(language))
            return id;

        return strings.TryGetValue(language, out var translations) && translations.TryGetValue(new StringHash(id), out var translation) && !string.IsNullOrEmpty(translation)
            ? translation
            : id;
    }

    public void Reset()
    {
        languages.Clear();
        strings.Clear();
        languageIndex = -1;
    }

    public void LoadJSONFile(string name, string language = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var jsonFile = resources?.GetResource<JSONFile>(name);
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
        if (!strings.TryGetValue(language, out var translations))
        {
            translations = new Dictionary<StringHash, string>();
            strings.Add(language, translations);
        }

        translations[new StringHash(id)] = translatedString;
        if (!languages.Contains(language, StringComparer.Ordinal))
            languages.Add(language);
        if (languageIndex < 0)
            languageIndex = 0;
    }
}
