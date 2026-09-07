# Cardado War Architecture

**Status:** Approved architectural decision  
**Scope:** War gameplay isolation

## 1. War is a temporary game context

A War is not a normal game turn with two players temporarily selected from the match.

When a War starts, Cardado creates a temporary `CardadoWarContext` containing exactly:

- the challenger,
- the target,
- the cards dealt for the War,
- the dice used for the War,
- War hand/turn state,
- War-specific effects and modifiers,
- the War wager and score.

The normal match state remains outside this context.

```text
Normal Match State
       |
       | War starts
       v
CardadoWarContext
  ├── Participant A
  │    ├── War cards
  │    └── War dice
  └── Participant B
       ├── War cards
       └── War dice
       |
       | War resolves
       v
Explicit War result applied to normal match state
```

The implementation must not temporarily replace or repurpose `CardadoGameManager.Players` as the War state.

## 2. Shared card vocabulary, isolated mutable state

War uses the same `CardData` and `CardInstance` definitions and the same card-effect semantics as the normal game.

The distinction is the state against which those effects execute:

- normal gameplay effects operate on normal match state;
- War effects operate only on `CardadoWarContext`.

Cards dealt into War become part of the War context. Cards held before the War remain part of the normal player's hand and are restored/retained according to the War rules after resolution.

## 3. War targeting

War has exactly two participants.

Therefore an effect that normally asks the player to select another player automatically resolves its player target to the other War participant.

There is no global player-selection step inside War.

Effects involving dice/cards must likewise resolve against the two War participants and their War state only.

## 4. Hidden information

A controller or strategy must only see the War information available to that player.

The existence of a `CardadoWarContext` must not be used as a reason to expose the opponent's private War hand to a CPU or remote player.

## 5. Rules/controller boundary

The War context is gameplay state, not presentation state.

Controllers request War actions. The War rules/context validate and execute them. Development overlays remain input/presentation clients and must not become the authoritative War-effect implementation.

## 6. Implementation sequence

1. Establish `CardadoWarContext` as the isolated state container.
2. Move War card/dice lifecycle from `CardadoWarManager` into the context.
3. Extract card-effect execution so it can operate against the War context.
4. Adapt the development overlay to submit War actions through the rules API.
5. Verify existing War behavior and restoration of normal match state.
6. Build CPU War decisions on top of the same action API.

This sequence intentionally keeps the CPU milestone behind the War boundary work.
