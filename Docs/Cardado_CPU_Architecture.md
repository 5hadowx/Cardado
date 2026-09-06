# Cardado CPU Architecture

**Status:** Planned / next implementation milestone  
**Related:** `Cardado_Architecture.md`

## 1. Purpose

CPU players must behave as actual Cardado players, not as special-case automated UI clicks.

The CPU must submit the same legal gameplay actions available to a human player. The GameManager remains responsible for validation and state mutation.

```text
CPU Player
   |
CpuPlayerController
   |
CpuStrategy
   |
Decision
   |
GameManager action API
```

---

## 2. CPU controller vs strategy

Separate **when/how a CPU participates** from **why it chooses an action**.

### CpuPlayerController

Responsible for:

- detecting when its player is active,
- requesting a decision from its strategy,
- waiting a configurable thinking interval,
- submitting the selected action,
- waiting for gameplay events before making the next decision.

It should not contain the personality rules themselves.

### CpuStrategy

Responsible for evaluating the currently visible situation and selecting an action.

The strategy receives a restricted player-perspective state/context rather than unrestricted access to every player's private information.

---

## 3. CPU profiles

At match initialization, every CPU player receives one profile selected randomly from the enabled profile pool.

The assignment is made once for the match unless a future design explicitly introduces personality changes.

The profile should be stored as player/controller configuration so the same profile continues throughout the match.

Initial planned profiles:

### Balanced

Default/general-purpose strategy.

- Makes moderate predictions.
- Preserves useful cards when possible.
- Takes opportunities to win hands without spending every powerful card immediately.
- Uses defensive cards when they have clear value.
- Declares sensible Wars rather than always seeking them.

### Aggressive

Prioritizes immediate hand wins and pressure.

- More willing to predict high numbers.
- More willing to spend strong cards to secure a hand.
- More likely to interfere with opponents.
- More willing to declare Wars.
- Accepts more risk.

### Conservative

Prioritizes reducing losses and preserving resources.

- More conservative predictions.
- Avoids spending powerful cards unless necessary.
- Protects valuable dice/cards more often.
- Less willing to initiate marginal Wars.
- Prefers decisions with lower variance.

### Opportunistic

Looks for tactical openings.

- Pays particular attention to visible opponent behavior.
- Exploits exposed weak dice.
- Uses offensive cards when they can swing an important hand.
- May change risk tolerance based on chip position and prediction requirements.
- More sensitive to War opportunities.

The exact algorithms are intentionally not fixed yet. Profiles define **behavioral priorities**, not a hard-coded list of every move.

---

## 4. Future extensibility

The architecture should allow additional profiles without modifying the GameManager.

Potential later profiles:

- Random/Easy
- Expert
- Deceptive
- Risky
- Endgame-focused

Do not implement all of these during the first CPU milestone.

---

## 5. Decision context

A CPU decision should be based on a context object or equivalent abstraction containing only information the CPU is entitled to know.

Conceptually:

```text
CpuDecisionContext
├── Own player state
├── Own cards
├── Own dice
├── Own prediction
├── Own chips
├── Public opponent state
├── Public dice
├── Public played cards/effects
├── Current phase
├── Current hand/turn
├── Current War state
└── Public history useful for strategy
```

It must not contain hidden opponent hands.

The strategy may use a controlled amount of history because Cardado rewards observing patterns.

---

## 6. Decision hierarchy

The CPU should generally evaluate decisions in this order:

1. Is the requested action legal?
2. Does the current prediction/round objective require a particular outcome?
3. What immediate tactical options are available?
4. What is the value of spending a card now versus preserving it?
5. What are the likely consequences for later hands?
6. What is the chip/risk situation?
7. Which legal action best matches the CPU profile?

This is a framework, not a requirement that every action use identical scoring.

---

## 7. Prediction strategy

Prediction is strategic rather than random.

The first CPU version can use a simple heuristic based on:

- number/value of own dice,
- number of hands in the round,
- expected ability to win with high dice,
- known/public opponent behavior if useful,
- current chip position.

The prediction must always pass the existing Cardado prediction-validity rules.

The CPU should call the same GameManager prediction API as a human player.

---

## 8. Card strategy

The CPU should not automatically play the strongest card.

A card decision should consider:

- current die values,
- whether the hand is important for satisfying prediction,
- opponent's visible die/action,
- whether the card is persistent,
- whether an opponent can counter it,
- remaining cards,
- future hands,
- profile personality.

The initial implementation may use a scoring model:

```text
candidate action
    -> legality
    -> immediate value
    -> prediction value
    -> resource cost
    -> future value
    -> personality modifier
    -> final score
```

---

## 9. Die selection

When multiple dice are legal, the CPU should evaluate the objective of the current hand rather than simply always playing the highest die.

Examples:

- If a hand must be won to meet prediction, a high die may be appropriate.
- If the opponent has already committed a winning value, preserving a high die may be better.
- A lower die may be intentionally spent to preserve a stronger die for a later hand.

The first CPU version can be deliberately simple and become more sophisticated after real playtesting.

---

## 10. War strategy

War is a strategic decision, not an automatic action merely because a valid claim exists.

The CPU should evaluate:

- whether it has a valid claim,
- chip count,
- quality of remaining cards,
- likely target strength based only on public information,
- wager risk,
- whether another War may be possible afterward,
- profile personality.

The CPU must use the centralized `CardadoWarCardRules` logic for claim legality/optimal claim selection.

It must not invent an alternative War-card interpretation.

---

## 11. CPU timing

Decision timing is intentionally separate from decision logic.

A CPU should not execute every action in the same frame.

Recommended conceptual flow:

```text
Turn begins
   |
   v
Thinking delay
   |
   v
Strategy evaluates context
   |
   v
Action submitted
   |
   v
GameManager resolves action
```

Thinking delay should be configurable for development and UI testing.

The strategy itself should remain deterministic given a supplied random source/context where practical, so bugs can be reproduced.

---

## 12. Randomness

Randomness may be used for:

- selecting the CPU profile,
- tie-breaking between similarly scored actions,
- small personality variation,
- non-critical timing variation.

Gameplay-authoritative randomness such as dice rolls and deck outcomes belongs to the game/session authority, not the strategy.

For future online play, CPU decisions run on the authoritative host/session side when the CPU occupies a network slot.

---

## 13. Debugging requirements

CPU decisions should be inspectable during development.

Useful optional logs:

```text
[Cardado][CPU] Player 2 profile=Aggressive
[Cardado][CPU] Prediction candidates: 1,2,3
[Cardado][CPU] Selected prediction=2
[Cardado][CPU] Hand 2 candidates: Die0=5, Die2=3
[Cardado][CPU] Selected Die0=5 score=...
```

Debug logging should be switchable and must not be required by gameplay.

A developer should be able to reproduce a problematic decision without changing the rules layer.

---

## 14. First implementation scope

The first CPU milestone should support the complete game loop:

- prediction,
- card-action decision,
- die selection,
- hand progression,
- round resolution,
- War claim/pass,
- War target,
- War wager,
- War order,
- War card actions,
- War die selection,
- repeat War decision.

The initial AI can be basic.

**Completeness is more important than intelligence at this stage.**

---

## 15. Acceptance criteria

CPU development is considered structurally complete when:

- a Human + CPU match can complete without developer/manual intervention,
- all CPU actions pass the same legality checks as human actions,
- CPU never reads hidden opponent hands,
- CPU profiles can be selected independently per CPU player,
- profile assignment is randomized at match start,
- changing a profile does not require changes to GameManager rules,
- developer/debug control remains available,
- the same controller architecture can later support RemotePlayerController.
