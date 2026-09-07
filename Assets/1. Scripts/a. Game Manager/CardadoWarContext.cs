using System;
using System.Collections.Generic;

/// <summary>
/// Isolated mutable state for one active War. It contains exactly the two War
/// participants and the temporary cards, dice, effects and turn state used by War.
/// </summary>
public sealed class CardadoWarContext
{
    public enum EffectType { Modifier, BodyguardDie, BodyguardPlayer, BodyguardHand }

    public sealed class Effect
    {
        public EffectType Type { get; internal set; }
        public CardInstance Card { get; internal set; }
        public int OwnerIndex { get; internal set; }
        public int TargetIndex { get; internal set; } = -1;
        public int DieIndex { get; internal set; } = -1;
        public int OriginalValue { get; internal set; }
    }

    public sealed class Participant
    {
        private readonly List<CardInstance> cards = new List<CardInstance>();
        private readonly List<int> dice = new List<int>();
        private readonly List<bool> playedDice = new List<bool>();

        public int PlayerIndex { get; }
        public string PlayerId { get; }
        public int Chips { get; set; }
        public IReadOnlyList<CardInstance> Cards => cards;
        public IReadOnlyList<int> Dice => dice;
        public IReadOnlyList<bool> PlayedDice => playedDice;

        internal Participant(int playerIndex, string playerId, int chips)
        {
            PlayerIndex = playerIndex;
            PlayerId = playerId;
            Chips = chips;
        }

        internal List<CardInstance> MutableCards => cards;
        internal List<int> MutableDice => dice;
        internal List<bool> MutablePlayedDice => playedDice;
    }

    public Participant Challenger { get; }
    public Participant Target { get; }
    public int Wager { get; internal set; }
    public int HandNumber { get; internal set; }
    public int CurrentTurnSlot { get; internal set; }
    public int ChallengerHandsWon { get; internal set; }
    public int TargetHandsWon { get; internal set; }
    public bool WarCardPlayedByChallenger { get; internal set; }
    public bool WarCardPlayedByTarget { get; internal set; }

    private readonly List<Effect> effects = new List<Effect>();
    private readonly HashSet<int> blockedPlayers = new HashSet<int>();

    public IReadOnlyList<Effect> Effects => effects;
    public Participant CurrentPlayer => CurrentTurnSlot == 0 ? Challenger : Target;

    private CardadoWarContext(Participant challenger, Participant target)
    {
        Challenger = challenger;
        Target = target;
        HandNumber = 1;
    }

    public static CardadoWarContext Create(int challengerIndex, string challengerId, int challengerChips,
        int targetIndex, string targetId, int targetChips)
    {
        if (challengerIndex < 0 || targetIndex < 0 || challengerIndex == targetIndex)
            throw new ArgumentException("A War requires two distinct valid participants.");
        return new CardadoWarContext(
            new Participant(challengerIndex, challengerId, challengerChips),
            new Participant(targetIndex, targetId, targetChips));
    }

    public Participant OpponentOf(Participant player)
    {
        if (ReferenceEquals(player, Challenger)) return Target;
        if (ReferenceEquals(player, Target)) return Challenger;
        throw new ArgumentException("The supplied player is not part of this War.", nameof(player));
    }

    public bool ContainsPlayer(int playerIndex) =>
        Challenger.PlayerIndex == playerIndex || Target.PlayerIndex == playerIndex;

    public Participant GetParticipant(int playerIndex)
    {
        if (Challenger.PlayerIndex == playerIndex) return Challenger;
        if (Target.PlayerIndex == playerIndex) return Target;
        return null;
    }

    internal void AddCard(Participant participant, CardInstance card)
    {
        if (card != null) participant.MutableCards.Add(card);
    }

    internal bool RemoveCard(Participant participant, CardInstance card) => participant.MutableCards.Remove(card);

    internal void AddDie(Participant participant, int value)
    {
        participant.MutableDice.Add(value);
        participant.MutablePlayedDice.Add(false);
    }

    internal bool IsDieAvailable(Participant participant, int dieIndex) =>
        dieIndex >= 0 && dieIndex < participant.MutableDice.Count &&
        dieIndex < participant.MutablePlayedDice.Count &&
        !participant.MutablePlayedDice[dieIndex] && participant.MutableDice[dieIndex] > 0;

    internal bool IsDieTargetable(Participant participant, int dieIndex) =>
        dieIndex >= 0 && dieIndex < participant.MutableDice.Count && participant.MutableDice[dieIndex] > 0;

    internal bool HasPlayedCard(Participant participant) =>
        ReferenceEquals(participant, Challenger) ? WarCardPlayedByChallenger : WarCardPlayedByTarget;

    internal void MarkCardPlayed(Participant participant)
    {
        if (ReferenceEquals(participant, Challenger)) WarCardPlayedByChallenger = true;
        else if (ReferenceEquals(participant, Target)) WarCardPlayedByTarget = true;
    }

    internal bool IsBlocked(Participant participant) => blockedPlayers.Contains(participant.PlayerIndex);
    internal void Block(Participant participant) => blockedPlayers.Add(participant.PlayerIndex);

    internal Effect AddEffect(EffectType type, CardInstance card, Participant owner, Participant target = null,
        int dieIndex = -1, int originalValue = 0)
    {
        Effect effect = new Effect
        {
            Type = type,
            Card = card,
            OwnerIndex = owner.PlayerIndex,
            TargetIndex = target == null ? -1 : target.PlayerIndex,
            DieIndex = dieIndex,
            OriginalValue = originalValue
        };
        effects.Add(effect);
        return effect;
    }

    internal void RemoveEffect(Effect effect)
    {
        if (effect != null) effects.Remove(effect);
    }

    internal void ClearHandScopedEffects()
    {
        for (int i = effects.Count - 1; i >= 0; i--)
            if (effects[i].Type == EffectType.BodyguardDie || effects[i].Type == EffectType.BodyguardHand)
                effects.RemoveAt(i);
        blockedPlayers.Clear();
        WarCardPlayedByChallenger = false;
        WarCardPlayedByTarget = false;
    }
}