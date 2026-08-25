using System.Collections.Generic;

/// <summary>
/// Centralized War eligibility rules. This is kept separate from the temporary
/// War UI so the card-combination rules can be tested independently and then
/// wired into the full War manager.
/// </summary>
public static class CardadoWarRules
{
    public static bool HasWarClaim(IReadOnlyList<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0)
            return false;

        // A Royalty card is enough by itself.
        foreach (CardInstance card in cards)
        {
            if (card?.data == null)
                continue;

            if (IsRoyaltyWarCard(card))
                return true;
        }

        // A Special Mirror/Executioner counts as two cards and is wild.
        // Therefore one of them plus any basic Artist/Knight/Bodyguard/Collector
        // is sufficient for a War claim.
        for (int i = 0; i < cards.Count; i++)
        {
            if (!IsSpecialBlackWildcard(cards[i]))
                continue;

            for (int j = 0; j < cards.Count; j++)
            {
                if (i == j || cards[j]?.data == null)
                    continue;

                if (IsBasicWarSymbol(cards[j].data.cardType))
                    return true;
            }
        }

        // Special normal-symbol card + matching normal card counts as three.
        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                if (IsSpecialMatchingPair(cards[i], cards[j]))
                    return true;
            }
        }

        // Otherwise, three cards must form a valid same-symbol or
        // three-different-symbol combination, with Mirror/Executioner wildcards.
        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                for (int k = j + 1; k < cards.Count; k++)
                {
                    if (IsValidThreeCardClaim(cards[i], cards[j], cards[k]))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool IsRoyaltyWarCard(CardInstance card)
    {
        CardType type = card.data.cardType;
        return type == CardType.King || type == CardType.Queen || type == CardType.GordonRobleys;
    }

    private static bool IsSpecialBlackWildcard(CardInstance card)
    {
        if (card?.data == null || card.data.rarity != CardRarity.Special)
            return false;

        return card.data.cardType == CardType.Mirror || card.data.cardType == CardType.Executioner;
    }

    private static bool IsBasicWarSymbol(CardType type)
    {
        return type == CardType.Artist ||
               type == CardType.Knight ||
               type == CardType.Bodyguard ||
               type == CardType.Collector;
    }

    private static bool IsSpecialMatchingPair(CardInstance a, CardInstance b)
    {
        if (a?.data == null || b?.data == null)
            return false;

        bool aSpecial = a.data.rarity == CardRarity.Special;
        bool bSpecial = b.data.rarity == CardRarity.Special;

        if (aSpecial == bSpecial)
            return false;

        if (IsBlackWildcard(a.data.cardType) || IsBlackWildcard(b.data.cardType))
            return false;

        return a.data.cardType == b.data.cardType && IsBasicWarSymbol(a.data.cardType);
    }

    private static bool IsValidThreeCardClaim(CardInstance a, CardInstance b, CardInstance c)
    {
        if (a?.data == null || b?.data == null || c?.data == null)
            return false;

        CardType[] types = { a.data.cardType, b.data.cardType, c.data.cardType };
        bool[] wildcards =
        {
            IsBlackWildcard(types[0]),
            IsBlackWildcard(types[1]),
            IsBlackWildcard(types[2])
        };

        List<CardType> concrete = new List<CardType>();
        for (int i = 0; i < types.Length; i++)
        {
            if (!wildcards[i])
                concrete.Add(types[i]);
        }

        int wildcardCount = 0;
        for (int i = 0; i < wildcards.Length; i++)
        {
            if (wildcards[i])
                wildcardCount++;
        }

        if (concrete.Count == 0)
            return true;

        bool allSame = true;
        for (int i = 1; i < concrete.Count; i++)
        {
            if (concrete[i] != concrete[0])
            {
                allSame = false;
                break;
            }
        }

        if (allSame)
            return true;

        HashSet<CardType> distinct = new HashSet<CardType>(concrete);
        return distinct.Count + wildcardCount >= 3;
    }

    private static bool IsBlackWildcard(CardType type)
    {
        return type == CardType.Mirror || type == CardType.Executioner;
    }
}
