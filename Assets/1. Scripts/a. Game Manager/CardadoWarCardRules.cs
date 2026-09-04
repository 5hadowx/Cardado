using System.Collections.Generic;

/// <summary>
/// Rules used only to determine whether a player's cards allow a War declaration.
/// Royalty is a standalone claim. Special cards count as two card-equivalents.
/// Mirror and Executioner are black wildcards; Special Mirror/Executioner therefore
/// count as two wildcards and can combine with any other valid card.
/// </summary>
public static class CardadoWarCardRules
{
    public static int GetWarValue(CardInstance card)
    {
        if (card == null || card.data == null)
            return 0;
        return card.data.rarity == CardRarity.Special ? 2 : 1;
    }

    public static bool IsBlackWildcard(CardInstance card)
    {
        return card != null && card.data != null &&
               (card.data.cardType == CardType.Mirror || card.data.cardType == CardType.Executioner);
    }

    public static bool IsRoyalty(CardInstance card)
    {
        if (card == null || card.data == null)
            return false;
        return card.data.cardType == CardType.King ||
               card.data.cardType == CardType.Queen ||
               card.data.cardType == CardType.GordonRobleys;
    }

    public static bool HasValidClaim(List<CardInstance> cards)
    {
        return FindOptimalClaim(cards) != null;
    }

    /// <summary>
    /// Returns the smallest/optimal subset to consume for one War.
    /// Priority is intentional: standalone royalty first, then two-card
    /// three-equivalent combinations, then three-card combinations.
    /// </summary>
    public static List<CardInstance> FindOptimalClaim(List<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0)
            return null;

        // Royalty is independently worth one War claim and should be consumed alone.
        for (int i = 0; i < cards.Count; i++)
        {
            if (IsRoyalty(cards[i]))
                return new List<CardInstance> { cards[i] };
        }

        // Special black wildcard (2 wildcards) + any other valid card (1) = War.
        for (int i = 0; i < cards.Count; i++)
        {
            CardInstance a = cards[i];
            if (a == null || a.data == null || !IsBlackWildcard(a) || a.data.rarity != CardRarity.Special)
                continue;

            for (int j = 0; j < cards.Count; j++)
            {
                if (j == i || cards[j] == null || cards[j].data == null)
                    continue;
                return new List<CardInstance> { a, cards[j] };
            }
        }

        // Special normal card (2) + matching normal card (1) = War.
        // Special normal card (2) + black wildcard (1) = War.
        for (int i = 0; i < cards.Count; i++)
        {
            CardInstance a = cards[i];
            if (a == null || a.data == null || a.data.rarity != CardRarity.Special || IsBlackWildcard(a))
                continue;

            for (int j = 0; j < cards.Count; j++)
            {
                if (j == i) continue;
                CardInstance b = cards[j];
                if (b == null || b.data == null || b.data.rarity == CardRarity.Special)
                    continue;

                if (IsBlackWildcard(b) || a.data.cardType == b.data.cardType)
                    return new List<CardInstance> { a, b };
            }
        }

        // Three-card matching-symbol or different-symbol combination.
        // Current CardData has no separate symbol field, so card type is the
        // grouping key available to the gameplay model.
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
        if (a == null || b == null || c == null ||
            a.data == null || b.data == null || c.data == null)
            return false;

        CardInstance[] cards = { a, b, c };
        int nonWildcardCount = 0;
        CardType firstType = default(CardType);
        bool hasFirstType = false;
        bool allSame = true;

        for (int i = 0; i < cards.Length; i++)
        {
            if (IsBlackWildcard(cards[i]))
                continue;

            nonWildcardCount++;
            if (!hasFirstType)
            {
                firstType = cards[i].data.cardType;
                hasFirstType = true;
            }
            else if (cards[i].data.cardType != firstType)
            {
                allSame = false;
            }
        }

        if (nonWildcardCount <= 1)
            return true;

        if (allSame)
            return true;

        // Two different non-wild symbols plus one black wildcard form three different symbols.
        return nonWildcardCount == 2;
    }

    public static bool IsValidThreeCardCombination(List<CardInstance> cards)
    {
        if (cards == null || cards.Count != 3)
            return false;
        return IsValidThreeCardCombination(cards[0], cards[1], cards[2]);
    }
}
