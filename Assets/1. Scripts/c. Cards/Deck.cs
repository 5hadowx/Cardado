using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    private List<CardInstance> cards = new List<CardInstance>();
    private System.Random rng = new System.Random();

    public Deck(List<CardData> cardDefinitions)
    {
        foreach (var def in cardDefinitions)
        {
            cards.Add(new CardInstance(def));
        }
    }

    public void Shuffle()
    {
        int n = cards.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            (cards[k], cards[n]) = (cards[n], cards[k]);
        }
    }

    public CardInstance Draw()
    {
        if (cards.Count == 0) return null;
        var card = cards[0];
        cards.RemoveAt(0);
        return card;
    }

    public int Count => cards.Count;
}

