# Cardado Network Architecture

**Status:** Future architecture / constraints to respect now  
**Related:** `Cardado_Architecture.md`

## 1. Goal

Cardado should eventually support online sessions where one player creates a room and acts as the session host/authority, similar in concept to a host-created game session rather than requiring a permanently running dedicated game server.

Networking is a future milestone. The current single-player/CPU implementation must nevertheless preserve the boundaries required to add it without rewriting the game rules.

---

## 2. Intended session model

```text
                 HOST
          Game + Session Authority
                    |
       +------------+------------+
       |            |            |
    Local         Remote       Remote
    Human         Human        Human
                    |
                 optional
                   CPU
```

The host owns the authoritative match state for the session.

Clients own presentation and local input for their assigned player.

A client does not become authoritative merely because it displays or predicts a state.

---

## 3. Room lifecycle

Future online flow:

```text
Create Room
    |
    v
Host receives room/session identifier
    |
    v
Players invite/join
    |
    v
Lobby
    |
    v
Host starts match
    |
    v
Authoritative Cardado match begins
    |
    v
Gameplay synchronization
    |
    v
Match ends
    |
    v
Room closes / returns to lobby
```

The exact invitation mechanism is intentionally undecided.

Possible future mechanisms can include a room code, platform invitation, direct connection invitation, or another discovery mechanism supported by the chosen transport/platform.

Do not hardcode one mechanism into gameplay code.

---

## 4. Authority model

The host/session authority is responsible for authoritative:

- phase transitions,
- turn ownership,
- action legality,
- dice rolls,
- deck draws/shuffles,
- card ownership,
- card effects,
- predictions,
- scoring,
- War claims,
- War outcomes,
- GameOver.

Clients submit requests/actions.

Conceptually:

```text
CLIENT
  |
  | ActionRequest
  v
HOST AUTHORITY
  |
  | validate
  v
CARDADO RULES
  |
  | state change
  v
HOST STATE
  |
  | state/event update
  +-------> clients
```

The network transport must not implement a second copy of Cardado rules.

---

## 5. Why the current controller architecture matters

The future controller set should be:

```text
HumanPlayerController
CpuPlayerController
RemotePlayerController
DebugPlayerController
```

For online play, `RemotePlayerController` represents the remote player's input/request stream from the host's perspective.

The GameManager still receives a normalized action request and validates it.

This means the same rules can execute:

```text
Human action -> GameManager
CPU action   -> GameManager
Remote action -> GameManager
Debug action -> GameManager
```

without separate gameplay implementations.

---

## 6. Hidden information

Hidden information is a first-class networking concern.

A client must receive only information its player is entitled to know.

For example, a client should receive:

- its own cards,
- its own dice,
- public dice,
- public played-card/effect information,
- public chips and scores,
- turn/phase information.

It must not receive other players' private hands merely because the host internally has them.

This is why presentation should use player-perspective data rather than render raw authoritative state.

### Important

Client-side hiding is not sufficient security. If private data is sent to a client and merely hidden by UI, the information has already been leaked.

The eventual network protocol must filter private state before transmission.

---

## 7. Client actions

A remote client should send intent, not arbitrary state.

Good:

```text
PlayDie(dieIndex=2)
PlayCard(cardId=...)
ChooseWarTarget(playerId=...)
ChooseWarWager(1)
ChooseWarOrder(first=true)
```

Bad:

```text
SetDieValue(6)
SetChips(10)
SetHand([...])
SetPhase(WarResolution)
```

The host determines the resulting state.

---

## 8. Synchronization model

The first online implementation should prefer an authoritative state/event model over peer-to-peer independent simulation.

A conceptual update is:

```text
Host state
   |
   +--> phase/state snapshot
   +--> public event
   +--> player-private update
```

The exact frequency and serialization format will be chosen with the eventual networking technology.

Do not prematurely add networking packages or transport dependencies to the current MVP unless explicitly approved.

---

## 9. Randomness and cheating prevention

The host is authoritative for gameplay randomness.

Examples:

- setup dice,
- player dice rolls,
- deck shuffle,
- card draws,
- CPU profile assignment,
- other gameplay-random outcomes.

A remote client must not be able to choose or replace these outcomes.

A client can animate a result locally, but the authoritative result comes from the host.

---

## 10. CPU in online games

CPU players should remain compatible with the host architecture.

If a host fills an empty slot with CPU:

```text
Host
 |
 CpuPlayerController
 |
 CpuStrategy
 |
 GameManager
```

The CPU decision is executed by the authoritative host.

Clients receive the resulting public/private updates appropriate to their perspective.

A CPU must not exist only as a client-side visual automation.

---

## 11. Disconnect policy

Not defined yet.

The architecture must leave room for policies such as:

- temporary reconnect,
- CPU takeover,
- player elimination,
- host migration,
- room termination.

No policy should be implemented during the current CPU/UI milestone unless explicitly decided.

---

## 12. Host migration

Not defined yet.

Because the desired model is host-based, host migration may eventually be needed if the host leaves.

Do not build host migration now.

Do avoid putting irreversible host-specific assumptions into individual gameplay classes.

---

## 13. Transport technology

**Not selected yet.**

The project should remain transport-agnostic until:

1. the Human + CPU game is stable,
2. final UI exists,
3. local multiplayer assumptions are understood,
4. platform requirements are known,
5. a networking technology can be evaluated against those requirements.

Do not select a networking package solely because it is convenient during CPU development.

---

## 14. No permanent paid server requirement

The intended design does not require a permanently running dedicated game server for ordinary matches.

The host-player model means the host provides the authoritative match process while the room is active.

However, a real-world online game may still require external infrastructure for services such as:

- matchmaking/discovery,
- relay/NAT traversal,
- authentication,
- platform invitations,
- presence,
- abuse prevention.

Whether any of those are necessary depends on the final platform and networking technology. Do not assume that host-based gameplay means zero infrastructure cost in every deployment.

---

## 15. Networking acceptance criteria

Before calling online architecture complete, the system should support conceptually:

- host creates room,
- players join room,
- each player has an assigned player identity,
- host owns authoritative match state,
- remote actions are validated by host,
- hidden information is filtered per player,
- authoritative randomness remains on host,
- CPU can occupy a slot without a separate rules implementation,
- disconnect behavior is explicitly defined,
- transport can be replaced without rewriting Cardado rules.

---

## 16. Current non-goals

Do not implement now:

- dedicated servers,
- room discovery services,
- authentication,
- NAT/relay infrastructure,
- host migration,
- reconnect logic,
- anti-cheat systems beyond basic authority boundaries,
- network synchronization code.

The current task is only to keep the architecture compatible with these future requirements.
