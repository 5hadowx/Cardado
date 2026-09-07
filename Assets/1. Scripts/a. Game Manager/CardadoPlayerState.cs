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
    public int diceBid;
    public int handsWon;
    public bool hasPlacedBid;

    // Die values remain available for card effects after the die is played.
    // playedDice tracks whether that die has already been played in the current round.
    public List<int> dice = new List<int>();
    public List<bool> playedDice = new List<bool>();

    public Hand hand = new Hand();

    public CardadoPlayerState(string playerId, int startingChips)
    {
        this.playerId = playerId;
        chips = startingChips;
    }

    public void ResetRoundScore()
    {
        diceBid = 0;
        hasPlacedBid = false;
        handsWon = 0;
        playedDice.Clear();
    }
}
