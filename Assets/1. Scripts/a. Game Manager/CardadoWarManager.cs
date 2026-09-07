using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardadoWarPendingChoice
{
    None,
    ModifierTarget,
    ModifierSign,
    ArtistDie,
    KnightDie,
    CollectorOpponentCard,
    BodyguardDie,
    MirrorOwnDie,
    MirrorOpponentDie,
    JokerPlayer,
    JokerDie,
    SpecialArtistMode,
    SpecialArtistDie,
    SpecialArtistResult,
    SpecialKnightMode,
    SpecialKnightDie,
    SpecialCollectorMode,
    SpecialCollectorOwnCard,
    SpecialCollectorOpponentCard,
    SpecialCollectorPlayChoice,
    SpecialBodyguardMode,
    SpecialMirrorMode,
    SpecialMirrorOwnDie,
    SpecialMirrorOpponentDie,
    NoblemanEffect
}

/// <summary>
/// Authoritative War rules. War cards and dice live only in CardadoWarContext;
/// normal CardadoPlayerState hands/dice are never repurposed for the temporary War.
/// Controllers/UI request actions through this API; this class owns validation and mutation.
/// </summary>
public class CardadoWarManager : MonoBehaviour
{
    private enum WarUiStep { Claim, Target, Wager, Order, Playing, Complete }

    [SerializeField] private CardadoGameManager gameManager;
    [SerializeField, Min(1)] private int warCardCount = 3;
    [SerializeField, Min(1)] private int warDiceCount = 3;

    private readonly List<int> claimOrder = new List<int>();

    private CardadoWarContext warContext;
    private WarUiStep uiStep;
    private int currentClaimPosition;
    private int challengerIndex = -1;
    private int targetIndex = -1;
    private int warWager;
    private bool challengerPlaysFirst;
    private int currentWarTurn;
    private int currentHandTurns;
    private int challengerCurrentDieIndex = -1;
    private int targetCurrentDieIndex = -1;
    private bool warResolved;
    private bool warCardActionPending;
    private bool bonusCardActionAvailable;

    private CardadoWarPendingChoice pendingChoice;
    private CardadoWarContext.Participant pendingChoiceActor;
    private CardInstance pendingChoiceCard;
    private bool pendingCollectorOwnSelected;
    private bool pendingCollectorOpponentSelected;
    private CardInstance pendingCollectorOwnCard;
    private CardInstance pendingCollectorOpponentCard;
    private bool pendingMirrorSwapOne = true;
    private CardadoWarContext.Participant pendingJokerTarget;
    private int pendingArtistDieIndex = -1;
    private readonly int[] pendingArtistResults = new int[3];
    private int pendingModifierTargetIndex = -1;
    private int pendingModifierDieIndex = -1;
    private int pendingMirrorOwnDieIndex = -1;


    public bool WarInProgress => warContext != null && !warResolved;
    public bool IsWarPlaying => WarInProgress && uiStep == WarUiStep.Playing;
    public bool IsWarCardActionPending => warCardActionPending;
    public CardadoWarContext Context => warContext;
    public CardadoWarPendingChoice PendingChoice => pendingChoice;
    public CardadoWarContext.Participant PendingChoiceActor => pendingChoiceActor;
    public CardInstance PendingChoiceCard => pendingChoiceCard;
    public bool PendingCollectorOwnSelected => pendingCollectorOwnSelected;
    public bool PendingCollectorOpponentSelected => pendingCollectorOpponentSelected;
    public CardInstance PendingCollectorOwnCard => pendingCollectorOwnCard;
    public CardInstance PendingCollectorOpponentCard => pendingCollectorOpponentCard;
    public int PendingArtistDieIndex => pendingArtistDieIndex;
    public int WarOpponentIndex => warContext == null ? -1 :
        (GetCurrentWarPlayerIndex() == challengerIndex ? targetIndex : challengerIndex);

    public bool IsWarInProgressForDevelopment() => WarInProgress;

    private bool warPhaseInitialized;

    public void EnsureWarPhaseInitialized()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager == null || gameManager.Phase != CardadoGamePhase.WarResolution) return;
        if (warPhaseInitialized) return;
        BeginWarPhase();
    }

    public CardadoWarContext.Participant GetJokerTargetForDevelopment() => pendingJokerTarget;

    public int GetPendingArtistResult(int index)
    {
        return index >= 0 && index < pendingArtistResults.Length ? pendingArtistResults[index] : 0;
    }

    private void Awake()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
        if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
        CleanupContextWithoutApplyingResult();
    }

    private void Start()
    {
        EnsureWarPhaseInitialized();
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        if (phase == CardadoGamePhase.WarResolution) BeginWarPhase();
    }

    private void BeginWarPhase()
    {
        warPhaseInitialized = true;
        Debug.Log("[Cardado] War phase initialized.");
        CleanupContextWithoutApplyingResult();
        claimOrder.Clear();
        currentClaimPosition = 0;
        challengerIndex = -1;
        targetIndex = -1;
        warWager = 0;
        warResolved = false;
        uiStep = WarUiStep.Claim;
        ClearPendingChoice();

        int start = gameManager.StartingPlayerIndex;
        if (start < 0) start = 0;
        for (int offset = 0; offset < gameManager.Players.Count; offset++)
            claimOrder.Add((start + offset) % gameManager.Players.Count);
        AdvanceToCurrentClaimant();
    }

    private void AdvanceToCurrentClaimant()
    {
        challengerIndex = -1;
        targetIndex = -1;
        warWager = 0;
        warResolved = false;
        uiStep = WarUiStep.Claim;
        ClearPendingChoice();

        while (currentClaimPosition < claimOrder.Count)
        {
            int playerIndex = claimOrder[currentClaimPosition];
            if (CanClaimWar(playerIndex)) return;
            currentClaimPosition++;
        }
        uiStep = WarUiStep.Complete;
    }

    public bool CanClaimWar(int playerIndex)
    {
        if (gameManager == null || playerIndex < 0 || playerIndex >= gameManager.Players.Count) return false;
        return CardadoWarCardRules.FindOptimalClaim(gameManager.Players[playerIndex].hand.cardsInHand) != null;
    }

    public bool TryClaimWar(int playerIndex)
    {
        if (uiStep != WarUiStep.Claim || currentClaimPosition >= claimOrder.Count) return false;
        if (playerIndex != claimOrder[currentClaimPosition] || !CanClaimWar(playerIndex)) return false;

        List<CardInstance> claim = CardadoWarCardRules.FindOptimalClaim(gameManager.Players[playerIndex].hand.cardsInHand);
        if (claim == null || claim.Count == 0) return false;

        foreach (CardInstance card in claim)
        {
            if (!gameManager.Players[playerIndex].hand.cardsInHand.Remove(card)) return false;
            card.isPlayed = true;
            gameManager.DiscardResolvedCard(card);
        }

        challengerIndex = playerIndex;
        uiStep = WarUiStep.Target;
        return true;
    }

    public bool TryPassWar(int playerIndex)
    {
        if (uiStep != WarUiStep.Claim || currentClaimPosition >= claimOrder.Count) return false;
        if (playerIndex != claimOrder[currentClaimPosition]) return false;
        currentClaimPosition++;
        AdvanceToCurrentClaimant();
        return true;
    }

    public bool TryChooseTarget(int playerIndex)
    {
        if (uiStep != WarUiStep.Target || challengerIndex < 0) return false;
        if (playerIndex < 0 || playerIndex >= gameManager.Players.Count || playerIndex == challengerIndex) return false;
        if (gameManager.Players[playerIndex].chips < 1) return false;
        targetIndex = playerIndex;
        uiStep = WarUiStep.Wager;
        return true;
    }

    public bool TryChooseWarWager(int wager)
    {
        if (uiStep != WarUiStep.Wager || challengerIndex < 0 || targetIndex < 0) return false;
        if (wager != 1) return false;
        if (gameManager.Players[targetIndex].chips < 1) return false;
        if (gameManager.Players[challengerIndex].chips < 0) return false;
        warWager = 1;
        uiStep = WarUiStep.Order;
        return true;
    }

    public bool TryChooseWarOrder(bool challengerFirst)
    {
        if (uiStep != WarUiStep.Order || challengerIndex < 0 || targetIndex < 0) return false;
        challengerPlaysFirst = challengerFirst;
        StartWar();
        return true;
    }

    private void StartWar()
    {
        if (gameManager.RoundDeck == null)
            throw new InvalidOperationException("War cannot start because the round deck is not initialized.");

        warContext = CardadoWarContext.Create(
            challengerIndex, gameManager.Players[challengerIndex].playerId, gameManager.Players[challengerIndex].chips,
            targetIndex, gameManager.Players[targetIndex].playerId, gameManager.Players[targetIndex].chips);
        warContext.Wager = warWager;
        warContext.CurrentTurnSlot = challengerPlaysFirst ? 0 : 1;

        DealWarCards(warContext.Challenger, warCardCount);
        DealWarCards(warContext.Target, warCardCount);
        RollWarDice(warContext.Challenger, warDiceCount);
        RollWarDice(warContext.Target, warDiceCount);

        currentWarTurn = warContext.CurrentTurnSlot;
        currentHandTurns = 0;
        challengerCurrentDieIndex = -1;
        targetCurrentDieIndex = -1;
        warResolved = false;
        uiStep = WarUiStep.Playing;
        BeginWarHand();
    }

    private void DealWarCards(CardadoWarContext.Participant participant, int count)
    {
        for (int i = 0; i < count; i++)
        {
            CardInstance card = gameManager.RoundDeck.Draw();
            if (card == null) throw new InvalidOperationException("No cards are available to complete the War deal.");
            card.isPlayed = false;
            warContext.AddCard(participant, card);
        }
    }

    private void RollWarDice(CardadoWarContext.Participant participant, int count)
    {
        for (int i = 0; i < count; i++) warContext.AddDie(participant, UnityEngine.Random.Range(1, 7));
    }

    private void BeginWarHand()
    {
        if (warContext == null || warResolved) return;
        currentHandTurns = 0;
        challengerCurrentDieIndex = -1;
        targetCurrentDieIndex = -1;
        warContext.HandNumber = warContext.HandNumber <= 0 ? 1 : warContext.HandNumber;
        warContext.CurrentTurnSlot = currentWarTurn;
        warContext.ClearHandScopedEffects();
        ClearPendingChoice();
        BeginWarTurn();
    }

    private void BeginWarTurn()
    {
        if (warContext == null) return;
        warContext.CurrentTurnSlot = currentWarTurn;
        CardadoWarContext.Participant current = warContext.CurrentPlayer;
        if (current == null) return;

        bonusCardActionAvailable = true;
        warCardActionPending = current.Cards.Count > 0 && !warContext.IsBlocked(current);
        gameManager.NotifyWarHandTurnStarted(gameManager.Players[current.PlayerIndex], warContext.HandNumber, current.PlayerIndex);
        if (warCardActionPending)
            gameManager.RequestWarCardAction(gameManager.Players[current.PlayerIndex], CardadoCardActionRequestType.ChooseCard);
    }

    public bool TrySkipCardAction(int playerIndex)
    {
        if (!WarInProgress || uiStep != WarUiStep.Playing || pendingChoice != CardadoWarPendingChoice.None) return false;
        if (playerIndex != GetCurrentWarPlayerIndex()) return false;
        warCardActionPending = false;
        return true;
    }

    public bool TryPlayWarCard(int playerIndex, int cardIndex)
    {
        if (!WarInProgress || !warCardActionPending || pendingChoice != CardadoWarPendingChoice.None) return false;
        if (playerIndex != GetCurrentWarPlayerIndex()) return false;

        CardadoWarContext.Participant actor = warContext.GetParticipant(playerIndex);
        if (actor == null || warContext.IsBlocked(actor)) return false;
        if (cardIndex < 0 || cardIndex >= actor.Cards.Count) return false;

        CardInstance card = actor.Cards[cardIndex];
        if (card == null || card.data == null) return false;

        actor.MutableCards.Remove(card);
        card.isPlayed = true;
        warContext.MarkCardPlayed(actor);
        warCardActionPending = false;

        bool persistent = ResolveWarCard(actor, card);
        if (pendingChoice != CardadoWarPendingChoice.None) return true;
        FinishCardResolution(actor, card, persistent);
        return true;
    }

    private bool ResolveWarCard(CardadoWarContext.Participant actor, CardInstance card)
    {
        if (card.data.isBlankCard) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(actor);

        if (card.data.cardType == CardType.GordonRobleys)
        {
            BeginPendingChoice(CardadoWarPendingChoice.NoblemanEffect, actor, card);
            return false;
        }

        if (card.data.rarity == CardRarity.Special)
            return ResolveSpecial(actor, opponent, card);

        if (card.data.isModifier)
        {
            BeginPendingChoice(CardadoWarPendingChoice.ModifierTarget, actor, card);
            return false;
        }

        switch (card.data.cardType)
        {
            case CardType.Artist:
                BeginPendingChoice(CardadoWarPendingChoice.ArtistDie, actor, card);
                return false;
            case CardType.Knight:
                BeginPendingChoice(CardadoWarPendingChoice.KnightDie, actor, card);
                return false;
            case CardType.Collector:
                BeginPendingChoice(CardadoWarPendingChoice.CollectorOpponentCard, actor, card);
                return false;
            case CardType.Bodyguard:
                BeginPendingChoice(CardadoWarPendingChoice.BodyguardDie, actor, card);
                return false;
            case CardType.Mirror:
                BeginPendingChoice(CardadoWarPendingChoice.MirrorOwnDie, actor, card);
                return false;
            case CardType.Executioner:
                ResolveExecutioner(actor, opponent);
                return false;
            case CardType.Joker:
                BeginPendingChoice(CardadoWarPendingChoice.JokerPlayer, actor, card);
                return false;
            case CardType.King:
                RerollAll(actor);
                RerollAll(opponent);
                GrantBonusCardAction(actor);
                return false;
            case CardType.Queen:
                ResolveQueen(actor, opponent);
                GrantBonusCardAction(actor);
                return false;
            default:
                return false;
        }
    }

    private bool ResolveSpecial(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent, CardInstance card)
    {
        switch (card.data.cardType)
        {
            case CardType.Artist:
                BeginPendingChoice(CardadoWarPendingChoice.SpecialArtistMode, actor, card);
                return false;
            case CardType.Knight:
                BeginPendingChoice(CardadoWarPendingChoice.SpecialKnightMode, actor, card);
                return false;
            case CardType.Collector:
                BeginPendingChoice(CardadoWarPendingChoice.SpecialCollectorMode, actor, card);
                return false;
            case CardType.Bodyguard:
                BeginPendingChoice(CardadoWarPendingChoice.SpecialBodyguardMode, actor, card);
                return false;
            case CardType.Mirror:
                BeginPendingChoice(CardadoWarPendingChoice.SpecialMirrorMode, actor, card);
                return false;
            case CardType.Executioner:
                DiscardWarCards(opponent);
                return false;
            default:
                return false;
        }
    }

    private void BeginPendingChoice(CardadoWarPendingChoice choice, CardadoWarContext.Participant actor, CardInstance card)
    {
        pendingChoice = choice;
        pendingChoiceActor = actor;
        pendingChoiceCard = card;
        pendingCollectorOwnSelected = false;
        pendingCollectorOpponentSelected = false;
        pendingCollectorOwnCard = null;
        pendingCollectorOpponentCard = null;
        pendingJokerTarget = null;
        pendingArtistDieIndex = -1;
        pendingModifierTargetIndex = -1;
        pendingModifierDieIndex = -1;
        pendingMirrorOwnDieIndex = -1;
        for (int i = 0; i < pendingArtistResults.Length; i++) pendingArtistResults[i] = 0;
    }

    private void ClearPendingChoice()
    {
        pendingChoice = CardadoWarPendingChoice.None;
        pendingChoiceActor = null;
        pendingChoiceCard = null;
        pendingCollectorOwnSelected = false;
        pendingCollectorOpponentSelected = false;
        pendingCollectorOwnCard = null;
        pendingCollectorOpponentCard = null;
        pendingJokerTarget = null;
        pendingArtistDieIndex = -1;
        pendingModifierTargetIndex = -1;
        pendingModifierDieIndex = -1;
        pendingMirrorOwnDieIndex = -1;
        for (int i = 0; i < pendingArtistResults.Length; i++) pendingArtistResults[i] = 0;
    }

    public bool TryChooseNoblemanEffect(CardType specialType)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.NoblemanEffect) return false;
        if (specialType != CardType.Artist && specialType != CardType.Knight &&
            specialType != CardType.Collector && specialType != CardType.Bodyguard) return false;

        switch (specialType)
        {
            case CardType.Artist:
                pendingChoice = CardadoWarPendingChoice.SpecialArtistMode;
                return true;
            case CardType.Knight:
                pendingChoice = CardadoWarPendingChoice.SpecialKnightMode;
                return true;
            case CardType.Collector:
                pendingChoice = CardadoWarPendingChoice.SpecialCollectorMode;
                return true;
            case CardType.Bodyguard:
                pendingChoice = CardadoWarPendingChoice.SpecialBodyguardMode;
                return true;
        }
        return false;
    }

    public bool TryChooseModifierDie(int targetPlayerIndex, int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.ModifierTarget || pendingChoiceActor == null) return false;
        if (targetPlayerIndex != pendingChoiceActor.PlayerIndex && targetPlayerIndex != warContext.OpponentOf(pendingChoiceActor).PlayerIndex) return false;
        CardadoWarContext.Participant target = warContext.GetParticipant(targetPlayerIndex);
        if (target == null || !warContext.IsDieTargetable(target, dieIndex) || IsProtected(target, dieIndex)) return false;
        pendingModifierTargetIndex = targetPlayerIndex;
        pendingModifierDieIndex = dieIndex;
        pendingChoice = CardadoWarPendingChoice.ModifierSign;
        return true;
    }

    public bool TryChooseModifierValue(int delta)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.ModifierSign || pendingChoiceActor == null || pendingChoiceCard == null) return false;
        if (delta != 1 && delta != -1) return false;
        CardadoWarContext.Participant target = warContext.GetParticipant(pendingModifierTargetIndex);
        if (target == null || !warContext.IsDieTargetable(target, pendingModifierDieIndex) || IsProtected(target, pendingModifierDieIndex)) return false;
        if (delta > 0 && !pendingChoiceCard.data.canAdd) return false;
        if (delta < 0 && !pendingChoiceCard.data.canSubtract) return false;

        int old = target.MutableDice[pendingModifierDieIndex];
        target.MutableDice[pendingModifierDieIndex] = old + delta;
        warContext.AddEffect(CardadoWarContext.EffectType.Modifier, pendingChoiceCard, pendingChoiceActor, target, pendingModifierDieIndex, old);
        CardInstance resolvedCard = pendingChoiceCard;
        CardadoWarContext.Participant actor = pendingChoiceActor;
        ClearPendingChoice();
        FinishCardResolution(actor, resolvedCard, true);
        return true;
    }

    public bool TryChooseArtistDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.ArtistDie || pendingChoiceActor == null) return false;
        if (!warContext.IsDieTargetable(pendingChoiceActor, dieIndex) || IsProtected(pendingChoiceActor, dieIndex)) return false;
        Reroll(pendingChoiceActor, dieIndex, 1);
        CompletePendingCard(false);
        return true;
    }

    public bool TryChooseKnightDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.KnightDie || pendingChoiceActor == null) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(pendingChoiceActor);
        if (!warContext.IsDieTargetable(opponent, dieIndex) || IsProtected(opponent, dieIndex)) return false;
        Reroll(opponent, dieIndex, 1);
        CompletePendingCard(false);
        return true;
    }

    public bool TryChooseCollectorCard(int cardIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.CollectorOpponentCard || pendingChoiceActor == null) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(pendingChoiceActor);
        if (cardIndex < 0 || cardIndex >= opponent.Cards.Count) return false;

        CardInstance stolen = opponent.MutableCards[cardIndex];
        opponent.MutableCards.RemoveAt(cardIndex);
        if (stolen == null || stolen.data == null) return false;
        stolen.isPlayed = true;

        CardInstance collector = pendingChoiceCard;
        CardadoWarContext.Participant actor = pendingChoiceActor;
        ClearPendingChoice();
        gameManager.DiscardResolvedCard(collector);
        ResolveStolenCard(actor, stolen);
        return true;
    }

    private void ResolveStolenCard(CardadoWarContext.Participant actor, CardInstance stolen)
    {
        bool persistent = ResolveWarCard(actor, stolen);
        if (pendingChoice != CardadoWarPendingChoice.None) return;
        FinishCardResolution(actor, stolen, persistent);
    }

    public bool TryChooseWarBodyguardDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.BodyguardDie || pendingChoiceActor == null || pendingChoiceCard == null) return false;
        if (!warContext.IsDieAvailable(pendingChoiceActor, dieIndex) || IsProtected(pendingChoiceActor, dieIndex)) return false;
        CardInstance card = pendingChoiceCard;
        CardadoWarContext.Participant actor = pendingChoiceActor;
        warContext.AddEffect(CardadoWarContext.EffectType.BodyguardDie, card, actor, actor, dieIndex);
        ClearPendingChoice();
        return true;
    }

    public bool TryChooseMirrorOwnDie(int dieIndex)
    {
        if (!WarInProgress || (pendingChoice != CardadoWarPendingChoice.MirrorOwnDie && pendingChoice != CardadoWarPendingChoice.SpecialMirrorOwnDie) || pendingChoiceActor == null) return false;
        if (!warContext.IsDieTargetable(pendingChoiceActor, dieIndex) || IsProtected(pendingChoiceActor, dieIndex)) return false;
        pendingMirrorOwnDieIndex = dieIndex;
        pendingChoice = pendingChoice == CardadoWarPendingChoice.MirrorOwnDie
            ? CardadoWarPendingChoice.MirrorOpponentDie
            : CardadoWarPendingChoice.SpecialMirrorOpponentDie;
        return true;
    }

    public bool TryChooseMirrorOpponentDie(int dieIndex)
    {
        if (!WarInProgress || (pendingChoice != CardadoWarPendingChoice.MirrorOpponentDie && pendingChoice != CardadoWarPendingChoice.SpecialMirrorOpponentDie) || pendingChoiceActor == null) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(pendingChoiceActor);
        if (!warContext.IsDieTargetable(opponent, dieIndex) || IsProtected(opponent, dieIndex)) return false;
        Exchange(pendingChoiceActor, pendingMirrorOwnDieIndex, opponent, dieIndex);
        CompletePendingCard(false);
        return true;
    }

    public bool TryChooseJokerTarget(bool targetOpponent)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.JokerPlayer || pendingChoiceActor == null) return false;
        pendingJokerTarget = targetOpponent ? warContext.OpponentOf(pendingChoiceActor) : pendingChoiceActor;
        pendingChoice = CardadoWarPendingChoice.JokerDie;
        return true;
    }

    public bool TryChooseJokerDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.JokerDie || pendingJokerTarget == null) return false;
        if (!warContext.IsDieTargetable(pendingJokerTarget, dieIndex) || IsProtected(pendingJokerTarget, dieIndex)) return false;
        Flip(pendingJokerTarget, dieIndex);
        CompletePendingCard(false);
        return true;
    }

    public bool TryChooseSpecialArtistMode(bool rerollAllOwnDice)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialArtistMode || pendingChoiceActor == null) return false;
        if (rerollAllOwnDice)
        {
            RerollAll(pendingChoiceActor);
            CompletePendingCard(false);
            return true;
        }
        pendingChoice = CardadoWarPendingChoice.SpecialArtistDie;
        return true;
    }

    public bool TryChooseSpecialArtistDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialArtistDie || pendingChoiceActor == null) return false;
        if (!warContext.IsDieTargetable(pendingChoiceActor, dieIndex) || IsProtected(pendingChoiceActor, dieIndex)) return false;
        pendingArtistDieIndex = dieIndex;
        for (int i = 0; i < pendingArtistResults.Length; i++) pendingArtistResults[i] = UnityEngine.Random.Range(1, 7);
        pendingChoice = CardadoWarPendingChoice.SpecialArtistResult;
        return true;
    }

    public bool TryChooseSpecialArtistResult(int resultIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialArtistResult || pendingArtistDieIndex < 0) return false;
        if (resultIndex < 0 || resultIndex >= pendingArtistResults.Length) return false;
        pendingChoiceActor.MutableDice[pendingArtistDieIndex] = pendingArtistResults[resultIndex];
        CompletePendingCard(false);
        return true;
    }

    public bool TryChooseSpecialKnightMode(bool rerollAllOpponentDice)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialKnightMode || pendingChoiceActor == null) return false;
        if (rerollAllOpponentDice)
        {
            RerollAll(warContext.OpponentOf(pendingChoiceActor));
            CompletePendingCard(false);
            return true;
        }
        pendingChoice = CardadoWarPendingChoice.SpecialKnightDie;
        return true;
    }

    public bool TryChooseSpecialKnightDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialKnightDie || pendingChoiceActor == null) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(pendingChoiceActor);
        if (!warContext.IsDieTargetable(opponent, dieIndex) || IsProtected(opponent, dieIndex)) return false;
        Reroll(opponent, dieIndex, 1);
        CompletePendingCard(false);
        return true;
    }

    public bool TryChooseSpecialCollectorMode(bool takeOneCardFromEach)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialCollectorMode || pendingChoiceActor == null) return false;
        if (!takeOneCardFromEach)
        {
            for (int i = 0; i < 3; i++)
            {
                CardInstance drawn = gameManager.RoundDeck.Draw();
                if (drawn == null) break;
                drawn.isPlayed = false;
                warContext.AddCard(pendingChoiceActor, drawn);
            }
            CompletePendingCard(false);
            return true;
        }
        pendingChoice = CardadoWarPendingChoice.SpecialCollectorOwnCard;
        return true;
    }

    public bool TryChooseSpecialCollectorOwnCard(int cardIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialCollectorOwnCard || pendingChoiceActor == null) return false;
        if (cardIndex < 0 || cardIndex >= pendingChoiceActor.Cards.Count) return false;
        pendingCollectorOwnCard = pendingChoiceActor.Cards[cardIndex];
        pendingCollectorOwnSelected = true;
        pendingChoice = CardadoWarPendingChoice.SpecialCollectorOpponentCard;
        return true;
    }

    public bool TryChooseSpecialCollectorOpponentCard(int cardIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialCollectorOpponentCard || pendingChoiceActor == null) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(pendingChoiceActor);
        if (cardIndex < 0 || cardIndex >= opponent.Cards.Count) return false;
        pendingCollectorOpponentCard = opponent.Cards[cardIndex];
        pendingCollectorOpponentSelected = true;
        pendingChoice = CardadoWarPendingChoice.SpecialCollectorPlayChoice;
        return true;
    }

    public bool TryChooseSpecialCollectorPlayedCard(bool playOwnCard)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialCollectorPlayChoice || pendingChoiceActor == null) return false;
        CardadoWarContext.Participant actor = pendingChoiceActor;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(actor);
        CardInstance playCard = playOwnCard ? pendingCollectorOwnCard : pendingCollectorOpponentCard;
        CardInstance discardCard = playOwnCard ? pendingCollectorOpponentCard : pendingCollectorOwnCard;
        if (playCard == null || discardCard == null) return false;

        CardadoWarContext.Participant playOwner = playOwnCard ? actor : opponent;
        CardadoWarContext.Participant discardOwner = playOwnCard ? opponent : actor;
        if (!playOwner.MutableCards.Remove(playCard) || !discardOwner.MutableCards.Remove(discardCard)) return false;

        playCard.isPlayed = true;
        discardCard.isPlayed = true;
        CardInstance originalCard = pendingChoiceCard;
        ClearPendingChoice();
        gameManager.DiscardResolvedCard(originalCard);
        gameManager.DiscardResolvedCard(discardCard);
        ResolveStolenCard(actor, playCard);
        return true;
    }

    public bool TryChooseSpecialBodyguardMode(bool protectOnlyOwnDice)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialBodyguardMode || pendingChoiceActor == null || pendingChoiceCard == null) return false;
        CardInstance card = pendingChoiceCard;
        CardadoWarContext.Participant actor = pendingChoiceActor;
        if (protectOnlyOwnDice)
            warContext.AddEffect(CardadoWarContext.EffectType.BodyguardPlayer, card, actor, actor);
        else
            warContext.AddEffect(CardadoWarContext.EffectType.BodyguardHand, card, actor);
        ClearPendingChoice();
        return true;
    }

    public bool TryChooseSpecialMirrorMode(bool swapOneDieEach)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialMirrorMode || pendingChoiceActor == null) return false;
        pendingMirrorSwapOne = swapOneDieEach;
        // In a two-player War both legal special Mirror modes select one die from each participant.
        pendingChoice = CardadoWarPendingChoice.SpecialMirrorOwnDie;
        return true;
    }

    public bool TryChooseSpecialMirrorOwnDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialMirrorOwnDie) return false;
        return TryChooseMirrorOwnDie(dieIndex);
    }

    public bool TryChooseSpecialMirrorOpponentDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != CardadoWarPendingChoice.SpecialMirrorOpponentDie) return false;
        return TryChooseMirrorOpponentDie(dieIndex);
    }

    private void CompletePendingCard(bool persistent)
    {
        CardadoWarContext.Participant actor = pendingChoiceActor;
        CardInstance card = pendingChoiceCard;
        ClearPendingChoice();
        FinishCardResolution(actor, card, persistent);
    }

    private void FinishCardResolution(CardadoWarContext.Participant actor, CardInstance card, bool persistent)
    {
        if (card == null) return;
        if (!persistent) gameManager.DiscardResolvedCard(card);
        if (!persistent && (card.data.cardType == CardType.King || card.data.cardType == CardType.Queen))
            GrantBonusCardAction(actor);
    }

    private void GrantBonusCardAction(CardadoWarContext.Participant actor)
    {
        if (actor == null || actor.PlayerIndex != GetCurrentWarPlayerIndex() || !bonusCardActionAvailable) return;
        bonusCardActionAvailable = false;
        warCardActionPending = actor.Cards.Count > 0 && !warContext.IsBlocked(actor);
        if (warCardActionPending)
            gameManager.RequestWarCardAction(gameManager.Players[actor.PlayerIndex], CardadoCardActionRequestType.ChooseCard);
    }

    private void ResolveQueen(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent)
    {
        DiscardWarCards(actor);
        DiscardWarCards(opponent);
        DealWarCards(actor, warCardCount);
        DealWarCards(opponent, warCardCount);
    }

    private void ResolveExecutioner(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent)
    {
        if (!warContext.HasPlayedCard(opponent))
        {
            warContext.Block(opponent);
            return;
        }

        List<CardadoWarContext.Effect> effects = new List<CardadoWarContext.Effect>(warContext.Effects);
        foreach (CardadoWarContext.Effect effect in effects)
        {
            if (effect.OwnerIndex != opponent.PlayerIndex) continue;
            if (effect.Type == CardadoWarContext.EffectType.Modifier && effect.TargetIndex >= 0 && effect.DieIndex >= 0)
            {
                CardadoWarContext.Participant target = warContext.GetParticipant(effect.TargetIndex);
                if (target != null && effect.DieIndex < target.MutableDice.Count)
                    target.MutableDice[effect.DieIndex] = effect.OriginalValue;
            }
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
            warContext.RemoveEffect(effect);
        }
    }

    private void Reroll(CardadoWarContext.Participant participant, int dieIndex, int times)
    {
        if (participant == null || dieIndex < 0 || !warContext.IsDieTargetable(participant, dieIndex) || IsProtected(participant, dieIndex)) return;
        for (int i = 0; i < times; i++) participant.MutableDice[dieIndex] = UnityEngine.Random.Range(1, 7);
    }

    private void RerollAll(CardadoWarContext.Participant participant)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.MutableDice.Count; i++)
            if (warContext.IsDieTargetable(participant, i) && !IsProtected(participant, i))
                participant.MutableDice[i] = UnityEngine.Random.Range(1, 7);
    }

    private void Exchange(CardadoWarContext.Participant a, int ad, CardadoWarContext.Participant b, int bd)
    {
        if (a == null || b == null || ad < 0 || bd < 0) return;
        if (!warContext.IsDieTargetable(a, ad) || !warContext.IsDieTargetable(b, bd)) return;
        if (IsProtected(a, ad) || IsProtected(b, bd)) return;
        int value = a.MutableDice[ad];
        a.MutableDice[ad] = b.MutableDice[bd];
        b.MutableDice[bd] = value;
    }

    private void Flip(CardadoWarContext.Participant participant, int dieIndex)
    {
        if (participant == null || dieIndex < 0 || !warContext.IsDieTargetable(participant, dieIndex) || IsProtected(participant, dieIndex)) return;
        participant.MutableDice[dieIndex] = 7 - participant.MutableDice[dieIndex];
    }

    private bool IsProtected(CardadoWarContext.Participant participant, int dieIndex)
    {
        foreach (CardadoWarContext.Effect effect in warContext.Effects)
        {
            if (effect.Type == CardadoWarContext.EffectType.BodyguardHand) return true;
            if (effect.Type == CardadoWarContext.EffectType.BodyguardDie &&
                effect.TargetIndex == participant.PlayerIndex && effect.DieIndex == dieIndex) return true;
            if (effect.Type == CardadoWarContext.EffectType.BodyguardPlayer &&
                effect.TargetIndex == participant.PlayerIndex) return true;
        }
        return false;
    }

    public bool TryPlayWarDieForPlayer(int playerIndex, int dieIndex)
    {
        if (!WarInProgress || uiStep != WarUiStep.Playing || warCardActionPending || pendingChoice != CardadoWarPendingChoice.None) return false;
        if (playerIndex != GetCurrentWarPlayerIndex() || !IsWarDieAvailable(playerIndex, dieIndex)) return false;

        CardadoWarContext.Participant player = warContext.GetParticipant(playerIndex);
        int value = player.MutableDice[dieIndex];
        player.MutablePlayedDice[dieIndex] = true;
        if (playerIndex == challengerIndex) challengerCurrentDieIndex = dieIndex;
        else targetCurrentDieIndex = dieIndex;
        currentHandTurns++;
        gameManager.NotifyWarDiePlayed(gameManager.Players[playerIndex], dieIndex, value);
        RemoveEffectsForPlayedDie(player, dieIndex);

        if (currentHandTurns < 2)
        {
            currentWarTurn = 1 - currentWarTurn;
            BeginWarTurn();
            return true;
        }

        ResolveCurrentWarHand();
        return true;
    }

    private void RemoveEffectsForPlayedDie(CardadoWarContext.Participant player, int dieIndex)
    {
        List<CardadoWarContext.Effect> remove = new List<CardadoWarContext.Effect>();
        foreach (CardadoWarContext.Effect effect in warContext.Effects)
        {
            if (effect.Type == CardadoWarContext.EffectType.Modifier &&
                effect.TargetIndex == player.PlayerIndex && effect.DieIndex == dieIndex)
                remove.Add(effect);
            else if (effect.Type == CardadoWarContext.EffectType.BodyguardDie &&
                effect.TargetIndex == player.PlayerIndex && effect.DieIndex == dieIndex)
                remove.Add(effect);
        }

        foreach (CardadoWarContext.Effect effect in remove)
        {
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
            warContext.RemoveEffect(effect);
        }

        RemoveBodyguardPlayerIfAllDicePlayed(player);
    }

    private void RemoveBodyguardPlayerIfAllDicePlayed(CardadoWarContext.Participant player)
    {
        for (int i = 0; i < player.MutablePlayedDice.Count; i++)
            if (!player.MutablePlayedDice[i]) return;

        List<CardadoWarContext.Effect> remove = new List<CardadoWarContext.Effect>();
        foreach (CardadoWarContext.Effect effect in warContext.Effects)
            if (effect.Type == CardadoWarContext.EffectType.BodyguardPlayer && effect.TargetIndex == player.PlayerIndex)
                remove.Add(effect);

        foreach (CardadoWarContext.Effect effect in remove)
        {
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
            warContext.RemoveEffect(effect);
        }
    }

    public bool IsWarDieAvailable(int playerIndex, int dieIndex)
    {
        if (warContext == null) return false;
        CardadoWarContext.Participant participant = warContext.GetParticipant(playerIndex);
        return participant != null && warContext.IsDieAvailable(participant, dieIndex);
    }

    public bool IsWarDieTargetable(int playerIndex, int dieIndex)
    {
        if (warContext == null) return false;
        CardadoWarContext.Participant participant = warContext.GetParticipant(playerIndex);
        return participant != null && warContext.IsDieTargetable(participant, dieIndex) && !IsProtected(participant, dieIndex);
    }

    private void ResolveCurrentWarHand()
    {
        int challengerValue = challengerCurrentDieIndex >= 0 ? warContext.Challenger.MutableDice[challengerCurrentDieIndex] : 0;
        int targetValue = targetCurrentDieIndex >= 0 ? warContext.Target.MutableDice[targetCurrentDieIndex] : 0;

        if (challengerValue > targetValue) warContext.ChallengerHandsWon++;
        else if (targetValue > challengerValue) warContext.TargetHandsWon++;
        else if (challengerPlaysFirst) warContext.ChallengerHandsWon++;
        else warContext.TargetHandsWon++;

        DiscardHandScopedEffects();
        if (warContext.ChallengerHandsWon >= 2 || warContext.TargetHandsWon >= 2 || warContext.HandNumber >= warDiceCount)
        {
            int winner = warContext.ChallengerHandsWon > warContext.TargetHandsWon ? challengerIndex :
                warContext.TargetHandsWon > warContext.ChallengerHandsWon ? targetIndex :
                (challengerPlaysFirst ? challengerIndex : targetIndex);
            ResolveWar(winner);
            return;
        }

        warContext.HandNumber++;
        currentWarTurn = challengerPlaysFirst ? 0 : 1;
        BeginWarHand();
    }

    private void DiscardHandScopedEffects()
    {
        List<CardadoWarContext.Effect> remove = new List<CardadoWarContext.Effect>();
        foreach (CardadoWarContext.Effect effect in warContext.Effects)
            if (effect.Type == CardadoWarContext.EffectType.BodyguardDie || effect.Type == CardadoWarContext.EffectType.BodyguardHand)
                remove.Add(effect);

        foreach (CardadoWarContext.Effect effect in remove)
        {
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
            warContext.RemoveEffect(effect);
        }
        warContext.ClearHandScopedEffects();
    }

    private void ResolveWar(int winnerIndex)
    {
        int loserIndex = winnerIndex == challengerIndex ? targetIndex : challengerIndex;
        int transfer = Math.Min(warWager, gameManager.Players[loserIndex].chips);
        if (transfer > 0)
        {
            gameManager.Players[loserIndex].chips -= transfer;
            gameManager.Players[winnerIndex].chips += transfer;
        }

        DiscardWarCards(warContext.Challenger);
        DiscardWarCards(warContext.Target);
        DiscardAllPersistentEffects();
        warContext = null;
        warCardActionPending = false;
        warResolved = true;
        ClearPendingChoice();
        uiStep = WarUiStep.Complete;
    }

    private void DiscardWarCards(CardadoWarContext.Participant participant)
    {
        List<CardInstance> cards = new List<CardInstance>(participant.Cards);
        participant.MutableCards.Clear();
        foreach (CardInstance card in cards)
            if (card != null) gameManager.DiscardResolvedCard(card);
    }

    private void DiscardAllPersistentEffects()
    {
        if (warContext == null) return;
        List<CardadoWarContext.Effect> effects = new List<CardadoWarContext.Effect>(warContext.Effects);
        foreach (CardadoWarContext.Effect effect in effects)
        {
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
            warContext.RemoveEffect(effect);
        }
    }

    private void CleanupContextWithoutApplyingResult()
    {
        if (warContext == null) return;
        DiscardWarCards(warContext.Challenger);
        DiscardWarCards(warContext.Target);
        DiscardAllPersistentEffects();
        warContext = null;
        warCardActionPending = false;
        ClearPendingChoice();
    }

    public int CurrentWarClaimantIndex => GetCurrentWarPlayerIndex();

    public bool TryDeclareAnotherWar(int playerIndex)
    {
        if (uiStep != WarUiStep.Complete || !warResolved || playerIndex != GetCurrentWarPlayerIndex() || !CanClaimWar(playerIndex)) return false;
        AdvanceToCurrentClaimant();
        return true;
    }

    public bool TryContinueWarPhase()
    {
        if (uiStep != WarUiStep.Complete || !warResolved) return false;
        currentClaimPosition++;
        AdvanceToCurrentClaimant();
        return true;
    }

    public bool TryFinishWarPhase()
    {
        if (uiStep != WarUiStep.Complete || (warContext != null && !warResolved)) return false;
        gameManager.CompleteWarPhase();
        return true;
    }

    private int GetCurrentWarPlayerIndex() => currentWarTurn == 0 ? challengerIndex : targetIndex;

}