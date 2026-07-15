namespace RedBolt;

public readonly record struct ChangeLanguage;

public sealed class Localization : Object
{
    private readonly List<string> _languages = [];
    private readonly Dictionary<string, Dictionary<StringHash, string>> _strings = new(StringComparer.Ordinal);
    private int _languageIndex = -1;

    public Localization()
    {
    }

    // TODO: port - C++ code:
    // /// Destruct. Free all resources.
    // ~Localization() override;

    public int GetNumLanguages() => _languages.Count;

    public int GetLanguageIndex() => _languageIndex;

    // C++: if (language.empty()) { URHO3D_LOGWARNING("...language name is empty"); return -1; }
    // C++: if (GetNumLanguages() == 0) { URHO3D_LOGWARNING("...no loaded languages"); return -1; }
    public int GetLanguageIndex(string language)
    {
        if (string.IsNullOrEmpty(language))
            return -1;
        return _languages.FindIndex(existing => string.Equals(existing, language, StringComparison.Ordinal));
    }

    // C++: checks languageIndex_ == -1 and logs URHO3D_LOGWARNING("...no loaded languages") returning EMPTY_STRING.
    // C++: after check, returns languages_[languageIndex_] without range check.
    public string GetLanguage()
    {
        return _languageIndex >= 0 && _languageIndex < _languages.Count ? _languages[_languageIndex] : string.Empty;
    }

    // C++: three separate checks with URHO3D_LOGWARNING for (no languages), (index out of range), then returns languages_[index].
    public string GetLanguage(int index)
    {
        return index >= 0 && index < _languages.Count ? _languages[index] : string.Empty;
    }

    // C++: checks GetNumLanguages()==0 → URHO3D_LOGWARNING("no loaded languages"); checks index out of range → URHO3D_LOGWARNING("index out of range").
    public void SetLanguage(int index)
    {
        if (index >= 0 && index < _languages.Count && index != _languageIndex)
        {
            _languageIndex = index;
            SendEvent<ChangeLanguage>();
        }
    }

    // C++: checks language.empty() → URHO3D_LOGWARNING; checks GetNumLanguages()==0 → URHO3D_LOGWARNING; checks index==-1 → URHO3D_LOGWARNING("language not found").
    public void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
            return;
        var index = GetLanguageIndex(language);
        if (index >= 0)
            SetLanguage(index);
    }

    // C++: URHO3D_LOGWARNING("no loaded languages") when GetNumLanguages()==0.
    // C++: URHO3D_LOGWARNING("invalid language index") when index >= languages_.size().
    // C++: URHO3D_LOGTRACE when translation not found.
    // C++: strings_[index < 0 ? GetLanguage() : languages_[index]][StringHash(id)] — no extra null-language guard.
    public string Get(string id, int index = -1)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        if (_languages.Count == 0)
            return id;
        if (index >= _languages.Count)
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

    // C++: does not validate name parameter; does not null-check cache before dereferencing.
    public void LoadJSONFile(string name, string language = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var cache = GetSubsystem<ResourceCache>();
        if (cache is null)
            return;
        var jsonFile = cache.GetResource<JSONFile>(name);
        if (jsonFile is null)
            return;

        if (string.IsNullOrEmpty(language))
            LoadMultipleLanguageJSON(jsonFile.GetRoot());
        else
            LoadSingleLanguageJSON(jsonFile.GetRoot(), language);
    }

    // C++: logs URHO3D_LOGWARNING entries for empty string ID, empty language name, empty translation, and override detection.
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

    // C++: language parameter has default = EMPTY_STRING (optional).
    // C++: does not save/restore _languageIndex around StoreTranslation calls.
    // C++: logs URHO3D_LOGWARNING for empty string ID, empty translation, and override detection.
    // C++: LoadSingleLanguageJSON does NOT set languageIndex_ = 0 when it was -1 (only LoadMultipleLanguageJSON does).
    public void LoadSingleLanguageJSON(JSONValue source, string language)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var languageIndex = _languageIndex;
        foreach (var entry in source.GetObject())
        {
            if (string.IsNullOrEmpty(entry.Key) || !entry.Value.IsString)
                continue;

            var translatedString = entry.Value.GetString();
            if (!string.IsNullOrEmpty(translatedString))
                StoreTranslation(entry.Key, language, translatedString);
        }
        _languageIndex = languageIndex;
    }

    // C++: in LoadMultipleLanguageJSON, checks strings_[StringHash(lang)][StringHash(id)] != EMPTY_STRING
    // and logs URHO3D_LOGWARNING("override translation") before overwriting.
    private void StoreTranslation(string id, string language, string translatedString)
    {
        if (!_strings.TryGetValue(language, out var translations))
        {
            translations = new Dictionary<StringHash, string>();
            _strings.Add(language, translations);
        }

        translations[new StringHash(id)] = translatedString;
        var hasLanguage = false;
        foreach (var existing in _languages)
        {
            if (string.Equals(existing, language, StringComparison.Ordinal))
            {
                hasLanguage = true;
                break;
            }
        }

        if (!hasLanguage)
            _languages.Add(language);
        if (_languageIndex < 0)
            _languageIndex = 0;
    }
}
