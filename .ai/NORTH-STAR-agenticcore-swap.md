# North Star: Swap the Ghost's Brain — DeclarativeGameInterface → AgenticCore

**Status:** Approved direction (parity-first scope chosen by default; Ben can widen).
**Worktree:** `.worktrees/agenticcore-swap`, branch `agenticcore-swap` off `origin/v2`.
**Source grounding:** curator `DeclarativeGameInterface/*` (LLMInterface, Interpreter, Sensors, Prompter, prompts), `Scripts/` (EventBus, NarrativeIntegrity, Logger, LatencyStatistics, Config, TargetResolution), `Curator/Curator.tscn`, `levels/node_3d.tscn`, `project.godot`, `log-parser.py` (existence);
AgenticCore @ local `main` 93250ae (`Core/AgenticEntity.cs`, `Core/PersonaAgentBehavior.cs`, `LLM/ReflectionToolSource.cs`, `LLM/ToolAttributes.cs`, `LLM/LLMClient.cs`, `LLM/OpenRouterLLMClient.cs` ctor region, `build/AgenticCore.Agnostic.props`, `PORT-NOTES.md`, `examples/ConsoleAgent`); Emergent-Agentics host wiring (pre-refactor pin — informative only).
**Known unverified:** OpenRouterLLMClient full send path details (read ctor + interface only); MainThread marshaling of tool execution (verify before relying on it — affects whether tools emit signals directly or via `CallDeferred`); Godot 4.3 + net8.0 + `LangVersion latest` build (verify at task 2, it gates everything).

## Product thesis

Curator's ghost is an LLM playing antagonist in a Phasmophobia-like. In 2023 the only way
to get "agentic" behavior was to stream free text and regex `verb(args)` commands out of it
mid-flight. AgenticCore is Ben's 2026 runtime that does this properly: native tool calls,
schema validation, tool results fed back within a turn, retries/rate-limits/caching/cost
tracking. The swap replaces the *transport and interpretation* layer of the ghost's brain
while leaving the *game* — EventBus effects, interactables, rooms, TTS, multiplayer RPCs,
fear pacing — untouched.

**Core tension:** the old design *acts while the model is still talking* (streamed DSL);
the new design *acts when the model calls tools* (per-turn, with real results fed back).
We accept that trade deliberately: shorter responses (no mandated inline CoT), validated
arguments, same-turn error correction — in exchange for losing mid-stream execution.
Everything else preserves observable behavior.

**The version constraint that shapes the mount:** AgenticCore local `main` (93250ae) is
36 commits ahead of GitHub (and 2 behind — the Ollama commits pushed from Emergent-Agentics'
embedded copy; diverged histories). The submodule pins 93250ae with the canonical GitHub URL;
**curator's branch cannot be pushed/cloned-fresh until Ben pushes AgenticCore main.**

## Scenes

### Scene 1 — The AI tick fires: the ghost thinks and acts

Every `SENSOR_READ_INTERVAL` (17.5 s, halved during a chase), if AI is enabled, the player
is alive/inside, the game hasn't ended, and the previous cycle finished, `Sensors` kicks a
think cycle. Today it assembles ~7 messages and fires them at a hand-rolled SSE client;
`Interpreter` regexes commands out of chunks. That entire path is what we swap.

**Consequence — `GhostAgent : IAgenticBehavior` exists** because AgenticCore's loop asks
the *behavior* for context and tools each send. Sensors stays the sensory organ (event
accumulation, status, fear factor, cadence gating) and hands assembly to the behavior:

```
Sensors._Process(delta):
  entity.Process(delta)                      # drives pacing/completion
  if tickDue and gatesPass and entityIdle:
    entity.Think()                           # replaces Interface.Send(messages)

GhostAgent.BuildEphemeralContext(persistent):
  [ system: persona (Main.txt, DSL sections removed)
  , user:   game info (rooms, sounds, backstory)     # stable-ish head
  ] + persistent                                      # rolling turn history
  + [ user: timeline since last prompt + ghost/player status
           + attention markers + fear factor + system feedback ]  # volatile tail

GhostAgent.GetAvailableTools(): ReflectionToolSource(GhostTools).GetTools(null)
```

**Non-consequence — the per-cycle summarizer does not survive.** Today every full response
is summarized by the AUX model into a rolling `History` (because responses were long CoT
walls). New-world responses are tool calls plus a short line; `PersistentContext` +
`MaxHistoryMessages` autocompaction bound memory natively. One less LLM call per cycle.
This is a deliberate deviation from literal parity; reversible if the feel regresses.

**Non-consequence — `Prompter.cs` gets no replacement.** It is attached to no scene and its
debug node path doesn't exist in the current level; it was already dead.

Invariants: no LLM request unless `Multiplayer.IsServer()`; think cadence and its gates
(AIEnabled, dead player, ended game, busy loop) behave exactly as today; chase still
doubles the tick rate.

### Scene 2 — The ghost acts on the world (and sometimes on things that aren't there)

The model calls `flickerlights(kitchen)`. Today the Interpreter validates the room via
`TargetResolution`, emits `ObjectInteraction("flicker","lights","kitchen")`, and if no
interactable acknowledges it by end-of-response, a `SystemFeedback` message scolds the
model *next* cycle.

**Consequence — `GhostTools` exists**: one plain class, one `[Tool]` method per verb in
Interpreter's exact verb set (16 object-interaction verbs; ghost actions incl. move/chase/
appear/deposit/sounds/chimes; 3 player effects; `amendSystemFeedback` kept). Each method
emits the **same EventBus signals with the same arguments** so every consumer downstream
(interactables, RPCs, GhostData, endgame) is untouched.

**Consequence — the acknowledgment handshake becomes a synchronous tool result.** The
game's answer to "did that exist?" already arrives via `ObjectInteractionAcknowledged`
within a frame or two. Tool execution is async, so:

```
GhostTools.flickerLights(room):
  if not TargetResolution.IsValidTarget(room): return Fail("TARGET DOESN'T EXIST: ...")
  emit ObjectInteraction("flicker","lights",room)
  await up to N frames for ObjectInteractionAcknowledged match
  return acked ? Ok("flickered lights in " + room)
               : Fail("OBJECT DOESN'T EXIST: no lights in " + room + " — see ROOM INFORMATION")
```

The model now hears about a bad target *in the same turn* and can retry — the entire
`InvalidCommandsSet` bookkeeping in Interpreter dies.

**Consequence — the pacing nudges re-home.** Interpreter's end-of-response housekeeping
(evidence-deposit cadence, chase cadence, repetition warnings, "no commands performed")
is game design, not transport. It moves into `GhostAgent.OnThinkingCompleted`, feeding the
same `SystemFeedback` accumulator with the same strings.

Invariants: signal names/arg shapes unchanged; target validation identical
(`NormalizeTargetString` + `IsValidTarget`); chase still force-locks the entrance;
`speakasghost`-style argument sanitization preserved where it exists today.

### Scene 3 — The ghost speaks

`speakAsGhost(message)`: today the message passes `NarrativeIntegrity.CheckIntegrityForAudio`
(AUX-model sanitize), gets character-filtered, then `GhostTalked` drives TTS. Same flow, now
inside the tool method (it is already `async`). The spoken line arrives as one complete
string either way — TTS never consumed the token stream — so nothing user-facing changes.

### Scene 4 — The world happens between turns

Players talk (voice), doors slam, evidence appears. Sensors already accumulates
`NotableEvents`/`SystemFeedback` with dedup windows. Those keep accumulating exactly as now;
the only change is where they're flushed: `OnBeforeRequestSubmit` — AgenticCore's documented
hook for exactly this ("game-side event bus accumulator") — folds "timeline since last
prompt" into the send. Late events ride the next turn, never lost.

### Scene 5 — Game start and game end need one-shot, off-persona LLM calls

Backstory generation (2 calls at start), endgame summary (on loss), speech sanitization —
all currently `SendIsolated` on the AUX model.

**Consequence — a one-shot completion helper over AgenticCore's `LLMClient`** (wrap
`SendWithIndefiniteRetry` with no tools in a `TaskCompletionSource`).

**Consequence — likely small upstream change:** `OpenRouterLLMClient` reads `MODEL` from
`AgentConfig` at construction; the aux path needs a different model. The boring fix is an
optional ctor override (`model`, `temperature`) upstream in AgenticCore — generic, tiny,
committed in AgenticCore's repo (dependencies point inward; curator just passes
`Config.Get("AUX_MODEL")`). If the client turns out to re-read config per-send, the fix
adjusts accordingly — decided at implementation, not guessed now.

Invariant: `settings.txt` remains the single player-facing config file. A
`CuratorConfigProvider : IConfigProvider` bridges it to `AgentConfig.Current`
(`API_KEY→OPEN_ROUTER_API_KEY`, `MODEL→MODEL`, `MODEL_TEMPERATURE→TEMPERATURE`, …).
Missing settings file keeps its current warning behavior.

### Scene 6 — Ben reads the data (the thesis instrumentation must not die)

`Logger.cs` writes session logs (`LLMPrompted`, `LLMFullResponse|…`,
`InterpreterCommandRecognized|…`) that `log-parser.py` consumes; `LatencyStatistics` and
`ModeReadout` render live timing.

**Consequence — the new runtime emits the same EventBus lifecycle signals:**
`LLMPrompted` when a send starts; `LLMFirstResponseChunk`/`LLMLastResponseChunk`/
`LLMFullResponse` once, at response completion (chunk-level streaming is gone; "first
chunk" latency collapses to completion latency — accepted, documented here);
`InterpreterCommandRecognized` per executed tool call, rendered as `verb(args)` so the
log format stays parseable.

### Scene 7 — The operator pokes the ghost (debug)

`CommandLine.gd` fake-streams typed text as LLM chunks — meaningless once nothing parses
chunks. It repoints to injecting the typed text as an operator note into the entity's
context (`entity.AddMessage(user, "[OPERATOR] …")`), which is strictly more useful for
testing and keeps the debug affordance alive.

### Scene 8 — A multiplayer client joins

Nothing changes. Only the server constructs/ticks the `AgenticEntity` (same
`Multiplayer.IsServer()` gates that guard `Send` today); effects propagate through the
existing interactable-level RPCs. Client builds compile the same code but never think.

### Scene 9 — Mounting the runtime

**Consequence — submodule + agnostic-props import, not a copy, and no Godot-tier compile:**

```
git submodule add https://github.com/fractaal/AgenticCore AgenticCore
(fetch 93250ae from local clone; checkout)          # unpushed-commit workaround

G4fps.csproj:
  net8.0, LangVersion latest                        # AgenticCore uses C# 11/12 syntax
  <Compile Remove="AgenticCore/**/*.cs" />          # kill the default glob
  <Import Project="AgenticCore/build/AgenticCore.Agnostic.props" />  # re-include agnostic set
```

We compile **only** the engine-agnostic subset. AgenticCore's Godot tier (its
`Interactable`, `TargetResolution`, `Vision`, `TelemetryClient`) is *not* used — curator
keeps its own room/object model — so curator stays on Godot 4.3 with zero 4.6-API risk.

**Consequence — a bootstrap autoload (`GhostMindBootstrap`) exists** because the
engine-agnostic core needs its five seams wired before the first entity (per PORT-NOTES,
never yet exercised by a real Godot host — small integration fixes are expected and go
upstream):

```
_Ready():
  AgentLog.Current   = GodotLogger()                  # GD.Print/PrintErr/PushWarning
  MainThread.AttachPump(ProcessFrame drain)
  AgentConfig.Current = CuratorConfigProvider()       # settings.txt bridge
  AgentLLM.Factory    = OpenRouterFactory()           # + RateLimitedLLMClient wrap
  # AgentTelemetry: CoreEconomics default already records tokens/$ — free
```

### What dies

`LLMInterface.cs`, `Interpreter.cs`, `Prompter.cs`, `FailureStatistics.cs` (verify no
callers first), Summarizer prompts + per-cycle summarize pass, the `Curator` autoload's
ReasoningEngine scene (or the autoload entirely if nothing else lives there), the DSL
sections of `Main.txt`/`BehaviorPrompt.txt`, and the `FuzzySharp` verb-rescue path
(FuzzySharp the package stays only if `FuzzySharpGodotBridge`/TargetResolution still uses it — verify).

## As-built deviations (post-review, 2026-07-12)

- **Scene 4's `OnBeforeRequestSubmit` flush is not used.** Sensors snapshots the volatile turn
  context once per think dispatch instead (stable across re-prompts; mid-turn events ride the
  next turn — exactly the old single-shot semantics). More faithful than the hook.
- **The "AI director performed no commands!" nudge is replaced** by AgenticCore's built-in
  `WarnOnNoToolCalls` (in-context, same-turn delivery). Nothing instruments the legacy string.
- **`FailureStatistics.BaseFailures` goes permanently quiet**: "command doesn't exist" is
  structurally impossible under schema-constrained tool calls — itself a thesis-relevant result.
- **Debug readouts degrade cosmetically**: ModeReadout's INTERPRET phase is invisible and the
  latency bar snaps, because chunk signals now fire once, same-frame, at completion. Accepted;
  there is no streaming phase left to indicate.
- **The legacy player-effect false-error bug is not reproduced** (old Interpreter emitted a
  spurious "command does not exist" after every player-effect command — missing `continue`).

## Non-goals (V1)

Ghost `MemoryIndex`, telemetry viewer wiring, AgenticCore Godot-tier interactable adoption,
prompt tuning beyond DSL removal, Chutes/Codex/Ollama backends, editor upgrade past 4.3.

## Acceptance contract

1. `dotnet build` green in the worktree (Godot 4.3 SDK, net8.0).
2. **Mock end-to-end, no network:** headless scene boots bootstrap + entity with a
   scripted `MockLLMClient`: `flickerlights(kitchen)` → `ObjectInteraction` signal
   observed with exact legacy args; `speakasghost(...)` → `GhostTalked` observed;
   a bad-target call returns a Fail tool result containing "DOESN'T EXIST";
   `InterpreterCommandRecognized` fired per call. Exit 0 prints PASS.
3. **Signal-parity:** session log from the mock run contains `LLMPrompted` and
   `LLMFullResponse|…` lines in the format `log-parser.py` expects.
4. `grep` proves no live references to deleted classes/signals remain.
5. Server-only guarantee unchanged (inspection: every Think/aux path behind IsServer).
6. **Live smoke (requires Ben / API key):** in a real run, ghost acts within one
   sensor interval, speaks via TTS, and OpenRouter costs appear via CoreEconomics logs.
   Explicitly *not* claimable by the implementing agent without a live run.

## Rollback

Everything lands on branch `agenticcore-swap`; v2 untouched. Revert = don't merge.
The submodule pin is the only cross-repo coupling, and it's read-only.
