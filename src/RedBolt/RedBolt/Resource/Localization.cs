using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Language changed.</summary>
public readonly record struct ChangeLanguage;

/// <summary>Localization subsystem. Stores all the strings in all languages.</summary>
public class Localization : RedBolt.Object
{
    /// <summary>Construct.</summary>
    public Localization()
    {
    }

    /// <summary>Destruct. Free all resources.</summary>
    public override void Dispose()
    {
        _strings.Clear();
        _pattern = null;
        _languages.Clear();
        base.Dispose();
    }

    /// <summary>Return the number of languages.</summary>
    public int GetNumLanguages() => _languages.Count;

    /// <summary>Return the index number of current language. The index is determined by the order of loading.</summary>
    public int GetLanguageIndex() => _languageIndex;

    /// <summary>Return the index number of language. The index is determined by the order of loading.</summary>
    public int GetLanguageIndex(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            Log.GetLogger().LogWarning("Cannot get localization language index: language name is empty");
            return -1;
        }

        if (GetNumLanguages() == 0)
        {
            Log.GetLogger().LogWarning("Cannot get localization language index: no loaded languages");
            return -1;
        }

        for (var i = 0; i < GetNumLanguages(); ++i)
        {
            if (_languages[i] == language)
                return i;
        }

        return -1;
    }

    /// <summary>Return the name of current language.</summary>
    public string GetLanguage()
    {
        if (_languageIndex == -1)
        {
            Log.GetLogger().LogWarning("Cannot get localization language: no loaded languages");
            return string.Empty;
        }
        return _languages[_languageIndex];
    }

    /// <summary>Return the name of language.</summary>
    public string GetLanguage(int index)
    {
        if (GetNumLanguages() == 0)
        {
            Log.GetLogger().LogWarning("Cannot get localization language: no loaded languages");
            return string.Empty;
        }

        if (index < 0 || index >= GetNumLanguages())
        {
            Log.GetLogger().LogWarning("Cannot get localization language: index out of range");
            return string.Empty;
        }

        return _languages[index];
    }

    /// <summary>
    /// Set regex pattern for localizable strings.
    /// If empty, localization is attempted for all strings.
    /// If non-empty, localization is attempted only for strings that match regular expression pattern.
    /// </summary>
    public void SetStringPattern(string pattern)
    {
        try
        {
            _pattern = string.IsNullOrEmpty(pattern) ? null : new Regex(pattern);
        }
        catch (Exception exception)
        {
            Log.GetLogger().LogError("Cannot set localizable string pattern: {Message}", exception.Message);
            _pattern = null;
        }
    }

    /// <summary>Set current language.</summary>
    public void SetLanguage(int index)
    {
        if (GetNumLanguages() == 0)
        {
            Log.GetLogger().LogWarning("Cannot set localization language: no loaded languages");
            return;
        }

        if (index < 0 || index >= GetNumLanguages())
        {
            Log.GetLogger().LogWarning("Cannot set localization language: index out of range");
            return;
        }

        if (index != _languageIndex)
        {
            _languageIndex = index;
            SendEvent<ChangeLanguage>();
        }
    }

    /// <summary>Set current language.</summary>
    public void SetLanguage(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            Log.GetLogger().LogWarning("Cannot set localization language: language name is empty");
            return;
        }

        if (GetNumLanguages() == 0)
        {
            Log.GetLogger().LogWarning("Cannot set localization language: no loaded languages");
            return;
        }

        var index = GetLanguageIndex(language);
        if (index == -1)
        {
            Log.GetLogger().LogWarning("Cannot set localization language: language not found");
            return;
        }

        SetLanguage(index);
    }

    /// <summary>
    /// Return a string in the current language. Returns an empty string if id is empty. Returns id if translation is not
    /// found and logs a warning.
    /// </summary>
    public string Get(string id, int index = -1)
    {
        if (string.IsNullOrEmpty(id))
            return string.Empty;

        if (GetNumLanguages() == 0)
        {
            Log.GetLogger().LogWarning("Cannot get localized string: no loaded languages");
            return id;
        }

        if (index >= _languages.Count)
        {
            Log.GetLogger().LogWarning("Cannot get localized string: invalid language index");
            return id;
        }

        if (_pattern is not null)
        {
            var match = _pattern.Match(id);
            if (!match.Success || match.Index != 0 || match.Length != id.Length)
            {
                // Quietly ignore strings that don't match the pattern
                return id;
            }
        }

        var language = index < 0 ? GetLanguage() : _languages[index];
        var languageHash = new StringHash(language);
        if (!_strings.TryGetValue(languageHash, out var translations))
        {
            translations = [];
            _strings[languageHash] = translations;
        }

        var idHash = new StringHash(id);
        if (!translations.TryGetValue(idHash, out var result))
        {
            result = string.Empty;
            translations[idHash] = result;
        }

        if (string.IsNullOrEmpty(result))
        {
            if (_pattern is not null)
                Log.GetLogger().LogWarning("Cannot get localized string for \"{Id}\" ({Language})", id, language);
            else
                Log.GetLogger().LogTrace("Cannot get localized string for \"{Id}\" ({Language})", id, language);
            return id;
        }
        return result;
    }

    /// <summary>Clear all loaded strings.</summary>
    public void Reset()
    {
        _languages.Clear();
        _languageIndex = -1;
        _strings.Clear();
    }

    /// <summary>Load strings from JSONFile. The file should be UTF8 without BOM.</summary>
    public void LoadJSONFile(string name, string language = "")
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var jsonFile = cache.GetResource<JSONFile>(name);
        if (jsonFile is not null)
        {
            if (string.IsNullOrEmpty(language))
                LoadMultipleLanguageJSON(jsonFile.Root);
            else
                LoadSingleLanguageJSON(jsonFile.Root, language);
        }
    }

    /// <summary>Load strings from JSONValue.</summary>
    public void LoadMultipleLanguageJSON(JSONValue source)
    {
        foreach (var item in source.GetObject())
        {
            var id = item.Key;
            if (string.IsNullOrEmpty(id))
            {
                Log.GetLogger().LogWarning("Error loading localization JSON: string ID is empty");
                continue;
            }

            var value = item.Value;
            if (value.IsObject)
            {
                foreach (var translation in value.GetObject())
                {
                    var language = translation.Key;
                    if (string.IsNullOrEmpty(language))
                    {
                        Log.GetLogger().LogWarning("Error loading localization JSON: language name is empty for \"{Id}\"", id);
                        continue;
                    }

                    var translatedString = translation.Value.GetString();
                    if (string.IsNullOrEmpty(translatedString))
                    {
                        Log.GetLogger().LogWarning(
                            "Error loading localization JSON: translation is empty for \"{Id}\" ({Language})", id, language);
                        continue;
                    }

                    var languageHash = new StringHash(language);
                    if (!_strings.TryGetValue(languageHash, out var translations))
                    {
                        translations = [];
                        _strings[languageHash] = translations;
                    }

                    var idHash = new StringHash(id);
                    if (translations.TryGetValue(idHash, out var existing) && existing != string.Empty)
                        Log.GetLogger().LogWarning("Overriding translation for \"{Id}\" ({Language})", id, language);

                    translations[idHash] = translatedString;
                    if (!_languages.Contains(language))
                        _languages.Add(language);
                    if (_languageIndex == -1)
                        _languageIndex = 0;
                }
            }
            else
                Log.GetLogger().LogWarning("Error loading localization JSON values for \"{Id}\"", id);
        }
    }

    /// <summary>Load strings from JSONValue for specific language.</summary>
    public void LoadSingleLanguageJSON(JSONValue source, string language = "")
    {
        foreach (var item in source.GetObject())
        {
            var id = item.Key;
            if (string.IsNullOrEmpty(id))
            {
                Log.GetLogger().LogWarning("Error loading localization JSON: string ID is empty");
                continue;
            }

            var value = item.Value;
            if (value.IsString)
            {
                if (string.IsNullOrEmpty(value.GetString()))
                {
                    Log.GetLogger().LogWarning(
                        "Error loading localization JSON: translation is empty for \"{Id}\" ({Language})", id, language);
                    continue;
                }

                var languageHash = new StringHash(language);
                if (!_strings.TryGetValue(languageHash, out var translations))
                {
                    translations = [];
                    _strings[languageHash] = translations;
                }

                var idHash = new StringHash(id);
                if (translations.TryGetValue(idHash, out var existing) && existing != string.Empty)
                    Log.GetLogger().LogWarning("Overriding translation for \"{Id}\" ({Language})", id, language);

                translations[idHash] = value.GetString();
                if (!_languages.Contains(language))
                    _languages.Add(language);
            }
            else
                Log.GetLogger().LogWarning("Error loading localization JSON values for \"{Id}\" ({Language})", id, language);
        }
    }

    /// <summary>Language names.</summary>
    private readonly List<string> _languages = [];
    /// <summary>Index of current language.</summary>
    private int _languageIndex = -1;
    /// <summary>Localizable string pattern.</summary>
    private Regex? _pattern;
    /// <summary>Storage strings: &lt;Language &lt;StringId, Value&gt; &gt;.</summary>
    private readonly Dictionary<StringHash, Dictionary<StringHash, string>> _strings = [];
}
