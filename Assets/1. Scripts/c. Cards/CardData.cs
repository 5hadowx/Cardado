using UnityEngine;
using UnityEngine.Localization;

public enum CardType
{
    Artist,
    Knight,
    Collector,
    Bodyguard,
    Mirror,
    Executioner,
    Joker,
    King,
    Queen,
    GordonRobleys
}

public enum CardRarity
{
    Empty,
    Normal,
    Special,
    Royalty
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Cardado/Card")]
public class CardData : ScriptableObject
{
    public string id;

    [Header("Localization")]
    public LocalizedString cardName;
    public LocalizedString cardDescription;

    [Header("Metadata")]
    public CardType cardType;
    public CardRarity rarity;
    public Sprite artwork;

    [Header("Gameplay")]
    public bool isBlankCard;
    public bool isModifier;
    public bool canAdd;
    public bool canSubtract;
    public bool isPersistent;
}
