# Cardado Project Guardrails

**Purpose:** Instructions for future development sessions and AI-assisted implementation.

This document is intended to be used together with:

- `Cardado_Architecture.md`
- `Cardado_CPU_Architecture.md`
- `Cardado_Network_Architecture.md`
- the current development handover/source files

---

## 1. Mandatory source check

Before proposing or implementing a substantial Cardado change, read the relevant architecture documents first.

At minimum, check:

1. `Docs/Cardado_Architecture.md`
2. `Docs/Cardado_CPU_Architecture.md` for CPU/AI work
3. `Docs/Cardado_Network_Architecture.md` for networking, player identity, hidden information, authority, or synchronization work

Then inspect the actual current C# implementation before editing it.

Do not rely on an old conversation summary when the repository can be inspected.

---

## 2. Architecture before implementation

Before changing code, identify:

- which layer owns the feature,
- which component owns the authoritative state,
- which controller produces the action,
- which existing GameManager API should validate/execute it,
- which event/UI update should represent the result,
- whether the change affects future CPU or networking compatibility.

If the proposed implementation violates the architecture, stop and explain the conflict before proceeding.

---

## 3. Preserve the rules layer

Never move Cardado rules into UI code merely because it makes a screen easier to implement.

Never make a CPU-specific copy of gameplay rules.

Never make a network-specific copy of gameplay rules.

The same authoritative gameplay path must serve:

```text
Debug -> Rules
Human -> Rules
CPU -> Rules
Remote -> Rules
```

---

## 4. Preserve hidden-information boundaries

Never expose private opponent cards to the local UI just because they are available in the GameManager.

Never give CPU strategies access to hidden opponent hands.

For future networking, never assume that hiding information in UI is sufficient; private information must not be sent to clients that are not entitled to it.

---

## 5. Preserve developer mode

Developer/debug controls are not disposable code.

They are required for:

- reproducing bugs,
- testing individual mechanics,
- testing card effects,
- validating edge cases,
- developing without waiting for AI or UI.

When introducing final UI or CPU controllers, preserve a way to run controlled/manual development tests.

---

## 6. CPU rules

CPU is a player controller plus a strategy, not a separate game mode.

CPU decisions must:

- use legal GameManager actions,
- respect hidden information,
- support profile-based behavior,
- be independently testable,
- avoid directly mutating gameplay state.

CPU profiles are behavioral strategies. They should not be hardcoded into GameManager.

CPU profile assignment is randomized per CPU player at match start.

---

## 7. Networking rules

Networking is future work, but all new player-facing architecture must remain compatible with:

- host-authoritative gameplay,
- remote player controllers,
- player-specific hidden-information views,
- authoritative randomness,
- action requests instead of arbitrary client state mutation.

Do not add a permanent dedicated-server dependency to solve a single-player problem.

Do not select a networking technology prematurely.

---

## 8. Smallest coherent change

Prefer the smallest implementation that establishes the correct architectural boundary.

Do not refactor unrelated systems while implementing a feature.

Do not replace working Cardado flow merely because another architecture is theoretically cleaner unless there is a demonstrated problem.

---

## 9. Inspect before assuming

Before modifying an existing system:

1. inspect the current file,
2. inspect callers,
3. inspect relevant serialized configuration/assets when applicable,
4. identify current event flow,
5. identify existing development bridges/overlays,
6. then make the change.

Do not invent fields, methods, asset structure, or scene wiring without checking the repository.

---

## 10. Unity limitation

There is currently no guaranteed Unity compiler/CI environment available through the development tooling.

Therefore:

- review C# changes carefully,
- check method names/signatures against current source,
- avoid speculative refactors,
- report that Unity compilation/runtime validation could not be performed when applicable,
- provide an exact Unity test procedure after gameplay changes.

---

## 11. Commit discipline

When implementing an approved change directly in the repository:

1. inspect the current branch and HEAD,
2. make the smallest coherent change,
3. commit directly to the active development branch unless the user requests another workflow,
4. report the commit SHA,
5. summarize changed files/behavior,
6. state validation limitations honestly,
7. give the exact next Unity test when needed.

Avoid mixing unrelated features into one commit.

---

## 12. Do not prematurely polish

The current priority is functional MVP progression.

Prefer:

```text
Correct architecture
-> complete functionality
-> testing
-> UI refinement
-> balance/polish
```

over:

```text
Beautiful UI
-> hidden architectural coupling
-> difficult testing
-> rewrite
```

---

## 13. Current roadmap

### Completed structural flow

- Dealer/setup
- Round setup
- Prediction
- Card dealing
- Dice/reveal
- Normal hands
- Card actions
- Round resolution
- War
- War cleanup/recycling
- Next round/GameOver structure

### Current milestone

Build Human/CPU player-control architecture and a basic but complete CPU capable of playing the full match.

### Next milestone

Build the real player-perspective board UI while preserving developer controls.

### After UI

Use repeated playtesting with non-technical players to test:

- card effects,
- War edge cases,
- clarity of rules/UI,
- pacing,
- balance,
- CPU behavior.

### Future

Add host-created online rooms and RemotePlayerController using the network architecture document.

---

## 14. Conflict handling

If user requirements conflict with these documents, do not silently override either side.

Explicitly state:

- the requested change,
- the architectural rule it conflicts with,
- the smallest viable resolution,
- whether the architecture document should be updated first.

The user can then approve the architectural decision.

Architecture documents should be updated when a deliberate project-level decision changes the design.

---

## 15. AI assistant project instruction

Recommended project-level instruction:

> **Cardado architecture is source-controlled in `Docs/Cardado_Architecture.md`, `Docs/Cardado_CPU_Architecture.md`, `Docs/Cardado_Network_Architecture.md`, and `Docs/Cardado_Project_Guardrails.md`. Before answering implementation questions or modifying the repository, inspect the relevant architecture document(s) and the current source code. Treat these documents as the project architecture source of truth unless the user explicitly changes an architectural decision. Keep gameplay rules authoritative in the GameManager/rules layer; controllers (Human/CPU/Debug/Remote) request actions rather than directly mutating gameplay state. Preserve hidden-information boundaries. Keep CPU strategy separate from rules. Keep the architecture compatible with future host-authoritative networking and RemotePlayerController without implementing networking prematurely. Preserve developer/debug testing paths. Prefer the smallest coherent change, inspect current code before assuming APIs or structure, and report Unity compilation/runtime limitations honestly. When implementing an approved repository change, commit it to the active development branch and report the commit SHA plus the exact Unity test procedure. If a requested change conflicts with the architecture, identify the conflict before making the change rather than silently drifting the architecture.**

---

## 16. Session-start checklist

At the beginning of a new Cardado development conversation:

```text
[ ] Read Cardado_Architecture.md
[ ] Read the relevant specialist architecture document
[ ] Read the latest handover/status if provided
[ ] Inspect current repository branch/HEAD
[ ] Inspect current implementation before editing
[ ] Identify the current milestone
[ ] Avoid implementing future milestones prematurely
```

At the end of an implementation:

```text
[ ] Confirm architecture was not violated
[ ] Confirm unrelated systems were not changed
[ ] Commit coherent changes
[ ] Report commit SHA
[ ] State validation limitations
[ ] Give next test/next milestone
```
