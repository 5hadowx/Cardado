using System.Collections.Generic;

/// <summary>
/// Rules used only to determine whether a player's cards allow a War declaration.
/// Royalty is a standalone claim. Special cards count as two card-equivalents.
/// Mirror and Executioner are black wildcards and can represent any symbol.
/// </summary>
public static class CardadoWarCardRules
{
    public static int GetWarValue(CardInstance card)
    {
        if (card == null || card.data == null) return 0;
        return card.data.rarity == CardRarity.Special ? 2 : 1;
    }

    public static bool IsBlackWildcard(CardInstance card)
    {
        return card != null && card.data != null &&
               (card.data.cardType == CardType.Mirror || card.data.cardType == CardType.Executioner);
    }

    public static bool IsRoyalty(CardInstance card)
    {
        if (card == null || card.data == null) return false;
        return card.data.cardType == CardType.King ||
               card.data.cardType == CardType.Queen ||
               card.data.cardType == CardType.GordonRobleys;
    }

    public static bool HasValidClaim(List<CardInstance> cards) => FindOptimalClaim(cards) != null;

    /// <summary>
    /// Returns the smallest valid subset for one War claim.
    /// Priority: standalone royalty, then two-card special+normal combinations,
    /// then three physical cards forming three equal or three different symbols,
    /// with black wildcards filling missing symbols.
    /// </summary>
    public static List<CardInstance> FindOptimalClaim(List<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0) return null;

        for (int i = 0; i < cards.Count; i++)
            if (IsRoyalty(cards[i])) return new List<CardInstance> { cards[i] };

        // A special card counts as two. A special card plus one normal card is
        // sufficient only when the normal card matches its symbol, or when the
        // special card itself is a black wildcard.
        for (int i = 0; i < cards.Count; i++)
        {
            CardInstance special = cards[i];
            if (special == null || special.data == null || special.data.rarity != CardRarity.Special) continue;

            for (int j = 0; j < cards.Count; j++)
            {
                if (i == j) continue;
                CardInstance normal = cards[j];
                if (normal == null || normal.data == null || normal.data.rarity == CardRarity.Special) continue;
                if (IsBlackWildcard(special) || IsBlackWildcard(normal) || special.data.cardType == normal.data.cardType)
                    return new List<CardInstance> { special, normal };
            }
        }

        if (cards.Count >= 3)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                for (int j = i + 1; j < cards.Count; j++)
                {
                    for (int k = j + 1; k < cards.Count; k++)
                    {
                        if (IsValidThreeCardCombination(cards[i], cards[j], cards[k]))
                            return new List<CardInstance> { cards[i], cards[j], cards[k] };
                    }
                }
            }
        }

        return null;
    }

    public static bool IsValidThreeCardCombination(CardInstance a, CardInstance b, CardInstance c)
    {
        if (a == null || b == null || c == null || a.data == null || b.data == null || c.data == null)
            return false;

        CardInstance[] cards = { a, b, c };
        int nonWildcardCount = 0;
        CardType firstType = default(CardType);
        bool hasFirstType = false;
        bool allSame = true;

        foreach (CardInstance card in cards)
        {
            if (IsBlackWildcard(card)) continue;
            nonWildcardCount++;
            if (!hasFirstType)
            {
                firstType = card.data.cardType;
                hasFirstType = true;
            }
            else if (card.data.cardType != firstType)
            {
                allSame = false;
            }
        }

        // Three wildcards, or one/two wildcards with only one concrete symbol,
        // can all be assigned to the same symbol.
        if (nonWildcardCount <= 1) return true;

        // Three concrete cards of one symbol are valid.
        if (allSame) return true;

        // Three concrete cards of three different symbols are valid.
        // With one wildcard, two concrete different symbols are also valid.
        return nonWildcardCount == 2 || nonWildcardCount == 3 && HasThreeDifferentConcreteSymbols(cards);
    }

    private static bool HasThreeDifferentConcreteSymbols(CardInstance[] cards)
    {
        CardType first = default(CardType);
        CardType second = default(CardType);
        int count = 0;
        foreach (CardInstance card in cards)
        {
            if (IsBlackWildcard(card)) continue;
            if (count == 0) first = card.data.cardType;
            else if (count == 1) second = card.data.cardType;
            else if (card.data.cardType == first || card.data.cardType == second) return false;
            count++;
        }
        return count == 3;
    }

    public static bool IsValidThreeCardCombination(List<CardInstance> cards)
    {
        return cards != null && cards.Count == 3 && IsValidThreeCardCombination(cards[0], cards[1], cards[2]);
    }
}
