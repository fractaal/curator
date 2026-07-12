using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Godot;
using Jint;
using Jint.Runtime;
using JintEngine = Jint.Engine;

/// <summary>
/// The ghost's reflex layer: a Jint host for the LLM-authored HuntTick —
/// `update(ctx, api, inputs, state)` — executed at ~10Hz while a hunt is active.
/// Port of the generic machinery from Emergent-Agentics' ComputerCore (compile/patch
/// plumbing, statement/time/action budgets, api bridge generated from tool specs,
/// promptLLM cooldown), with the starship context builders rewritten as ghost senses.
///
/// Design law (North Star): ctx carries SENSED data only — never true player
/// positions. Stimuli are synthesized here, host-side, from replicated state.
///
/// No-fallback ruling: a runtime error ABORTS the hunt (total cleanup via Enemy.gd)
/// but the script stays installed and enabled — the next hunt runs it again until
/// the cortex patches it. (Deviation from ComputerCore, which disables on error.)
///
/// Runs host-side only: created by Sensors.EnsureMind, which is IsServer-gated.
/// </summary>
public partial class HuntCore : Node
{
	// ---- Engine-owned budgets and tunables (script-untouchable) ----
	private const double TickIntervalSec = 0.1;
	private const int MaxStatements = 10_000;
	private const int MaxRuntimeMs = 50;
	private const int MaxActionsPerTick = 6;
	private const double PromptCooldownSec = 8.0;

	private const float WalkSpeedThreshold = 2.0f;
	private const float RunSpeedThreshold = 5.0f;
	private const float WalkHearingRange = 12f;
	private const float RunHearingRange = 24f;
	private const double FootstepDedupSec = 1.0;
	private const double StimulusExpirySec = 10.0;
	private const double LastKnownExpirySec = 45.0;
	private const uint LineOfSightMask = 11; // same layers as Enemy.gd's LineOfSightCheck
	private const float LineOfSightRange = 25f; // ghosts don't see forever, even unobstructed

	// Tools the reflex may NOT call — cortex-domain (script management, hunt start,
	// feedback bookkeeping). Everything else in GhostTools bridges into api.*.
	private static readonly HashSet<string> ScriptBlockedTools = new(StringComparer.Ordinal)
	{
		"setHuntScript",
		"patchHuntScript",
		"getHuntScript",
		"setHuntInputs",
		"setHuntInputValue",
		"getHuntInputs",
		"chasePlayerAsGhost",
		"amendSystemFeedback",
	};

	private EventBus Bus;
	private ReflectionToolSource Source;
	private Sensors Brain;
	private Node3D Ghost;
	private PlayerManager Players;

	private JintEngine engine;
	private string scriptSource = "";
	private string lastError = "";
	private string lastLog = "";
	private bool compiled;

	private bool hunting; // ticking enabled (false after an abort, mid-session)
	private bool inSession; // between ChaseStarted and ChaseEnded — owns the debrief
	private double nextTickAt;
	private double nextSenseAt;
	private long tickCount;
	private bool executingTools;
	private bool budgetDroppedThisTick;
	private List<(string Name, string ArgsJson)> queuedTools;
	private List<string> lastActionReports = new();
	private double lastPromptAt = -999;

	// The model's self-defined tuning knobs: schema JSON verbatim + current values.
	private string inputsSchemaJson = "";
	private readonly Dictionary<string, JsonNode> inputValues = new(StringComparer.Ordinal);

	// ---- Senses ----
	private sealed class Stimulus
	{
		public string Kind; // footsteps | voice | door | switch
		public string Detail;
		public string Room;
		public int Player; // 0 = unidentified
		public Vector3 Direction;
		public double AtSec;
	}

	private readonly List<Stimulus> stimuli = new();
	private readonly Dictionary<int, (string Room, double AtSec)> lastKnown = new();
	private readonly Dictionary<int, Vector3> lastObservedPositions = new();
	private readonly Dictionary<int, double> lastFootstepAt = new();

	// This tick's ground-truth LOS (engine knowledge, used to gate lungeAt).
	private int losPlayerNumber;
	private float losDistance;
	private Vector3 losDirection;

	// ---- Debrief ----
	private readonly List<string> huntLog = new();
	private const int HuntLogCap = 120;
	private double huntStartedAtSec;

	public bool HasCompiledScript => compiled;
	public string LastError => lastError ?? "";

	public void Setup(EventBus bus, GhostTools tools, Sensors brain, Node3D ghost)
	{
		Bus = bus;
		Source = new ReflectionToolSource(tools);
		Brain = brain;
		Ghost = ghost;
		Players = PlayerManager.Get();

		Bus.PlayerTalked += OnPlayerTalked;
		Bus.NotableEventOccurred += OnNotableEvent;
		Bus.ChaseStarted += OnChaseStarted;
		Bus.ChaseEnded += OnChaseEnded;
	}

	// ------------------------------------------------------------------
	// Script lifecycle (compile-gated; a broken script can never be installed)
	// ------------------------------------------------------------------

	public string GetScriptSource() => scriptSource ?? "";

	public bool TrySetScriptSource(string source, out string error)
	{
		var previousSource = scriptSource;
		var previousEngine = engine;
		var previousCompiled = compiled;

		scriptSource = source ?? "";
		if (!TryCompile(scriptSource, out error))
		{
			scriptSource = previousSource;
			engine = previousEngine;
			compiled = previousCompiled;
			return false;
		}

		lastError = "";
		return true;
	}

	public bool SetInputSchemaJson(string schemaJson, out string error)
	{
		error = "";
		inputValues.Clear();
		inputsSchemaJson = "";

		if (string.IsNullOrWhiteSpace(schemaJson))
		{
			return true;
		}

		JsonNode root;
		try
		{
			root = JsonNode.Parse(schemaJson);
		}
		catch (Exception e)
		{
			error = "Input schema JSON could not be parsed: " + e.Message;
			return false;
		}

		var inputs = root is JsonArray arr ? arr : root?["inputs"] as JsonArray;
		if (inputs == null)
		{
			error = "Input schema must be a JSON array of inputs or an object with an 'inputs' array.";
			return false;
		}

		foreach (var entry in inputs)
		{
			var name = entry?["name"]?.GetValue<string>();
			if (string.IsNullOrWhiteSpace(name))
			{
				error = "Every input needs a 'name'.";
				return false;
			}
			inputValues[name] = entry["default"]?.DeepClone();
		}

		inputsSchemaJson = schemaJson;
		return true;
	}

	public string GetInputsJson()
	{
		var values = new JsonObject();
		foreach (var kv in inputValues)
		{
			values[kv.Key] = kv.Value?.DeepClone();
		}
		return new JsonObject { ["schema"] = string.IsNullOrWhiteSpace(inputsSchemaJson)
				? null
				: JsonNode.Parse(inputsSchemaJson), ["values"] = values }.ToJsonString();
	}

	public bool SetInputValue(string key, string value, out string error)
	{
		error = "";
		if (string.IsNullOrWhiteSpace(key) || !inputValues.ContainsKey(key))
		{
			error = $"Unknown input '{key}'. Define it with setHuntInputs first. "
				+ $"Known inputs: {string.Join(", ", inputValues.Keys)}";
			return false;
		}

		if (double.TryParse(value, out var number))
		{
			inputValues[key] = number;
		}
		else if (bool.TryParse(value, out var flag))
		{
			inputValues[key] = flag;
		}
		else
		{
			inputValues[key] = value ?? "";
		}
		return true;
	}

	private bool TryCompile(string source, out string error)
	{
		error = "";
		compiled = false;
		engine = null;

		try
		{
			engine = new JintEngine(options =>
			{
				options.Strict();
				options.MaxStatements(MaxStatements);
			});
			engine.SetValue("__enqueueTool", new Action<string, string>(EnqueueToolFromScript));
			engine.SetValue("__moveToward", new Action<string>(MoveTowardFromScript));
			engine.SetValue("__lungeAt", new Action<string>(LungeAtFromScript));
			engine.SetValue("__endHunt", new Action(EndHuntFromScript));
			engine.SetValue("__promptLLM", new Action<string, bool>(PromptLLMFromScript));
			engine.SetValue("__log", new Action<string>(LogFromScript));
			engine.SetValue("__toolSpecsJson", GetBridgedToolSpecsJson());
			engine.Execute(BuildBootstrapScript());
			engine.Execute(source ?? "");
			engine.Execute(
				"if (typeof update !== 'function') throw new Error('script must define update(ctx, api, inputs, state)');"
			);
			compiled = true;
			return true;
		}
		catch (JavaScriptException e)
		{
			error = "Script compile error: " + e.Message;
			engine = null;
			return false;
		}
		catch (Exception e)
		{
			error = "Script compile failed: " + e.Message;
			engine = null;
			return false;
		}
	}

	private string BuildBootstrapScript()
	{
		var sb = new StringBuilder(1024);
		sb.AppendLine("var state = {};");
		sb.AppendLine("var api = {};");
		sb.AppendLine(
			"api.callTool = function(name, args){ if (!name) throw new Error('tool name required'); if (args === undefined || args === null) { args = {}; } if (typeof args !== 'object') throw new Error('tool args must be an object'); __enqueueTool(String(name), JSON.stringify(args)); };"
		);
		sb.AppendLine("api.call = api.callTool;");
		sb.AppendLine(
			"api.moveToward = function(args){ __moveToward(JSON.stringify(args || {})); };"
		);
		sb.AppendLine("api.lungeAt = function(args){ __lungeAt(JSON.stringify(args || {})); };");
		sb.AppendLine("api.endHunt = function(){ __endHunt(); };");
		sb.AppendLine(
			"api.promptLLM = function(reason, urgent){ __promptLLM(String(reason || ''), !!urgent); };"
		);
		sb.AppendLine("api.log = function(msg){ __log(String(msg)); };");
		sb.AppendLine("api.getToolSpecs = function(){ return JSON.parse(__toolSpecsJson || '[]'); };");
		sb.AppendLine(
			"api.listTools = function(){ var specs = api.getToolSpecs(); var names = []; for (var i = 0; i < specs.length; i++) { if (specs[i] && specs[i].function && specs[i].function.name) names.push(specs[i].function.name); } return names; };"
		);

		foreach (var name in GetBridgedToolNames())
		{
			sb.AppendLine($"api.{name} = function(args){{ return api.callTool('{name}', args); }};");
		}

		sb.AppendLine("Object.freeze(api);");
		return sb.ToString();
	}

	private List<string> GetBridgedToolNames()
	{
		var names = new List<string>();
		foreach (var tool in Source.GetTools(null) ?? new List<Tool>())
		{
			var name = tool?.Function?.Name;
			if (string.IsNullOrWhiteSpace(name))
			{
				continue;
			}
			if (ScriptBlockedTools.Contains(name))
			{
				continue;
			}
			if (!name.All(c => char.IsLetterOrDigit(c) || c == '_') || char.IsDigit(name[0]))
			{
				continue;
			}
			names.Add(name);
		}
		return names;
	}

	private string GetBridgedToolSpecsJson()
	{
		var tools = (Source.GetTools(null) ?? new List<Tool>())
			.Where(t => t?.Function?.Name != null && !ScriptBlockedTools.Contains(t.Function.Name))
			.ToList();
		return LLMTool.AllToolsJson(tools);
	}

	// ------------------------------------------------------------------
	// Hunt session lifecycle (driven by the engine's ChaseStarted/ChaseEnded)
	// ------------------------------------------------------------------

	private void OnChaseStarted()
	{
		if (!compiled)
		{
			// Gate in GhostTools should make this unreachable for LLM-started hunts;
			// the endgame cinematic doesn't emit ChaseStarted at all.
			return;
		}
		hunting = true;
		inSession = true;
		tickCount = 0;
		huntLog.Clear();
		lastActionReports = new List<string>();
		huntStartedAtSec = NowSec();
		HuntLogAdd("hunt started");
	}

	private void OnChaseEnded()
	{
		// inSession (not hunting) owns the debrief: an aborted hunt stops ticking
		// before ChaseEnded arrives, and crashed hunts need their debrief the most.
		if (!inSession)
		{
			return;
		}
		inSession = false;
		hunting = false;

		var reason = Ghost != null ? Ghost.Get("last_hunt_end_reason").AsString() : "";
		HuntLogAdd($"hunt ended ({reason})");
		SendDebrief(reason);
	}

	// _PhysicsProcess, not _Process: the LOS raycasts need DirectSpaceState, which
	// is only accessible during the physics step. The scheduler below still paces
	// script execution at TickIntervalSec regardless of the physics rate.
	public override void _PhysicsProcess(double delta)
	{
		var now = NowSec();

		if (now >= nextSenseAt)
		{
			nextSenseAt = now + TickIntervalSec;
			SynthesizeFootstepStimuli(now);
			ExpireSenses(now);
		}

		if (!hunting || !compiled || executingTools)
		{
			return;
		}
		if (now < nextTickAt)
		{
			return;
		}
		nextTickAt = now + TickIntervalSec;
		RunTick(now);
	}

	private void RunTick(double now)
	{
		tickCount++;
		queuedTools = new List<(string, string)>();
		budgetDroppedThisTick = false;

		// ctx.lastActions carries the PREVIOUS tick's reports; reset after building
		// so native failures and tool results from this tick land in the next ctx.
		var ctxJson = BuildContextJson(now);
		var inputsJson = BuildInputValuesJson();
		lastActionReports = new List<string>();

		var timer = Stopwatch.StartNew();
		try
		{
			engine.SetValue("__ctxJson", ctxJson);
			engine.SetValue("__inputsJson", inputsJson);
			engine.Execute("var ctx = JSON.parse(__ctxJson); var inputs = JSON.parse(__inputsJson);");
			engine.Execute("update(ctx, api, inputs, state);");
		}
		catch (JavaScriptException e)
		{
			AbortHuntForScriptError("Script error: " + e.Message);
			return;
		}
		catch (Exception e)
		{
			AbortHuntForScriptError("Script execution failed: " + e.Message);
			return;
		}
		finally
		{
			timer.Stop();
		}

		if (timer.ElapsedMilliseconds > MaxRuntimeMs)
		{
			AbortHuntForScriptError(
				$"Script exceeded time budget ({timer.ElapsedMilliseconds}ms > {MaxRuntimeMs}ms)."
			);
			return;
		}

		if (queuedTools.Count > 0)
		{
			ExecuteQueuedToolsAsync(queuedTools);
		}
	}

	// Per the no-fallback ruling: abort the hunt entirely. The script stays
	// installed and compiled — the next hunt runs it again until the cortex
	// patches it. The cortex hears about it loudly either way.
	private void AbortHuntForScriptError(string message)
	{
		lastError = message ?? "";
		hunting = false;
		HuntLogAdd("CRASH: " + lastError);
		GD.PrintErr($"[HuntCore] SCRIPT FAULT at tick {tickCount} — {lastError}");

		Bus.EmitSignal(
			EventBus.SignalName.SystemFeedback,
			$"‼️ YOUR HUNT INSTINCTS CRASHED (tick {tickCount}): {lastError} "
				+ "— THE HUNT WAS ABORTED. Review with getHuntScript and fix with patchHuntScript "
				+ "before hunting again."
		);

		// Total engine-side cleanup (doors unlock, ChaseEnded, SFX) on every peer.
		Ghost?.Call("abort_hunt", "script_error");
	}

	private async void ExecuteQueuedToolsAsync(List<(string Name, string ArgsJson)> queued)
	{
		if (executingTools)
		{
			return;
		}
		executingTools = true;
		var reports = new List<string>();

		try
		{
			int index = 0;
			foreach (var (name, argsJson) in queued)
			{
				var call = new ToolCall
				{
					Index = index++,
					Id = Guid.NewGuid().ToString(),
					Type = "function",
					Function = new ToolFunction { Name = name, RawArguments = argsJson ?? "{}" },
				};

				ToolCallResult result;
				try
				{
					result = await Source.Invoke(call, null);
				}
				catch (Exception e)
				{
					reports.Add($"{name}: Fail ({e.Message})");
					continue;
				}

				var status = result?.Status.ToString() ?? "Fail";
				var message = ResultText(result);
				reports.Add(string.IsNullOrWhiteSpace(message) ? $"{name}: {status}" : $"{name}: {status} — {message}");
			}
		}
		finally
		{
			foreach (var report in reports)
			{
				HuntLogAdd("action " + report);
			}
			// Append (not replace): the natives may have written failures for this
			// tick already, and both belong in the next tick's ctx.lastActions.
			lastActionReports.AddRange(reports);
			executingTools = false;
		}
	}

	// ------------------------------------------------------------------
	// Script-callable natives
	// ------------------------------------------------------------------

	private void EnqueueToolFromScript(string name, string argsJson)
	{
		if (queuedTools == null || string.IsNullOrWhiteSpace(name))
		{
			return;
		}
		if (ScriptBlockedTools.Contains(name))
		{
			// Cortex-domain tool from the reflex = a script bug worth crashing over.
			throw new Exception($"tool '{name}' cannot be called from the hunt script");
		}
		if (queuedTools.Count >= MaxActionsPerTick)
		{
			NoteBudgetDropped(name);
			return;
		}
		queuedTools.Add((name, string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson));
	}

	private void MoveTowardFromScript(string argsJson)
	{
		if (!hunting)
		{
			return;
		}

		var args = ParseArgs(argsJson);
		Vector3 position;
		string label;

		var lastKnownOf = args?["lastKnownOf"]?.GetValue<int>() ?? 0;
		if (lastKnownOf > 0)
		{
			if (!lastKnown.TryGetValue(lastKnownOf, out var known))
			{
				lastActionReports.Add($"moveToward: Fail — no last known position for Player {lastKnownOf}");
				return;
			}
			position = TargetResolution.GetTargetPosition(known.Room);
			label = $"lastKnownOf Player {lastKnownOf} ({known.Room})";
		}
		else
		{
			var room = args?["room"]?.GetValue<string>() ?? "";
			if (string.IsNullOrWhiteSpace(room))
			{
				lastActionReports.Add("moveToward: Fail — pass {room} or {lastKnownOf}");
				return;
			}
			position = TargetResolution.GetTargetPosition(room);
			label = room;
		}

		if (position == Vector3.Zero)
		{
			lastActionReports.Add($"moveToward: Fail — unknown room '{label}'");
			return;
		}

		Ghost?.Call("hunt_move_toward", position);
		HuntLogAdd($"moveToward {label}");
	}

	private void LungeAtFromScript(string argsJson)
	{
		if (!hunting)
		{
			return;
		}

		var args = ParseArgs(argsJson);
		var playerNumber = args?["player"]?.GetValue<int>() ?? 0;

		if (playerNumber <= 0 || playerNumber != losPlayerNumber)
		{
			lastActionReports.Add(
				$"lungeAt: Fail — Player {playerNumber} is not in your line of sight (lunge requires LOS)"
			);
			return;
		}

		if (!Players.TryGetPeerForPlayerNumber(playerNumber, out var peerId))
		{
			lastActionReports.Add($"lungeAt: Fail — no such player ({playerNumber})");
			return;
		}

		var node = Players.GetPlayer(peerId);
		var accepted = Ghost != null && Ghost.Call("hunt_lunge", node).AsBool();
		lastActionReports.Add(accepted ? $"lungeAt Player {playerNumber}: Ok" : "lungeAt: Fail — lunge on cooldown");
		if (accepted)
		{
			HuntLogAdd($"LUNGE at Player {playerNumber} (dist {losDistance:0.0}m)");
		}
	}

	private void EndHuntFromScript()
	{
		if (!hunting)
		{
			return;
		}
		HuntLogAdd("script chose to end the hunt");
		Ghost?.Call("abort_hunt", "script_choice");
	}

	private void PromptLLMFromScript(string reason, bool urgent)
	{
		var now = NowSec();
		if (now - lastPromptAt < PromptCooldownSec)
		{
			return;
		}
		lastPromptAt = now;

		var text = string.IsNullOrWhiteSpace(reason) ? "Hunt script requested attention." : reason.Trim();
		HuntLogAdd("promptLLM: " + text);
		Bus.EmitSignal(
			EventBus.SignalName.NotableEventOccurred,
			$"🧠 [HUNT INSTINCTS] {text}"
		);
		if (urgent)
		{
			Brain?.PokeThink();
		}
	}

	private void LogFromScript(string message)
	{
		lastLog = message ?? "";
		HuntLogAdd("log: " + lastLog);
	}

	private void NoteBudgetDropped(string toolName)
	{
		if (budgetDroppedThisTick)
		{
			return;
		}
		budgetDroppedThisTick = true;
		HuntLogAdd($"budget: dropped '{toolName}' (max {MaxActionsPerTick} actions/tick)");
		Bus.EmitSignal(
			EventBus.SignalName.SystemFeedback,
			$"[INSTINCTS WARN] Action budget of {MaxActionsPerTick} per tick reached; '{toolName}' and "
				+ "subsequent calls were dropped this tick. The script keeps running — make it act less per tick."
		);
	}

	private static JsonObject ParseArgs(string argsJson)
	{
		try
		{
			return JsonNode.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson) as JsonObject;
		}
		catch
		{
			return new JsonObject();
		}
	}

	// ------------------------------------------------------------------
	// Senses — the ONLY knowledge the reflex gets
	// ------------------------------------------------------------------

	private void OnPlayerTalked(string message)
	{
		// Messages are attributed at source: "Player N: \"...\""
		var playerNumber = ParseLeadingPlayerNumber(message);
		var room = playerNumber > 0 ? GetRoomOfPlayerNumber(playerNumber) : "";
		AddStimulus(new Stimulus
		{
			Kind = "voice",
			Detail = "a voice",
			Room = room,
			Player = playerNumber,
			Direction = DirectionToPlayerNumber(playerNumber),
			AtSec = NowSec(),
		});
		if (playerNumber > 0 && !string.IsNullOrWhiteSpace(room) && room != "None")
		{
			lastKnown[playerNumber] = (room, NowSec());
		}
	}

	private void OnNotableEvent(string message)
	{
		// Attributed interactable events: "Player N opened door in Kitchen" etc.
		var playerNumber = ParseLeadingPlayerNumber(message);
		if (playerNumber <= 0)
		{
			return;
		}

		string kind = null;
		if (message.Contains(" door in ", StringComparison.Ordinal))
		{
			kind = "door";
		}
		else if (message.Contains(" the lights in ", StringComparison.Ordinal)
			|| message.Contains(" the radio in ", StringComparison.Ordinal))
		{
			kind = "switch";
		}
		if (kind == null)
		{
			return;
		}

		var inIndex = message.LastIndexOf(" in ", StringComparison.Ordinal);
		var room = inIndex >= 0 ? message[(inIndex + 4)..].Trim() : "";

		AddStimulus(new Stimulus
		{
			Kind = kind,
			Detail = message,
			Room = room,
			Player = playerNumber,
			Direction = DirectionToPlayerNumber(playerNumber),
			AtSec = NowSec(),
		});
		if (!string.IsNullOrWhiteSpace(room))
		{
			lastKnown[playerNumber] = (room, NowSec());
		}
	}

	private void SynthesizeFootstepStimuli(double now)
	{
		if (Ghost == null || Players == null)
		{
			return;
		}

		foreach (var player in Players.GetLivingPlayers())
		{
			var number = player.Get("player_number").AsInt32();
			var position = player.GlobalPosition;

			if (!lastObservedPositions.TryGetValue(number, out var previous))
			{
				lastObservedPositions[number] = position;
				continue;
			}
			lastObservedPositions[number] = position;

			var speed = (position - previous).Length() / (float)TickIntervalSec;
			string pace = null;
			float range = 0;
			if (speed >= RunSpeedThreshold)
			{
				pace = "running";
				range = RunHearingRange;
			}
			else if (speed >= WalkSpeedThreshold)
			{
				pace = "walking";
				range = WalkHearingRange;
			}
			if (pace == null)
			{
				continue;
			}

			var offset = position - Ghost.GlobalPosition;
			if (offset.Length() > range)
			{
				continue;
			}

			if (lastFootstepAt.TryGetValue(number, out var lastAt) && now - lastAt < FootstepDedupSec)
			{
				continue;
			}
			lastFootstepAt[number] = now;

			// Footsteps don't identify WHO — only pace, rough direction, and room.
			AddStimulus(new Stimulus
			{
				Kind = "footsteps",
				Detail = pace + " footsteps",
				Room = GetRoomOfNode(player),
				Player = 0,
				Direction = offset.Normalized(),
				AtSec = now,
			});
		}
	}

	private void AddStimulus(Stimulus stimulus)
	{
		stimuli.Add(stimulus);
		if (stimuli.Count > 64)
		{
			stimuli.RemoveAt(0);
		}
	}

	private void ExpireSenses(double now)
	{
		stimuli.RemoveAll(s => now - s.AtSec > StimulusExpirySec);
		foreach (var number in lastKnown.Keys.Where(k => now - lastKnown[k].AtSec > LastKnownExpirySec).ToList())
		{
			lastKnown.Remove(number);
		}
	}

	private void ComputeLineOfSight()
	{
		losPlayerNumber = 0;
		losDistance = 0;
		losDirection = Vector3.Zero;

		if (Ghost == null || Players == null)
		{
			return;
		}

		var space = Ghost.GetWorld3D()?.DirectSpaceState;
		if (space == null)
		{
			return;
		}

		var eye = Ghost.GlobalPosition + new Vector3(0, 1.725f, 0);
		float nearest = float.MaxValue;

		foreach (var player in Players.GetLivingPlayers())
		{
			if ((player.GlobalPosition - Ghost.GlobalPosition).Length() > LineOfSightRange)
			{
				continue;
			}

			var target = player.GlobalPosition + new Vector3(0, 0.75f, 0);
			var query = PhysicsRayQueryParameters3D.Create(eye, target, LineOfSightMask);
			query.Exclude = new Godot.Collections.Array<Rid> { (Ghost as CollisionObject3D)?.GetRid() ?? default };

			var hit = space.IntersectRay(query);
			if (hit.Count == 0)
			{
				continue;
			}

			var collider = hit["collider"].As<Node>();
			var belongsToPlayer = false;
			while (collider != null)
			{
				if (collider == player)
				{
					belongsToPlayer = true;
					break;
				}
				collider = collider.GetParent();
			}
			if (!belongsToPlayer)
			{
				continue;
			}

			var offset = player.GlobalPosition - Ghost.GlobalPosition;
			var distance = offset.Length();
			if (distance < nearest)
			{
				nearest = distance;
				losPlayerNumber = player.Get("player_number").AsInt32();
				losDistance = distance;
				losDirection = offset.Normalized();

				var room = GetRoomOfNode(player);
				if (!string.IsNullOrWhiteSpace(room) && room != "None")
				{
					lastKnown[losPlayerNumber] = (room, NowSec());
				}
			}
		}
	}

	// ------------------------------------------------------------------
	// ctx / inputs serialization
	// ------------------------------------------------------------------

	private string BuildContextJson(double now)
	{
		ComputeLineOfSight();

		var stimuliJson = new JsonArray();
		foreach (var s in stimuli.OrderBy(s => s.AtSec))
		{
			stimuliJson.Add(new JsonObject
			{
				["kind"] = s.Kind,
				["detail"] = s.Detail,
				["room"] = s.Room,
				["player"] = s.Player > 0 ? s.Player : null,
				["direction"] = new JsonObject
				{
					["x"] = Math.Round(s.Direction.X, 2),
					["z"] = Math.Round(s.Direction.Z, 2),
				},
				["ageSec"] = Math.Round(now - s.AtSec, 1),
			});
		}

		var lastKnownJson = new JsonArray();
		foreach (var kv in lastKnown.OrderBy(kv => kv.Key))
		{
			lastKnownJson.Add(new JsonObject
			{
				["player"] = kv.Key,
				["room"] = kv.Value.Room,
				["ageSec"] = Math.Round(now - kv.Value.AtSec, 1),
			});
		}

		var playersJson = new JsonArray();
		foreach (var player in Players.GetAllPlayers().Values.Where(GodotObject.IsInstanceValid))
		{
			playersJson.Add(new JsonObject
			{
				["number"] = player.Get("player_number").AsInt32(),
				["alive"] = !player.Get("dead").AsBool(),
			});
		}

		var lastActionsJson = new JsonArray();
		foreach (var report in lastActionReports)
		{
			lastActionsJson.Add(JsonValue.Create(report));
		}

		var ghostPosition = Ghost?.GlobalPosition ?? Vector3.Zero;
		var ctx = new JsonObject
		{
			["timeSec"] = Math.Round(now, 1),
			["hunt"] = new JsonObject
			{
				["active"] = hunting,
				["elapsedSec"] = Math.Round(now - huntStartedAtSec, 1),
				["remainingSec"] = Ghost != null ? Math.Round(Ghost.Get("hunt_remaining_sec").AsDouble(), 1) : 0,
				["gracePeriod"] = Ghost != null && Ghost.Get("hunt_in_grace").AsBool(),
			},
			["self"] = new JsonObject
			{
				["room"] = GetRoomOfNode(Ghost),
				["position"] = new JsonObject
				{
					["x"] = Math.Round(ghostPosition.X, 1),
					["y"] = Math.Round(ghostPosition.Y, 1),
					["z"] = Math.Round(ghostPosition.Z, 1),
				},
			},
			["stimuli"] = stimuliJson,
			["lineOfSight"] = losPlayerNumber > 0
				? new JsonObject
				{
					["player"] = losPlayerNumber,
					["distance"] = Math.Round(losDistance, 1),
					["direction"] = new JsonObject
					{
						["x"] = Math.Round(losDirection.X, 2),
						["z"] = Math.Round(losDirection.Z, 2),
					},
				}
				: null,
			["lastKnown"] = lastKnownJson,
			["players"] = playersJson,
			["lunge"] = new JsonObject
			{
				["ready"] = Ghost != null && Ghost.Get("hunt_lunge_ready").AsBool(),
			},
			["lastActions"] = lastActionsJson,
		};

		return ctx.ToJsonString();
	}

	private string BuildInputValuesJson()
	{
		var values = new JsonObject();
		foreach (var kv in inputValues)
		{
			values[kv.Key] = kv.Value?.DeepClone();
		}
		return values.ToJsonString();
	}

	// ------------------------------------------------------------------
	// Prompt blocks (static docs cached in the system prompt; dynamic status ephemeral)
	// ------------------------------------------------------------------

	public string BuildApiDocsBlock()
	{
		var sb = new StringBuilder(1024);
		sb.AppendLine("<HUNT_INSTINCTS_API>");
		sb.AppendLine("Your hunting instincts are a JavaScript function YOU write and install with setHuntScript:");
		sb.AppendLine("    function update(ctx, api, inputs, state) { ... }");
		sb.AppendLine("The engine executes it ~10 times per second while a hunt is active. You cannot start a hunt");
		sb.AppendLine("without instincts installed. If the script throws at runtime, THE HUNT ABORTS — there is no");
		sb.AppendLine("fallback. Iterate with patchUpdateScript-style edits via patchHuntScript.");
		sb.AppendLine("");
		sb.AppendLine("ctx (SENSED knowledge only — you do NOT know where players are unless you sense them):");
		sb.AppendLine("- timeSec, hunt { active, elapsedSec, remainingSec, gracePeriod }");
		sb.AppendLine("- self { room, position{x,y,z} } — the ghost's own location");
		sb.AppendLine("- stimuli[] { kind: footsteps|voice|door|switch, detail, room, player (null for footsteps —");
		sb.AppendLine("  sounds don't identify people), direction{x,z} (from you, normalized), ageSec }");
		sb.AppendLine("- lineOfSight { player, distance, direction{x,z} } | null — a player you can SEE right now");
		sb.AppendLine("- lastKnown[] { player, room, ageSec } — where players were last sensed (decays after ~45s)");
		sb.AppendLine("- players[] { number, alive } — the crew roster");
		sb.AppendLine("- lunge { ready } — whether a lunge is off cooldown");
		sb.AppendLine("- lastActions[] — results of the api calls from your previous tick");
		sb.AppendLine("");
		sb.AppendLine("api:");
		sb.AppendLine("- moveToward({room: \"Kitchen\"}) or moveToward({lastKnownOf: 2}) — navigate the ghost");
		sb.AppendLine("- lungeAt({player: 2}) — commit to a fast burst at a player. ONLY works with line of sight.");
		sb.AppendLine("  This is how you actually catch someone. Has an engine-enforced cooldown.");
		sb.AppendLine("- endHunt() — break the hunt off early");
		sb.AppendLine("- api.<toolName>({...}) — most of your normal tools work from the script (slam doors behind");
		sb.AppendLine("  you, kill lights in the room you enter, speak). Use api.listTools() to introspect.");
		sb.AppendLine("- promptLLM(reason, urgent) — wake your slow mind mid-hunt when instincts aren't enough (cooldown-limited)");
		sb.AppendLine("- log(msg) — breadcrumbs; these become your post-hunt debrief. Log your decisions.");
		sb.AppendLine("");
		sb.AppendLine("inputs: your own tuning knobs, defined with setHuntInputs and adjusted with setHuntInputValue");
		sb.AppendLine("state: a plain object that persists across ticks AND across hunts — your memory. Learn in it.");
		sb.AppendLine("");
		sb.AppendLine("Engine rules your script cannot override: the hunt timer, the grace period, the kill");
		sb.AppendLine("(contact at close range), speed limits, and the lunge cooldown.");
		sb.AppendLine("</HUNT_INSTINCTS_API>");
		return sb.ToString();
	}

	public string BuildStatusBlock()
	{
		var sb = new StringBuilder(256);
		sb.AppendLine("<HUNT_INSTINCTS_STATUS>");
		if (!compiled)
		{
			sb.AppendLine("‼️ NO HUNT INSTINCTS INSTALLED — you CANNOT hunt until you write them.");
			sb.AppendLine("Install with setHuntScript: function update(ctx, api, inputs, state) { ... }");
		}
		else
		{
			sb.AppendLine($"Instincts installed ({CountLines(scriptSource)} lines). Hunting: {(hunting ? "ACTIVE" : "idle")}.");
			if (!string.IsNullOrWhiteSpace(lastError))
			{
				sb.AppendLine($"⚠️ LAST HUNT CRASHED: {lastError}");
				sb.AppendLine("Fix it (getHuntScript → patchHuntScript) before hunting again.");
			}
			if (!string.IsNullOrWhiteSpace(lastLog))
			{
				sb.AppendLine($"Last instinct log: {lastLog}");
			}
		}
		sb.AppendLine("</HUNT_INSTINCTS_STATUS>");
		return sb.ToString();
	}

	// ------------------------------------------------------------------
	// Debrief
	// ------------------------------------------------------------------

	private void SendDebrief(string reason)
	{
		var sb = new StringBuilder(512);
		sb.AppendLine("<HUNT_DEBRIEF>");
		sb.AppendLine($"Outcome: {reason} after {Math.Round(NowSec() - huntStartedAtSec, 1)}s ({tickCount} instinct ticks).");
		sb.AppendLine("Timeline:");
		foreach (var line in huntLog)
		{
			sb.AppendLine("  " + line);
		}
		sb.AppendLine("Review your instincts' performance. If they misjudged something, patch them now");
		sb.AppendLine("(getHuntScript → patchHuntScript) while the memory is fresh.");
		sb.AppendLine("</HUNT_DEBRIEF>");

		Brain?.AddHuntDebrief(sb.ToString());
	}

	private void HuntLogAdd(string line)
	{
		if (huntLog.Count >= HuntLogCap)
		{
			return;
		}
		var stamp = hunting ? $"[t+{Math.Round(NowSec() - huntStartedAtSec, 1)}s] " : "";
		huntLog.Add(stamp + line);
	}

	// ------------------------------------------------------------------
	// Helpers
	// ------------------------------------------------------------------

	private static double NowSec() => Time.GetTicksMsec() / 1000.0;

	/// <summary>Tool result text lives in Parts (Results.OkText/FailText); Message is
	/// an internal-only field that is usually null.</summary>
	public static string ResultText(ToolCallResult result)
	{
		if (result?.Parts == null)
		{
			return result?.Message ?? "";
		}
		var texts = result.Parts.Where(p => !string.IsNullOrEmpty(p?.Text)).Select(p => p.Text);
		var joined = string.Join("\n", texts);
		return string.IsNullOrWhiteSpace(joined) ? result.Message ?? "" : joined;
	}

	private static int CountLines(string text) =>
		string.IsNullOrEmpty(text) ? 0 : text.Count(c => c == '\n') + 1;

	private static int ParseLeadingPlayerNumber(string message)
	{
		if (string.IsNullOrWhiteSpace(message) || !message.StartsWith("Player ", StringComparison.Ordinal))
		{
			return 0;
		}
		var rest = message["Player ".Length..];
		var digits = new string(rest.TakeWhile(char.IsDigit).ToArray());
		return int.TryParse(digits, out var number) ? number : 0;
	}

	private string GetRoomOfPlayerNumber(int number)
	{
		if (!Players.TryGetPeerForPlayerNumber(number, out var peerId))
		{
			return "";
		}
		return GetRoomOfNode(Players.GetPlayer(peerId));
	}

	private Vector3 DirectionToPlayerNumber(int number)
	{
		if (Ghost == null || !Players.TryGetPeerForPlayerNumber(number, out var peerId))
		{
			return Vector3.Zero;
		}
		var node = Players.GetPlayer(peerId);
		if (node == null)
		{
			return Vector3.Zero;
		}
		return (node.GlobalPosition - Ghost.GlobalPosition).Normalized();
	}

	private static string GetRoomOfNode(Node node)
	{
		var locator = node?.GetNodeOrNull("Locator") ?? node?.GetNodeOrNull("RoomLocator");
		return locator != null ? locator.Get("Room").AsString() : "";
	}
}
