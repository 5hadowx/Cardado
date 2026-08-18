#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Localization;

public class CardGenerator
{
    [MenuItem("Cardado/Generate Cards")]
    public static void GenerateCards()
    {
        string savePath = "Assets/0. ScriptableObjects/Cards/";
        if (!AssetDatabase.IsValidFolder(savePath))
        {
            AssetDatabase.CreateFolder("Assets/GameData", "Cards");
        }

        // Define all the cards here (ID, Artwork, NameKey, DescKey)
        CardDefinition[] cardDefs = new CardDefinition[]
        {
            new CardDefinition("artist_basic", "Artista.png", "card_artist_name", "card_artist_desc"),
            new CardDefinition("soldier_basic", "Soldado.png", "card_soldier_name", "card_soldier_desc"),
            new CardDefinition("collector_basic", "Coleccionista.png", "card_collector_name", "card_collector_desc"),
            new CardDefinition("bodyguard_basic", "Escolta.png", "card_bodyguard_name", "card_bodyguard_desc"),
            new CardDefinition("artist_special", "Artista Especial.png", "card_specialArtist_name", "card_specialArtist_desc"),
            new CardDefinition("soldier_special", "Soldado Especial.png", "card_specialSoldier_name", "card_specialSoldier_desc"),
            new CardDefinition("collector_special", "Coleccionista Especial.png", "card_collector_name", "card_collector_desc"),
            new CardDefinition("bodyguard_special", "Escolta Especial.png", "card_specialBodyguard_name", "card_specialBodyguard_desc"),
            new CardDefinition("artist_modifier", "+-1 Artista.png", "card_diceModifier_name", "card_diceModifier_desc"),
            new CardDefinition("soldier_modifier", "+-1 Soldado.png", "card_diceModifier_name", "card_diceModifier_desc"),
            new CardDefinition("collector_modifier", "+-1 Coleccionista.png", "card_diceModifier_name", "card_diceModifier_desc"),
            new CardDefinition("bodyguard_modifier", "+-1 Escolta.png", "card_diceModifier_name", "card_diceModifier_desc"),
            new CardDefinition("mirror", "Espejo.png", "card_mirror_name", "card_mirror_desc"),
            new CardDefinition("executioner", "Verdugo.png", "card_executioner_name", "card_executioner_desc"),
            new CardDefinition("joker", "Bufon.png", "joker_name", "joker_desc"),
            new CardDefinition("queen", "Reina.png", "card_queen_name", "card_queen_desc"),
            new CardDefinition("king", "Rey.png", "card_king_name", "card_king_desc"),
            new CardDefinition("nobleman", "Gordon.png", "card_nobleman_name", "card_nobleman_desc"),
        };

        foreach (var def in cardDefs)
        {
            string assetPath = savePath + def.id + ".asset";

            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(card, assetPath);
            }

            card.id = def.id;
            card.artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Cards/" + def.artworkFile);

            card.cardName = new LocalizedString { TableReference = "CardTexts", TableEntryReference = def.nameKey };
            card.cardDescription = new LocalizedString { TableReference = "CardTexts", TableEntryReference = def.descKey };

            EditorUtility.SetDirty(card);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✅ Card generation complete!");
    }

    private class CardDefinition
    {
        public string id;
        public string artworkFile;
        public string nameKey;
        public string descKey;

        public CardDefinition(string id, string artworkFile, string nameKey, string descKey)
        {
            this.id = id;
            this.artworkFile = artworkFile;
            this.nameKey = nameKey;
            this.descKey = descKey;
        }
    }
}
#endif
