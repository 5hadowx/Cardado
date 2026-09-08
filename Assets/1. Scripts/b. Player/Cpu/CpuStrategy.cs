using System;
using System.Collections.Generic;

/// <summary>
/// Produces CPU decisions from a player-perspective context.
/// Strategy owns decision logic; it never mutates gameplay state.
/// </summary>
public abstract class CpuStrategy
{
    public abstract CpuProfile Profile { get; }
    public abstract int ChoosePrediction(CpuDecisionContext context);

    public virtual CpuCardDecision ChooseCardAction(CpuDecisionContext context) => CpuCardDecision.Skip;

    public virtual int ChooseDie(CpuDecisionContext context) => context.FindBestAvailableDieIndex();
}

public enum CpuProfile { Balanced, Aggressive, Conservative, Opportunistic }
public enum CpuCardDecisionType { Skip, Play }

public readonly struct CpuCardDecision
{
    public static CpuCardDecision Skip => new CpuCardDecision(CpuCardDecisionType.Skip, -1);
    public CpuCardDecisionType Type { get; }
    public int CardIndex { get; }
    public CpuCardDecision(CpuCardDecisionType type, int cardIndex) { Type = type; CardIndex = cardIndex; }
}

/// <summary>
/// Restricted decision input. It contains a snapshot of the CPU player's
/// private state plus only public match information. It intentionally does
/// not retain a GameManager reference or expose the Players collection.
/// </summary>
public sealed class CpuDecisionContext
{
    private readonly int playerIndex;
    private readonly string playerId;
    private readonly int chips;
    private readonly int diceBid;
    private readonly int handsWon;
    private readonly bool hasPlacedBid;
    private readonly List<int> dice;
    private readonly List<bool> playedDice;
    private readonly List<CardInstance> hand;
    private readonly CardadoGamePhase phase;
    private readonly int playerCount;
    private readonly int currentHandNumber;
    private readonly int currentHandPlayerIndex;
    private readonly int roundDiceCount;
    private readonly int roundCardCount;
    private readonly int dealerPlayerIndex;
    private readonly int startingPlayerIndex;
    private readonly int placedDicePredictionTotalExcludingSelf;

    public int PlayerIndex => playerIndex;
    public string PlayerId => playerId;
    public int Chips => chips;
    public int DiceBid => diceBid;
    public int HandsWon => handsWon;
    public bool HasPlacedBid => hasPlacedBid;
    public IReadOnlyList<int> Dice => dice;
    public IReadOnlyList<bool> PlayedDice => playedDice;
    public IReadOnlyList<CardInstance> Hand => hand;
    public CardadoGamePhase Phase => phase;
    public int PlayerCount => playerCount;
    public int CurrentHandNumber => currentHandNumber;
    public int CurrentHandPlayerIndex => currentHandPlayerIndex;
    public int RoundDiceCount => roundDiceCount;
    public int RoundCardCount => roundCardCount;
    public int DealerPlayerIndex => dealerPlayerIndex;
    public int StartingPlayerIndex => startingPlayerIndex;
    public int PlacedDicePredictionTotalExcludingSelf => placedDicePredictionTotalExcludingSelf;
    public bool IsOwnTurn => CurrentHandPlayerIndex == PlayerIndex;

    public CpuDecisionContext(CardadoGameManager manager, int index)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (index < 0 || index >= manager.Players.Count) throw new ArgumentOutOfRangeException(nameof(index));

        CardadoPlayerState player = manager.Players[index];
        playerIndex = index;
        playerId = player.playerId;
        chips = player.chips;
        diceBid = player.diceBid;
        handsWon = player.handsWon;
        hasPlacedBid = player.hasPlacedBid;
        dice = new List<int>(player.dice);
        playedDice = new List<bool>(player.playedDice);
        hand = player.hand == null || player.hand.cardsInHand == null
            ? new List<CardInstance>()
            : new List<CardInstance>(player.hand.cardsInHand);
        phase = manager.Phase;
        playerCount = manager.Players.Count;
        currentHandNumber = manager.CurrentHandNumber;
        currentHandPlayerIndex = manager.CurrentHandPlayerIndex;
        roundDiceCount = manager.RoundDiceCount;
        roundCardCount = manager.RoundCardCount;
        dealerPlayerIndex = manager.DealerPlayerIndex;
        startingPlayerIndex = manager.StartingPlayerIndex;

        int placedTotal = 0;
        for (int i = 0; i < manager.Players.Count; i++)
        {
            if (i == index || !manager.Players[i].hasPlacedBid) continue;
            placedTotal += manager.Players[i].diceBid;
        }
        placedDicePredictionTotalExcludingSelf = placedTotal;
    }

    public bool IsPredictionLegal(int prediction)
    {
        if (prediction < 0 || prediction > RoundDiceCount) return false;
        if (PlayerIndex != DealerPlayerIndex) return true;
        return PlacedDicePredictionTotalExcludingSelf + prediction != RoundDiceCount;
    }

    public int GetClosestLegalPrediction(int preferredPrediction)
    {
        int clamped = Math.Max(0, Math.Min(RoundDiceCount, preferredPrediction));
        if (IsPredictionLegal(clamped)) return clamped;

        for (int distance = 1; distance <= RoundDiceCount; distance++)
        {
            int lower = clamped - distance;
            if (lower >= 0 && IsPredictionLegal(lower)) return lower;
            int upper = clamped + distance;
            if (upper <= RoundDiceCount && IsPredictionLegal(upper)) return upper;
        }
        return -1;
    }

    public int FindBestAvailableDieIndex()
    {
        int bestIndex = -1;
        int bestValue = int.MinValue;
        for (int i = 0; i < Dice.Count; i++)
        {
            if (i < PlayedDice.Count && PlayedDice[i]) continue;
            if (Dice[i] > bestValue) { bestValue = Dice[i]; bestIndex = i; }
        }
        return bestIndex;
    }
}

public sealed class BalancedCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Balanced;
    public override int ChoosePrediction(CpuDecisionContext context)
    {
        int preferred = Math.Min(context.RoundDiceCount, Math.Max(0, context.Dice.Count / 2));
        return context.GetClosestLegalPrediction(preferred);
    }
}

public sealed class AggressiveCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Aggressive;
    public override int ChoosePrediction(CpuDecisionContext context)
    {
        int preferred = Math.Min(context.RoundDiceCount, Math.Max(0, context.Dice.Count));
        return context.GetClosestLegalPrediction(preferred);
    }
}

public sealed class ConservativeCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Conservative;
    public override int ChoosePrediction(CpuDecisionContext context)
    {
        int preferred = Math.Min(context.RoundDiceCount, Math.Max(0, context.Dice.Count / 3));
        return context.GetClosestLegalPrediction(preferred);
    }
}

public sealed class OpportunisticCpuStrategy : CpuStrategy
{
    public override CpuProfile Profile => CpuProfile.Opportunistic;
    public override int ChoosePrediction(CpuDecisionContext context)
    {
        int preferred = Math.Min(context.RoundDiceCount, Math.Max(0, context.Dice.Count / 2));
        return context.GetClosestLegalPrediction(preferred);
    }
}
