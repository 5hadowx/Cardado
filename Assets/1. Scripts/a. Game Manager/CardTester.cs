using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.InputSystem; // ✅ New Input System

public class CardTester : MonoBehaviour
{
    [Header("References")]
    public CardDisplay cardDisplay;

    [Header("Test Cards")]
    public CardData artistCard;
    public CardData knightCard;

    private int currentLocaleIndex = 0;

    void Update()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame)
        {
            if (artistCard != null)
            {
                cardDisplay.ShowCard(artistCard);
                Debug.Log("🖌️ Showing Artist card");
            }
        }

        if (Keyboard.current.sKey.wasPressedThisFrame)
        {
            if (knightCard != null)
            {
                cardDisplay.ShowCard(knightCard);
                Debug.Log("🛡️ Showing Knight card");
            }
        }

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            SwitchLanguage();
        }
    }

    private void SwitchLanguage()
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales.Count == 0) return;

        currentLocaleIndex = (currentLocaleIndex + 1) % locales.Count;
        LocalizationSettings.SelectedLocale = locales[currentLocaleIndex];

        Debug.Log("🌐 Switched language to: " + LocalizationSettings.SelectedLocale.Identifier.Code);
    }
}
