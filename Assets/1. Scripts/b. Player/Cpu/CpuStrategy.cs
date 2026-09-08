using System;

/// <summary>
/// Produces CPU decisions from a player-perspective context.
/// Strategy owns decision logic; it never mutates gameplay state.
/// </summary>
public abstract class CpuStrategy
{
    public abstract CpuProfile Profile { get; }

    public abstract int ChoosePrediction(CpuDecisionContext context);

    public virtual CpuCardDecision ChooseCardAction(CpuDecisionContext context)
    {
        return CpuCardDecision.Skip;
    }

    public virtual int ChooseDie(CpuDecisionContext context)
    {
        return context.FindBestAvailableDieIndex();
    }
}

public enum CpuProfile
{
    Balanced,
    Aggressive,
    Conservative,
    Opportunistic
}

public enum CpuCardDecisionType
{
    Skip,
    Play
}

public readonly struct CpuCardDecision
{
    public static CpuCardDecision Skip => new CpuCardDecision(CpuCardDecisionType.Skip, -1);

    public CpuCardDecisionType Type { get; }
    public int CardIndex { get; }

    public CpuCardDecision(CpuCardDecisionType type, int cardIndex)
    {
        Type = type;
        CardIndex = cardIndex;
    }
}

/// <summary>
/// Restricted decision input. It intentionally exposes only the CPU player's
/// own state plus public match information.
/// </summary>
public sealed class CpuDecisionContext
{
    private readonly CardadoGameManager gameManager;
    private readonly int playerIndex;

    public CardadoGameManager GameManager => gameManager;
    public int PlayerIndex => playerIndex;
    public CardadoPlayerState Player => gameManager.Players[playerIndex];
    public CardadoGamePhase Phase => gameManager.Phase;
    public int PlayerCount => gameManager.Players.Count;
    public int CurrentHandNumber => gameManager.CurrentHandNumber;
    public int CurrentHandPlayerIndex => gameManager.CurrentHandPlayerIndex;
    public int RoundDiceCount => gameManager.RoundDiceCount;
    public int RoundCardCount => gameManager.RoundCardCount;

    public CpuDecisionContext(CardadoGameManager manager, int index)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (index < 0 || index >= manager.Players.Count) throw new ArgumentOutOfRangeException(nameof(index));
        gameManager = manager;
        playerIndex = index;
    }

    public bool IsOwnTurn => CurrentHandPlayerIndex == playerIndex;

    public int FindBestAvailableDieIndex()
    {
        int bestIndex = -1;
        int bestValue = int.MinValue;
        for (int i = 0; i < Player.dice.Count; i++)
        {
            if (!gameManager.IsDieAvailable(playerIndex, i)) continue;
            if (Player.dice[i] > bestValue)
            {
                bestValue = Player.dice[i];
                bestIndex = i;
            }
        }
        return bestIndex;
    }
}

public sealed class BalancedCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Balanced;

    public override int ChoosePrediction(CpuDecisionContext context)
    {
        int availableDice = context.Player.dice.Count;
        return Math.Min(context.RoundDiceCount, Math.Max(0, availableDice / 2));
    }
}

public sealed class AggressiveCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Aggressive;

    public override int ChoosePrediction(CpuDecisionContext context)
    {
        return Math.Min(context.RoundDiceCount, Math.Max(0, context.Player.dice.Count));
    }
}

public sealed class ConservativeCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Conservative;

    public override int ChoosePrediction(CpuDecisionContext context)
    {
        return Math.Min(context.RoundDiceCount, Math.Max(0, context.Player.dice.Count / 3));
    }
}

public sealed class OpportunisticCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Opportunistic;

    public override int ChoosePrediction(CpuDecisionContext context)
    {
        return Math.Min(context.RoundDiceCount, Math.Max(0, context.Player.dice.Count / 2));
    }
}
