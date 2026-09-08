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
            int choice = GameManager.PendingDealerDecision.Value == RoundSetupDecisionType.ChooseDiceCount
                ? strategy.ChooseDealerDiceCount(context)
                : strategy.ChooseDealerCardCount(context);
            choice = Math.Max(1, Math.Min(5, choice));
            GameManager.ResolveDealerChoice(choice);
            if (debugLogging)
                Debug.Log($"[Cardado][CPU] Player {PlayerIndex + 1} dealer setup: chose {choice} for {GameManager.PendingDealerDecision.GetValueOrDefault()}.", this);
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
            if (debugLogging)
                Debug.LogWarning($"[Cardado][CPU] Player {PlayerIndex + 1} reached unsupported War pending choice {warManager.PendingChoice}; War card actions are skipped by the initial CPU War policy.", this);
            return;
        }

        if (warManager.IsWarCardActionPending)
        {
            if (warManager.TrySkipCardAction(PlayerIndex)) LogWar("skipped War card action");
            return;
        }

        int dieIndex = strategy.ChooseWarDie(warManager.Context.CurrentPlayer.Dice, warManager.Context.CurrentPlayer.PlayedDice);
        if (dieIndex >= 0 && warManager.TryPlayWarDieForPlayer(PlayerIndex, dieIndex)) LogWar($"played War die {dieIndex}");
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