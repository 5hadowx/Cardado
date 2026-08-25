using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardadoGamePhase
{
    WaitingForDealer,
    RoundSetupRoll,
    DealerSetupDecision,
    Betting,
    RollDice,
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
    public int CurrentHandStarterIndex { get; private set; } = -1;
    public int CurrentHandPlayerIndex { get; private set; } = -1;
    public int CurrentHandNumber { get; private set; }
    public int CurrentHandWinnerIndex { get; private set; } = -1;
    public int CurrentHandWinningValue { get; private set; }
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
    public event Action<CardadoPlayerState> PlayerDiceRolled;
    public event Action<CardadoPlayerState, int> BettingTurnStarted;
    public event Action BettingCompleted;
    public event Action<CardadoPlayerState, int, int> HandTurnStarted;
    public event Action<CardadoPlayerState, int, int> DiePlayed;
    public event Action<int, int> HandCompleted;
    public event Action RoundPlayingCompleted;

    private readonly List<CardadoPlayerState> players = new List<CardadoPlayerState>();
    private readonly CardadoRoundSetup roundSetup = new CardadoRoundSetup();
    private Queue<RoundSetupDecisionType> pendingDealerDecisions;

    private int handTurnsCompleted;

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
    /// Completes round setup in the physical-game order: deal cards, roll each
    /// player's hidden dice, then begin betting.
    /// </summary>
    public void BeginBetting()
    {
        if (Phase != CardadoGamePhase.DealerSetupDecision || PendingDealerDecision.HasValue)
            throw new InvalidOperationException("Round setup is not complete.");

        DealInitialHands();

        foreach (var player in players)
            player.ResetRoundScore();

        RollRoundDiceForPlayers();

        CurrentBettingPlayerIndex = StartingPlayerIndex;
        SetPhase(CardadoGamePhase.Betting);
        NotifyBettingTurn();
    }

    /// <summary>
    /// Places both parts of the round call atomically: chip wager and predicted
    /// number of dice won. The dealer's final-call restriction prevents the total
    /// predictions from matching the round dice count.
    /// </summary>
    public bool TryPlaceRoundCall(int playerIndex, int chipBet, int dicePrediction)
    {
        ValidatePlayerIndex(playerIndex);

        if (Phase != CardadoGamePhase.Betting || playerIndex != CurrentBettingPlayerIndex)
            return false;

        int maximumChipBet = matchConfig.GetMaximumRoundBet(players[playerIndex].chips);
        if (chipBet < 1 || chipBet > maximumChipBet)
            return false;

        if (!IsValidDicePrediction(playerIndex, dicePrediction))
            return false;

        players[playerIndex].roundBet = chipBet;
        players[playerIndex].diceBid = dicePrediction;
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

    public bool IsValidDicePrediction(int playerIndex, int dicePrediction)
    {
        ValidatePlayerIndex(playerIndex);

        if (dicePrediction < 0 || dicePrediction > RoundDiceCount)
            return false;

        if (playerIndex != DealerPlayerIndex)
            return true;

        int previousPredictions = GetPlacedDicePredictionsExcluding(playerIndex);
        return previousPredictions + dicePrediction != RoundDiceCount;
    }

    public int GetMaximumRoundBetForPlayer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        return matchConfig.GetMaximumRoundBet(players[playerIndex].chips);
    }

    public int GetMinimumDicePredictionForPlayer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (playerIndex == DealerPlayerIndex)
        {
            int previousPredictions = GetPlacedDicePredictionsExcluding(playerIndex);
            if (previousPredictions == RoundDiceCount)
                return 1;
        }

        return 0;
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
        if (Phase != CardadoGamePhase.Betting || !AreAllBidsPlaced())
            return false;

        int totalPredictedDice = 0;
        foreach (var player in players)
            totalPredictedDice += player.diceBid;

        return totalPredictedDice != RoundDiceCount;
    }

    public void BeginPlayingHands()
    {
        if (Phase != CardadoGamePhase.Betting || !AreBidsValid())
            throw new InvalidOperationException("Bidding is incomplete or invalid.");

        CurrentHandNumber = 1;
        CurrentHandStarterIndex = StartingPlayerIndex;
        CurrentHandPlayerIndex = StartingPlayerIndex;
        CurrentHandWinnerIndex = -1;
        CurrentHandWinningValue = 0;
        handTurnsCompleted = 0;

        SetPhase(CardadoGamePhase.PlayingHands);
        NotifyHandTurnStarted();
    }

    /// <summary>
    /// Plays one die for the current player. Dice are indexed from zero and a value
    /// of zero means that die has already been played. Card effects are deliberately
    /// not resolved here yet; the future card-action step will modify the player's
    /// available dice before this method is called.
    /// </summary>
    public bool TryPlayDie(int playerIndex, int dieIndex)
    {
        ValidatePlayerIndex(playerIndex);

        if (Phase != CardadoGamePhase.PlayingHands || playerIndex != CurrentHandPlayerIndex)
            return false;

        CardadoPlayerState player = players[playerIndex];
        if (dieIndex < 0 || dieIndex >= player.dice.Count)
            return false;

        int dieValue = player.dice[dieIndex];
        if (dieValue <= 0)
            return false;

        player.dice[dieIndex] = 0;
        handTurnsCompleted++;

        if (handTurnsCompleted == 1 || dieValue > CurrentHandWinningValue)
        {
            CurrentHandWinningValue = dieValue;
            CurrentHandWinnerIndex = playerIndex;
        }

        DiePlayed?.Invoke(player, dieIndex, dieValue);

        if (handTurnsCompleted < players.Count)
        {
            CurrentHandPlayerIndex = GetNextPlayerIndex(playerIndex);
            NotifyHandTurnStarted();
            return true;
        }

        CompleteCurrentHand();
        return true;
    }

    public int GetAvailableDieCount(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);

        int count = 0;
        foreach (int die in players[playerIndex].dice)
        {
            if (die > 0)
                count++;
        }

        return count;
    }

    public bool IsDieAvailable(int playerIndex, int dieIndex)
    {
        ValidatePlayerIndex(playerIndex);
        return dieIndex >= 0 && dieIndex < players[playerIndex].dice.Count && players[playerIndex].dice[dieIndex] > 0;
    }

    public void SetNextHandStarter(int winnerPlayerIndex)
    {
        ValidatePlayerIndex(winnerPlayerIndex);
        StartingPlayerIndex = winnerPlayerIndex;
        CurrentHandStarterIndex = winnerPlayerIndex;
    }

    public void DiscardResolvedCard(CardInstance card)
    {
        if (RoundDeck == null)
            throw new InvalidOperationException("The round deck has not been initialized.");

        RoundDeck.Discard(card);
    }

    private void CompleteCurrentHand()
    {
        int winnerIndex = CurrentHandWinnerIndex;
        players[winnerIndex].handsWon++;
        StartingPlayerIndex = winnerIndex;
        CurrentHandStarterIndex = winnerIndex;

        HandCompleted?.Invoke(winnerIndex, CurrentHandWinningValue);

        if (CurrentHandNumber >= RoundDiceCount)
        {
            CurrentHandPlayerIndex = -1;
            RoundPlayingCompleted?.Invoke();
            SetPhase(CardadoGamePhase.RoundResolution);
            return;
        }

        CurrentHandNumber++;
        CurrentHandPlayerIndex = winnerIndex;
        CurrentHandWinnerIndex = -1;
        CurrentHandWinningValue = 0;
        handTurnsCompleted = 0;
        NotifyHandTurnStarted();
    }

    private void NotifyHandTurnStarted()
    {
        if (CurrentHandPlayerIndex < 0)
            return;

        HandTurnStarted?.Invoke(
            players[CurrentHandPlayerIndex],
            CurrentHandNumber,
            CurrentHandStarterIndex);
    }

    private void DealInitialHands()
    {
        if (RoundCardCount <= 0)
            throw new InvalidOperationException("Round card count must be resolved before dealing.");

        if (RoundDeck == null)
            InitializeDeck();

        foreach (var player in players)
        {
            player.hand.cardsInHand.Clear();

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

    private void RollRoundDiceForPlayers()
    {
        if (RoundDiceCount <= 0)
            throw new InvalidOperationException("Round dice count must be resolved before rolling player dice.");

        foreach (var player in players)
        {
            player.dice.Clear();

            for (int i = 0; i < RoundDiceCount; i++)
                player.dice.Add(UnityEngine.Random.Range(1, 7));

            PlayerDiceRolled?.Invoke(player);
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

        BettingTurnStarted?.Invoke(players[CurrentBettingPlayerIndex], CurrentBettingPlayerIndex);
    }

    private int GetPlacedDicePredictionsExcluding(int excludedPlayerIndex)
    {
        int total = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (i == excludedPlayerIndex)
                continue;

            if (players[i].hasPlacedBid)
                total += players[i].diceBid;
        }

        return total;
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
