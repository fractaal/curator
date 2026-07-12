# North Star — One Ghost, Many Humans (multiplayer wiring)

## Status / scope / grounding

Branch `multiplayer-wiring`, stacked on `agenticcore-swap` (the AgenticCore
ghost-brain transplant — see `NORTH-STAR-agenticcore-swap.md`). It stacks on
the swap because the sensor/tool layer this plan touches (`Sensors.cs`,
`GhostTools.cs`) only exists there.

Grounded in reads of (2026-07-12): `Multiplayer/MultiplayerManager.gd`,
`Multiplayer/RPCUtils.gd`, `Multiplayer/MultiplayerHUD.gd`,
`Scripts/PlayerManager.cs`, `Scenes/Player/player.gd` + `player.tscn`
replication config, `Scenes/Door.gd` + `Door.tscn`, `Scenes/GameLight.gd`,
`Scenes/Enemy.gd` + `Ghost.tscn`, `models/PhysicsObject.gd`,
`Scenes/Tools/SpiritBox.cs`, `Scripts/TargetResolution.cs`,
`Scripts/FearFactor.cs`, `Scripts/EndgameHandler.cs`, `SaySomething.gd`,
`models/GhostGuesser.gd`, `UI.gd`, `levels/node_3d.tscn` (spawner + HUD),
`DeclarativeGameInterface/Sensors.cs` / `GhostTools.cs` / `GhostMind.cs`.

Ben's own framing, which this plan adopts wholesale: *"the designated host
runs the LLM loop and everyone else just basically receives the LLM commands
from the host … player positions and some visuals can be replicated."*

## Product thesis

One LLM brain haunts a crew of humans. The brain is expensive, stateful, and
authoritative — it must run in exactly one place (the host). But horror is
personal: every player must be heard by the ghost, hurt by the ghost, and
killable by the ghost, or the client seats are spectators, not victims.

The core tension: **the LLM layer must never learn that multiplayer exists,
while every human must experience the full single-player contract.** The
agenticcore-swap deliberately kept GhostTools emitting plain local EventBus
signals; this plan keeps that boundary sacred and finishes the plumbing on
the *game* side of it.

## The two membranes (vocabulary)

The v2 wip commits already committed to an architecture with two membranes,
and it's the right one:

1. **Host EventBus = LLM ↔ game membrane.** `EventBus` is a per-process
   autoload; signals never cross the network. The brain (host-only, gated by
   `Multiplayer.IsServer()` in `Sensors.PrepareTurnAndThink`,
   `GhostMind.AuxCompleteAsync`, `NarrativeIntegrity`) reads and writes only
   the host's bus.
2. **RPC + MultiplayerSynchronizer = host ↔ client membrane.** Continuous
   state replicates via `SceneReplicationConfig` (player/ghost
   position+rotation, ghost `manifesting`/`chasing`, door `isOpen`/`locked`,
   holdable transforms). One-shot effects broadcast via server-authority
   RPCs (`Door._handle_open.rpc()`, `RPCUtils.try_rpc_call → *_impl`,
   `Enemy._rpc_chase/_rpc_appear`, `SpiritBox.RpcGhostSpeak`,
   `PhysicsObject` jolt/throw/shift).

Other vocabulary: a **crossing** is a specific EventBus signal that is
explicitly relayed across membrane 2. The **authority peer** of a player is
the machine whose human controls it (`player_id` = peer id, set as
multiplayer authority in `player.gd`). The **crew** is all registered
players on the host's `PlayerManager`.

What already works through these membranes (verified in code, do not
rebuild): ghost movement/appearance/chase broadcast on clients,
doors/lights reacting to both ghost commands and client-player
interactions, physics object torment, holdable pickup with authority
transfer, spirit-box ghost speech TTS on every peer.

Corrected by review (2026-07-12): two claims of "already works" were false.
`Enemy.gd`'s chase target selection helpers (`get_closest_player` etc.)
existed but had ZERO callers — `current_target` was permanently null and a
chase silently no-oped, so the ghost could never actually catch anyone.
`chase()` now acquires the nearest living player. And two
`current_target.has_property("dead")` calls used a method that does not
exist in Godot 4 (runtime error aborting the chase-end path — doors stayed
locked, `ChaseEnded` never fired); both are now `"dead" in current_target`.

---

# User story 1: A client speaks, and the ghost hears

Player 2 (client) presses ENTER, types "is anyone there?", presses ENTER.
In singleplayer this lands in the prompt via `SaySomething.gd` →
`EventBus.PlayerTalked` → `Sensors` accumulator. In multiplayer today it
dies on the client's local bus — **the ghost is deaf to every client.**
Same failure class: `Door.gd interact()` emits `NotableEventOccurred`
("Player opened door in Kitchen") on the *interacting peer's* bus, and
`GhostGuesser.gd` emits `PlayerDecidedGhostType` on the guesser's bus — so
client door usage is invisible and a client's endgame guess does nothing.

This forces the first crossing direction: **inbound sensor relay**
(client → host), because the brain's senses live exclusively on the host
bus.

## Consequence: `Multiplayer/EventBusRelay.cs` (new autoload), inbound half

One tiny autoload owns ALL crossings. Nothing else in the codebase may
relay EventBus signals across the network — one file to audit, one place
the two membranes touch.

New relay/discovery modules are **C#** (Ben's call): `EventBus` is already
C# with generated typed signal delegates, so each crossing is an explicit
typed method pair — wrong arity or a typo'd signal name fails at
`dotnet build`, not at runtime. There is no string-keyed dispatcher at all,
which also removes the "client injects an arbitrary signal name" surface by
construction. Edits to existing GDScript files stay GDScript.

```csharp
// Inbound crossings:  PlayerTalked(string), NotableEventOccurred(string),
//                     PlayerDecidedGhostType(string)
// Outbound crossings: PlayerEffect(string, string, long targetPeer), GameWon(string),
//                     GameLost(string), EndgameSummary(string), GhostBackstory(string)
// INBOUND ∩ OUTBOUND must stay empty (loop safety). One method pair per crossing:

Bus.PlayerTalked += msg => {
    if (Multiplayer.IsServer()) return;       // host emissions already reach the brain
    RpcId(1, MethodName.HostPlayerTalked, msg);
};

[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
void HostPlayerTalked(string msg) =>
    Bus.EmitSignal(EventBus.SignalName.PlayerTalked, msg);  // host bus → brain hears it
```

Invariants:
- `EventBus` itself stays process-local forever. No wholesale sync — the
  bus carries LLM-internal chatter (`SystemFeedback`, turn bookkeeping)
  that must never leak to clients, and wholesale sync invites re-emission
  loops.
- Loop safety is structural, not behavioral: the inbound and outbound
  signal sets are disjoint, and each direction gates on `IsServer()`.
- Only enumerated, explicitly-typed RPC methods exist — nothing generic for
  an untrusted peer to aim at.
- Solo play (host, zero clients): relay is a no-op; behavior byte-identical
  to today.

# User story 2: The ghost torments a *client*

The ghost calls `throwPlayerAround`. `GhostTools.PlayerEffect` emits
`EventBus.PlayerEffect("throwplayeraround", "")` on the host bus. Today the
only listener that reacts in-world is the host's own player (`player.gd`
connects to its local bus, authority-side only) — **clients are immune to
pullPlayerToGhost / throwPlayerAround / dimPlayerFlashlight.** The scariest
tools in the kit silently skip everyone but Ben.

This forces the outbound half of the relay.

## Consequence: EventBusRelay, outbound half

```csharp
Bus.PlayerEffect += (verb, args) => {
    if (!Multiplayer.IsServer()) return;
    if (Multiplayer.GetPeers().Length == 0) return;  // offline/solo: no-op
    Rpc(MethodName.ClientPlayerEffect, verb, args);  // NOT CallLocal — host bus already fired
};

[Rpc(MultiplayerApi.RpcMode.Authority)]
void ClientPlayerEffect(string verb, string args) =>
    Bus.EmitSignal(EventBus.SignalName.PlayerEffect, verb, args);
    // each client's own player/UI reacts locally
```

`PlayerEffect` carries a target: the tools (pullPlayerToGhost,
throwPlayerAround, dimPlayerFlashlight) take an optional `target` argument —
a player number or "all" (default). GhostTools resolves the number to a peer
id via PlayerManager (failing loudly with the valid numbers listed, same
shape as bad room targets); the signal broadcasts to every peer and each
peer's authority player applies the effect only if it is the target (or the
target is everyone). The host bus still fires unconditionally so FearFactor
hears every effect regardless of victim.

## Non-consequence: FearFactor needs no change

`FearFactor` is a host-side global scalar fed entirely by host-bus signals
(GhostAction / PlayerEffect / acks). It is the ghost's *performance
intensity*, not a per-player stat — it keeps working untouched. Per-player
fear is explicitly out of scope.

# User story 3: A client dies, and everyone knows the game ended

The ghost catches Player 2. Today `Enemy.gd:282` calls
`current_target.kill()` as a plain method — on the **server's replica** of
Player 2. The real Player 2's screen never runs the death sequence; they
keep walking around a game they already lost. Then `GameLost` fires on the
host bus only: no endgame UI (`levels/EndGameButtons.gd`) for any client.
The mirror problem: when a client's guess ends the game (Story 1 delivers
`PlayerDecidedGhostType` to the host), the win/lose spectacle runs host-side
— lights/doors already broadcast via the interactable RPCs, but the
`GameWon`/`GameLost` UI signals don't.

## Consequence: `kill()` becomes an RPC to the victim's authority peer

```gdscript
# Enemy.gd (server-side chase logic)
current_target.kill_remote.rpc_id(current_target.get_multiplayer_authority())

# player.gd — NOTE the rpc-mode gotcha: mode "authority" means "only the NODE's
# authority may CALL this", and the node's authority is the victim, not the
# server — the client would reject the server's RPC. So: any_peer + sender guard.
@rpc("any_peer", "call_local")    # call_local keeps host-victim (solo) path identical
func kill_remote():
  if multiplayer.get_remote_sender_id() > 1: return   # only the server may kill
  kill()
```

- `kill()`'s side effects (death cam, mouse release, `ObjectInteraction
  "explode lights all"`) run once, on the victim's machine. The lights
  explosion routes back through the existing `RPCUtils` request path, so
  every peer sees the house go dark — already-built plumbing doing its job.
- Add `dead` to `player.tscn`'s `SceneReplicationConfig`: the server's
  chase logic reads `current_target.dead` (`Enemy.gd:296,388`) and must see
  the client's death state.

## Consequence: `GameWon`/`GameLost` are outbound crossings

Already listed in `OUTBOUND` above. Each client's `EndGameButtons` UI and
any endgame ambience react on their own bus. `Enemy.gd`'s own
GameWon/GameLost handlers run host-side (it is server-authoritative) —
unaffected.

Invariant: mid-game *restart* is a non-goal (see Non-goals). The endgame
buttons on a client may say "quit to desktop" semantics for now; the
implementer should not build scene-reload-in-multiplayer.

# User story 4: The brain meets the crew

Two humans split up — Player 1 in the Basement, Player 2 in the Kitchen.
The prompt the brain reads still says "PLAYER STATUS" (singular): `Sensors`
holds `Player = PlayerMgr.GetFirstPlayer()` and one `Stats`. The ghost
cannot reason about *whom* to stalk because it doesn't know two people
exist. Worse, `TargetResolution.GetTarget("player")` resolves via
`FindChild("Player")` — and since the wip commits renamed spawned players
`Player_1`, `Player_2`, it matches **nothing**: every "player"-targeted
tool quietly broke even in solo-hosted play.

This is the thesis-interesting story: the prompt stops describing a victim
and starts describing a crew the ghost chooses from.

## Consequence: `player_number` — stable human-facing crew numbers

Godot 4 client peer ids are **random 32-bit ints** (only the server is 1) —
"Player 1268801932" is unusable in a prompt, an event log, or a tool target.
So the server assigns a monotonic `player_number` at spawn (1 = host, then
2, 3, ...; never reused within a session), replicated to every peer via the
player's spawn config. Peer ids keep driving authority and RPC routing under
the hood; every human- and LLM-facing surface (crew status, attributions,
tool targets, victim names) speaks in player numbers.

## Consequence: Sensors speak in plural

```csharp
// Sensors.GetGameInfo() — PLAYERS STATUS section
foreach ((id, stats) in PlayerMgr.GetAllPlayerStats()):
    lines += $"Player {id}{(id == 1 ? " (host)" : "")}: {stats.getStatus()}"
// gates: skip-turn only when ALL players dead; attention markers per player
```

- The `Player`/`Stats` singular fields go away; every consumer inside
  Sensors iterates the crew (status, attention markers, dead-gate,
  chase-cadence warnings).
- Speaker identity is attributed **at the source**, not in the relay: the
  emitting site knows who acted (SaySomething and VoiceWebServer look up the
  local player's number; Door/Radio/Switch use the interacting player passed
  to `interact()`; Enemy names the caught victim). Messages relay verbatim,
  so host and client emissions read identically.
- `TargetResolution.GetTarget("player")` → nearest living player to the
  ghost via `PlayerManager` (fuzzy "player" target = "closest victim", the
  most defensible reading). `GhostTools`' `GetPlayer` closure gets the same
  definition.
- Prompt copy (`Main.txt`): one or two lines — multiple investigators may
  be present; "player"-targeted tools affect the nearest one. No new tools;
  per-name targeting is future flavor, not this plan.

## Consequence: clients register the crew too

Only `MultiplayerManager.start_host` ever calls `RegisterPlayer` — clients
have an empty `PlayerManager`, making it a lie off-host (`UI.gd` already
works around it with node-path lookup). Fix at the root: registration keys
off the spawned node arriving, not off who spawned it —
`PlayerSpawnLocation` child-entered (or `player.gd _enter_tree`) registers
into the local `PlayerManager` on every peer, `_exit_tree` unregisters.
`MultiplayerManager`'s explicit register/unregister calls become redundant
and are deleted (one mechanism, not two).

# User story 5: A friend finds the game (lobby + LAN autodiscover)

Ben hosts. His friend on the same LAN opens the game and — today —
nothing: `create_server(4444, **1**)` allows exactly one client,
`set_bind_ip("127.0.0.1")` refuses non-loopback connections, and
`join_server` ignores its `_address` argument and dials `127.0.0.1`. The
current lobby is a loopback dev rig.

## Consequence: unhardcode the plumbing (trivial)

```gdscript
create_server(SERVER_PORT, MAX_CLIENTS)   # 8; drop set_bind_ip (bind all interfaces)
join_server(address):
  client_peer.create_client(address, SERVER_PORT)   # actually use the argument
```

## Consequence: `Multiplayer/LanDiscovery.cs` — UDP broadcast beacon

No external services, no master server. The paved LAN pattern:

```gdscript
# Host, after start_host succeeds — 1 Hz heartbeat:
beacon = PacketPeerUDP.new(); beacon.set_broadcast_enabled(true)
every 1.0s: send JSON {magic, players, max} to 255.255.255.255:DISCOVERY_PORT
            and a second copy to 127.0.0.1:DISCOVERY_PORT
            # broadcast isn't delivered to same-machine listeners; the loopback
            # copy never leaves the host (proven by the two-instance relay test)

# Client, while lobby UI open:
listener = PacketPeerUDP.new(); listener.bind(DISCOVERY_PORT)
poll: collect beacons keyed by sender IP → lobby list entries
      drop entries not re-heard for 3s
```

- `MultiplayerHUD` grows: a lobby `ItemList` (click to join), the existing
  Host button, and a manual-address `LineEdit` + Join.
- The manual field is a hard requirement, not a fallback nicety: UDP
  broadcast does not cross subnets or VPNs, so playing over
  Tailscale/ZeroTier means typing the tailnet IP. Say so in the UI copy.
- Known dev wart (accepted): two instances on one machine contend for
  `DISCOVERY_PORT`; if `listen()` fails, hide the list and show the manual
  field. Loopback testing types `127.0.0.1` like it always has.

# User story 6: A late joiner sees the house as it is

Player 2 joins ten minutes in. The Basement door is open (`isOpen=true`
arrives via the synchronizer's spawn/sync) — but the door *mesh* sits at
its default closed rotation, because rotation is driven by tween callbacks,
not by the synced flag. And `Door.gd _enter_tree` contains a broken attempt
at exactly this fix: `sync_state.rpc_id(multiplayer.get_remote_sender_id())`
runs outside any RPC context (`get_remote_sender_id()` = 0, peer not even
created yet at scene load) — a no-op at best.

## Consequence: server-pushed join sync via the `late_join_synced` group

Delete the `_enter_tree` attempt. The working mechanism is server-driven:
on `peer_connected`, `MultiplayerManager` walks the `late_join_synced`
group and calls `late_join_sync(peer_id)` on each member, which
`rpc_id`s its authoritative state to just that peer. Scripts that own
one-shot state self-register in `_ready` — a group walk over `Interactable`
child nodes was rejected because FloorLamp wraps its parts in a composite
node and ceiling lights aren't in the `interactables` group at all.

Members: **Door** (mesh pose + locked), **GameLight** (isDead — in no
replication config anywhere), **Radio** (power/playback), **Switch**
(handle pose), **Enemy** (ghost identity — its `_rpc_set_ghost_properties`
broadcast fires in `_ready`, before any client exists), and **Sensors**
(the sanitized ghost backstory, generated before anyone joins).

Invariant: transient effects (a flicker in progress) may be missed —
accepted.

## Consequence (discovered during the audit): Radio and Switch weren't multiplayer at all

The wip commits RPC-ified doors, lights, and physics objects — but
`Radio.gd` had **no RPC layer** (ghost-played freaky music was host-only
audio) and `Switch.gd`'s handle flip was local-only. Radio's four verbs now
route through `RPCUtils.try_rpc_call → *_impl` exactly like GameLight;
Switch broadcasts its handle visual the same way while its light
`ObjectInteraction` emission stays on the originating peer (GameLight
already routes and broadcasts that — double-routing would storm).
`VoiceWebServer` also got a guard (second same-machine instance can't bind
port 6900 — voice input degrades gracefully instead of throwing in
`_Ready`).

---

# What this naturally implies per file

| Surface | Change | Story |
|---|---|---|
| `Multiplayer/EventBusRelay.cs` | NEW C# autoload — the only network crossing for EventBus signals; explicit typed method pair per crossing | 1, 2, 3 |
| `Scripts/EventBus.cs` | `PlayerEffect` gains `targetPeerId` (0 = everyone) | 2 |
| `Scenes/Player/player.gd` | `kill_remote()` RPC wrapper (any_peer + server-only sender guard); target filter in `_on_player_effect`; `player_number`; self-registration in PlayerManager on every peer | 2, 3, 4 |
| `Scenes/Player/player.tscn` | add `dead` + `player_number` to replication config | 3, 4 |
| `Scenes/Enemy.gd` | `kill_remote.rpc_id(authority)`; victim named by number; ghost identity in `late_join_synced` | 3, 4, 6 |
| `DeclarativeGameInterface/Sensors.cs` | crew-plural status/markers/gates; host-gated backstory/summary generation; backstory late-join push | 4, 6 |
| `Scripts/TargetResolution.cs` | "player" → nearest living player via PlayerManager | 4 |
| `DeclarativeGameInterface/GhostTools.cs` | targeted player effects; "player" object-target → nearest player's room at the choke point; crew-aware chase gate | 2, 4 |
| `DeclarativeGameInterface/prompts/Main.txt` | PLAYERS section: crew, per-victim targeting | 4 |
| `Scripts/PlayerManager.cs` | nearest-living/number lookups; registration driven by player nodes themselves | 2, 4 |
| `Scripts/FearFactor.cs`, `Scripts/EndgameHandler.cs` | signal arity; host-only endgame gate | 2, 3 |
| `Multiplayer/MultiplayerManager.gd` | MAX_CLIENTS, bind all interfaces, honor join address, free player nodes on disconnect, assign player numbers, drive late-join sync, start beacon | 3–6 |
| `Multiplayer/LanDiscovery.cs` | NEW C# — beacon (broadcast + loopback copy) + listener | 5 |
| `Multiplayer/MultiplayerHUD.gd` + `levels/node_3d.tscn` | lobby list + manual address field | 5 |
| `Scenes/Door.gd`, `Scenes/GameLight.gd`, `Scenes/Radio.gd`, `Scenes/Switch.gd` | attribution by number; Radio/Switch RPC routing; `late_join_synced` membership | 4, 6 |
| `SaySomething.gd`, `Scripts/VoiceWebServer.cs` | speech attributed by player number; voice server port guard | 1, 4 |
| `Multiplayer/RelayTest/*` | NEW two-instance ENet loopback test of relay + discovery | verification |

Explicitly untouched: everything under `AgenticCore/` and
`DeclarativeGameInterface/GhostMind.cs`/`GhostAgent.cs` — the brain never
learns multiplayer exists. `FearFactor`, `SpiritBox`, `RPCUtils`,
`PhysicsObject`, holdables: already correct.

# Acceptance contract & verification evidence

Everything above ships as **one release** — there is no "simpler version
now, better version later" tier anywhere in this plan (Ben's call,
2026-07-12). What follows is what "done" means and how much of it is
already machine-verified on this branch.

**Machine-verified (automated, both green as of 2026-07-12):**
- `godot --headless res://DeclarativeGameInterface/MockTest/GhostMindMockTest.tscn`
  — 9/9: the solo-brain contract (interaction acks, failure feedback,
  speech) **plus** targeted player effects (resolve player 2 → targeted
  signal; bogus player 7 → loud failure listing valid numbers).
- `Multiplayer/RelayTest/run.sh` — two headless instances over real ENet
  loopback: all three inbound crossings reach the host bus; targeted
  `PlayerEffect` + `GameLost` reach the client bus; LanDiscovery beacon
  produces a lobby entry on the client. Both instances must exit 0.
- `dotnet build` clean; the client instance runs with no `settings.txt`.

**Playtest-verified (needs humans and a real house — Ben):**
- Two windowed instances: client speech/door usage shows in the host
  prompt attributed by number; ghost tools shove/dim the right victim;
  ghost catches a client → death cam on the victim's screen, endgame on
  both; client journal guess ends the game on both.
- A second LAN machine sees the lobby within ~2s and joins by click;
  manual IP join still works (Tailscale path).
- Late join: door poses, dead lights, radio playback, ghost identity,
  and backstory all correct on arrival.
- The kill RPC path specifically — it is traced but not covered by the
  automated tests (needs the full player scene + Enemy chase).

# Non-goals

- Internet matchmaking, master server, NAT punchthrough — manual IP over a
  VPN covers remote friends; anything more is infra this thesis doesn't
  need.
- Per-player fear model, per-name tool targeting ("throw Ben around"),
  proximity voice chat — future flavor.
- In-session restart/rematch and host migration — endgame means everyone
  relaunches, like it always has.
- Dedicated/headless servers — the host is always a playing human.
