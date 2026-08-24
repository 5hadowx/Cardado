using System;

/// <summary>
/// Match-level rules selected before a Cardado match begins.
/// Runtime round state should not modify these values.
/// </summary>
[Serializable]
public class CardadoMatchConfig
{
    public int startingChips = 3;
    public int winningPoints = 10;
    public bool betCapEnabled = true;
    public int maxBet = 3;

    public int GetMaximumBid(int roundDiceCount, int playerChips)
    {
        int maximum = Math.Min(roundDiceCount, playerChips);

        if (betCapEnabled)
            maximum = Math.Min(maximum, maxBet);

        return Math.Max(0, maximum);
    }

    public void Validate()
    {
        if (startingChips < 1)
            throw new InvalidOperationException("Starting chips must be at least 1.");

        if (winningPoints < 1)
            throw new InvalidOperationException("Winning points must be at least 1.");

        if (betCapEnabled && maxBet < 1)
            throw new InvalidOperationException("The maximum bet must be at least 1 when the betting cap is enabled.");
    }
}
