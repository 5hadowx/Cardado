using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoundSetupDecisionType
{
    ChooseDiceCount,
    ChooseCardCount
}

/// <summary>
/// Result of the two-dice setup roll.
/// Both dice are generated together before any dealer decision is requested.
/// </summary>
[Serializable]
public struct RoundSetupRoll
{
    public int diceCountDie;
    public int cardCountDie;

    public bool NeedsDiceChoice => diceCountDie == 6;
    public bool NeedsCardChoice => cardCountDie == 6;
}

/// <summary>
/// Gameplay logic for the beginning-of-round setup.
/// It deliberately has no UI or animation responsibilities.
/// </summary>
public class CardadoRoundSetup
{
    public RoundSetupRoll RollSetupDice()
    {
        return new RoundSetupRoll
        {
            diceCountDie = UnityEngine.Random.Range(1, 7),
            cardCountDie = UnityEngine.Random.Range(1, 7)
        };
    }

    public Queue<RoundSetupDecisionType> BuildDealerDecisions(RoundSetupRoll roll)
    {
        var decisions = new Queue<RoundSetupDecisionType>();

        // Both dice are evaluated first. If both are 6, choices are resolved sequentially.
        if (roll.NeedsDiceChoice)
            decisions.Enqueue(RoundSetupDecisionType.ChooseDiceCount);

        if (roll.NeedsCardChoice)
            decisions.Enqueue(RoundSetupDecisionType.ChooseCardCount);

        return decisions;
    }

    public int ResolveDiceCount(RoundSetupRoll roll, int? dealerChoice)
    {
        return ResolveCount(roll.diceCountDie, dealerChoice);
    }

    public int ResolveCardCount(RoundSetupRoll roll, int? dealerChoice)
    {
        return ResolveCount(roll.cardCountDie, dealerChoice);
    }

    public bool IsValidDealerChoice(int value)
    {
        return value >= 1 && value <= 5;
    }

    private int ResolveCount(int rolledValue, int? dealerChoice)
    {
        if (rolledValue != 6)
            return rolledValue;

        if (!dealerChoice.HasValue || !IsValidDealerChoice(dealerChoice.Value))
            throw new ArgumentException("A setup die showing 6 requires a dealer choice from 1 to 5.");

        return dealerChoice.Value;
    }
}
