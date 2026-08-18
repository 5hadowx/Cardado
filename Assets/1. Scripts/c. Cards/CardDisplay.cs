using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

public class CardDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Image artworkImage;
    public LocalizeStringEvent nameTextLocalized;
    public LocalizeStringEvent descriptionTextLocalized;

    private CardData currentData;

    public void ShowCard(CardData data)
{
    if (data == null)
    {
        Debug.LogError("CardDisplay.ShowCard called with NULL data!");
        return;
    }

    Debug.Log($"Showing card: {data.id} | Artwork: {(data.artwork != null ? data.artwork.name : "NULL")}");

    currentData = data;
    artworkImage.sprite = data.artwork;

    if (nameTextLocalized != null)
    {
        nameTextLocalized.StringReference = data.cardName;
        Debug.Log($"Name localized key: {data.cardName.TableReference}/{data.cardName.TableEntryReference}");
    }

    if (descriptionTextLocalized != null)
    {
        descriptionTextLocalized.StringReference = data.cardDescription;
        Debug.Log($"Description localized key: {data.cardDescription.TableReference}/{data.cardDescription.TableEntryReference}");
    }
}

}
