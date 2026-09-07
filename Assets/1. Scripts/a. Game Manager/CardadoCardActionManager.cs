using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardadoCardActionPendingChoice
{
    None,
    ModifierDirection,
    ModifierTarget,
    ArtistDie,
    KnightTarget,
    KnightDie,
    CollectorTarget,
    CollectorCard,
    BodyguardDie,
    MirrorTarget,
    MirrorOwnDie,
    MirrorOpponentDie,
    ExecutionerTarget,
    SpecialArtistMode,
    SpecialArtistDie,
    SpecialArtistResult,
    SpecialKnightMode,
    SpecialKnightTarget,
    SpecialKnightDie,
    SpecialKnightAllDie,
    SpecialCollectorMode,
    SpecialCollectorTake,
    SpecialCollectorPlay,
    SpecialBodyguardMode,
    SpecialMirrorMode,
    SpecialMirrorTarget,
    SpecialMirrorOwnDie,
    SpecialMirrorOpponentDie,
    SpecialMirrorFirstOpponent,
    SpecialMirrorFirstDie,
    SpecialMirrorSecondOpponent,
    SpecialMirrorSecondDie,
    SpecialExecutionerTarget,
    JokerTarget,
    JokerDie,
    NoblemanEffect
}

/// <summary>
/// Authoritative normal-match card action rules. Presentation and controllers only
/// request actions through this API; this class owns validation, effect lifetime,
/// card ownership and state mutation for card actions outside War.
/// </summary>
public sealed class CardadoCardActionManager : MonoBehaviour
{
    private enum EffectType { Modifier, BodyguardDie, BodyguardPlayer, BodyguardRound }

    private sealed class Effect
    {
        public EffectType Type;
        public CardInstance Card;
        public int OwnerIndex;
        public int TargetIndex = -1;
        public int DieIndex = -1;
        public int OriginalValue;
    }

    private CardadoGameManager gameManager;
    private CardInstance pendingCard;
    private CardadoCardActionPendingChoice pendingChoice;
    private int pendingActorIndex = -1;
    private int pendingTargetIndex = -1;
    private int pendingSecondTargetIndex = -1;
    private int pendingDieIndex = -1;
    private int modifierDirection;
    private int specialArtistDieIndex = -1;
    private readonly int[] specialArtistResults = new int[3];
    private readonly List<int> specialCollectorOpponents = new List<int>();
    private readonly List<CardInstance> specialCollectorPool = new List<CardInstance>();
    private int specialCollectorPosition;

    private readonly List<Effect> effects = new List<Effect>();
    private readonly HashSet<int> blockedThisHand = new HashSet<int>();
    private readonly HashSet<int> playedCardThisHand = new HashSet<int>();
    private int trackedHandNumber = -1;

    public CardadoCardActionPendingChoice PendingChoice => pendingChoice;
    public CardInstance PendingCardActionCard => pendingCard;
    public int PendingActorIndex => pendingActorIndex;
    public int PendingTargetIndex => pendingTargetIndex;
    public int PendingSecondTargetIndex => pendingSecondTargetIndex;
    public int PendingDieIndex => pendingDieIndex;
    public int PendingModifierDirection => modifierDirection;
    public int SpecialArtistDieIndex => specialArtistDieIndex;
    public int GetSpecialArtistResult(int index) => index >= 0 && index < specialArtistResults.Length ? specialArtistResults[index] : 0;
    public IReadOnlyList<CardInstance> SpecialCollectorPool => specialCollectorPool;
    public int SpecialCollectorPosition => specialCollectorPosition;

    private void Awake()
    {
        if (gameManager == null) gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null) gameManager = FindFirstObjectByType<CardadoGameManager>();
    }

    private void OnEnable()
    {
        if (gameManager == null) gameManager = GetComponent<CardadoGameManager>();
        if (gameManager == null) return;
        gameManager.CardActionRequested += OnCardActionRequested;
        gameManager.HandTurnStarted += OnHandTurnStarted;
        gameManager.DiePlayed += OnDiePlayed;
        gameManager.RoundResolutionCompleted += OnRoundResolved;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.CardActionRequested -= OnCardActionRequested;
        gameManager.HandTurnStarted -= OnHandTurnStarted;
        gameManager.DiePlayed -= OnDiePlayed;
        gameManager.RoundResolutionCompleted -= OnRoundResolved;
        ClearRuntimeState();
    }

    private void OnCardActionRequested(CardadoPlayerState player, CardadoCardActionRequestType request)
    {
        if (gameManager == null || gameManager.Phase == CardadoGamePhase.WarResolution) return;
        int playerIndex = IndexOf(player);
        if (playerIndex < 0) return;

        ClearPendingChoice();
        pendingActorIndex = playerIndex;
        if (blockedThisHand.Contains(playerIndex))
            gameManager.CompleteCardActionAfterRules(playerIndex);
    }

    private void OnHandTurnStarted(CardadoPlayerState player, int handNumber, int starterIndex)
    {
        if (trackedHandNumber != handNumber)
        {
            trackedHandNumber = handNumber;
            blockedThisHand.Clear();
            playedCardThisHand.Clear();
        }

        ClearPendingChoice();
        pendingActorIndex = IndexOf(player);
        specialCollectorPool.Clear();
        specialCollectorOpponents.Clear();
        specialCollectorPosition = 0;
    }

    private void OnDiePlayed(CardadoPlayerState player, int dieIndex, int dieValue)
    {
        int playerIndex = IndexOf(player);
        if (playerIndex < 0) return;

        List<Effect> remove = new List<Effect>();
        foreach (Effect effect in effects)
        {
            if ((effect.Type == EffectType.Modifier || effect.Type == EffectType.BodyguardDie) &&
                effect.TargetIndex == playerIndex && effect.DieIndex == dieIndex)
                remove.Add(effect);
        }

        foreach (Effect effect in remove)
        {
            gameManager.DiscardResolvedCard(effect.Card);
            effects.Remove(effect);
        }

        if (!AllDicePlayed(player)) return;
        remove.Clear();
        foreach (Effect effect in effects)
            if (effect.Type == EffectType.BodyguardPlayer && effect.TargetIndex == playerIndex)
                remove.Add(effect);
        foreach (Effect effect in remove)
        {
            gameManager.DiscardResolvedCard(effect.Card);
            effects.Remove(effect);
        }
    }

    private void OnRoundResolved()
    {
        foreach (Effect effect in new List<Effect>(effects))
            if (effect.Card != null) gameManager.DiscardResolvedCard(effect.Card);
        effects.Clear();
        blockedThisHand.Clear();
        playedCardThisHand.Clear();
        ClearPendingChoice();
    }

    public bool TryPlayCard(int playerIndex, int cardIndex)
    {
        if (!IsNormalCardActionPhase(playerIndex) || pendingChoice != CardadoCardActionPendingChoice.None) return false;
        CardadoPlayerState player = gameManager.Players[playerIndex];
        if (cardIndex < 0 || cardIndex >= player.hand.cardsInHand.Count) return false;

        CardInstance card = player.hand.cardsInHand[cardIndex];
        if (card == null || card.data == null) return false;

        player.hand.RemoveCard(card);
        card.isPlayed = true;
        playedCardThisHand.Add(playerIndex);
        pendingActorIndex = playerIndex;
        pendingCard = card;
        gameManager.NotifyCardPlayed(player, card);
        ResolveCard(playerIndex, card);
        return true;
    }

    public bool TrySkipCardAction(int playerIndex)
    {
        if (gameManager.Phase == CardadoGamePhase.WarResolution) return false;
        if (gameManager.Phase != CardadoGamePhase.CardActionDecision || gameManager.CurrentHandPlayerIndex != playerIndex)
            return false;
        if (pendingCard != null || pendingChoice != CardadoCardActionPendingChoice.None) return false;
        gameManager.CompleteCardActionAfterRules(playerIndex);
        return true;
    }

    public bool TryChooseModifierDirection(int direction)
    {
        if (!IsPending(CardadoCardActionPendingChoice.ModifierDirection) || (direction != 1 && direction != -1)) return false;
        if (direction > 0 && !pendingCard.data.canAdd) return false;
        if (direction < 0 && !pendingCard.data.canSubtract) return false;
        modifierDirection = direction;
        pendingChoice = CardadoCardActionPendingChoice.ModifierTarget;
        return true;
    }

    public bool TryChooseModifierTarget(int targetPlayerIndex, int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.ModifierTarget)) return false;
        if (!IsTargetPlayer(targetPlayerIndex) || !gameManager.IsDieTargetable(targetPlayerIndex, dieIndex) || IsProtected(targetPlayerIndex, dieIndex)) return false;
        int value = gameManager.Players[targetPlayerIndex].dice[dieIndex];
        if ((modifierDirection > 0 && value >= 6) || (modifierDirection < 0 && value <= 1)) return false;

        gameManager.Players[targetPlayerIndex].dice[dieIndex] = value + modifierDirection;
        effects.Add(new Effect { Type = EffectType.Modifier, Card = pendingCard, OwnerIndex = pendingActorIndex, TargetIndex = targetPlayerIndex, DieIndex = dieIndex, OriginalValue = value });
        FinishPersistentCard();
        return true;
    }

    public bool TryChooseArtistDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.ArtistDie) || !CanAffect(pendingActorIndex, dieIndex)) return false;
        Reroll(pendingActorIndex, dieIndex);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseKnightTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.KnightTarget) || !IsOpponent(targetPlayerIndex)) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.KnightDie;
        return true;
    }

    public bool TryChooseKnightDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.KnightDie) || !CanAffect(pendingTargetIndex, dieIndex)) return false;
        Reroll(pendingTargetIndex, dieIndex);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseCollectorTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.CollectorTarget) || !IsOpponent(targetPlayerIndex)) return false;
        if (gameManager.Players[targetPlayerIndex].hand.cardsInHand.Count == 0) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.CollectorCard;
        return true;
    }

    public bool TryChooseCollectorCard(int cardIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.CollectorCard)) return false;
        Hand source = gameManager.Players[pendingTargetIndex].hand;
        if (cardIndex < 0 || cardIndex >= source.cardsInHand.Count) return false;
        CardInstance stolen = source.cardsInHand[cardIndex];
        source.RemoveCard(stolen);
        if (stolen == null || stolen.data == null) return false;

        CardInstance collector = pendingCard;
        int actor = pendingActorIndex;
        ClearPendingChoice();
        gameManager.DiscardResolvedCard(collector);
        stolen.isPlayed = true;
        pendingActorIndex = actor;
        pendingCard = stolen;
        ResolveCard(actor, stolen);
        return true;
    }

    public bool TryChooseBodyguardDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.BodyguardDie) || !CanAffect(pendingActorIndex, dieIndex)) return false;
        effects.Add(new Effect { Type = EffectType.BodyguardDie, Card = pendingCard, OwnerIndex = pendingActorIndex, TargetIndex = pendingActorIndex, DieIndex = dieIndex });
        FinishPersistentCard();
        return true;
    }

    public bool TryChooseMirrorTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.MirrorTarget) || !IsOpponent(targetPlayerIndex)) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.MirrorOwnDie;
        return true;
    }

    public bool TryChooseMirrorOwnDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.MirrorOwnDie) || !CanAffect(pendingActorIndex, dieIndex)) return false;
        pendingDieIndex = dieIndex;
        pendingChoice = CardadoCardActionPendingChoice.MirrorOpponentDie;
        return true;
    }

    public bool TryChooseMirrorOpponentDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.MirrorOpponentDie) || !CanAffect(pendingTargetIndex, dieIndex)) return false;
        Exchange(pendingActorIndex, pendingDieIndex, pendingTargetIndex, dieIndex);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseExecutionerTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.ExecutionerTarget) || !IsOpponent(targetPlayerIndex)) return false;
        if (!playedCardThisHand.Contains(targetPlayerIndex))
        {
            blockedThisHand.Add(targetPlayerIndex);
            FinishResolvedCard();
            return true;
        }

        foreach (Effect effect in new List<Effect>(effects))
        {
            if (effect.OwnerIndex != targetPlayerIndex) continue;
            if (effect.Type != EffectType.Modifier && effect.Type != EffectType.BodyguardDie && effect.Type != EffectType.BodyguardPlayer && effect.Type != EffectType.BodyguardRound) continue;
            if (effect.Type == EffectType.Modifier && effect.TargetIndex >= 0 && effect.DieIndex >= 0)
                gameManager.Players[effect.TargetIndex].dice[effect.DieIndex] = effect.OriginalValue;
            gameManager.DiscardResolvedCard(effect.Card);
            effects.Remove(effect);
        }

        FinishResolvedCard();
        return true;
    }

    public bool TryChooseSpecialArtistMode(bool rerollAll)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialArtistMode)) return false;
        if (rerollAll)
        {
            RerollAll(pendingActorIndex);
            FinishResolvedCard();
            return true;
        }
        pendingChoice = CardadoCardActionPendingChoice.SpecialArtistDie;
        return true;
    }

    public bool TryChooseSpecialArtistDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialArtistDie) || !CanAffect(pendingActorIndex, dieIndex)) return false;
        specialArtistDieIndex = dieIndex;
        for (int i = 0; i < specialArtistResults.Length; i++) specialArtistResults[i] = UnityEngine.Random.Range(1, 7);
        pendingChoice = CardadoCardActionPendingChoice.SpecialArtistResult;
        return true;
    }

    public bool TryChooseSpecialArtistResult(int resultIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialArtistResult) || resultIndex < 0 || resultIndex >= 3) return false;
        gameManager.Players[pendingActorIndex].dice[specialArtistDieIndex] = specialArtistResults[resultIndex];
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseSpecialKnightMode(bool rerollAllOpponentDice)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialKnightMode)) return false;
        if (rerollAllOpponentDice)
        {
            RerollAllOpponents();
            FinishResolvedCard();
            return true;
        }
        pendingChoice = CardadoCardActionPendingChoice.SpecialKnightAllDie;
        return true;
    }

    public bool TryChooseSpecialKnightDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialKnightDie) || !CanAffect(pendingTargetIndex, dieIndex)) return false;
        Reroll(pendingTargetIndex, dieIndex);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseSpecialKnightTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialKnightTarget) || !IsOpponent(targetPlayerIndex)) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.SpecialKnightDie;
        return true;
    }

    public bool TryChooseSpecialKnightAllDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialKnightAllDie)) return false;
        bool affected = false;
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            if (p == pendingActorIndex || !CanAffect(p, dieIndex)) continue;
            Reroll(p, dieIndex);
            affected = true;
        }
        if (!affected) return false;
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseSpecialCollectorMode(bool takeFromEach)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialCollectorMode)) return false;
        if (!takeFromEach)
        {
            for (int i = 0; i < 3; i++)
            {
                CardInstance drawn = gameManager.RoundDeck.Draw();
                if (drawn == null) break;
                drawn.isPlayed = false;
                gameManager.Players[pendingActorIndex].hand.AddCard(drawn);
            }
            FinishResolvedCard();
            return true;
        }

        specialCollectorOpponents.Clear();
        for (int p = 0; p < gameManager.Players.Count; p++)
            if (p != pendingActorIndex && gameManager.Players[p].hand.cardsInHand.Count > 0)
                specialCollectorOpponents.Add(p);
        specialCollectorPool.Clear();
        specialCollectorPosition = 0;
        if (specialCollectorOpponents.Count == 0)
        {
            FinishResolvedCard();
            return true;
        }
        pendingChoice = CardadoCardActionPendingChoice.SpecialCollectorTake;
        return true;
    }

    public bool TryChooseSpecialCollectorCard(int cardIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialCollectorTake)) return false;
        if (specialCollectorPosition < 0 || specialCollectorPosition >= specialCollectorOpponents.Count) return false;
        int target = specialCollectorOpponents[specialCollectorPosition];
        Hand source = gameManager.Players[target].hand;
        if (cardIndex < 0 || cardIndex >= source.cardsInHand.Count) return false;
        CardInstance stolen = source.cardsInHand[cardIndex];
        source.RemoveCard(stolen);
        if (stolen == null || stolen.data == null) return false;
        stolen.isPlayed = true;
        specialCollectorPool.Add(stolen);
        specialCollectorPosition++;
        if (specialCollectorPosition >= specialCollectorOpponents.Count)
            pendingChoice = CardadoCardActionPendingChoice.SpecialCollectorPlay;
        return true;
    }

    public bool TryChooseSpecialCollectorPlay(int poolIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialCollectorPlay) || poolIndex < 0 || poolIndex >= specialCollectorPool.Count) return false;
        CardInstance chosen = specialCollectorPool[poolIndex];
        for (int i = 0; i < specialCollectorPool.Count; i++)
            if (i != poolIndex) gameManager.DiscardResolvedCard(specialCollectorPool[i]);
        specialCollectorPool.Clear();

        CardInstance originalCollector = pendingCard;
        int actor = pendingActorIndex;
        ClearPendingChoice();
        gameManager.DiscardResolvedCard(originalCollector);
        pendingActorIndex = actor;
        pendingCard = chosen;
        ResolveCard(actor, chosen);
        return true;
    }

    public bool TryChooseSpecialBodyguardMode(bool protectOwnDice)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialBodyguardMode)) return false;
        effects.Add(new Effect
        {
            Type = protectOwnDice ? EffectType.BodyguardPlayer : EffectType.BodyguardRound,
            Card = pendingCard,
            OwnerIndex = pendingActorIndex,
            TargetIndex = protectOwnDice ? pendingActorIndex : -1
        });
        FinishPersistentCard();
        return true;
    }

    public bool TryChooseSpecialMirrorMode(bool ownAgainstOpponent)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorMode)) return false;
        if (ownAgainstOpponent)
        {
            pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorTarget;
            return true;
        }
        if (gameManager.Players.Count < 3) return false;
        pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorFirstOpponent;
        return true;
    }

    public bool TryChooseSpecialMirrorTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorTarget) || !IsOpponent(targetPlayerIndex)) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorOwnDie;
        return true;
    }

    public bool TryChooseSpecialMirrorOwnDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorOwnDie) || !CanAffect(pendingActorIndex, dieIndex)) return false;
        pendingDieIndex = dieIndex;
        pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorOpponentDie;
        return true;
    }

    public bool TryChooseSpecialMirrorOpponentDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorOpponentDie) || !CanAffect(pendingTargetIndex, dieIndex)) return false;
        Exchange(pendingActorIndex, pendingDieIndex, pendingTargetIndex, dieIndex);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseSpecialMirrorFirstOpponent(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorFirstOpponent) || !IsOpponent(targetPlayerIndex)) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorFirstDie;
        return true;
    }

    public bool TryChooseSpecialMirrorFirstDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorFirstDie) || !CanAffect(pendingTargetIndex, dieIndex)) return false;
        pendingDieIndex = dieIndex;
        pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorSecondOpponent;
        return true;
    }

    public bool TryChooseSpecialMirrorSecondOpponent(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorSecondOpponent) || !IsOpponent(targetPlayerIndex) || targetPlayerIndex == pendingTargetIndex) return false;
        pendingSecondTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorSecondDie;
        return true;
    }

    public bool TryChooseSpecialMirrorSecondDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialMirrorSecondDie) || !CanAffect(pendingSecondTargetIndex, dieIndex)) return false;
        Exchange(pendingTargetIndex, pendingDieIndex, pendingSecondTargetIndex, dieIndex);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseSpecialExecutionerTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.SpecialExecutionerTarget) || !IsOpponent(targetPlayerIndex)) return false;
        CardadoPlayerState target = gameManager.Players[targetPlayerIndex];
        List<CardInstance> cards = new List<CardInstance>(target.hand.cardsInHand);
        target.hand.cardsInHand.Clear();
        foreach (CardInstance card in cards) gameManager.DiscardResolvedCard(card);
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseJokerTarget(int targetPlayerIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.JokerTarget) || !IsTargetPlayer(targetPlayerIndex)) return false;
        if (targetPlayerIndex != pendingActorIndex && !IsOpponent(targetPlayerIndex)) return false;
        pendingTargetIndex = targetPlayerIndex;
        pendingChoice = CardadoCardActionPendingChoice.JokerDie;
        return true;
    }

    public bool TryChooseJokerDie(int dieIndex)
    {
        if (!IsPending(CardadoCardActionPendingChoice.JokerDie) || !CanAffect(pendingTargetIndex, dieIndex)) return false;
        gameManager.Players[pendingTargetIndex].dice[dieIndex] = 7 - gameManager.Players[pendingTargetIndex].dice[dieIndex];
        FinishResolvedCard();
        return true;
    }

    public bool TryChooseNoblemanEffect(CardType specialType)
    {
        if (!IsPending(CardadoCardActionPendingChoice.NoblemanEffect)) return false;
        switch (specialType)
        {
            case CardType.Artist: pendingChoice = CardadoCardActionPendingChoice.SpecialArtistMode; return true;
            case CardType.Knight: pendingChoice = CardadoCardActionPendingChoice.SpecialKnightMode; return true;
            case CardType.Collector: pendingChoice = CardadoCardActionPendingChoice.SpecialCollectorMode; return true;
            case CardType.Bodyguard: pendingChoice = CardadoCardActionPendingChoice.SpecialBodyguardMode; return true;
            default: return false;
        }
    }

    private void ResolveCard(int actorIndex, CardInstance card)
    {
        pendingActorIndex = actorIndex;
        pendingCard = card;
        if (card.data.isBlankCard)
        {
            FinishResolvedCard();
            return;
        }

        if (card.data.isModifier)
        {
            pendingChoice = CardadoCardActionPendingChoice.ModifierDirection;
            return;
        }

        if (card.data.rarity == CardRarity.Special)
        {
            switch (card.data.cardType)
            {
                case CardType.Artist: pendingChoice = CardadoCardActionPendingChoice.SpecialArtistMode; return;
                case CardType.Knight: pendingChoice = CardadoCardActionPendingChoice.SpecialKnightMode; return;
                case CardType.Collector: pendingChoice = CardadoCardActionPendingChoice.SpecialCollectorMode; return;
                case CardType.Bodyguard: pendingChoice = CardadoCardActionPendingChoice.SpecialBodyguardMode; return;
                case CardType.Mirror: pendingChoice = CardadoCardActionPendingChoice.SpecialMirrorMode; return;
                case CardType.Executioner: pendingChoice = CardadoCardActionPendingChoice.SpecialExecutionerTarget; return;
            }
        }

        if (card.data.rarity == CardRarity.Royalty)
        {
            switch (card.data.cardType)
            {
                case CardType.Joker: pendingChoice = CardadoCardActionPendingChoice.JokerTarget; return;
                case CardType.King: ResolveKing(); return;
                case CardType.Queen: ResolveQueen(); return;
                case CardType.GordonRobleys: pendingChoice = CardadoCardActionPendingChoice.NoblemanEffect; return;
            }
        }

        switch (card.data.cardType)
        {
            case CardType.Artist: pendingChoice = CardadoCardActionPendingChoice.ArtistDie; break;
            case CardType.Knight: pendingChoice = CardadoCardActionPendingChoice.KnightTarget; break;
            case CardType.Collector: pendingChoice = CardadoCardActionPendingChoice.CollectorTarget; break;
            case CardType.Bodyguard: pendingChoice = CardadoCardActionPendingChoice.BodyguardDie; break;
            case CardType.Mirror: pendingChoice = CardadoCardActionPendingChoice.MirrorTarget; break;
            case CardType.Executioner: pendingChoice = CardadoCardActionPendingChoice.ExecutionerTarget; break;
            default: FinishResolvedCard(); break;
        }
    }

    private void ResolveKing()
    {
        for (int p = 0; p < gameManager.Players.Count; p++) RerollAll(p);
        FinishResolvedCard(true);
        gameManager.RequestAdditionalCardAction(pendingActorIndex);
    }

    private void ResolveQueen()
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
        {
            List<CardInstance> cards = new List<CardInstance>(gameManager.Players[p].hand.cardsInHand);
            gameManager.Players[p].hand.cardsInHand.Clear();
            foreach (CardInstance card in cards) gameManager.DiscardResolvedCard(card);
        }

        for (int p = 0; p < gameManager.Players.Count; p++)
            for (int i = 0; i < 3; i++)
            {
                CardInstance card = gameManager.RoundDeck.Draw();
                if (card == null) break;
                card.isPlayed = false;
                gameManager.Players[p].hand.AddCard(card);
            }

        FinishResolvedCard(true);
        gameManager.RequestAdditionalCardAction(pendingActorIndex);
    }

    private void Reroll(int playerIndex, int dieIndex)
    {
        if (!CanAffect(playerIndex, dieIndex)) return;
        gameManager.Players[playerIndex].dice[dieIndex] = UnityEngine.Random.Range(1, 7);
    }

    private void RerollAll(int playerIndex)
    {
        for (int d = 0; d < gameManager.Players[playerIndex].dice.Count; d++)
            if (CanAffect(playerIndex, d)) gameManager.Players[playerIndex].dice[d] = UnityEngine.Random.Range(1, 7);
    }

    private void RerollAllOpponents()
    {
        for (int p = 0; p < gameManager.Players.Count; p++)
            if (p != pendingActorIndex) RerollAll(p);
    }

    private void Exchange(int playerA, int dieA, int playerB, int dieB)
    {
        int value = gameManager.Players[playerA].dice[dieA];
        gameManager.Players[playerA].dice[dieA] = gameManager.Players[playerB].dice[dieB];
        gameManager.Players[playerB].dice[dieB] = value;
    }

    private bool CanAffect(int playerIndex, int dieIndex)
    {
        return playerIndex >= 0 && playerIndex < gameManager.Players.Count &&
               gameManager.IsDieTargetable(playerIndex, dieIndex) && !IsProtected(playerIndex, dieIndex);
    }

    private bool IsProtected(int playerIndex, int dieIndex)
    {
        foreach (Effect effect in effects)
        {
            if (effect.Type == EffectType.BodyguardRound) return true;
            if (effect.Type == EffectType.BodyguardDie && effect.TargetIndex == playerIndex && effect.DieIndex == dieIndex) return true;
            if (effect.Type == EffectType.BodyguardPlayer && effect.TargetIndex == playerIndex) return true;
        }
        return false;
    }

    private bool IsNormalCardActionPhase(int playerIndex)
    {
        return gameManager != null && gameManager.Phase == CardadoGamePhase.CardActionDecision &&
               gameManager.CurrentHandPlayerIndex == playerIndex && !blockedThisHand.Contains(playerIndex);
    }

    private bool IsPending(CardadoCardActionPendingChoice choice)
    {
        return pendingChoice == choice && pendingCard != null && pendingActorIndex >= 0;
    }

    private bool IsOpponent(int playerIndex) => playerIndex >= 0 && playerIndex < gameManager.Players.Count && playerIndex != pendingActorIndex;
    private bool IsTargetPlayer(int playerIndex) => playerIndex >= 0 && playerIndex < gameManager.Players.Count;

    private void FinishPersistentCard()
    {
        int actor = pendingActorIndex;
        ClearPendingChoice();
        gameManager.CompleteCardActionAfterRules(actor);
    }

    private void FinishResolvedCard(bool suppressAdvance = false)
    {
        int actor = pendingActorIndex;
        CardInstance card = pendingCard;
        ClearPendingChoice();
        if (card != null) gameManager.DiscardResolvedCard(card);
        if (!suppressAdvance && actor >= 0) gameManager.CompleteCardActionAfterRules(actor);
    }

    private void ClearPendingChoice()
    {
        pendingCard = null;
        pendingChoice = CardadoCardActionPendingChoice.None;
        pendingTargetIndex = -1;
        pendingSecondTargetIndex = -1;
        pendingDieIndex = -1;
        modifierDirection = 0;
        specialArtistDieIndex = -1;
        for (int i = 0; i < specialArtistResults.Length; i++) specialArtistResults[i] = 0;
    }

    private void ClearRuntimeState()
    {
        ClearPendingChoice();
        specialCollectorOpponents.Clear();
        specialCollectorPool.Clear();
        effects.Clear();
        blockedThisHand.Clear();
        playedCardThisHand.Clear();
    }

    private bool AllDicePlayed(CardadoPlayerState player)
    {
        for (int i = 0; i < player.dice.Count; i++)
            if (i >= player.playedDice.Count || !player.playedDice[i]) return false;
        return true;
    }

    private int IndexOf(CardadoPlayerState player)
    {
        for (int i = 0; i < gameManager.Players.Count; i++)
            if (ReferenceEquals(gameManager.Players[i], player)) return i;
        return -1;
    }
}
