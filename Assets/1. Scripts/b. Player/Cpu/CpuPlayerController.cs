using System;
using UnityEngine;

/// <summary>
/// CPU player controller. It handles timing and participation while delegating
/// decision logic to CpuStrategy and submitting actions through the base controller.
/// </summary>
public sealed class CpuPlayerController : CardadoPlayerController
{
    [SerializeField, Min(0f)] private float thinkingDelay = 0.5f;
    [SerializeField] private CpuProfile profile = CpuProfile.Balanced;
    [SerializeField] private bool randomizeProfile = true;
    [SerializeField] private bool debugLogging;

    private CpuStrategy strategy;
    private float decisionReadyAt = -1f;
    private bool decisionPending;

    public CpuProfile Profile => strategy != null ? strategy.Profile : profile;
    public float ThinkingDelay => thinkingDelay;

    protected override void OnBound()
    {
        strategy = CreateStrategy(ResolveProfile());
        decisionPending = false;
        decisionReadyAt = -1f;
    }

    protected override void OnUnbound()
    {
        strategy = null;
        decisionPending = false;
        decisionReadyAt = -1f;
    }

    protected override void Update()
    {
        if (!IsBound || GameManager == null || strategy == null) return;
        if (GameManager.Phase == CardadoGamePhase.GameOver) return;
        if (!IsDecisionOpportunity())
        {
            decisionPending = false;
            decisionReadyAt = -1f;
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
        if (GameManager.Phase == CardadoGamePhase.Prediction)
            return GameManager.CurrentPredictionPlayerIndex == PlayerIndex;

        if (GameManager.Phase == CardadoGamePhase.CardActionDecision || GameManager.Phase == CardadoGamePhase.PlayingHands)
            return GameManager.CurrentHandPlayerIndex == PlayerIndex;

        return false;
    }

    private void MakeDecision()
    {
        CpuDecisionContext context = new CpuDecisionContext(GameManager, PlayerIndex);

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
        }
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
