using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LanguageManager : MonoBehaviour
{
    public void SetLanguage(string localeCode)
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;
        var locale = locales.Find(l => l.Identifier.Code == localeCode);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
        }
    }
}

