using System.Collections.Generic;

/// <summary>
/// Runtime state for one player during a Cardado match.
/// Gameplay state lives here; UI should read from it rather than own the rules.
/// </summary>
[System.Serializable]
public class CardadoPlayerState
{
    public string playerId;
    public int chips;
    public int roundBet;
    public int diceBid;
    public int handsWon;
    public bool hasPlacedBid;

    // Values of dice currently available to the player.
    // A value of 0 means the die is no longer available to play.
    public List<int> dice = new List<int>();

    public Hand hand = new Hand();

    public CardadoPlayerState(string playerId, int startingChips)
    {
        this.playerId = playerId;
        chips = startingChips;
    }

    public void ResetRoundScore()
    {
        roundBet = 0;
        diceBid = 0;
        hasPlacedBid = false;
        handsWon = 0;
    }
}
