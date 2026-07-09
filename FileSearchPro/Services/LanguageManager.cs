using System.Windows;

namespace FileSearchPro.Services;

public static class LanguageManager
{
    private const string RuKey = "ru";
    private const string EnKey = "en";
    private static string _currentLanguage = RuKey;

    public static string CurrentLanguage => _currentLanguage;

    public static event Action? LanguageChanged;

    public static void SetLanguage(string lang)
    {
        if (lang != RuKey && lang != EnKey) return;
        if (lang == _currentLanguage) return;

        _currentLanguage = lang;

        var app = Application.Current;
        var dictionaries = app.Resources.MergedDictionaries;

        dictionaries.Clear();
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"Resources/Strings.{lang}.xaml", UriKind.Relative)
        });

        LanguageChanged?.Invoke();
    }

    public static string GetString(string key)
    {
        if (Application.Current.TryFindResource(key) is string value)
            return value;
        return key;
    }
}
