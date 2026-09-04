using System.Collections.Generic;

/// <summary>
/// Rules shared by the War claim system and the War card layer.
/// War uses the card definitions already present in CardData/CardInstance.
///
/// This class deliberately contains only rules that are explicitly defined for
/// War. It does not invent War-specific effects where the rules document does
/// not define a different effect from normal play.
/// </summary>
public static class CardadoWarCardRules
{
    /// <summary>
    /// Returns the War value of one physical card when counting card value.
    /// Special cards count double; all other cards count as one.
    /// </summary>
    public static int GetWarValue(CardInstance card)
    {
        if (card == null || card.data == null)
            return 0;

        return card.data.rarity == CardRarity.Special ? 2 : 1;
    }

    /// <summary>
    /// Mirror and Executioner are black wildcards and may represent any symbol
    /// when constructing a three-card War claim.
    /// </summary>
    public static bool IsBlackWildcard(CardInstance card)
    {
        if (card == null || card.data == null)
            return false;

        return card.data.cardType == CardType.Mirror ||
               card.data.cardType == CardType.Executioner;
    }

    public static bool IsBlackWildcard(CardType type)
    {
        return type == CardType.Mirror || type == CardType.Executioner;
    }

    /// <summary>
    /// Checks whether the player's remaining cards contain a valid War claim.
    /// A standalone King, Queen or Gordon Robleys is a valid claim. Otherwise
    /// the player needs a combination equivalent to three cards.
    /// </summary>
    public static bool HasValidClaim(List<CardInstance> cards)
    {
        if (cards == null || cards.Count == 0)
            return false;

        foreach (CardInstance card in cards)
        {
            if (card == null || card.data == null)
                continue;

            if (card.data.cardType == CardType.King ||
                card.data.cardType == CardType.Queen ||
                card.data.cardType == CardType.GordonRobleys)
                return true;
        }

        // Two matching symbols can satisfy a three-card claim when one of the
        // two cards is Special (2 + 1 = 3).
        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                CardInstance a = cards[i];
                CardInstance b = cards[j];
                if (a == null || b == null || a.data == null || b.data == null)
                    continue;

                if (a.data.cardType == b.data.cardType &&
                    (a.data.rarity == CardRarity.Special || b.data.rarity == CardRarity.Special))
                    return true;
            }
        }

        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                for (int k = j + 1; k < cards.Count; k++)
                {
                    if (IsValidThreeCardCombination(cards[i], cards[j], cards[k]))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Valid combinations are three equal symbols, three different symbols, or
    /// a combination made valid by one or more black wildcards.
    /// </summary>
    public static bool IsValidThreeCardCombination(CardInstance a, CardInstance b, CardInstance c)
    {
        if (a == null || b == null || c == null ||
            a.data == null || b.data == null || c.data == null)
            return false;

        CardType[] types = { a.data.cardType, b.data.cardType, c.data.cardType };
        bool[] wild =
        {
            IsBlackWildcard(types[0]),
            IsBlackWildcard(types[1]),
            IsBlackWildcard(types[2])
        };

        int wildcardCount = (wild[0] ? 1 : 0) + (wild[1] ? 1 : 0) + (wild[2] ? 1 : 0);

        if (wildcardCount > 0)
        {
            List<CardType> concrete = new List<CardType>();
            if (!wild[0]) concrete.Add(types[0]);
            if (!wild[1]) concrete.Add(types[1]);
            if (!wild[2]) concrete.Add(types[2]);

            // A wildcard can complete a set of equal symbols, or complete a
            // set of three different symbols.
            if (concrete.Count == 0)
                return true;

            bool allEqual = true;
            for (int i = 1; i < concrete.Count; i++)
            {
                if (concrete[i] != concrete[0])
                {
                    allEqual = false;
                    break;
                }
            }

            if (allEqual)
                return true;

            HashSet<CardType> distinct = new HashSet<CardType>(concrete);
            if (distinct.Count + wildcardCount >= 3)
                return true;
        }

        if (types[0] == types[1] && types[1] == types[2])
            return true;

        return types[0] != types[1] &&
               types[0] != types[2] &&
               types[1] != types[2];
    }
}
