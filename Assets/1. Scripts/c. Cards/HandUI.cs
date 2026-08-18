using System;
using System.Collections.Generic;
using UnityEngine;

public class HandUI : MonoBehaviour
{
    [Header("References")]
    public Transform handArea;
    public CardDisplay smallCardPrefab;   // only artwork prefab

    private List<CardDisplay> activeCards = new List<CardDisplay>();

    public void ShowHand(List<CardInstance> handCards, Action<CardInstance> onHoverEnter, Action<CardInstance> onHoverExit)
    {
        ClearHand();

        foreach (var card in handCards)
        {
            var display = Instantiate(smallCardPrefab, handArea);
            display.ShowCard(card.data); // just artwork
            activeCards.Add(display);

            // Add hover logic
            var hoverable = display.gameObject.AddComponent<CardHover>();
            hoverable.Init(card, onHoverEnter, onHoverExit);
        }
    }

    private void ClearHand()
    {
        foreach (var display in activeCards)
        {
            Destroy(display.gameObject);
        }
        activeCards.Clear();
    }
}
