# North Star — The Ghost Writes Its Own Instincts (HuntTick)

## Status / scope / grounding

Branch `hunt-tick`, stacked on `multiplayer-wiring` (which stacks on
`agenticcore-swap`). Design conversation with Ben, 2026-07-12.

Grounded in reads of: curator `Scenes/Enemy.gd` (the current chase loop, its
exact mechanics verified line-by-line), `DeclarativeGameInterface/Sensors.cs`
/ `GhostTools.cs` / `GhostMind.cs` (the AgenticCore brain integration), and —
the reference implementation this plan ports —
`~/CodeProjects/Emergent-Agentics/StarshipSandbox/Starship/Scripts/ComputerCore.cs`
(1815 lines, Jint host), `StarshipAgent.cs` (slow-brain cadence + PokeThink),
`Scripts/ComputerCoreLinter.cs` (695 lines, pure static text analysis,
portable), `Interactables/ComputerCoreInteractable.cs` (the script-authoring
tool surface).

Two design rulings from Ben are LAW in this document:

1. **No quality tiers** — everything here ships together (standing ruling).
2. **No fallback, anywhere** (2026-07-12). If the HuntTick doesn't compile,
   the hunt is blocked. If it errors mid-hunt, the hunt aborts. The old
   autopilot chase does not survive as a safety net — it gets deleted.

## Product thesis

The 2023 hunt was an admission of weakness: LLMs couldn't handle
second-scale urgency, so `chasePlayerAsGhost` fired a deterministic
autopilot — the ghost's nav target set to the player's exact position at
10Hz, omniscient through walls, kill at 1.25m, rubber-banded speed. The LLM
"directed" a hunt it had zero influence over while the prompt screamed at it
to spam VFX.

The 2026 resolution is not "put the LLM in the 10Hz loop" — latency makes
that impossible forever. It is a **cortex/reflex split**: the LLM *authors*
a JavaScript `update(ctx, api, inputs, state)` function — its hunting
instincts — and the engine executes it at tick rate. The model pays its
latency when latency is free (between hunts, writing and patching code) and
the hunt runs on the reflexes it wrote. Intelligence stops being reaction
speed and becomes *authorship*: a Demon writes relentless pursuit, a Shade
writes skittish ambush, and after a failed hunt the ghost patches its own
instincts. The thesis arc writes itself: 2023 — the model triggers a
scripted hunt because it can't handle urgency; 2026 — the model programs
the monster's nervous system and debugs it from experience.

The core tension: **the reflex must be fast and the knowledge must be
imperfect.** Give the tick true player positions and it just re-implements
the omniscient autopilot in JS — nothing gained. The engine therefore feeds
the script only what the ghost *senses*.

## Vocabulary

- **Cortex** — the existing AgenticCore think loop (Sensors + GhostAgent +
  GhostTools). Slow, expensive, smart.
- **Reflex / HuntTick** — the LLM-authored JS `update(ctx, api, inputs,
  state)` executed by the engine during hunts. Fast, free, exactly as smart
  as the cortex made it.
- **HuntCore** — the C# Jint host that compiles, budgets, ticks, and
  sandboxes the script. A port of ComputerCore's generic machinery.
- **Stimulus** — an engine-computed sensed event (footsteps heard, voice
  heard, door moved, line of sight). The ONLY knowledge the reflex gets.
- **Instincts** — Ben-facing name for the installed script; the prompt uses
  this word.

---

# User story 1: The ghost prepares its instincts

Early in the match — house quiet, players unpacking — the ghost writes its
hunting instincts. The behavior prompt makes this a mandatory early beat
("before you can hunt, you must have instincts"), and the existing
chase-cadence nag in Sensors extends naturally: where it now says "you
haven't chased in 100s", with no script installed it says "you have NO HUNT
INSTINCTS INSTALLED — write them with setHuntScript."

This forces the script-authoring tool surface, ported from
ComputerCoreInteractable:

## Consequence: script tools on GhostTools

```
setHuntScript(source)        — install/replace the full update() source.
                               Compile-gated: bad source is REJECTED with the
                               compile error; the previously-installed script
                               (if any) is untouched.
patchHuntScript(old, new)    — surgical edit, literal Edit-tool semantics:
                               fails loudly on missing/ambiguous old_string,
                               running script UNCHANGED on any failure.
getHuntScript()              — read back current source.
setHuntInputs(schemaJson)    — the model defines its OWN tuning knobs
                               (aggression, patience, preferred prey...) it
                               can then adjust without rewriting code.
```

Lint warnings (ported ComputerCoreLinter — pure text analysis) ride along in
the tool result: the script installs, but the model is told about JS
footguns immediately.

Invariants:
- Compile failure can therefore only exist at install time. Mid-game the
  ghost has either NO script or a VALID compiled script — never a broken one.
- The API docs block (`api` surface, ctx schema, inputs schema) lives in the
  cached system prompt, exactly like ComputerCore does, so the model isn't
  charged full price for multi-KB docs every turn.

# User story 2: A hunt runs on reflexes

The ghost calls `chasePlayerAsGhost` (verb name kept — FearFactor, logs, and
log-parser.py key off it). NEW gate, per Ben's no-fallback ruling:

```csharp
if (!HuntCore.HasCompiledScript)
    return Results.FailText(
        "HUNT BLOCKED: you have no hunting instincts installed. "
      + "Write your update() script with setHuntScript first.");
```

With a script installed, the engine enters **hunt mode** — a small
deterministic state machine that OWNS the rules while the script owns the
behavior:

- Engine-owned, script-untouchable: 5s grace period, 30–45s hunt timer,
  contact-kill at 1.25m (moves from the deleted chase loop into Enemy.gd's
  `_physics_process`, gated on hunt mode), ghost speed clamped to
  `[min, max]`, entrance lock, `ChaseStarted`/`ChaseEnded`, door unlock at
  hunt end.
- Script-owned, per tick (~10Hz, budgets below):

```js
function update(ctx, api, inputs, state) {
  // ctx — SENSED DATA ONLY:
  //   ctx.stimuli          — [{kind: "footsteps"|"voice"|"door"|"light",
  //                            room, direction, ageSec, player?}, ...]
  //   ctx.lineOfSight      — {player, distance, heading} | null   (the
  //                           existing LineOfSightCheck raycast, finally
  //                           feeding the brain instead of just the UI)
  //   ctx.lastKnown        — [{player, room, ageSec}]  (decays; goes stale)
  //   ctx.self             — {room, position, speed}
  //   ctx.hunt             — {remainingSec, gracePeriod}
  // api — the reflex's hands:
  //   api.moveToward({room}|{lastKnownOf: 2})   — nav target request
  //   api.lungeAt({player: 2})                  — LOS-gated speed burst; the
  //                                               commit that gets you killed
  //   api.endHunt({})                           — break off early
  //   api.<any GhostTool>({...})                — the FULL tool registry
  //                                               bridges in (slam the door
  //                                               behind you, kill the lights
  //                                               in the room you enter)
  //   api.promptLLM(reason, urgent)             — wake the cortex (cooldown)
  //   api.log(msg)                              — debrief breadcrumbs
  // state — persists across ticks AND across hunts. This is where learning
  //          lives ("player 2 hides in closets").
}
```

**Never in ctx: true player positions.** Stimuli are synthesized host-side
from state the host already has — replicated player velocity/position gives
"running footsteps, close, toward the Basement" (speed above walk threshold
within earshot radius); `PlayerTalked` (already relayed from clients by
EventBusRelay) gives "a voice from the Garage"; door/light events already
flow through the host bus with room + attribution. **Zero new multiplayer
wiring** — every sense already lives host-side, and with a crew, choosing
whom to stalk emerges from stimulus weighing, which is exactly the
intelligence we want on display.

## Consequence: the autopilot chase loop is DELETED

`Enemy.gd`'s 10Hz `update_target_location(player.global_position)` loop —
the omniscient tracker — is removed, not demoted. One hunt path exists.

## Non-consequence: the endgame execution is not a hunt

`chase("end")` — the wrong-guess unbound-ghost cinematic (speed 35,
unbounded timer) — has no LLM in the loop and stays as its own small
deterministic path. The HuntTick governs real hunts only.

## Consequence: HuntCore, the ported Jint host

`Jint 3.0.0` (one PackageReference, no Godot coupling — same version
Emergent-Agentics ships). HuntCore ports ComputerCore's generic ~60%:
tick scheduler, compile/patch plumbing, budget enforcement
(`MaxStatements≈10k`, `MaxRuntimeMs≈250`, `MaxActionsPerTick≈6` — excess
actions dropped WITH a warning fed back to the cortex), worker-thread
execution with concurrent-queue marshaling of tool calls to the main
thread, `promptLLM` cooldown, the `api.<toolName>` shorthand generation
from tool specs, and the status block for the prompt. The starship-specific
~40% (ctx builders) is rewritten as ghost senses. Linter ports
near-verbatim.

# User story 3: The reflex wakes the cortex

Mid-hunt, the script hits something it wasn't written for — both players
vanish and go silent, or `inputs.callForJudgment` conditions trigger — and
calls `api.promptLLM("lost all contact for 10s; both players silent",
urgent=true)`. The cortex gets an early think with the hunt status in
context and can `patchHuntScript` **while the hunt is still running** (the
patch compile-gates; a bad patch changes nothing) or `endHunt` or redirect.

This forces a poke path in Sensors mirroring StarshipAgent.PokeThink: an
early `PrepareTurnAndThink()` outside the normal cadence, coalesced so N
pokes during one in-flight think collapse to one follow-up. Cooldown
engine-side (`PromptCooldownSec`) so a screaming script can't burn tokens.

# User story 4: A hunt dies to its own bug

Tick 40, the script dereferences a stimulus shape it didn't expect and
throws. **Per the no-fallback ruling: the hunt aborts immediately.** Engine
exits hunt mode — ghost breaks off, doors unlock, `ChaseEnded` fires — and
the error, offending line, and tick number land in the cortex's next
context as loud SystemFeedback.

In-game this reads as horror-neutral (the ghost "lost the scent"). In the
thesis it reads as *data*: a hunt that died to the ghost's own buggy
reflexes is a measurement of the model's code quality under pressure, not
an embarrassment to paper over. The pressure to write robust instincts IS
the experiment.

Invariants:
- Abort is total: no partial hunt-mode residue (speed reset, skeleton
  visibility, entrance unlock — the same cleanup path as timer expiry).
- The broken script STAYS INSTALLED (it compiled; it has a runtime bug).
  The next `chasePlayerAsGhost` is not blocked — it will run, and likely
  abort again, until the cortex patches it. The feedback loop closes
  through SystemFeedback + FailureStatistics, the same channel as every
  other ghost failure. No silent disable.

# User story 5: The ghost learns from a failed hunt

The hunt ends — timer, abort, kill, or `endHunt`. The engine assembles a
**hunt debrief** into the cortex's persistent context: outcome, duration,
tick count, the stimuli timeline, the actions taken (`api.log`
breadcrumbs), and the error if one aborted it. The cortex reviews and
patches: "Player 2 breaks line of sight at doorways — next time, lock the
door of the room I'm entering." `state` persisted across hunts carries the
cheap version of memory; script patches carry the durable version.

This is the paper's money shot: diffable, timestamped evolution of the
monster's behavior authored by the monster.

---

# What this naturally implies per file

| Surface | Change | Story |
|---|---|---|
| `G4fps.csproj` | + `Jint 3.0.0` | 2 |
| `DeclarativeGameInterface/HuntCore.cs` | NEW — ported Jint host: compile/patch, budgets, tick, worker marshaling, api bridge from tool specs, promptLLM cooldown, status block | 1–4 |
| `DeclarativeGameInterface/HuntCoreLinter.cs` | NEW — near-verbatim ComputerCoreLinter port | 1 |
| `DeclarativeGameInterface/GhostTools.cs` | + setHuntScript / patchHuntScript / getHuntScript / setHuntInputs; compile gate on chasePlayerAsGhost | 1, 2 |
| `Scenes/Enemy.gd` | DELETE autopilot chase loop; hunt-mode state machine (grace, timer, contact-kill in `_physics_process`, speed clamps, cleanup); movement primitives for api (moveToward/lunge); keep `chase("end")` cinematic | 2, 4 |
| `DeclarativeGameInterface/Sensors.cs` | stimulus synthesis (footsteps from replicated velocity, voice, doors); PokeThink-style early think with coalescing; hunt debrief message; api-docs block in cached system prompt | 2, 3, 5 |
| `DeclarativeGameInterface/prompts/Main.txt` + `BehaviorPrompt.txt` | HUNTING section: instincts-first beat, what ctx/api are, replace the "GO CRAZY" hunt markers (the cortex is mostly idle during a hunt unless poked) | 1, 3 |
| Mock tests | headless: install known script → synthetic stimuli → assert nav/lunge decisions, budget enforcement, abort-on-throw, compile gate failure | all |

Explicitly untouched: `Multiplayer/*` (all senses are host-side already),
`AgenticCore/` submodule, kill adjudication (contact physics, engine-side
forever).

# Acceptance contract

Machine-verifiable (headless, deterministic — the reflex is a pure function
of synthesized ctx):
- `setHuntScript` with broken JS → Fail with compile error, nothing
  installed; `chasePlayerAsGhost` → "HUNT BLOCKED" Fail.
- Valid script installed → hunt enters; scripted stimuli produce the
  expected `moveToward`/`lungeAt` sequence; action budget drops the 7th
  call with a warning.
- Script that throws on a crafted stimulus → hunt aborts, cleanup complete
  (doors unlocked, `ChaseEnded` observed), error surfaced in SystemFeedback.
- `patchHuntScript` with ambiguous old_string → Fail, running script
  byte-identical.
- Existing suites stay green: GhostMindMockTest, RelayTest.

Playtest-verifiable (Ben): whether an actual model writes instincts that
hunt *well*, whether promptLLM moments feel eerie rather than laggy, and
whether the debrief→patch loop produces visible behavioral evolution across
a session. That last one is the thesis result.

# As-built deviations (2026-07-12, implementation session)

- **Main-thread execution only.** ComputerCore's worker-thread runtime was not
  ported: it exists for fleets of ships ticking concurrently; curator has one
  ghost with a tiny script under hard budgets (`MaxRuntimeMs` 50 — worst case
  a 3-frame hitch, normal case sub-millisecond). The worker machinery is the
  single biggest chunk of ComputerCore and it buys nothing here.
- **Linter written fresh, not ported.** ComputerCoreLinter's checks are
  starship doctrine (shield faces, weapon gating). `HuntScriptLinter` keeps
  the structure and the transferable spirit with hunt-domain rules: HNT00
  multiple update() definitions, HNT01 script never acts, HNT02 blind hunting
  (never reads ctx senses), OBS01 no api.log. `ScriptPatcher` ported verbatim.
- **Inputs simplified**: schema JSON stored verbatim + values typed by parse
  (number/bool/string), instead of ComputerCore's full typed-definition
  machinery. Same tool surface, ~150 fewer lines.
- **Lunge mechanics** (engine constants in Enemy.gd, playtest-tunable): hunt
  speed 3.0 slow / 3.8 fast, lunge 6.5 for ≤2.5s with 4s cooldown; during a
  lunge the ghost tracks the victim only while LOS holds, then runs to the
  last seen point. The old omniscient distance rubber-band is deleted with
  the autopilot.
- **current_target reinterpreted as the LOS probe** (nearest living player):
  it drives `inLineOfSight` (player heartbeat UI — already replicated in
  Ghost.tscn — and the lungeAt gate), never pursuit knowledge. While hunting,
  the ghost faces its *movement direction* unless it actually sees someone —
  facing through walls telegraphed knowledge it doesn't have.
- **Runtime abort keeps the session debriefing**: `hunting` (ticking) and
  `inSession` (ChaseStarted→ChaseEnded) are separate flags, so a crashed hunt
  still delivers its debrief — crashed hunts need it most.
- **Endgame execution got its own RPC broadcast** (`start_endgame_execution`)
  — the old `chase("end")` ran host-only, so clients never saw the wrong-guess
  cinematic at all (pre-existing gap, fixed in passing). It now kills the crew
  sequentially, nearest first.
- **Dead code deleted**: the `settargetasghost` verb branch (no tool ever
  emitted it), `set_target()`, `get_random_player()`.
- promptLLM(urgent=false) lands as a NotableEventOccurred (rides the next
  scheduled turn); urgent=true additionally pokes an early think.

# Non-goals

- **Fallback of any kind** — ruled out. No autopilot resurrect, no
  "default script we ship", no silent recovery. (Shipping a default script
  would also poison the experiment: the point is that the MODEL writes it.)
- Ambient (non-hunt) reflex ticks — the ctx/api design shouldn't preclude
  it, but this plan is hunts only.
- Letting the script adjudicate kills, timers, or speed limits.
- Script persistence across game sessions (a fresh ghost writes fresh
  instincts; cross-session memory is a different feature).
