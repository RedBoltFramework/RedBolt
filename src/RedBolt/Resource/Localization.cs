using Microsoft.Extensions.Logging;

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

    public int GetNumLanguages() => _languages.Count;

    public int GetLanguageIndex() => _languageIndex;

    public int GetLanguageIndex(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            Log.GetLogger().LogWarning("Localization.GetLanguageIndex: language name is empty");
            return -1;
        }
        if (_languages.Count == 0)
        {
            Log.GetLogger().LogWarning("Localization.GetLanguageIndex: no loaded languages");
            return -1;
        }
        return _languages.FindIndex(existing => string.Equals(existing, language, StringComparison.Ordinal));
    }

    public string GetLanguage()
    {
        if (_languageIndex == -1)
        {
            Log.GetLogger().LogWarning("Localization.GetLanguage: no loaded languages");
            return string.Empty;
        }
        return _languages[_languageIndex];
    }

    public string GetLanguage(int index)
    {
        if (_languages.Count == 0)
        {
            Log.GetLogger().LogWarning("Localization.GetLanguage(index): no loaded languages");
            return string.Empty;
        }
        if (index < 0 || index >= _languages.Count)
        {
            Log.GetLogger().LogWarning("Localization.GetLanguage(index): index out of range");
            return string.Empty;
        }
        return _languages[index];
    }

    public void SetLanguage(int index)
    {
        if (_languages.Count == 0)
        {
            Log.GetLogger().LogWarning("Localization.SetLanguage(index): no loaded languages");
            return;
        }
        if (index < 0 || index >= _languages.Count)
        {
            Log.GetLogger().LogWarning("Localization.SetLanguage(index): index out of range");
            return;
        }
        if (index != _languageIndex)
        {
            _languageIndex = index;
            SendEvent<ChangeLanguage>();
        }
    }

    public void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            Log.GetLogger().LogWarning("Localization.SetLanguage(language): language name is empty");
            return;
        }
        if (_languages.Count == 0)
        {
            Log.GetLogger().LogWarning("Localization.SetLanguage(language): no loaded languages");
            return;
        }
        var index = GetLanguageIndex(language);
        if (index == -1)
        {
            Log.GetLogger().LogWarning("Localization.SetLanguage(language): language not found");
            return;
        }
        SetLanguage(index);
    }

    public string Get(string id, int index = -1)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;
        if (_languages.Count == 0)
        {
            Log.GetLogger().LogWarning("Localization.Get(id): no loaded languages");
            return id;
        }
        if (index >= _languages.Count)
        {
            Log.GetLogger().LogWarning("Localization.Get(id): invalid language index");
            return id;
        }

        var language = index >= 0 && index < _languages.Count ? _languages[index] : GetLanguage();
        if (_strings.TryGetValue(language, out var translations) && translations.TryGetValue(new StringHash(id), out var translation) && !string.IsNullOrEmpty(translation))
            return translation;

        Log.GetLogger().LogTrace("Localization.Get(\"{Id}\") not found translation, language=\"{Language}\"", id, language);
        return id;
    }

    public void Reset()
    {
        _languages.Clear();
        _strings.Clear();
        _languageIndex = -1;
    }

    public void LoadJSONFile(string name, string language = "")
    {
        var cache = GetSubsystem<ResourceCache>();
        var jsonFile = cache!.GetResource<JSONFile>(name);

        if (jsonFile is not null)
        {
            if (string.IsNullOrEmpty(language))
                LoadMultipleLanguageJSON(jsonFile.Root);
            else
                LoadSingleLanguageJSON(jsonFile.Root, language);
        }
    }

    public void LoadMultipleLanguageJSON(JSONValue source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var entry in source.GetObject())
        {
            if (string.IsNullOrEmpty(entry.Key) || !entry.Value.IsObject)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    Log.GetLogger().LogWarning("Localization.LoadMultipleLanguageJSON: string ID is empty");
                else
                    Log.GetLogger().LogWarning("Localization.LoadMultipleLanguageJSON: failed to load values, string ID=\"{Id}\"", entry.Key);
                continue;
            }

            foreach (var translation in entry.Value.GetObject())
            {
                if (string.IsNullOrEmpty(translation.Key))
                {
                    Log.GetLogger().LogWarning("Localization.LoadMultipleLanguageJSON: language name is empty, string ID=\"{Id}\"", entry.Key);
                    continue;
                }

                var translatedString = translation.Value.GetString();
                if (string.IsNullOrEmpty(translatedString))
                {
                    Log.GetLogger().LogWarning("Localization.LoadMultipleLanguageJSON: translation is empty, string ID=\"{Id}\", language=\"{Language}\"", entry.Key, translation.Key);
                    continue;
                }

                StoreTranslation(entry.Key, translation.Key, translatedString, true);
            }
        }
    }

    public void LoadSingleLanguageJSON(JSONValue source, string language = "")
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var entry in source.GetObject())
        {
            if (string.IsNullOrEmpty(entry.Key) || !entry.Value.IsString)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    Log.GetLogger().LogWarning("Localization.LoadSingleLanguageJSON: string ID is empty");
                else
                    Log.GetLogger().LogWarning("Localization.LoadSingleLanguageJSON: failed to load value, string ID=\"{Id}\", language=\"{Language}\"", entry.Key, language);
                continue;
            }

            var translatedString = entry.Value.GetString();
            if (!string.IsNullOrEmpty(translatedString))
                StoreTranslation(entry.Key, language, translatedString, false);
            else
                Log.GetLogger().LogWarning("Localization.LoadSingleLanguageJSON: translation is empty, string ID=\"{Id}\", language=\"{Language}\"", entry.Key, language);
        }
    }

    private void StoreTranslation(string id, string language, string translatedString, bool selectFirstLanguage)
    {
        if (!_strings.TryGetValue(language, out var translations))
        {
            translations = new Dictionary<StringHash, string>();
            _strings.Add(language, translations);
        }

        var hash = new StringHash(id);
        if (translations.TryGetValue(hash, out var existingTranslation) && !string.IsNullOrEmpty(existingTranslation))
            Log.GetLogger().LogWarning("Localization: override translation, string ID=\"{Id}\", language=\"{Language}\"", id, language);

        translations[hash] = translatedString;
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
        if (selectFirstLanguage && _languageIndex < 0)
            _languageIndex = 0;
    }
}
