using System;
using System.Collections.Generic;

/// <summary>
/// Isolated mutable state for one active War. It intentionally does not expose
/// CardadoGameManager.Players, so War card effects can never target unrelated
/// match participants through the War state itself.
/// </summary>
public sealed class CardadoWarContext
{
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

    public Participant CurrentPlayer => CurrentTurnSlot == 0 ? Challenger : Target;

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

    public static CardadoWarContext Create(int challengerIndex, string challengerId, int challengerChips,
        int targetIndex, string targetId, int targetChips)
    {
        if (challengerIndex < 0 || targetIndex < 0 || challengerIndex == targetIndex)
            throw new ArgumentException("A War requires two distinct valid participants.");

        return new CardadoWarContext(
            new Participant(challengerIndex, challengerId, challengerChips),
            new Participant(targetIndex, targetId, targetChips));
    }

    private CardadoWarContext(Participant challenger, Participant target)
    {
        Challenger = challenger;
        Target = target;
        HandNumber = 1;
    }

    internal void CopyCardsFrom(IEnumerable<CardInstance> source, Participant destination)
    {
        destination.MutableCards.Clear();
        foreach (CardInstance card in source)
            if (card != null) destination.MutableCards.Add(card);
    }

    internal void AddCard(Participant participant, CardInstance card)
    {
        if (card != null) participant.MutableCards.Add(card);
    }

    internal bool RemoveCard(Participant participant, CardInstance card) =>
        participant.MutableCards.Remove(card);

    internal void ClearDice(Participant participant)
    {
        participant.MutableDice.Clear();
        participant.MutablePlayedDice.Clear();
    }

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

    internal void MarkDiePlayed(Participant participant, int dieIndex) =>
        participant.MutablePlayedDice[dieIndex] = true;

    internal int GetDieValue(Participant participant, int dieIndex) =>
        IsDieTargetable(participant, dieIndex) ? participant.MutableDice[dieIndex] : 0;

    internal void SetDieValue(Participant participant, int dieIndex, int value)
    {
        if (!IsDieTargetable(participant, dieIndex))
            throw new ArgumentOutOfRangeException(nameof(dieIndex));
        participant.MutableDice[dieIndex] = value;
    }
}
