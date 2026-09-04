using System;
using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    private readonly List<CardInstance> drawPile = new List<CardInstance>();
    private readonly List<CardInstance> discardPile = new List<CardInstance>();
    private readonly Random rng = new Random();

    public Deck(List<CardData> cardDefinitions)
    {
        foreach (var def in cardDefinitions)
        {
            if (def != null)
                drawPile.Add(new CardInstance(def));
        }

        Debug.Log($"[Cardado] Deck initialized: {drawPile.Count} cards in draw pile.");
    }

    public void Shuffle()
    {
        ShuffleList(drawPile);
    }

    public CardInstance Draw()
    {
        RecycleDiscardIfNeeded();

        if (drawPile.Count == 0)
        {
            Debug.LogWarning("[Cardado] Deck draw failed: no cards available in draw or discard pile.");
            return null;
        }

        var card = drawPile[0];
        drawPile.RemoveAt(0);
        return card;
    }

    public void Discard(CardInstance card)
    {
        if (card != null)
            discardPile.Add(card);
    }

    public int Count => drawPile.Count;
    public int DiscardCount => discardPile.Count;
    public int AvailableCount => drawPile.Count + discardPile.Count;

    private void RecycleDiscardIfNeeded()
    {
        if (drawPile.Count > 0 || discardPile.Count == 0)
            return;

        int recycledCount = discardPile.Count;
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle();
        Debug.Log($"[Cardado] Deck recycled discard pile: {recycledCount} cards shuffled back into draw pile.");
    }

    private void ShuffleList(List<CardInstance> list)
    {
        for (int n = list.Count; n > 1; n--)
        {
            int k = rng.Next(n);
            (list[k], list[n - 1]) = (list[n - 1], list[k]);
        }
    }
}
