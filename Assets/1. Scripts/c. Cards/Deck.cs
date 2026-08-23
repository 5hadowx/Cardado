using System;
using System.Collections.Generic;

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
    }

    public void Shuffle()
    {
        ShuffleList(drawPile);
    }

    public CardInstance Draw()
    {
        RecycleDiscardIfNeeded();

        if (drawPile.Count == 0)
            return null;

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

    private void RecycleDiscardIfNeeded()
    {
        if (drawPile.Count > 0 || discardPile.Count == 0)
            return;

        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle();
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
