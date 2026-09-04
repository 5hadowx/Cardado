using System.Collections.Generic;

/// <summary>
/// Rules used only to determine whether a player's current cards allow a War declaration.
/// Once War starts, the cards use the normal Cardado card effects.
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
               (card.data.cardType == CardType.Mirror ||
                card.data.cardType == CardType.Executioner);
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
        if (cards == null || cards.Count == 0)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].data == null)
                continue;

            // King / Queen / Gordon Robleys are standalone War claims.
            if (IsRoyalty(cards[i]))
                return true;
        }

        // The remaining declaration patterns require three cards.
        if (cards.Count != 3)
            return false;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].data == null)
                return false;
        }

        // Mirror / Executioner are black wildcards.
        if (IsBlackWildcard(cards[0]) ||
            IsBlackWildcard(cards[1]) ||
            IsBlackWildcard(cards[2]))
        {
            return IsValidThreeCardCombination(cards);
        }

        // Special + matching normal card = three-card equivalent.
        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                CardInstance a = cards[i];
                CardInstance b = cards[j];

                if (a.data.rarity == CardRarity.Special &&
                    b.data.rarity != CardRarity.Special &&
                    !IsBlackWildcard(b) &&
                    a.data.cardType == b.data.cardType)
                    return true;

                if (b.data.rarity == CardRarity.Special &&
                    a.data.rarity != CardRarity.Special &&
                    !IsBlackWildcard(a) &&
                    b.data.cardType == a.data.cardType)
                    return true;
            }
        }

        // The current CardData model has no separate symbol field, so card type
        // is the available grouping key for the matching/different-symbol test.
        bool allSame = cards[0].data.cardType == cards[1].data.cardType &&
                       cards[0].data.cardType == cards[2].data.cardType;
        if (allSame)
            return true;

        bool allDifferent = cards[0].data.cardType != cards[1].data.cardType &&
                            cards[0].data.cardType != cards[2].data.cardType &&
                            cards[1].data.cardType != cards[2].data.cardType;
        return allDifferent;
    }

    public static bool IsValidThreeCardCombination(List<CardInstance> cards)
    {
        if (cards == null || cards.Count != 3)
            return false;

        int nonWildcardCount = 0;
        CardType firstType = default(CardType);
        bool hasFirstType = false;
        bool allNonWildcardsSame = true;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == null || cards[i].data == null)
                return false;

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
                allNonWildcardsSame = false;
            }
        }

        // One or more black wildcards can complete either the matching or
        // different-symbol three-card declaration pattern.
        if (nonWildcardCount <= 1)
            return true;

        if (allNonWildcardsSame)
            return true;

        // Two non-wild cards of different groups can be completed by the wildcard.
        return nonWildcardCount == 2;
    }
}
