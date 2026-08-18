using System.Collections.Generic;
using UnityEngine;

public class DeckTester : MonoBehaviour
{
    [Header("All Card Definitions")]
    public CardData[] allCards;

    [Header("References")]
    public HandUI handUI;
    public CardDisplay bigDisplay; // for showing hovered card
    public CanvasGroup bigCardCanvasGroup; // attach a CanvasGroup to big card
    public CanvasGroup handAreaCanvasGroup; // attach the handArea canvas group

    [Header("Animation")]
    public float fadeDuration = 0.2f;

    private Deck testDeck;
    private Hand playerHand = new Hand();

    private void Start()
    {
        // Initialize deck
        testDeck = new Deck(new List<CardData>(allCards));
        testDeck.Shuffle();

        // Draw first 5 cards into hand
        for (int i = 0; i < 5; i++)
        {
            var drawnCard = testDeck.Draw();
            if (drawnCard != null)
                playerHand.AddCard(drawnCard);
        }

        // Ensure hidden at start
        bigCardCanvasGroup.alpha = 0f;
        bigDisplay.gameObject.SetActive(false);

        // Show hand with hover callbacks (enter takes CardInstance, exit is parameterless)
        handUI.ShowHand(playerHand.cardsInHand, OnCardHoverEnter, OnCardHoverExit);
    }

    private void OnCardHoverEnter(CardInstance card)
    {
        bigDisplay.ShowCard(card.data);
        
        bigDisplay.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeCanvasGroup(bigCardCanvasGroup, bigCardCanvasGroup.alpha, 1f));
    }

    private void OnCardHoverExit(CardInstance card)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutAndHide());
    }

    private System.Collections.IEnumerator FadeOutAndHide()
    {
        yield return FadeCanvasGroup(bigCardCanvasGroup, bigCardCanvasGroup.alpha, 0f);
        bigDisplay.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
    }

    private string GetHierarchyPath(Transform current)
    {
        string path = current.name;

        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}

