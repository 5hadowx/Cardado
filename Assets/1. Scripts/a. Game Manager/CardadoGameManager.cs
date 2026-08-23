using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardadoGamePhase
{
    WaitingForDealer,
    RoundSetupRoll,
    DealerSetupDecision,
    Betting,
    RevealDice,
    PlayingHands,
    RoundResolution,
    WarResolution,
    GameOver
}

/// <summary>
/// High-level state machine for the Cardado match.
/// The class owns game flow; presentation scripts should react to its state/events.
/// </summary>
public class CardadoGameManager : MonoBehaviour
{
    [Header("Match Setup")]
    [SerializeField] private int startingChips = 3;

    [Header("Players")]
    [SerializeField] private List<string> playerIds = new List<string>();

    public IReadOnlyList<CardadoPlayerState> Players => players;
    public CardadoGamePhase Phase { get; private set; } = CardadoGamePhase.WaitingForDealer;
    public int DealerPlayerIndex { get; private set; } = -1;
    public int StartingPlayerIndex { get; private set; } = -1;
    public int RoundDiceCount { get; private set; }
    public int RoundCardCount { get; private set; }
    public RoundSetupRoll SetupRoll { get; private set; }
    public RoundSetupDecisionType? PendingDealerDecision { get; private set; }

    public event Action<CardadoGamePhase> PhaseChanged;
    public event Action<RoundSetupRoll> SetupDiceRolled;
    public event Action<RoundSetupDecisionType> DealerDecisionRequested;
    public event Action<int, int> RoundSetupCompleted;

    private readonly List<CardadoPlayerState> players = new List<CardadoPlayerState>();
    private readonly CardadoRoundSetup roundSetup = new CardadoRoundSetup();
    private Queue<RoundSetupDecisionType> pendingDealerDecisions;

    private void Awake()
    {
        BuildPlayers();
    }

    /// <summary>
    /// The caller/UI/network layer can choose the dealer once the seating/order is known.
    /// We intentionally do not invent a dealer-selection rule here.
    /// </summary>
    public void SetDealer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        DealerPlayerIndex = playerIndex;
        StartingPlayerIndex = GetPlayerToRightOf(playerIndex);
        SetPhase(CardadoGamePhase.RoundSetupRoll);
    }

    /// <summary>
    /// Rolls the two setup dice as one logical action.
    /// Both results exist before any dealer decision is requested.
    /// </summary>
    public void RollRoundSetupDice()
    {
        if (DealerPlayerIndex < 0)
            throw new InvalidOperationException("A dealer must be selected before rolling the setup dice.");

        if (Phase != CardadoGamePhase.RoundSetupRoll)
            throw new InvalidOperationException("The game is not waiting for the round setup roll.");

        SetupRoll = roundSetup.RollSetupDice();
        pendingDealerDecisions = roundSetup.BuildDealerDecisions(SetupRoll);

        SetupDiceRolled?.Invoke(SetupRoll);
        RequestNextDealerDecision();
    }

    /// <summary>
    /// Resolves the currently requested 6-choice. If both setup dice are 6,
    /// the second choice is requested only after the first one is completed.
    /// </summary>
    public void ResolveDealerChoice(int chosenCount)
    {
        if (Phase != CardadoGamePhase.DealerSetupDecision || !PendingDealerDecision.HasValue)
            throw new InvalidOperationException("There is no pending dealer decision.");

        if (!roundSetup.IsValidDealerChoice(chosenCount))
            throw new ArgumentOutOfRangeException(nameof(chosenCount), "Dealer choices must be between 1 and 5.");

        if (PendingDealerDecision.Value == RoundSetupDecisionType.ChooseDiceCount)
            RoundDiceCount = chosenCount;
        else
            RoundCardCount = chosenCount;

        PendingDealerDecision = null;
        RequestNextDealerDecision();
    }

    /// <summary>
    /// Starts betting once the setup dice and any 6 choices have been resolved.
    /// </summary>
    public void BeginBetting()
    {
        if (Phase != CardadoGamePhase.DealerSetupDecision || PendingDealerDecision.HasValue)
            throw new InvalidOperationException("Round setup is not complete.");

        foreach (var player in players)
            player.ResetRoundScore();

        SetPhase(CardadoGamePhase.Betting);
    }

    public bool TryPlaceBid(int playerIndex, int bid)
    {
        ValidatePlayerIndex(playerIndex);

        if (Phase != CardadoGamePhase.Betting)
            return false;

        if (bid < 0 || bid > RoundDiceCount)
            return false;

        players[playerIndex].bid = bid;
        return true;
    }

    /// <summary>
    /// Cardado requires the total of all bids to be different from the number of hands.
    /// </summary>
    public bool AreBidsValid()
    {
        if (RoundDiceCount <= 0)
            return false;

        int totalBids = 0;
        foreach (var player in players)
            totalBids += player.bid;

        return totalBids != RoundDiceCount;
    }

    /// <summary>
    /// Once bidding is valid, reveal the dice and enter the hand-playing phase.
    /// Actual dice objects/animation will be connected later.
    /// </summary>
    public void BeginPlayingHands()
    {
        if (Phase != CardadoGamePhase.Betting || !AreBidsValid())
            throw new InvalidOperationException("Bidding is incomplete or invalid.");

        SetPhase(CardadoGamePhase.RevealDice);
        SetPhase(CardadoGamePhase.PlayingHands);
    }

    /// <summary>
    /// The winner of a hand becomes the first player of the next hand.
    /// </summary>
    public void SetNextHandStarter(int winnerPlayerIndex)
    {
        ValidatePlayerIndex(winnerPlayerIndex);
        StartingPlayerIndex = winnerPlayerIndex;
    }

    private void BuildPlayers()
    {
        players.Clear();

        foreach (string playerId in playerIds)
        {
            if (!string.IsNullOrWhiteSpace(playerId))
                players.Add(new CardadoPlayerState(playerId, startingChips));
        }
    }

    private void RequestNextDealerDecision()
    {
        if (pendingDealerDecisions != null && pendingDealerDecisions.Count > 0)
        {
            PendingDealerDecision = pendingDealerDecisions.Dequeue();
            SetPhase(CardadoGamePhase.DealerSetupDecision);
            DealerDecisionRequested?.Invoke(PendingDealerDecision.Value);
            return;
        }

        PendingDealerDecision = null;

        if (SetupRoll.diceCountDie != 6)
            RoundDiceCount = SetupRoll.diceCountDie;

        if (SetupRoll.cardCountDie != 6)
            RoundCardCount = SetupRoll.cardCountDie;

        RoundSetupCompleted?.Invoke(RoundDiceCount, RoundCardCount);
        SetPhase(CardadoGamePhase.DealerSetupDecision);
    }

    /// <summary>
    /// Player list order is clockwise around the table, so the next index is the player to the dealer's right.
    /// This is isolated here so the seating convention can be changed without touching round logic.
    /// </summary>
    private int GetPlayerToRightOf(int playerIndex)
    {
        if (players.Count == 0)
            return -1;

        return (playerIndex + 1) % players.Count;
    }

    private void SetPhase(CardadoGamePhase newPhase)
    {
        Phase = newPhase;
        PhaseChanged?.Invoke(newPhase);
    }

    private void ValidatePlayerIndex(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
    }
}
