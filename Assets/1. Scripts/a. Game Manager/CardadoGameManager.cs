using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardadoGamePhase { WaitingForDealer, RoundSetupRoll, DealerSetupDecision, Prediction, RollDice, RevealDice, CardActionDecision, PlayingHands, RoundResolution, WarResolution, GameOver }
public enum CardadoCardActionRequestType { ChooseCard, ChooseArtistDie }

public class CardadoGameManager : MonoBehaviour
{
    [SerializeField] private CardadoMatchConfig matchConfig = new CardadoMatchConfig();
    [SerializeField, Min(2)] private int maxPlayers = 5;
    [SerializeField] private List<string> playerIds = new List<string> { "Player 1", "Player 2", "Player 3", "Player 4" };
    [SerializeField] private CardData[] allCards;

    public IReadOnlyList<CardadoPlayerState> Players => players;
    public CardadoMatchConfig MatchConfig => matchConfig;
    public CardadoGamePhase Phase { get; private set; } = CardadoGamePhase.WaitingForDealer;
    public int DealerPlayerIndex { get; private set; } = -1;
    public int StartingPlayerIndex { get; private set; } = -1;
    public int CurrentPredictionPlayerIndex { get; private set; } = -1;
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
    public int MatchWinnerIndex { get; private set; } = -1;
    public CardInstance PendingCardActionCard { get; private set; }

    public event Action<CardadoGamePhase> PhaseChanged;
    public event Action<RoundSetupRoll> SetupDiceRolled;
    public event Action<RoundSetupDecisionType> DealerDecisionRequested;
    public event Action<int, int> RoundSetupCompleted;
    public event Action<CardadoPlayerState> PlayerHandDealt;
    public event Action<CardadoPlayerState> PlayerDiceRolled;
    public event Action<CardadoPlayerState, int> PredictionTurnStarted;
    public event Action PredictionCompleted;
    public event Action<CardadoPlayerState, CardadoCardActionRequestType> CardActionRequested;
    public event Action<CardadoPlayerState, CardInstance> CardPlayed;
    public event Action<CardadoPlayerState, CardInstance, int, int> CardEffectResolved;
    public event Action<CardadoPlayerState, int, int> HandTurnStarted;
    public event Action<CardadoPlayerState, int, int> DiePlayed;
    public event Action<int, int> HandCompleted;
    public event Action RoundPlayingCompleted;
    public event Action RoundResolutionCompleted;
    public event Action<int> MatchWon;

    private readonly List<CardadoPlayerState> players = new List<CardadoPlayerState>();
    private readonly CardadoRoundSetup roundSetup = new CardadoRoundSetup();
    private readonly List<int> currentHandPlayOrder = new List<int>();
    private readonly List<int> currentHandDieIndices = new List<int>();
    private Queue<RoundSetupDecisionType> pendingDealerDecisions;
    private int handTurnsCompleted;

    private void Awake()
    {
        if (matchConfig == null) matchConfig = new CardadoMatchConfig();
        matchConfig.Validate();
        ValidatePlayerConfiguration();
        BuildPlayers();
        InitializeDeck();
        if (GetComponent<CardadoCardActionDevelopmentOverlay>() == null) gameObject.AddComponent<CardadoCardActionDevelopmentOverlay>();
    }

    public void SetDealer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        DealerPlayerIndex = playerIndex;
        StartingPlayerIndex = GetPlayerToRightOf(playerIndex);
        CurrentPredictionPlayerIndex = -1;
        SetPhase(CardadoGamePhase.RoundSetupRoll);
    }

    public void RollRoundSetupDice()
    {
        if (DealerPlayerIndex < 0) throw new InvalidOperationException("A dealer must be selected before rolling the setup dice.");
        if (Phase != CardadoGamePhase.RoundSetupRoll) throw new InvalidOperationException("The game is not waiting for the round setup roll.");
        SetupRoll = roundSetup.RollSetupDice();
        pendingDealerDecisions = roundSetup.BuildDealerDecisions(SetupRoll);
        SetupDiceRolled?.Invoke(SetupRoll);
        RequestNextDealerDecision();
    }

    public void ResolveDealerChoice(int chosenCount)
    {
        if (Phase != CardadoGamePhase.DealerSetupDecision || !PendingDealerDecision.HasValue) throw new InvalidOperationException("There is no pending dealer decision.");
        if (!roundSetup.IsValidDealerChoice(chosenCount)) throw new ArgumentOutOfRangeException(nameof(chosenCount), "Dealer choices must be between 1 and 5.");
        if (PendingDealerDecision.Value == RoundSetupDecisionType.ChooseDiceCount) RoundDiceCount = chosenCount; else RoundCardCount = chosenCount;
        PendingDealerDecision = null;
        RequestNextDealerDecision();
    }

    public void BeginPrediction()
    {
        if (Phase != CardadoGamePhase.DealerSetupDecision || PendingDealerDecision.HasValue) throw new InvalidOperationException("Round setup is not complete.");
        foreach (var player in players) player.ResetRoundScore();
        RollRoundDiceForPlayers();
        CurrentPredictionPlayerIndex = StartingPlayerIndex;
        SetPhase(CardadoGamePhase.Prediction);
        NotifyPredictionTurn();
    }

    public bool TryPlaceDicePrediction(int playerIndex, int dicePrediction)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase != CardadoGamePhase.Prediction || playerIndex != CurrentPredictionPlayerIndex) return false;
        if (!IsValidDicePrediction(playerIndex, dicePrediction)) return false;
        players[playerIndex].diceBid = dicePrediction;
        players[playerIndex].hasPlacedBid = true;
        int nextPlayer = GetNextPlayerIndex(playerIndex);
        if (nextPlayer == StartingPlayerIndex)
        {
            CurrentPredictionPlayerIndex = -1;
            DealInitialHands();
            PredictionCompleted?.Invoke();
            BeginPlayingHands();
            return true;
        }
        CurrentPredictionPlayerIndex = nextPlayer;
        NotifyPredictionTurn();
        return true;
    }

    public bool IsValidDicePrediction(int playerIndex, int dicePrediction)
    {
        ValidatePlayerIndex(playerIndex);
        if (dicePrediction < 0 || dicePrediction > RoundDiceCount) return false;
        if (playerIndex != DealerPlayerIndex) return true;
        return GetPlacedDicePredictionsExcluding(playerIndex) + dicePrediction != RoundDiceCount;
    }

    public int GetMinimumDicePredictionForPlayer(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        return playerIndex == DealerPlayerIndex && GetPlacedDicePredictionsExcluding(playerIndex) == RoundDiceCount ? 1 : 0;
    }

    public bool AreAllPredictionsPlaced()
    {
        foreach (var player in players) if (!player.hasPlacedBid) return false;
        return CurrentPredictionPlayerIndex < 0;
    }

    public bool ArePredictionsValid()
    {
        if (Phase != CardadoGamePhase.Prediction || !AreAllPredictionsPlaced()) return false;
        int totalPredictedDice = 0;
        foreach (var player in players) totalPredictedDice += player.diceBid;
        return totalPredictedDice != RoundDiceCount;
    }

    public void BeginPlayingHands()
    {
        if (Phase != CardadoGamePhase.Prediction && Phase != CardadoGamePhase.PlayingHands) throw new InvalidOperationException("Prediction is incomplete.");
        if (Phase == CardadoGamePhase.Prediction && !ArePredictionsValid()) throw new InvalidOperationException("Predictions are incomplete or invalid.");
        CurrentHandNumber = 1;
        CurrentHandStarterIndex = StartingPlayerIndex;
        CurrentHandPlayerIndex = StartingPlayerIndex;
        CurrentHandWinnerIndex = -1;
        CurrentHandWinningValue = 0;
        handTurnsCompleted = 0;
        currentHandPlayOrder.Clear();
        currentHandDieIndices.Clear();
        BeginCurrentPlayerTurn();
    }

    public bool TrySkipCardAction(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase == CardadoGamePhase.WarResolution)
        {
            CardadoWarManager war = FindFirstObjectByType<CardadoWarManager>();
            return war != null && war.TrySkipCardAction(playerIndex);
        }
        if (Phase != CardadoGamePhase.CardActionDecision || playerIndex != CurrentHandPlayerIndex || PendingCardActionCard != null) return false;
        BeginDieSelectionForCurrentPlayer();
        return true;
    }

    public bool TryPlayCard(int playerIndex, int cardIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase != CardadoGamePhase.CardActionDecision || playerIndex != CurrentHandPlayerIndex || PendingCardActionCard != null) return false;
        CardadoPlayerState player = players[playerIndex];
        if (cardIndex < 0 || cardIndex >= player.hand.cardsInHand.Count) return false;
        CardInstance card = player.hand.cardsInHand[cardIndex];
        if (card == null || card.data == null || (!card.data.isBlankCard && card.data.cardType != CardType.Artist)) return false;
        player.hand.RemoveCard(card);
        card.isPlayed = true;
        CardPlayed?.Invoke(player, card);
        if (card.data.isBlankCard)
        {
            DiscardResolvedCard(card);
            BeginDieSelectionForCurrentPlayer();
            return true;
        }
        PendingCardActionCard = card;
        CardActionRequested?.Invoke(player, CardadoCardActionRequestType.ChooseArtistDie);
        return true;
    }

    public bool TryResolveArtistDie(int playerIndex, int dieIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase != CardadoGamePhase.CardActionDecision || playerIndex != CurrentHandPlayerIndex) return false;
        CardInstance card = PendingCardActionCard;
        if (card == null || card.data == null || card.data.cardType != CardType.Artist || !IsDieAvailable(playerIndex, dieIndex)) return false;
        CardadoPlayerState player = players[playerIndex];
        player.dice[dieIndex] = UnityEngine.Random.Range(1, 7);
        CardEffectResolved?.Invoke(player, card, dieIndex, player.dice[dieIndex]);
        PendingCardActionCard = null;
        DiscardResolvedCard(card);
        BeginDieSelectionForCurrentPlayer();
        return true;
    }

    public int GetAvailableDieCount(int playerIndex)
    {
        ValidatePlayerIndex(playerIndex);
        int count = 0;
        for (int i = 0; i < players[playerIndex].dice.Count; i++) if (IsDieAvailable(playerIndex, i)) count++;
        return count;
    }

    public bool IsDieAvailable(int playerIndex, int dieIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase == CardadoGamePhase.WarResolution)
        {
            CardadoWarManager war = FindFirstObjectByType<CardadoWarManager>();
            return war != null && war.IsWarDieAvailable(playerIndex, dieIndex);
        }
        CardadoPlayerState player = players[playerIndex];
        if (dieIndex < 0 || dieIndex >= player.dice.Count) return false;
        if (Phase == CardadoGamePhase.CardActionDecision) return player.dice[dieIndex] > 0;
        return dieIndex < player.playedDice.Count && !player.playedDice[dieIndex] && player.dice[dieIndex] > 0;
    }

    public bool IsDieTargetable(int playerIndex, int dieIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase == CardadoGamePhase.WarResolution)
        {
            CardadoWarManager war = FindFirstObjectByType<CardadoWarManager>();
            return war != null && war.IsWarDieTargetable(playerIndex, dieIndex);
        }
        CardadoPlayerState player = players[playerIndex];
        return dieIndex >= 0 && dieIndex < player.dice.Count && player.dice[dieIndex] > 0;
    }

    public void NotifyWarHandTurnStarted(CardadoPlayerState player, int handNumber, int starterIndex)
    {
        HandTurnStarted?.Invoke(player, handNumber, starterIndex);
    }

    public void RequestWarCardAction(CardadoPlayerState player, CardadoCardActionRequestType requestType)
    {
        CardActionRequested?.Invoke(player, requestType);
    }

    public void NotifyWarDiePlayed(CardadoPlayerState player, int dieIndex, int dieValue)
    {
        DiePlayed?.Invoke(player, dieIndex, dieValue);
    }

    public void SetNextHandStarter(int winnerPlayerIndex)
    {
        ValidatePlayerIndex(winnerPlayerIndex);
        StartingPlayerIndex = winnerPlayerIndex;
        CurrentHandStarterIndex = winnerPlayerIndex;
    }

    public void DiscardResolvedCard(CardInstance card)
    {
        if (RoundDeck == null) throw new InvalidOperationException("The round deck has not been initialized.");
        if (card != null) RoundDeck.Discard(card);
    }

    public bool TryPlayDie(int playerIndex, int dieIndex)
    {
        ValidatePlayerIndex(playerIndex);
        if (Phase == CardadoGamePhase.WarResolution)
        {
            CardadoWarManager war = FindFirstObjectByType<CardadoWarManager>();
            return war != null && war.TryPlayWarDieForPlayer(playerIndex, dieIndex);
        }
        if (Phase != CardadoGamePhase.PlayingHands || playerIndex != CurrentHandPlayerIndex) return false;
        CardadoPlayerState player = players[playerIndex];
        if (!IsDieAvailable(playerIndex, dieIndex)) return false;
        int dieValue = player.dice[dieIndex];
        player.playedDice[dieIndex] = true;
        currentHandPlayOrder.Add(playerIndex);
        currentHandDieIndices.Add(dieIndex);
        handTurnsCompleted++;
        DiePlayed?.Invoke(player, dieIndex, dieValue);
        if (handTurnsCompleted < players.Count)
        {
            CurrentHandPlayerIndex = GetNextPlayerIndex(playerIndex);
            BeginCurrentPlayerTurn();
            return true;
        }
        CompleteCurrentHand();
        return true;
    }

    public void ResolveRound()
    {
        if (Phase != CardadoGamePhase.RoundResolution) throw new InvalidOperationException("The round is not ready for resolution.");
        foreach (var player in players)
        {
            int difference = Math.Abs(player.handsWon - player.diceBid);
            int chipChange = difference == 0 ? player.diceBid : -difference;
            player.chips = Math.Max(0, player.chips + chipChange);
            Debug.Log($"[Cardado] {player.playerId}: {player.handsWon} hand(s) won, prediction {player.diceBid}, chip change {chipChange:+#;-#;0}, chips now {player.chips}.");
        }
        RoundResolutionCompleted?.Invoke();
        SetPhase(CardadoGamePhase.WarResolution);
    }

    public void CompleteWarPhase()
    {
        if (Phase != CardadoGamePhase.WarResolution) return;
        List<int> winnersAtTarget = new List<int>();
        int highestChipsAmongTargets = int.MinValue;
        foreach (var player in players) if (player.chips >= matchConfig.winningPoints) highestChipsAmongTargets = Math.Max(highestChipsAmongTargets, player.chips);
        if (highestChipsAmongTargets != int.MinValue)
            foreach (var player in players) if (player.chips >= matchConfig.winningPoints && player.chips == highestChipsAmongTargets) winnersAtTarget.Add(players.IndexOf(player));
        if (winnersAtTarget.Count == 1) { MatchWinnerIndex = winnersAtTarget[0]; MatchWon?.Invoke(MatchWinnerIndex); SetPhase(CardadoGamePhase.GameOver); return; }
        if (winnersAtTarget.Count > 1) { MatchWinnerIndex = -1; SetPhase(CardadoGamePhase.GameOver); return; }
        MatchWinnerIndex = -1;
        DiscardAllHandsToDeck();
        SetDealer(GetNextPlayerIndex(DealerPlayerIndex));
    }

    private void CompleteCurrentHand()
    {
        CurrentHandWinnerIndex = -1;
        CurrentHandWinningValue = 0;
        for (int i = 0; i < currentHandPlayOrder.Count; i++)
        {
            int playerIndex = currentHandPlayOrder[i];
            int dieIndex = currentHandDieIndices[i];
            int value = players[playerIndex].dice[dieIndex];
            if (CurrentHandWinnerIndex < 0 || value > CurrentHandWinningValue)
            {
                CurrentHandWinningValue = value;
                CurrentHandWinnerIndex = playerIndex;
            }
        }

        if (CurrentHandWinnerIndex < 0) throw new InvalidOperationException("A completed hand has no winning die.");
        players[CurrentHandWinnerIndex].handsWon++;
        StartingPlayerIndex = CurrentHandWinnerIndex;
        CurrentHandStarterIndex = CurrentHandWinnerIndex;
        HandCompleted?.Invoke(CurrentHandWinnerIndex, CurrentHandWinningValue);

        if (CurrentHandNumber >= RoundDiceCount)
        {
            CurrentHandPlayerIndex = -1;
            RoundPlayingCompleted?.Invoke();
            SetPhase(CardadoGamePhase.RoundResolution);
            ResolveRound();
            return;
        }

        CurrentHandNumber++;
        CurrentHandPlayerIndex = CurrentHandWinnerIndex;
        CurrentHandWinnerIndex = -1;
        CurrentHandWinningValue = 0;
        handTurnsCompleted = 0;
        currentHandPlayOrder.Clear();
        currentHandDieIndices.Clear();
        BeginCurrentPlayerTurn();
    }

    private void BeginCurrentPlayerTurn()
    {
        if (CurrentHandPlayerIndex < 0) return;
        PendingCardActionCard = null;
        CardadoPlayerState player = players[CurrentHandPlayerIndex];
        if (player.hand.cardsInHand.Count > 0)
        {
            SetPhase(CardadoGamePhase.CardActionDecision);
            CardActionRequested?.Invoke(player, CardadoCardActionRequestType.ChooseCard);
            return;
        }
        BeginDieSelectionForCurrentPlayer();
    }

    private void BeginDieSelectionForCurrentPlayer() { SetPhase(CardadoGamePhase.PlayingHands); NotifyHandTurnStarted(); }
    private void NotifyHandTurnStarted() { if (CurrentHandPlayerIndex >= 0) HandTurnStarted?.Invoke(players[CurrentHandPlayerIndex], CurrentHandNumber, CurrentHandStarterIndex); }

    private void DealInitialHands()
    {
        if (RoundCardCount <= 0) throw new InvalidOperationException("Round card count must be resolved before dealing.");
        if (RoundDeck == null) InitializeDeck();
        foreach (var player in players)
        {
            List<CardInstance> oldCards = new List<CardInstance>(player.hand.cardsInHand);
            player.hand.cardsInHand.Clear();
            foreach (CardInstance oldCard in oldCards) DiscardResolvedCard(oldCard);
            for (int i = 0; i < RoundCardCount; i++)
            {
                CardInstance card = RoundDeck.Draw();
                if (card == null) throw new InvalidOperationException("No cards are available to complete the round deal.");
                card.isPlayed = false;
                player.hand.AddCard(card);
            }
            PlayerHandDealt?.Invoke(player);
        }
    }

    private void DiscardAllHandsToDeck()
    {
        foreach (var player in players)
        {
            List<CardInstance> cards = new List<CardInstance>(player.hand.cardsInHand);
            player.hand.cardsInHand.Clear();
            foreach (CardInstance card in cards) DiscardResolvedCard(card);
        }
    }

    private void RollRoundDiceForPlayers()
    {
        if (RoundDiceCount <= 0) throw new InvalidOperationException("Round dice count must be resolved before rolling player dice.");
        foreach (var player in players)
        {
            player.dice.Clear();
            player.playedDice.Clear();
            for (int i = 0; i < RoundDiceCount; i++) { player.dice.Add(UnityEngine.Random.Range(1, 7)); player.playedDice.Add(false); }
            PlayerDiceRolled?.Invoke(player);
        }
    }

    private void InitializeDeck()
    {
        if (allCards == null || allCards.Length == 0) { RoundDeck = null; return; }
        RoundDeck = new Deck(new List<CardData>(allCards));
        RoundDeck.Shuffle();
    }

    private void BuildPlayers()
    {
        players.Clear();
        foreach (string playerId in playerIds) if (!string.IsNullOrWhiteSpace(playerId)) players.Add(new CardadoPlayerState(playerId, matchConfig.startingChips));
    }

    private void ValidatePlayerConfiguration()
    {
        int configuredPlayers = 0;
        foreach (string playerId in playerIds) if (!string.IsNullOrWhiteSpace(playerId)) configuredPlayers++;
        if (configuredPlayers < 2) throw new InvalidOperationException("Cardado requires at least 2 configured players.");
        if (configuredPlayers > maxPlayers) throw new InvalidOperationException($"Cardado supports a maximum of {maxPlayers} configured players.");
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
        if (SetupRoll.diceCountDie != 6) RoundDiceCount = SetupRoll.diceCountDie;
        if (SetupRoll.cardCountDie != 6) RoundCardCount = SetupRoll.cardCountDie;
        SetPhase(CardadoGamePhase.DealerSetupDecision);
        RoundSetupCompleted?.Invoke(RoundDiceCount, RoundCardCount);
    }

    private void NotifyPredictionTurn() { if (CurrentPredictionPlayerIndex >= 0) PredictionTurnStarted?.Invoke(players[CurrentPredictionPlayerIndex], CurrentPredictionPlayerIndex); }
    private int GetPlacedDicePredictionsExcluding(int excludedPlayerIndex) { int total = 0; for (int i = 0; i < players.Count; i++) if (i != excludedPlayerIndex && players[i].hasPlacedBid) total += players[i].diceBid; return total; }
    private int GetPlayerToRightOf(int playerIndex) => players.Count == 0 ? -1 : (playerIndex + 1) % players.Count;
    private int GetNextPlayerIndex(int playerIndex) => (playerIndex + 1) % players.Count;
    private void SetPhase(CardadoGamePhase newPhase) { Phase = newPhase; PhaseChanged?.Invoke(newPhase); }
    private void ValidatePlayerIndex(int playerIndex) { if (playerIndex < 0 || playerIndex >= players.Count) throw new ArgumentOutOfRangeException(nameof(playerIndex)); }
}
