using System.Collections.Generic;

public class Hand
{
    public List<CardInstance> cardsInHand = new List<CardInstance>();

    public void AddCard(CardInstance card)
    {
        if (card != null)
            cardsInHand.Add(card);
    }

    public void RemoveCard(CardInstance card)
    {
        if (cardsInHand.Contains(card))
            cardsInHand.Remove(card);
    }
}

