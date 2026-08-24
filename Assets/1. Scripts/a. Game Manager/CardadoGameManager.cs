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
    [SerializeField] private CardadoMatchConfig matchConfig = new CardadoMatchConfig();
    [SerializeField, Min(2)] private int maxPlayers = 5;

    [Header("Players")]
    [SerializeField] private List<string> playerIds = new List<string> { "Player 1", "Player 2", "Player 3", "Player 4" };

    [Header("Cards")]
    [SerializeField] private CardData[] allCards;

    public IReadOnlyList<CardadoPlayerState> Players => players;
    public CardadoMatchConfig MatchConfig => matchConfig;
    public CardadoGamePhase Phase { get; private set; } = CardadoGamePhase.WaitingForDealer;
    public int DealerPlayerIndex { get; private set; } = -1;
    public int StartingPlayerIndex { get; private set; } = -1;
    public int CurrentBettingPlayerIndex { get; private set; } = -1;
    public int RoundDiceCount { get; private set; }
    public int RoundCardCount { get; private set; }
    public RoundSetupRoll SetupRoll { get; private set; }
    public RoundSetupDecisionType? PendingDealerDecision { get; private set; }
    public Deck RoundDeck { get; private set; }

    public event Action<CardadoGamePhase> PhaseChanged;
    public event Action<RoundSetupRoll> SetupDiceRolled;
    public event Action<RoundSetupDecisionType> DealerDecisionRequested;
    public event Action<int, int> RoundSetupCompleted;
    public event Action<CardadoPlayerState> PlayerHandDealt;
    public event Action<CardadoPlayerState, int> BettingTurnStarted;
    public event Action BettingCompleted;

    private readonly List<CardadoPlayerState> players = new List<CardadoPlayerState>();
    private readonly CardadoRoundSetup roundSetup = new CardadoRoundSetup();
    private Queue<RoundSetupDecisionType> pendingDealerDecisions;

    private void Awake()
    {
        if (matchConfig == null)
            matchConfig = new CardadoMatchConfig();

        matchConfig.Validate();
        ValidatePlayerConfiguration();
        BuildPlayers();
        InitializeDeck();
    }

    public void SetDealer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        DealerPlayerIndex = playerIndex;
        StartingPlayerIndex = GetPlayerToRightOf(playerIndex);
        CurrentBettingPlayerIndex = -1;
        SetPhase(CardadoGamePhase.RoundSetupRoll);
    }

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
    /// Deals the initial round hand before betting. Each card is drawn independently,
    /// so the deck can recycle its discard pile exactly when a draw reaches an empty pile.
    /// </summary>
    public void BeginBetting()
    {
        if (Phase != CardadoGamePhase.DealerSetupDecision || PendingDealerDecision.HasValue)
            throw new InvalidOperationException("Round setup is not complete.");

        DealInitialHands();

        foreach (var player in players)
            player.ResetRoundScore();

        CurrentBettingPlayerIndex = StartingPlayerIndex;
        SetPhase(CardadoGamePhase.Betting);
        NotifyBettingTurn();
    }

    /// <summary>
    /// Places the current player's round prediction. Bids are independent;
    /// players do not have to make their bids add up to the number of dice.
    /// </summary>
    public bool TryPlaceBid(int playerIndex, int bid)
    {
        ValidatePlayerIndex(playerIndex);

        if (Phase != CardadoGamePhase.Betting)
            return false;

        if (playerIndex != CurrentBettingPlayerIndex)
            return false;

        int maximumBid = matchConfig.GetMaximumBid(RoundDiceCount, players[playerIndex].chips);
        if (bid < 0 || bid > maximumBid)
            return false;

        players[playerIndex].bid = bid;
        players[playerIndex].hasPlacedBid = true;

        int nextPlayer = GetNextPlayerIndex(playerIndex);
        if (nextPlayer == StartingPlayerIndex)
        {
            CurrentBettingPlayerIndex = -1;
            BettingCompleted?.Invoke();
            return true;
        }

        CurrentBettingPlayerIndex = nextPlayer;
        NotifyBettingTurn();
        return true;
    }

    public int GetMaximumBidForPlayer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        return matchConfig.GetMaximumBid(RoundDiceCount, players[playerIndex].chips);
    }

    public bool AreAllBidsPlaced()
    {
        foreach (var player in players)
        {
            if (!player.hasPlacedBid)
                return false;
        }

        return CurrentBettingPlayerIndex < 0;
    }

    public bool AreBidsValid()
    {
        return Phase == CardadoGamePhase.Betting && AreAllBidsPlaced();
    }

    public void BeginPlayingHands()
    {
        if (Phase != CardadoGamePhase.Betting || !AreBidsValid())
            throw new InvalidOperationException("Bidding is incomplete or invalid.");

        SetPhase(CardadoGamePhase.RevealDice);
        SetPhase(CardadoGamePhase.PlayingHands);
    }

    public void SetNextHandStarter(int winnerPlayerIndex)
    {
        ValidatePlayerIndex(winnerPlayerIndex);
        StartingPlayerIndex = winnerPlayerIndex;
    }

    /// <summary>
    /// Sends a resolved/non-permanent card to the current round's discard pile.
    /// Permanent cards remain active instead of being discarded here.
    /// </summary>
    public void DiscardResolvedCard(CardInstance card)
    {
        if (RoundDeck == null)
            throw new InvalidOperationException("The round deck has not been initialized.");

        RoundDeck.Discard(card);
    }

    private void DealInitialHands()
    {
        if (RoundCardCount <= 0)
            throw new InvalidOperationException("Round card count must be resolved before dealing.");

        if (RoundDeck == null)
            InitializeDeck();

        foreach (var player in players)
        {
            for (int i = 0; i < RoundCardCount; i++)
            {
                CardInstance card = RoundDeck.Draw();
                if (card == null)
                    throw new InvalidOperationException("No cards are available to complete the round deal.");

                player.hand.AddCard(card);
            }

            PlayerHandDealt?.Invoke(player);
        }
    }

    private void InitializeDeck()
    {
        if (allCards == null || allCards.Length == 0)
        {
            RoundDeck = null;
            return;
        }

        RoundDeck = new Deck(new List<CardData>(allCards));
        RoundDeck.Shuffle();
    }

    private void BuildPlayers()
    {
        players.Clear();

        foreach (string playerId in playerIds)
        {
            if (!string.IsNullOrWhiteSpace(playerId))
                players.Add(new CardadoPlayerState(playerId, matchConfig.startingChips));
        }
    }

    private void ValidatePlayerConfiguration()
    {
        int configuredPlayers = 0;
        foreach (string playerId in playerIds)
        {
            if (!string.IsNullOrWhiteSpace(playerId))
                configuredPlayers++;
        }

        if (configuredPlayers < 2)
            throw new InvalidOperationException("Cardado requires at least 2 configured players.");

        if (configuredPlayers > maxPlayers)
            throw new InvalidOperationException($"Cardado supports a maximum of {maxPlayers} configured players.");
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

        SetPhase(CardadoGamePhase.DealerSetupDecision);
        RoundSetupCompleted?.Invoke(RoundDiceCount, RoundCardCount);
    }

    private void NotifyBettingTurn()
    {
        if (CurrentBettingPlayerIndex < 0)
            return;

        BettingTurnStarted?.Invoke(
            players[CurrentBettingPlayerIndex],
            matchConfig.GetMaximumBid(RoundDiceCount, players[CurrentBettingPlayerIndex].chips));
    }

    private int GetPlayerToRightOf(int playerIndex)
    {
        if (players.Count == 0)
            return -1;

        return (playerIndex + 1) % players.Count;
    }

    private int GetNextPlayerIndex(int playerIndex)
    {
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
