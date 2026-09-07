using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative War rules. War cards and dice live only in CardadoWarContext;
/// normal CardadoPlayerState hands/dice are never repurposed for the temporary War.
/// </summary>
public class CardadoWarManager : MonoBehaviour
{
    private enum WarUiStep { Claim, Target, Wager, Order, Playing, Complete }
    private enum WarChoiceType { None, NoblemanEffect, NoblemanArtistDie, BodyguardDie }

    [SerializeField] private CardadoGameManager gameManager;
    [SerializeField] private bool showTemporaryUi = true;
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

    private WarChoiceType pendingChoice;
    private CardadoWarContext.Participant pendingChoiceActor;
    private CardInstance pendingChoiceCard;

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;

    public bool WarInProgress => warContext != null && !warResolved;
    public bool IsWarCardActionPending => warCardActionPending;
    public CardadoWarContext Context => warContext;
    public int WarOpponentIndex => warContext == null ? -1 :
        (GetCurrentWarPlayerIndex() == challengerIndex ? targetIndex : challengerIndex);

    private void Awake()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
        CleanupContextWithoutApplyingResult();
    }

    private void Start()
    {
        if (gameManager != null && gameManager.Phase == CardadoGamePhase.WarResolution) BeginWarPhase();
    }

    private void OnPhaseChanged(CardadoGamePhase phase)
    {
        if (phase == CardadoGamePhase.WarResolution) BeginWarPhase();
    }

    private void BeginWarPhase()
    {
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
        if (gameManager.Players[playerIndex].chips <= 0) return false;
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
        if (wager < 1 || wager > 2) return false;
        if (gameManager.Players[challengerIndex].chips < wager || gameManager.Players[targetIndex].chips < wager) return false;
        warWager = wager;
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
        if (gameManager.RoundDeck == null) throw new InvalidOperationException("War cannot start because the round deck is not initialized.");

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

        warCardActionPending = current.Cards.Count > 0 && !warContext.IsBlocked(current);
        gameManager.NotifyWarHandTurnStarted(gameManager.Players[current.PlayerIndex], warContext.HandNumber, current.PlayerIndex);
        if (warCardActionPending)
            gameManager.RequestWarCardAction(gameManager.Players[current.PlayerIndex], CardadoCardActionRequestType.ChooseCard);
    }

    public bool TrySkipCardAction(int playerIndex)
    {
        if (!WarInProgress || uiStep != WarUiStep.Playing || pendingChoice != WarChoiceType.None) return false;
        if (playerIndex != GetCurrentWarPlayerIndex()) return false;
        warCardActionPending = false;
        return true;
    }

    public bool TryPlayWarCard(int playerIndex, int cardIndex)
    {
        if (!WarInProgress || !warCardActionPending || pendingChoice != WarChoiceType.None) return false;
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
        if (pendingChoice != WarChoiceType.None) return true;
        if (!persistent) gameManager.DiscardResolvedCard(card);
        return true;
    }

    private bool ResolveWarCard(CardadoWarContext.Participant actor, CardInstance card)
    {
        if (card.data.isBlankCard) return false;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(actor);

        if (card.data.cardType == CardType.GordonRobleys)
        {
            BeginPendingChoice(WarChoiceType.NoblemanEffect, actor, card);
            return false;
        }

        if (card.data.rarity == CardRarity.Special)
            return ResolveSpecial(actor, opponent, card);

        if (card.data.isModifier)
        {
            int die = FindFirstTargetableDie(opponent);
            if (die < 0) return false;
            int old = opponent.MutableDice[die];
            int delta = card.data.canAdd ? 1 : -1;
            opponent.MutableDice[die] = old + delta;
            warContext.AddEffect(CardadoWarContext.EffectType.Modifier, card, actor, opponent, die, old);
            return true;
        }

        switch (card.data.cardType)
        {
            case CardType.Artist:
                Reroll(actor, FindFirstAvailableDie(actor), 1);
                break;
            case CardType.Knight:
                Reroll(opponent, FindFirstTargetableDie(opponent), 1);
                break;
            case CardType.Collector:
                ResolveCollector(actor, opponent);
                break;
            case CardType.Bodyguard:
                BeginPendingChoice(WarChoiceType.BodyguardDie, actor, card);
                return false;
            case CardType.Mirror:
                Exchange(actor, FindFirstTargetableDie(actor), opponent, FindFirstTargetableDie(opponent));
                break;
            case CardType.Executioner:
                ResolveExecutioner(actor, opponent);
                break;
            case CardType.Joker:
                Flip(opponent, FindFirstTargetableDie(opponent));
                break;
            case CardType.King:
                RerollAll(actor);
                RerollAll(opponent);
                break;
            case CardType.Queen:
                ResolveQueen(actor, opponent);
                break;
        }
        return false;
    }

    private bool ResolveSpecial(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent, CardInstance card)
    {
        switch (card.data.cardType)
        {
            case CardType.Artist:
                BeginPendingChoice(WarChoiceType.NoblemanArtistDie, actor, card);
                return false;
            case CardType.Knight:
                RerollAll(opponent);
                return false;
            case CardType.Collector:
                ResolveCollector(actor, opponent);
                return false;
            case CardType.Bodyguard:
                return AddPersistentEffect(CardadoWarContext.EffectType.BodyguardPlayer, card, actor, actor);
            case CardType.Mirror:
                Exchange(actor, FindFirstTargetableDie(actor), opponent, FindFirstTargetableDie(opponent));
                return false;
            case CardType.Executioner:
                DiscardWarCards(opponent);
                return false;
            default:
                return false;
        }
    }

    private void BeginPendingChoice(WarChoiceType choice, CardadoWarContext.Participant actor, CardInstance card)
    {
        pendingChoice = choice;
        pendingChoiceActor = actor;
        pendingChoiceCard = card;
    }

    private void ClearPendingChoice()
    {
        pendingChoice = WarChoiceType.None;
        pendingChoiceActor = null;
        pendingChoiceCard = null;
    }

    public bool TryChooseNoblemanEffect(CardType specialType)
    {
        if (!WarInProgress || pendingChoice != WarChoiceType.NoblemanEffect || pendingChoiceActor == null || pendingChoiceCard == null)
            return false;
        if (specialType != CardType.Artist && specialType != CardType.Knight &&
            specialType != CardType.Collector && specialType != CardType.Bodyguard)
            return false;

        CardadoWarContext.Participant actor = pendingChoiceActor;
        CardInstance card = pendingChoiceCard;
        CardadoWarContext.Participant opponent = warContext.OpponentOf(actor);
        bool persistent = false;

        switch (specialType)
        {
            case CardType.Artist:
                pendingChoice = WarChoiceType.NoblemanArtistDie;
                return true;
            case CardType.Knight:
                RerollAll(opponent);
                break;
            case CardType.Collector:
                ResolveNoblemanCollector(actor, opponent);
                break;
            case CardType.Bodyguard:
                persistent = AddPersistentEffect(CardadoWarContext.EffectType.BodyguardPlayer, card, actor, actor);
                break;
        }

        ClearPendingChoice();
        if (!persistent) gameManager.DiscardResolvedCard(card);
        return true;
    }

    public bool TryChooseWarBodyguardDie(int dieIndex)
    {
        if (!WarInProgress || pendingChoice != WarChoiceType.BodyguardDie || pendingChoiceActor == null || pendingChoiceCard == null)
            return false;
        if (dieIndex < 0 || !warContext.IsDieAvailable(pendingChoiceActor, dieIndex) || IsProtected(pendingChoiceActor, dieIndex))
            return false;

        CardadoWarContext.Participant actor = pendingChoiceActor;
        CardInstance card = pendingChoiceCard;
        AddPersistentEffect(CardadoWarContext.EffectType.BodyguardDie, card, actor, actor, dieIndex);
        ClearPendingChoice();
        gameManager.NotifyWarCardActionResolved(gameManager.Players[actor.PlayerIndex], card);
        return true;
    }

    private bool ResolvePendingNoblemanArtistDie(int dieIndex)
    {
        if (pendingChoice != WarChoiceType.NoblemanArtistDie || pendingChoiceActor == null || pendingChoiceCard == null)
            return false;
        if (dieIndex < 0 || !warContext.IsDieAvailable(pendingChoiceActor, dieIndex)) return false;

        CardadoWarContext.Participant actor = pendingChoiceActor;
        CardInstance card = pendingChoiceCard;
        Reroll(actor, dieIndex, 3);
        ClearPendingChoice();
        gameManager.DiscardResolvedCard(card);
        gameManager.NotifyWarCardActionResolved(gameManager.Players[actor.PlayerIndex], card);
        return true;
    }

    private void ResolveNoblemanCollector(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent)
    {
        if (opponent.MutableCards.Count == 0) return;
        CardInstance stolen = opponent.MutableCards[0];
        opponent.MutableCards.RemoveAt(0);
        stolen.isPlayed = true;

        // Collector is allowed to play the stolen card immediately. If that card
        // itself needs an interactive choice, keep the War UI on the same action
        // rather than silently resolving it against an arbitrary target.
        if (stolen.data != null && (stolen.data.cardType == CardType.Bodyguard || stolen.data.cardType == CardType.GordonRobleys))
        {
            gameManager.DiscardResolvedCard(stolen);
            return;
        }

        bool persistent = ResolveWarCard(actor, stolen);
        if (!persistent && pendingChoice == WarChoiceType.None) gameManager.DiscardResolvedCard(stolen);
    }

    private bool AddPersistentEffect(CardadoWarContext.EffectType type, CardInstance card,
        CardadoWarContext.Participant owner, CardadoWarContext.Participant target, int dieIndex = -1)
    {
        warContext.AddEffect(type, card, owner, target, dieIndex);
        return true;
    }

    private void ResolveCollector(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent)
    {
        if (opponent.MutableCards.Count == 0) return;
        CardInstance stolen = opponent.MutableCards[0];
        opponent.MutableCards.RemoveAt(0);
        stolen.isPlayed = true;
        bool persistent = ResolveWarCard(actor, stolen);
        if (!persistent && pendingChoice == WarChoiceType.None) gameManager.DiscardResolvedCard(stolen);
    }

    private void ResolveExecutioner(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent)
    {
        if (!warContext.HasPlayedCard(opponent))
        {
            warContext.Block(opponent);
            return;
        }

        for (int i = warContext.Effects.Count - 1; i >= 0; i--)
        {
            CardadoWarContext.Effect effect = warContext.Effects[i];
            if (effect.OwnerIndex != opponent.PlayerIndex) continue;
            if (effect.Type == CardadoWarContext.EffectType.Modifier && effect.TargetIndex == opponent.PlayerIndex && effect.DieIndex >= 0)
            {
                CardadoWarContext.Participant effectTarget = warContext.GetParticipant(effect.TargetIndex);
                if (effectTarget != null && effect.DieIndex < effectTarget.MutableDice.Count)
                    effectTarget.MutableDice[effect.DieIndex] = effect.OriginalValue;
            }
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
            warContext.RemoveEffect(effect);
            break;
        }
    }

    private void ResolveQueen(CardadoWarContext.Participant actor, CardadoWarContext.Participant opponent)
    {
        DiscardWarCards(actor);
        DiscardWarCards(opponent);
        DealWarCards(actor, warCardCount);
        DealWarCards(opponent, warCardCount);
    }

    private void DiscardWarCards(CardadoWarContext.Participant participant)
    {
        List<CardInstance> cards = new List<CardInstance>(participant.Cards);
        participant.MutableCards.Clear();
        foreach (CardInstance card in cards)
            if (card != null) gameManager.DiscardResolvedCard(card);
    }

    private void Reroll(CardadoWarContext.Participant participant, int dieIndex, int times)
    {
        if (participant == null || dieIndex < 0 || !warContext.IsDieTargetable(participant, dieIndex)) return;
        for (int i = 0; i < times; i++) participant.MutableDice[dieIndex] = UnityEngine.Random.Range(1, 7);
    }

    private void RerollAll(CardadoWarContext.Participant participant)
    {
        if (participant == null) return;
        for (int i = 0; i < participant.MutableDice.Count; i++)
            if (warContext.IsDieTargetable(participant, i)) participant.MutableDice[i] = UnityEngine.Random.Range(1, 7);
    }

    private void Exchange(CardadoWarContext.Participant a, int ad, CardadoWarContext.Participant b, int bd)
    {
        if (a == null || b == null || ad < 0 || bd < 0) return;
        if (!warContext.IsDieTargetable(a, ad) || !warContext.IsDieTargetable(b, bd)) return;
        int value = a.MutableDice[ad];
        a.MutableDice[ad] = b.MutableDice[bd];
        b.MutableDice[bd] = value;
    }

    private void Flip(CardadoWarContext.Participant participant, int dieIndex)
    {
        if (participant == null || dieIndex < 0 || !warContext.IsDieTargetable(participant, dieIndex)) return;
        participant.MutableDice[dieIndex] = 7 - participant.MutableDice[dieIndex];
    }

    private int FindFirstAvailableDie(CardadoWarContext.Participant participant)
    {
        for (int i = 0; i < participant.MutableDice.Count; i++)
            if (warContext.IsDieAvailable(participant, i)) return i;
        return -1;
    }

    private int FindFirstTargetableDie(CardadoWarContext.Participant participant)
    {
        for (int i = 0; i < participant.MutableDice.Count; i++)
            if (warContext.IsDieTargetable(participant, i) && !IsProtected(participant, i)) return i;
        return -1;
    }

    private bool IsProtected(CardadoWarContext.Participant participant, int dieIndex)
    {
        foreach (CardadoWarContext.Effect effect in warContext.Effects)
        {
            if (effect.Type == CardadoWarContext.EffectType.BodyguardDie &&
                effect.TargetIndex == participant.PlayerIndex && effect.DieIndex == dieIndex) return true;
            if (effect.Type == CardadoWarContext.EffectType.BodyguardPlayer &&
                effect.TargetIndex == participant.PlayerIndex) return true;
        }
        return false;
    }

    public bool TryPlayWarDieForPlayer(int playerIndex, int dieIndex)
    {
        if (!WarInProgress || uiStep != WarUiStep.Playing || warCardActionPending || pendingChoice != WarChoiceType.None) return false;
        if (playerIndex != GetCurrentWarPlayerIndex() || !IsWarDieAvailable(playerIndex, dieIndex)) return false;

        CardadoWarContext.Participant player = warContext.GetParticipant(playerIndex);
        int value = player.MutableDice[dieIndex];
        player.MutablePlayedDice[dieIndex] = true;
        if (playerIndex == challengerIndex) challengerCurrentDieIndex = dieIndex;
        else targetCurrentDieIndex = dieIndex;
        currentHandTurns++;
        gameManager.NotifyWarDiePlayed(gameManager.Players[playerIndex], dieIndex, value);

        if (currentHandTurns < 2)
        {
            currentWarTurn = 1 - currentWarTurn;
            BeginWarTurn();
            return true;
        }

        ResolveCurrentWarHand();
        return true;
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
        return participant != null && warContext.IsDieTargetable(participant, dieIndex);
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

    private int GetCurrentWarPlayerIndex() => currentWarTurn == 0 ? challengerIndex : targetIndex;

    private string BuildClaimOrderLabel()
    {
        List<string> names = new List<string>();
        foreach (int index in claimOrder)
            if (index >= 0 && index < gameManager.Players.Count) names.Add(gameManager.Players[index].playerId);
        return string.Join(" -> ", names);
    }

    private string DescribeOptimalClaim(int playerIndex)
    {
        List<CardInstance> claim = CardadoWarCardRules.FindOptimalClaim(gameManager.Players[playerIndex].hand.cardsInHand);
        if (claim == null) return "none";
        List<string> labels = new List<string>();
        foreach (CardInstance card in claim)
            labels.Add(card != null && card.data != null ? card.data.id : "?");
        return string.Join(" + ", labels);
    }

    private void OnGUI()
    {
        if (!showTemporaryUi || gameManager == null || gameManager.Phase != CardadoGamePhase.WarResolution) return;
        EnsureStyles();

        const float width = 760f;
        const float height = 500f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);

        switch (uiStep)
        {
            case WarUiStep.Claim: DrawClaimPanel(panel, width); break;
            case WarUiStep.Target: DrawTargetPanel(panel, width); break;
            case WarUiStep.Wager: DrawWagerPanel(panel, width); break;
            case WarUiStep.Order: DrawOrderPanel(panel, width); break;
            case WarUiStep.Playing: DrawPlayingPanel(panel, width); break;
            default: DrawCompletePanel(panel, width); break;
        }
    }

    private void DrawClaimPanel(Rect panel, float width)
    {
        if (currentClaimPosition >= claimOrder.Count) { DrawCompletePanel(panel, width); return; }
        int playerIndex = claimOrder[currentClaimPosition];
        CardadoPlayerState player = gameManager.Players[playerIndex];
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — DECLARE OR PASS", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 75, width - 50, 30), $"{player.playerId} — chips {player.chips}", GUI.skin.label);
        GUI.Label(new Rect(panel.x + 25, panel.y + 110, width - 50, 30), $"Optimal claim: {DescribeOptimalClaim(playerIndex)}", GUI.skin.label);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 160, width - 50, 60), "DECLARE WAR", buttonStyle)) TryClaimWar(playerIndex);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 235, width - 50, 60), "PASS", buttonStyle)) TryPassWar(playerIndex);
        GUI.Label(new Rect(panel.x + 25, panel.y + 330, width - 50, 30), $"Order: {BuildClaimOrderLabel()}", GUI.skin.label);
    }

    private void DrawTargetPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE OPPONENT", titleStyle);
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (i == challengerIndex || gameManager.Players[i].chips < 1) continue;
            if (GUI.Button(new Rect(panel.x + 25, panel.y + 95 + i * 65, width - 50, 55), $"{gameManager.Players[i].playerId} — {gameManager.Players[i].chips} chip(s)", buttonStyle))
                TryChooseTarget(i);
        }
    }

    private void DrawWagerPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE WAGER", titleStyle);
        for (int wager = 1; wager <= 2; wager++)
        {
            bool valid = gameManager.Players[challengerIndex].chips >= wager && gameManager.Players[targetIndex].chips >= wager;
            GUI.enabled = valid;
            if (GUI.Button(new Rect(panel.x + 25, panel.y + 105 + (wager - 1) * 80, width - 50, 60), $"{wager} CHIP{(wager == 1 ? "" : "S")}", buttonStyle))
                TryChooseWarWager(wager);
            GUI.enabled = true;
        }
    }

    private void DrawOrderPanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — CHOOSE ORDER", titleStyle);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 105, width - 50, 65), "CHALLENGER PLAYS FIRST", buttonStyle)) TryChooseWarOrder(true);
        if (GUI.Button(new Rect(panel.x + 25, panel.y + 190, width - 50, 65), "CHALLENGER PLAYS SECOND", buttonStyle)) TryChooseWarOrder(false);
    }

    private void DrawPlayingPanel(Rect panel, float width)
    {
        CardadoWarContext.Participant current = warContext == null ? null : warContext.CurrentPlayer;
        GUI.Label(new Rect(panel.x + 25, panel.y + 20, width - 50, 45), "WAR — TEMPORARY 1v1", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 70, width - 50, 30), $"{warContext.Challenger.PlayerId} {warContext.ChallengerHandsWon} — {warContext.TargetHandsWon} {warContext.Target.PlayerId}", GUI.skin.label);
        if (current != null) GUI.Label(new Rect(panel.x + 25, panel.y + 105, width - 50, 30), $"Hand {warContext.HandNumber} — {current.PlayerId}", GUI.skin.label);

        if (pendingChoice != WarChoiceType.None)
        {
            DrawPendingChoicePanel(panel, current);
            return;
        }

        DrawWarCards(panel, current);
        DrawWarDice(panel, warContext.Challenger, panel.y + 285);
        DrawWarDice(panel, warContext.Target, panel.y + 385);
    }

    private void DrawPendingChoicePanel(Rect panel, CardadoWarContext.Participant current)
    {
        if (pendingChoiceActor == null) return;

        string title = pendingChoice == WarChoiceType.NoblemanEffect ? "NOBLEMAN — CHOOSE A SPECIAL EFFECT" :
            pendingChoice == WarChoiceType.NoblemanArtistDie ? "NOBLEMAN / ARTIST — CHOOSE YOUR DIE" :
            "BODYGUARD — CHOOSE A DIE TO PROTECT";
        GUI.Label(new Rect(panel.x + 25, panel.y + 140, 700, 35), title, GUI.skin.label);

        if (pendingChoice == WarChoiceType.NoblemanEffect)
        {
            CardType[] options = { CardType.Artist, CardType.Knight, CardType.Collector, CardType.Bodyguard };
            string[] labels = { "ARTIST SPECIAL", "SOLDIER SPECIAL", "COLLECTOR SPECIAL", "BODYGUARD SPECIAL" };
            for (int i = 0; i < options.Length; i++)
            {
                float x = panel.x + 25 + i * 180;
                if (GUI.Button(new Rect(x, panel.y + 195, 165, 70), labels[i], buttonStyle))
                    TryChooseNoblemanEffect(options[i]);
            }
            return;
        }

        for (int i = 0; i < pendingChoiceActor.Dice.Count; i++)
        {
            if (!warContext.IsDieAvailable(pendingChoiceActor, i)) continue;
            if (pendingChoice == WarChoiceType.BodyguardDie && IsProtected(pendingChoiceActor, i)) continue;
            float x = panel.x + 25 + i * 120;
            if (GUI.Button(new Rect(x, panel.y + 205, 100, 60), $"DIE {i + 1}\n{pendingChoiceActor.Dice[i]}", buttonStyle))
            {
                if (pendingChoice == WarChoiceType.BodyguardDie) TryChooseWarBodyguardDie(i);
                else ResolvePendingNoblemanArtistDie(i);
            }
        }
    }

    private void DrawWarCards(Rect panel, CardadoWarContext.Participant current)
    {
        if (current == null) return;
        GUI.Label(new Rect(panel.x + 25, panel.y + 140, 700, 30), warCardActionPending ? "Choose a War card:" : "Card action resolved — choose a die.", GUI.skin.label);
        if (!warCardActionPending) return;

        float x = panel.x + 25;
        for (int i = 0; i < current.Cards.Count; i++)
        {
            CardInstance card = current.Cards[i];
            if (card == null || card.data == null) continue;
            if (GUI.Button(new Rect(x, panel.y + 175, 150, 75), card.data.id + "\n" + card.data.cardType, buttonStyle))
                TryPlayWarCard(current.PlayerIndex, i);
            x += 160;
        }
    }

    private void DrawWarDice(Rect panel, CardadoWarContext.Participant participant, float y)
    {
        GUI.Label(new Rect(panel.x + 25, y, 220, 30), participant.PlayerId, GUI.skin.label);
        float x = panel.x + 230;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            if (!IsWarDieAvailable(participant.PlayerIndex, i)) continue;
            GUI.enabled = !warCardActionPending && pendingChoice == WarChoiceType.None && participant.PlayerIndex == GetCurrentWarPlayerIndex();
            if (GUI.Button(new Rect(x, y - 5, 90, 55), participant.Dice[i].ToString(), buttonStyle))
                TryPlayWarDieForPlayer(participant.PlayerIndex, i);
            GUI.enabled = true;
            x += 105;
        }
    }

    private void DrawCompletePanel(Rect panel, float width)
    {
        GUI.Label(new Rect(panel.x + 25, panel.y + 30, width - 50, 45), "WAR PHASE COMPLETE", titleStyle);
        GUI.Label(new Rect(panel.x + 25, panel.y + 85, width - 50, 30), "The War result has been applied to the normal match state.", GUI.skin.label);

        if (warResolved && challengerIndex >= 0)
        {
            bool canAgain = CanClaimWar(challengerIndex);
            if (canAgain && GUI.Button(new Rect(panel.x + 25, panel.y + 145, width - 50, 60), "DECLARE ANOTHER WAR", selectedButtonStyle))
            {
                AdvanceToCurrentClaimant();
                return;
            }
            if (GUI.Button(new Rect(panel.x + 25, panel.y + (canAgain ? 220 : 145), width - 50, 60), "CONTINUE", selectedButtonStyle))
            {
                currentClaimPosition++;
                AdvanceToCurrentClaimant();
            }
            return;
        }

        if (GUI.Button(new Rect(panel.x + 25, panel.y + 150, width - 50, 60), "FINISH WAR PHASE", selectedButtonStyle))
            gameManager.CompleteWarPhase();
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(20, 20, 20, 20) };
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        selectedButtonStyle = new GUIStyle(buttonStyle);
    }

    private void NotifyPendingCardResolved(CardInstance card, int playerIndex)
    {
        if (card != null) gameManager.NotifyWarCardActionResolved(gameManager.Players[playerIndex], card);
    }
}
