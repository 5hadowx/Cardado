# Cardado Architecture — Source of Truth

**Status:** Active development architecture  
**Branch at definition:** `deck-discard-lifecycle`  
**Purpose:** Prevent implementation drift while the project moves from working gameplay flow to CPU, UI, and eventually online multiplayer.

---

## 1. Architectural goal

Cardado must separate **rules/state**, **decision-making**, **presentation**, and **transport**.

The game rules must not depend on whether an action was produced by:

- a human player using local UI,
- a CPU player,
- a remote player received over the network, or
- a developer/debug control.

All of those actors ultimately request the same legal gameplay actions from the game/rules layer.

The intended long-term pipeline is:

```text
Player Controller
    |
    | Action request
    v
Game / Rules Layer
    |
    | State change + events
    v
Presentation / UI
```

For online play, transport sits between the remote controller and the rules layer:

```text
Remote UI
   |
Network Transport
   |
Remote Player Controller
   |
Game / Rules Layer
```

The transport layer must never become the owner of Cardado rules.

---

## 2. Current game-flow state machine

The existing playable MVP flow is the structural foundation and should not be replaced by controller or UI work.

```text
WaitingForDealer
      |
      v
RoundSetupRoll
      |
      v
DealerSetupDecision
      |
      v
Prediction
      |
      v
RollDice
      |
      v
RevealDice
      |
      v
CardActionDecision
      |
      v
PlayingHands
      |
      v
RoundResolution
      |
      v
WarResolution
      |
      +---- no further war ----> next round / GameOver
```

The current `CardadoGameManager` already exposes this phase model, central player state, round deck, turn indices, and gameplay events. `CardadoPlayerState` contains gameplay state such as chips, prediction, hands won, dice, played-dice state, and hand contents. These remain gameplay-layer responsibilities rather than UI responsibilities.

---

## 3. Single source of truth for gameplay state

`CardadoGameManager` owns match-level flow and authoritative phase/turn state.

`CardadoPlayerState` owns runtime state belonging to one player.

`Deck` owns draw/discard lifecycle.

`CardData` describes card definitions; `CardInstance` represents runtime cards.

UI components may read and display state but must not create a second authoritative copy of rules or player state.

### Required rule

If a value can affect whether an action is legal, it belongs in gameplay state/rules, not only in the UI.

Examples:

- current phase,
- current player,
- available dice,
- prediction validity,
- chips,
- War eligibility,
- cards actually owned,
- whether a die is protected,
- whether a card action is pending.

---

## 4. Player controller architecture

Introduce a player-controller abstraction before building the final UI.

Conceptually:

```text
CardadoPlayerController
├── HumanPlayerController
├── CpuPlayerController
├── DebugPlayerController
└── RemotePlayerController   (future)
```

The exact C# inheritance/interface form can be chosen after inspecting the current code, but the architectural rule is fixed: **controllers decide actions; GameManager validates and executes them.**

### Human controller

Reads local input/UI and requests legal actions.

### CPU controller

Asks its CPU strategy for a decision and submits the resulting legal action through the same gameplay API.

### Debug controller

Preserves the existing development/testing ability to manually advance gameplay without requiring the final UI.

### Remote controller

Future network-backed controller. It receives a validated/authorized remote action and submits it to the same rules layer.

---

## 5. Action ownership

A controller should not directly mutate:

- player chips,
- player hands,
- dice lists,
- played-dice state,
- deck piles,
- War state,
- game phase.

Instead it should request an action.

Examples:

```text
PlacePrediction
PlayCard
SkipCard
PlayDie
ChooseArtistDie
ChooseWarTarget
ChooseWarWager
ChooseWarOrder
DeclareAnotherWar
PassWar
```

The rules layer validates the request against current state and performs the mutation.

This makes human, CPU, and remote play mechanically equivalent.

---

## 6. Hidden information / player-specific views

The internal game state and the information shown to a player are different concepts.

For a local human player, the UI should expose:

- that player's hand,
- that player's own dice,
- public/played dice,
- chips,
- public turn/round information,
- public effects and actions.

It must not expose other players' private cards.

CPU decision-making must obey the same information boundary. A CPU strategy must not simply inspect every opponent's private hand to make decisions.

This boundary is also the foundation for online multiplayer. A future network client must receive only the information that the corresponding player is entitled to see.

### Architectural rule

Do not build the final UI by rendering the complete internal `Players` collection indiscriminately. Build presentation around a **player perspective** or view model that can later be filtered for local/remote players.

---

## 7. Turn lifecycle

A turn should have an explicit lifecycle independent of UI presentation:

```text
Turn begins
   |
   v
Controller receives decision opportunity
   |
   +--> Human waits for input
   |
   +--> CPU thinks / schedules decision
   |
   +--> Remote waits for network action
   |
   v
Action submitted
   |
   v
Rules validate + execute
   |
   v
Gameplay event emitted
   |
   v
Next decision / next player
```

A CPU thinking delay is presentation/timing behavior around a decision, not a gameplay rule.

A future turn timeout is similarly a controller/session rule and must not be hardcoded into visual UI elements.

---

## 8. UI architecture

The final board UI should represent one player's perspective.

For the local player, the intended layout is approximately:

```text
                 CPU / Opponent
             private hand hidden

        +---------------------------+
 CPU    |                           |    CPU
        |           BOARD           |
        |       public dice         |
        |                           |
        +---------------------------+

                 LOCAL PLAYER
              private hand visible
```

During another player's turn:

- local hand remains visible to the local player,
- controls for the local player are disabled,
- public board state remains visible,
- a clear turn indicator identifies the active player,
- optionally a countdown/timeout indicator is shown.

The UI must not advance the game merely because a visual element changed. It sends action requests to the controller/gameplay layer.

---

## 9. Developer mode must remain available

The current development overlay/debug controls are valuable and must not be discarded when the real UI is introduced.

The project should support configurations such as:

```text
Developer test:
Player 1 = Debug/Human
Player 2 = Debug/Human
Player 3 = Debug/Human
Player 4 = Debug/Human
```

and:

```text
Playable MVP:
Player 1 = Human
Player 2 = CPU
Player 3 = CPU
Player 4 = CPU
```

Later:

```text
Online:
Player 1 = Human/Local
Player 2 = Remote
Player 3 = Remote
Player 4 = CPU
```

This prevents the final UI from becoming the only way to test mechanics.

---

## 10. CPU architecture

CPU behavior is defined in the separate `Cardado_CPU_Architecture.md` document.

The important boundary is:

```text
CpuPlayerController
       |
       v
CpuStrategy
       |
       v
Decision / Action
       |
       v
GameManager rules API
```

The first CPU implementation should prioritize **legal, believable, debuggable behavior** rather than maximum intelligence.

Multiple strategy profiles will eventually exist. A CPU player receives one profile at match setup, chosen randomly from the enabled profile pool.

---

## 11. Online architecture

Online architecture is defined in `Cardado_Network_Architecture.md`.

The intended model is player-hosted sessions:

```text
Host Player
   |
 Host Session / Authority
   |
 +---------+---------+---------+
 |         |         |         |
Local    Remote    Remote    CPU
player   player    player
```

The host is the authoritative game-state owner for the session. Other players connect to the host and submit player actions. The exact transport/discovery technology is intentionally deferred until the single-player architecture is stable.

The architecture must avoid assuming that a paid dedicated server exists.

However, the game rules must remain independent of the transport so the transport can change later without rewriting gameplay.

---

## 12. Randomness and authority

All gameplay-affecting randomness must eventually have one authoritative source.

Examples:

- dice rolls,
- deck shuffles,
- random CPU profile assignment,
- random setup outcomes.

In single-player, the local game manager can own this authority.

In host-based multiplayer, the host/session authority must own gameplay randomness and communicate resulting state/events to clients. Clients must not independently roll authoritative dice or decide authoritative deck outcomes.

This is an architectural requirement even though networking is not being implemented yet.

---

## 13. Persistence and reconnects

Not required for the current MVP.

Do not build persistence/reconnect systems prematurely.

The architecture should nevertheless avoid making runtime state inseparable from UI objects so that serialization can be added later if needed.

---

## 14. Current implementation priorities

### Phase 1 — Complete

- Core match flow
- Round flow
- Prediction
- Hand play
- Card-action bridge
- Round resolution
- War flow
- War/deck lifecycle
- GameOver transition structure

### Phase 2 — Current

- Player-controller abstraction
- Human controller path
- CPU controller
- CPU strategy abstraction
- Initial CPU profile set
- CPU timing/turn pacing
- Preserve developer/debug controls

### Phase 3

- Player-perspective board UI
- Private-hand rendering
- Public board rendering
- Turn indicator
- Countdown/timeout presentation
- CPU thinking presentation

### Phase 4

- Full gameplay testing with friends
- Card-effect testing
- Balance and AI tuning
- Edge-case testing

### Phase 5 — Future

- Host-created rooms
- Player invitation/join flow
- Remote player controller
- Authoritative host state
- Network synchronization
- Disconnect/reconnect policy

---

## 15. Non-goals for current CPU/UI milestone

Do not implement yet unless explicitly requested:

- dedicated online servers,
- matchmaking services,
- accounts,
- persistence,
- sophisticated neural AI,
- hidden-information cheating by CPU,
- final visual polish before functional UI exists,
- networking-specific gameplay rules.

---

## 16. Architecture decision rule

When a proposed implementation makes a feature work only for the current local/debug UI, stop and redesign the boundary.

Prefer:

```text
Rules/state -> controller -> action -> rules/state -> event -> UI
```

Avoid:

```text
UI button -> directly mutate gameplay state
```

The same action must be capable of being generated by human, CPU, debug, and eventually remote controllers.

---

## 17. Source-of-truth hierarchy

When implementation questions arise, use this order:

1. **Cardado game rules / user-approved gameplay decisions** — authoritative for what the game does.
2. **This architecture document** — authoritative for separation of systems and future compatibility.
3. **`Cardado_CPU_Architecture.md`** — authoritative for CPU structure and profiles.
4. **`Cardado_Network_Architecture.md`** — authoritative for multiplayer constraints.
5. **Current C# implementation** — authoritative for what currently exists, but not automatically authoritative for intended future architecture.
6. **Temporary development UI/overlays** — implementation/testing tools, not architecture.

If a source conflicts with another source, do not silently choose. Identify the conflict before making a substantial change.
