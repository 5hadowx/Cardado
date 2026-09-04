using System.Collections.Generic;

/// <summary>
/// Centralized rules for deciding whether a player's current three-card hand
/// is eligible to declare War. These values are used only for War declaration
/// claims; once War starts, cards use the normal Cardado card effects.
/// </summary>
public static class CardadoWarCardRules
{
    public static int GetWarValue(CardInstance card)
    {
        if (card == null)
            return 0;

        return card.cardType == CardType.Special ? 2 : 1;
    }

    public static bool IsBlackWildcard(CardInstance card)
    {
        if (card == null)
            return false;

        return card.cardType == CardType.Mirror ||
               card.cardType == CardType.Executioner;
    }

    public static bool HasValidClaim(List<CardInstance> cards)
    {
        if (cards == null || cards.Count != 3)
            return false;

        // A special card counts as two War-card value; a normal card counts as one.
        // A standalone King, Queen, or Gordon Robleys is also a valid claim.
        for (int i = 0; i < cards.Count; i++)
        {
            CardInstance card = cards[i];
            if (card == null)
                continue;

            if (card.cardType == CardType.King ||
                card.cardType == CardType.Queen ||
                card.cardType == CardType.GordonRobleys)
                return true;
        }

        // Special + matching normal card = three-card equivalent.
        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                CardInstance a = cards[i];
                CardInstance b = cards[j];
                if (a == null || b == null)
                    continue;

                if (a.cardType == CardType.Special &&
                    b.cardType != CardType.Special &&
                    b.cardType != CardType.Mirror &&
                    b.cardType != CardType.Executioner &&
                    a.symbol == b.symbol)
                    return true;

                if (b.cardType == CardType.Special &&
                    a.cardType != CardType.Special &&
                    a.cardType != CardType.Mirror &&
                    a.cardType != CardType.Executioner &&
                    b.symbol == a.symbol)
                    return true;
            }
        }

        // Three matching symbols.
        bool hasThreeMatchingSymbols = true;
        for (int i = 1; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[0] == null || cards[i].symbol != cards[0].symbol)
            {
                hasThreeMatchingSymbols = false;
                break;
            }
        }
        if (hasThreeMatchingSymbols)
            return true;

        // Three different symbols.
        if (cards[0] != null && cards[1] != null && cards[2] != null &&
            cards[0].symbol != cards[1].symbol &&
            cards[0].symbol != cards[2].symbol &&
            cards[1].symbol != cards[2].symbol)
            return true;

        // Mirror / Executioner are black wildcards for War declaration.
        if (IsBlackWildcard(cards[0]) || IsBlackWildcard(cards[1]) || IsBlackWildcard(cards[2]))
            return IsValidThreeCardCombination(cards);

        return false;
    }

    public static bool IsValidThreeCardCombination(List<CardInstance> cards)
    {
        if (cards == null || cards.Count != 3)
            return false;

        // A black wildcard can stand in for a missing symbol. With one or two
        // wildcards, the non-wild cards must not contradict a valid combination.
        List<CardInstance> nonWild = new List<CardInstance>();
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null)
                return false;
            if (!IsBlackWildcard(cards[i]))
                nonWild.Add(cards[i]);
        }

        if (nonWild.Count == 0)
            return true;

        if (nonWild.Count == 1)
            return true;

        // Two non-wild cards can form either the matching-symbol pattern or the
        // different-symbol pattern; the wildcard fills the third position.
        return nonWild[0].symbol == nonWild[1].symbol ||
               nonWild[0].symbol != nonWild[1].symbol;
    }
}
