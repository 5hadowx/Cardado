using System;
using UnityEngine;

/// <summary>
/// CPU player controller. It handles timing and participation while delegating
/// decision logic to CpuStrategy and submitting actions through the base controller.
/// </summary>
public sealed class CpuPlayerController : CardadoPlayerController
{
    private enum CpuWarStage { None, Target, Wager, Order, Resolve }

    [SerializeField, Min(0f)] private float thinkingDelay = 0.5f;
    [SerializeField] private CpuProfile profile = CpuProfile.Balanced;
    [SerializeField] private bool randomizeProfile = true;
    [SerializeField] private bool debugLogging;

    private CpuStrategy strategy;
    private CardadoWarManager warManager;
    private float decisionReadyAt = -1f;
    private bool decisionPending;
    private CpuWarStage warStage;
    private int warChallengerIndex = -1;

    public CpuProfile Profile => strategy != null ? strategy.Profile : profile;
    public float ThinkingDelay => thinkingDelay;

    private void Start()
    {
        if (IsBound) return;
        CardadoGameManager manager = FindFirstObjectByType<CardadoGameManager>();
        if (manager == null)
        {
            Debug.LogWarning("[Cardado][CPU] No CardadoGameManager found; CPU controller could not bind.", this);
            return;
        }
        if (PlayerIndex < 0 || PlayerIndex >= manager.Players.Count)
        {
            Debug.LogWarning($"[Cardado][CPU] Configured player index {PlayerIndex} is outside the current player range.", this);
            return;
        }
        Bind(manager, PlayerIndex);
    }

    protected override void OnBound()
    {
        strategy = CreateStrategy(ResolveProfile());
        warManager = FindFirstObjectByType<CardadoWarManager>();
        decisionPending = false;
        decisionReadyAt = -1f;
        warStage = CpuWarStage.None;
        warChallengerIndex = -1;
        if (debugLogging) Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} bound with profile={Profile}.", this);
    }

    protected override void OnUnbound()
    {
        strategy = null;
        warManager = null;
        decisionPending = false;
        decisionReadyAt = -1f;
        warStage = CpuWarStage.None;
        warChallengerIndex = -1;
    }

    protected override void Update()
    {
        if (!IsBound || GameManager == null || strategy == null) return;
        if (GameManager.Phase == CardadoGamePhase.GameOver) return;
        if (warManager == null) warManager = FindFirstObjectByType<CardadoWarManager>();

        if (!IsDecisionOpportunity())
        {
            decisionPending = false;
            decisionReadyAt = -1f;
            if (GameManager.Phase != CardadoGamePhase.WarResolution)
            {
                warStage = CpuWarStage.None;
                warChallengerIndex = -1;
            }
            return;
        }

        if (!decisionPending)
        {
            decisionPending = true;
            decisionReadyAt = Time.time + thinkingDelay;
            return;
        }
        if (Time.time < decisionReadyAt) return;
        decisionPending = false;
        decisionReadyAt = -1f;
        MakeDecision();
    }

    private bool IsDecisionOpportunity()
    {
        if (GameManager.Phase == CardadoGamePhase.DealerSetupDecision)
            return GameManager.DealerPlayerIndex == PlayerIndex && GameManager.PendingDealerDecision.HasValue;

        if (GameManager.Phase == CardadoGamePhase.Prediction)
            return GameManager.CurrentPredictionPlayerIndex == PlayerIndex;

        if (GameManager.Phase == CardadoGamePhase.CardActionDecision || GameManager.Phase == CardadoGamePhase.PlayingHands)
            return GameManager.CurrentHandPlayerIndex == PlayerIndex;

        if (GameManager.Phase == CardadoGamePhase.WarResolution)
        {
            if (warManager == null) return false;
            if (warStage != CpuWarStage.None) return true;
            if (warManager.Context != null)
                return warManager.Context.CurrentPlayer != null && warManager.Context.CurrentPlayer.PlayerIndex == PlayerIndex;
            return warManager.CurrentWarClaimantIndex == PlayerIndex || warManager.CurrentWarClaimantIndex < 0;
        }

        return false;
    }

    private void MakeDecision()
    {
        CpuDecisionContext context = new CpuDecisionContext(GameManager, PlayerIndex);

        if (GameManager.Phase == CardadoGamePhase.DealerSetupDecision)
        {
            if (!GameManager.PendingDealerDecision.HasValue) return;
            RoundSetupDecisionType decisionType = GameManager.PendingDealerDecision.Value;
            int choice = decisionType == RoundSetupDecisionType.ChooseDiceCount
                ? strategy.ChooseDealerDiceCount(context)
                : strategy.ChooseDealerCardCount(context);
            choice = Math.Max(1, Math.Min(5, choice));
            GameManager.ResolveDealerChoice(choice);
            if (debugLogging)
                Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} dealer setup: chose {choice} for {decisionType}.", this);
            return;
        }

        if (GameManager.Phase == CardadoGamePhase.Prediction)
        {
            int prediction = strategy.ChoosePrediction(context);
            if (debugLogging) Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} profile={Profile} prediction={prediction}.");
            RequestPrediction(prediction);
            return;
        }

        if (GameManager.Phase == CardadoGamePhase.CardActionDecision)
        {
            CpuCardDecision cardDecision = strategy.ChooseCardAction(context);
            if (cardDecision.Type == CpuCardDecisionType.Play && RequestPlayCard(cardDecision.CardIndex))
            {
                if (debugLogging) Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} played card index {cardDecision.CardIndex}.");
                return;
            }
            if (debugLogging) Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} skipped card action.");
            RequestSkipCardAction();
            return;
        }

        if (GameManager.Phase == CardadoGamePhase.PlayingHands)
        {
            int dieIndex = strategy.ChooseDie(context);
            if (debugLogging) Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} selected die index {dieIndex}.");
            if (dieIndex >= 0) RequestPlayDie(dieIndex);
            return;
        }

        if (GameManager.Phase == CardadoGamePhase.WarResolution) MakeWarDecision(context);
    }

    private void MakeWarDecision(CpuDecisionContext context)
    {
        if (warManager == null) return;

        if (warStage == CpuWarStage.Target)
        {
            int target = strategy.ChooseWarTarget(context, warChallengerIndex);
            if (target >= 0 && warManager.TryChooseTarget(target))
            {
                warStage = CpuWarStage.Wager;
                LogWar($"targeted Player {target + 1}");
            }
            return;
        }

        if (warStage == CpuWarStage.Wager)
        {
            if (warManager.TryChooseWarWager(1))
            {
                warStage = CpuWarStage.Order;
                LogWar("wagered 1 chip");
            }
            return;
        }

        if (warStage == CpuWarStage.Order)
        {
            if (warManager.TryChooseWarOrder(strategy.ChooseWarOrder(context)))
            {
                warStage = CpuWarStage.Resolve;
                LogWar("started War");
            }
            return;
        }

        if (warStage == CpuWarStage.Resolve && warManager.Context == null)
        {
            if (warManager.CurrentWarClaimantIndex < 0)
            {
                if (warManager.TryFinishWarPhase())
                {
                    warStage = CpuWarStage.None;
                    warChallengerIndex = -1;
                    LogWar("finished War phase");
                }
                return;
            }
            if (warManager.TryContinueWarPhase())
            {
                warStage = CpuWarStage.None;
                warChallengerIndex = -1;
                LogWar("continued to next War decision");
            }
            return;
        }

        if (warManager.Context == null)
        {
            int claimant = warManager.CurrentWarClaimantIndex;
            if (claimant >= 0)
            {
                if (claimant != PlayerIndex) return;

                if (!warManager.CanClaimWar(PlayerIndex) || !strategy.ShouldDeclareWar(context))
                {
                    if (warManager.TryPassWar(PlayerIndex)) LogWar("passed War claim");
                    return;
                }

                if (warManager.TryClaimWar(PlayerIndex))
                {
                    warChallengerIndex = PlayerIndex;
                    warStage = CpuWarStage.Target;
                    LogWar("declared War");
                }
                return;
            }

            if (warManager.CanClaimWar(PlayerIndex) && strategy.ShouldDeclareWar(context))
            {
                if (warManager.TryClaimWar(PlayerIndex))
                {
                    warChallengerIndex = PlayerIndex;
                    warStage = CpuWarStage.Target;
                    LogWar("declared War");
                    return;
                }
            }
            else if (warManager.TryPassWar(PlayerIndex))
            {
                LogWar("passed War claim");
                return;
            }

            if (warManager.TryFinishWarPhase()) LogWar("finished War phase");
            return;
        }

        if (warManager.Context.CurrentPlayer == null || warManager.Context.CurrentPlayer.PlayerIndex != PlayerIndex) return;

        if (warManager.PendingChoice != CardadoWarPendingChoice.None)
        {
            if (ResolveWarPendingChoice()) return;
            if (debugLogging)
                Debug.LogWarning($"[Cardado][CPU] Player {PlayerIndex + 1} could not resolve War pending choice {warManager.PendingChoice}.", this);
            return;
        }

        if (warManager.IsWarCardActionPending)
        {
            int cardIndex = strategy.ChooseWarCard(context, warManager.Context.CurrentPlayer.Cards);
            if (cardIndex >= 0 && warManager.TryPlayWarCard(PlayerIndex, cardIndex))
            {
                LogWar($"played War card {cardIndex}");
                return;
            }

            if (warManager.TrySkipCardAction(PlayerIndex)) LogWar("skipped War card action");
            return;
        }

        int dieIndex = strategy.ChooseWarDie(warManager.Context.CurrentPlayer.Dice, warManager.Context.CurrentPlayer.PlayedDice);
        if (dieIndex >= 0 && warManager.TryPlayWarDieForPlayer(PlayerIndex, dieIndex)) LogWar($"played War die {dieIndex}");
    }

    private bool ResolveWarPendingChoice()
    {
        CardadoWarPendingChoice choice = warManager.PendingChoice;
        CardadoWarContext.Participant actor = warManager.PendingChoiceActor;
        if (actor == null) return false;

        CardadoWarContext.Participant opponent = warManager.Context.OpponentOf(actor);

        switch (choice)
        {
            case CardadoWarPendingChoice.ModifierTarget:
            {
                int die = FindBestWarDie(actor, true);
                return die >= 0 && warManager.TryChooseModifierDie(actor.PlayerIndex, die);
            }
            case CardadoWarPendingChoice.ModifierSign:
                return warManager.TryChooseModifierValue(1);
            case CardadoWarPendingChoice.ArtistDie:
            {
                int die = FindBestWarDie(actor, true);
                return die >= 0 && warManager.TryChooseArtistDie(die);
            }
            case CardadoWarPendingChoice.KnightDie:
            {
                int die = FindBestWarDie(opponent, true);
                return die >= 0 && warManager.TryChooseKnightDie(die);
            }
            case CardadoWarPendingChoice.CollectorOpponentCard:
                return opponent.Cards.Count > 0 && warManager.TryChooseCollectorCard(0);
            case CardadoWarPendingChoice.BodyguardDie:
            {
                int die = FindBestWarDie(actor, true);
                return die >= 0 && warManager.TryChooseWarBodyguardDie(die);
            }
            case CardadoWarPendingChoice.MirrorOwnDie:
            {
                int die = FindBestWarDie(actor, true);
                return die >= 0 && warManager.TryChooseMirrorOwnDie(die);
            }
            case CardadoWarPendingChoice.MirrorOpponentDie:
            {
                int die = FindBestWarDie(opponent, true);
                return die >= 0 && warManager.TryChooseMirrorOpponentDie(die);
            }
            case CardadoWarPendingChoice.JokerPlayer:
                return warManager.TryChooseJokerTarget(true);
            case CardadoWarPendingChoice.JokerDie:
            {
                CardadoWarContext.Participant target = warManager.GetJokerTargetForDevelopment();
                int die = FindBestWarDie(target, true);
                return target != null && die >= 0 && warManager.TryChooseJokerDie(die);
            }
            case CardadoWarPendingChoice.SpecialArtistMode:
                return warManager.TryChooseSpecialArtistMode(true);
            case CardadoWarPendingChoice.SpecialArtistDie:
            {
                int die = FindBestWarDie(actor, true);
                return die >= 0 && warManager.TryChooseSpecialArtistDie(die);
            }
            case CardadoWarPendingChoice.SpecialArtistResult:
                return warManager.TryChooseSpecialArtistResult(ChooseBestArtistResult());
            case CardadoWarPendingChoice.SpecialKnightMode:
                return warManager.TryChooseSpecialKnightMode(true);
            case CardadoWarPendingChoice.SpecialKnightDie:
            {
                int die = FindBestWarDie(opponent, true);
                return die >= 0 && warManager.TryChooseSpecialKnightDie(die);
            }
            case CardadoWarPendingChoice.SpecialCollectorMode:
                return warManager.TryChooseSpecialCollectorMode(false);
            case CardadoWarPendingChoice.SpecialCollectorOwnCard:
                return actor.Cards.Count > 0 && warManager.TryChooseSpecialCollectorOwnCard(0);
            case CardadoWarPendingChoice.SpecialCollectorOpponentCard:
                return opponent.Cards.Count > 0 && warManager.TryChooseSpecialCollectorOpponentCard(0);
            case CardadoWarPendingChoice.SpecialCollectorPlayChoice:
                return warManager.TryChooseSpecialCollectorPlayedCard(warManager.PendingCollectorOwnSelected);
            case CardadoWarPendingChoice.SpecialBodyguardMode:
                return warManager.TryChooseSpecialBodyguardMode(true);
            case CardadoWarPendingChoice.SpecialMirrorMode:
                return warManager.TryChooseSpecialMirrorMode(true);
            case CardadoWarPendingChoice.SpecialMirrorOwnDie:
            {
                int die = FindBestWarDie(actor, true);
                return die >= 0 && warManager.TryChooseSpecialMirrorOwnDie(die);
            }
            case CardadoWarPendingChoice.SpecialMirrorOpponentDie:
            {
                int die = FindBestWarDie(opponent, true);
                return die >= 0 && warManager.TryChooseSpecialMirrorOpponentDie(die);
            }
            case CardadoWarPendingChoice.NoblemanEffect:
                return warManager.TryChooseNoblemanEffect(CardType.Artist);
            default:
                return false;
        }
    }

    private int FindBestWarDie(CardadoWarContext.Participant participant, bool highest)
    {
        if (participant == null) return -1;
        int selected = -1;
        int selectedValue = highest ? int.MinValue : int.MaxValue;
        for (int i = 0; i < participant.Dice.Count; i++)
        {
            if (!warManager.IsWarDieTargetable(participant.PlayerIndex, i)) continue;
            int value = participant.Dice[i];
            if ((highest && value > selectedValue) || (!highest && value < selectedValue))
            {
                selected = i;
                selectedValue = value;
            }
        }
        return selected;
    }

    private int ChooseBestArtistResult()
    {
        int bestIndex = 0;
        int bestValue = warManager.GetPendingArtistResult(0);
        for (int i = 1; i < 3; i++)
        {
            int value = warManager.GetPendingArtistResult(i);
            if (value > bestValue)
            {
                bestValue = value;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private void LogWar(string action)
    {
        if (debugLogging) Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} War: {action}.", this);
    }

    private CpuProfile ResolveProfile()
    {
        if (!randomizeProfile) return profile;
        Array profiles = Enum.GetValues(typeof(CpuProfile));
        return (CpuProfile)profiles.GetValue(UnityEngine.Random.Range(0, profiles.Length));
    }

    private static CpuStrategy CreateStrategy(CpuProfile selectedProfile)
    {
        switch (selectedProfile)
        {
            case CpuProfile.Aggressive: return new AggressiveCpuStrategy();
            case CpuProfile.Conservative: return new ConservativeCpuStrategy();
            case CpuProfile.Opportunistic: return new OpportunisticCpuStrategy();
            default: return new BalancedCpuStrategy();
        }
    }
}