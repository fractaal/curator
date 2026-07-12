using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The ghost's sensory organ and think-cadence driver. Accumulates notable events and system
/// feedback, assembles the per-turn world snapshot, and paces the AgenticCore entity
/// (GhostAgent + GhostTools) that replaced the old streaming-DSL pipeline.
/// </summary>
public partial class Sensors : Node
{
	public struct EventMessage
	{
		public string content;
		public int count;
		public ulong time;
	}

	private static int LoopCount = 0;

	private List<EventMessage> NotableEvents = new();
	private List<EventMessage> SystemFeedback = new();

	private Node3D Ghost;
	private PlayerManager PlayerMgr;

	private EventBus Bus;

	[Export]
	private RichTextLabel DebugView;

	[Export]
	private RichTextLabel PromptDebugView;

	private NarrativeIntegrity Integrity;

	private bool GameEnded = false;

	private ulong LLMPromptedTime = 0;

	private Node GhostData;
	private double TickInterval = 0.5;
	private double TickElapsed = 0;

	private double SensorReadInterval = 17.5;
	private double SensorReadElapsed = 0;

	private bool LoopCompleted = true;

	private ulong LastTimeChased = 0;

	private bool AIEnabled = false;

	private int MAX_HISTORY = 10;

	private ulong TimeSinceLastEvidenceDeposit = 0;

	private string GhostBackstory = "No backstory yet...";

	private bool PerformedInitialSilentAIEnable = false;

	private int FearFactor = 0;

	// ---- The AgenticCore mind ----
	private AgenticEntity Entity;
	private GhostAgent Behavior;
	private GhostTools Tools;
	private HuntCore Hunt;
	private int ContextCountAtTurnStart = 0;
	private bool PendingPoke = false;

	public HuntCore HuntCoreRef => Hunt;

	// Per-turn volatile snapshots. Assembled ONCE per think dispatch (before LLMPromptedTime
	// is bumped) so the "since last prompt" event filters cover everything since the previous
	// turn, and re-prompts within the same cycle see a stable turn context.
	public string CurrentTurnStatusPrompt { get; private set; } = "";
	public string CurrentTurnAttentionMarkers { get; private set; } = "";

	private string SYSTEM_PROMPT = FileAccess
		.Open("res://DeclarativeGameInterface/prompts/Main.txt", FileAccess.ModeFlags.Read)
		.GetAsText();

	private string BEHAVIOR_PROMPT = FileAccess
		.Open(
			"res://DeclarativeGameInterface/prompts/BehaviorPrompt.txt",
			FileAccess.ModeFlags.Read
		)
		.GetAsText();

	private string ENDGAME_SUMMARY_PROMPT = FileAccess
		.Open(
			"res://DeclarativeGameInterface/prompts/EndGameSummaryPrompt.txt",
			FileAccess.ModeFlags.Read
		)
		.GetAsText();

	public string SystemPrompt => SYSTEM_PROMPT;
	public string BehaviorPrompt => BEHAVIOR_PROMPT;

	private string GetTimeSinceLastChase()
	{
		string result = "TIME SINCE LAST CHASE: ";

		var difference = (Time.GetTicksMsec() - LastTimeChased) / 1000f;

		if (difference > 100)
		{
			result += $"‼‼ {difference}s ‼‼ -- Now is the time to START A CHASE!";
		}
		else
		{
			result += $"{difference}s";
		}

		return result;
	}

	public string GetGameInfo()
	{
		return $@"Room Information:
{Room.GetAllRoomInformation()}

Ghost Sounds It Can Emit:
{Ghost.GetNode<Node>("GhostSounds").Call("get_available_sound_names").AsString()}

Ghost Backstory:
{GhostBackstory}";
	}

	private string GetSystemFeedback()
	{
		var result = "";

		var events = from EventMessage e in SystemFeedback where e.time > LLMPromptedTime select e;

		result = EventMessagesToNaturalLanguageSimple(events.ToList());

		if (!result.Contains("None yet"))
		{
			result =
				"Some things went wrong or could be improved upon in your last response. Take care to amend these in the future:\n"
				+ result;
		}
		else
		{
			result = "";
		}

		return result;
	}

	private string GetArchivedPrompt()
	{
		var result = "";

		var events =
			from EventMessage e in NotableEvents
			where e.time > LLMPromptedTime
			where e.content.ToLower().Contains("player")
			select e;

		result =
			$@"TIME {Time.GetTicksMsec() / 1000f}s
# TIMELINE
{EventMessagesToNaturalLanguageSimple(events.ToList())}
---
{GetFearFactor()}
{GetTimeSinceLastChase()}
";
		return result;
	}

	private string GetCrewStatus()
	{
		var all = PlayerMgr.GetAllPlayers();

		if (all.Count == 0)
		{
			return "No players currently in game";
		}

		var lines = all.Values
			.Where(GodotObject.IsInstanceValid)
			.OrderBy(p => p.Get("player_number").AsInt32())
			.Select(p =>
			{
				var number = p.Get("player_number").AsInt32();
				var label = number == 1 ? "Player 1 (host)" : $"Player {number}";
				return $"{label}: {p.Call("getStatus").AsString()}";
			});

		return string.Join("\n", lines);
	}

	private string GetNextPromptWithPlayerAndGhostStatus()
	{
		var result = "";

		var events =
			from EventMessage e in NotableEvents
			where e.time > LLMPromptedTime
			where e.content.ToLower().Contains("player")
			select e;

		result =
			$@"CURRENT TIME {Time.GetTicksMsec() / 1000f}s

{GetContextualAttentionMarkers()}

# GHOST
{Ghost.Call("getStatus").AsString()}

{(Hunt != null ? Hunt.BuildStatusBlock() : "")}

{GetContextualAttentionMarkers()}

# PLAYERS
{GetCrewStatus()}

{GetContextualAttentionMarkers()}

# TIMELINE
{EventMessagesToNaturalLanguageSimple(events.ToList())}

{GetTimeSinceLastChase()}

{GetContextualAttentionMarkers()}

{GetFearFactor()}
";
		return result;
	}

	private void EnsureMind()
	{
		if (Entity != null)
		{
			return;
		}

		Tools = new GhostTools(Bus, Integrity);

		// The reflex layer. Two-phase: HuntCore's api bridge reflects over Tools,
		// and Tools' hunt gate needs HuntCore back.
		Hunt = new HuntCore();
		AddChild(Hunt);
		Hunt.Setup(Bus, Tools, this, Ghost);
		Tools.Hunt = Hunt;

		Behavior = new GhostAgent(this, Tools);
		Entity = new AgenticEntity(Behavior);
		Behavior.Agentic = Entity;

		Entity.Config = new AgenticEntityConfig
		{
			// Sensors paces the think cadence itself (SENSOR_READ_INTERVAL, chase-accelerated),
			// so the entity's own pacing floor is zeroed out.
			OptimalTurnaroundTime = 0.0,
			InitialStartDelay = 0.0,
			MaxHistoryMessages = Math.Max(MAX_HISTORY, 1) * 6,
		};

		Entity.ThinkingFinished += (_) =>
		{
			LoopCompleted = true;
			LoopCount++;

			// Coalesced early-think: N pokes during one in-flight cycle collapse
			// to a single follow-up (mirrors StarshipAgent.PokeThink).
			if (PendingPoke)
			{
				PendingPoke = false;
				Callable.From(PrepareTurnAndThink).CallDeferred();
			}
		};

		Entity.LLMProcessingCompleted += OnLLMResponseCompleted;

		// The old LLMInterface surfaced connection trouble via CriticalMessage; the closest
		// honest signal in the new runtime (which retries internally) is the too-long event.
		Entity.ThinkingTakingTooLong += () =>
		{
			Bus.EmitSignal(
				EventBus.SignalName.CriticalMessage,
				"[color=\"#FF0000\"]The AI is taking unusually long to respond — your internet connection may be unstable.[/color]"
			);
		};
	}

	/// <summary>Request an early think outside the normal sensor cadence — used by
	/// HuntCore for promptLLM(urgent) and post-hunt debriefs. Coalesces: pokes that
	/// arrive while a cycle is in flight collapse to one follow-up cycle.</summary>
	public void PokeThink()
	{
		if (!Multiplayer.IsServer() || Entity == null)
		{
			return;
		}
		if (!LoopCompleted)
		{
			PendingPoke = true;
			return;
		}
		PrepareTurnAndThink();
	}

	/// <summary>Persist a post-hunt debrief into the mind's context and reflect on it.</summary>
	public void AddHuntDebrief(string debrief)
	{
		if (!Multiplayer.IsServer() || Entity == null || string.IsNullOrWhiteSpace(debrief))
		{
			return;
		}
		Entity.AddMessage(LLMMessage.FromText("user", debrief));
		PokeThink();
	}

	private void PrepareTurnAndThink()
	{
		// Only the server should make LLM requests in multiplayer
		if (!Multiplayer.IsServer())
		{
			GD.Print("Sensors: Skipping LLM request - not server");
			return;
		}

		EnsureMind();

		// Snapshot the volatile turn context BEFORE bumping LLMPromptedTime.
		var turnFeedback = GetSystemFeedback();
		CurrentTurnStatusPrompt = GetNextPromptWithPlayerAndGhostStatus();
		CurrentTurnAttentionMarkers = GetContextualAttentionMarkers() + " " + GetFearFactor();

		// Persist this turn's feedback + timeline into the rolling history (parity with the
		// old History.Add calls; the entity autocompacts past MaxHistoryMessages). Feedback
		// persisted here lands at the end of the persistent middle — the same slot the old
		// assembler gave it — so GhostAgent must NOT add it again in the ephemeral tail.
		if (turnFeedback != "")
		{
			Entity.AddMessage(LLMMessage.FromText("user", turnFeedback));
		}

		Entity.AddMessage(LLMMessage.FromText("user", GetArchivedPrompt()));

		Bus.EmitSignal(EventBus.SignalName.GameDataRead, "");

		LLMPromptedTime = Time.GetTicksMsec();
		ContextCountAtTurnStart = Entity.PersistentContext.Count;
		LoopCompleted = false;

		Bus.EmitSignal(EventBus.SignalName.LLMPrompted, "");

		_ = Entity.Think();
	}

	// Emits the legacy LLM lifecycle signals once per completed response, so Logger,
	// LatencyStatistics, ModeReadout, and log-parser.py keep working unchanged.
	private void OnLLMResponseCompleted()
	{
		if (Entity == null)
		{
			return;
		}

		var texts = Entity
			.PersistentContext
			.Skip(ContextCountAtTurnStart)
			.Where(m => m != null && m.Role == "assistant")
			.Select(m => GhostMind.ExtractText(m))
			.Where(t => !string.IsNullOrWhiteSpace(t));

		var response = string.Join("\n", texts).Trim();

		LogManager.UpdateLog("llmResponse", response);

		if (PromptDebugView != null && Entity.LastSentContext != null)
		{
			PromptDebugView.Text = Entity
				.LastSentContext
				.Aggregate(
					"",
					(acc, message) =>
						acc
						+ "\n [color=\"#ff0000\"][b]"
						+ message.Role
						+ "[/b][/color]: "
						+ GhostMind.ExtractText(message)
						+ "\n"
				);
		}

		Bus.EmitSignal(EventBus.SignalName.LLMFirstResponseChunk, response);
		Bus.EmitSignal(EventBus.SignalName.LLMResponseChunk, response);
		Bus.EmitSignal(EventBus.SignalName.LLMLastResponseChunk, response);
		Bus.EmitSignal(EventBus.SignalName.LLMFullResponse, response);
	}

	private void OnNotableEventOccurred(string message, ulong time)
	{
		const ulong FiveSeconds = 5 * 1000; // Assuming time is in milliseconds
		int count = 1; // Starting with 1 for the current event

		if (message.ToLower().Contains("deposited evidence"))
		{
			TimeSinceLastEvidenceDeposit = time;
		}

		// Find the last event of the same type
		EventMessage lastEvent =
			new()
			{
				content = "",
				count = 0,
				time = 0
			};

		try
		{
			NotableEvents.FindLast(e => e.content.Contains(message));
		}
		catch (Exception)
		{
			//
		}

		// Check if the last event exists and is within 5 seconds
		if (lastEvent.time != 0 && (time - lastEvent.time) <= FiveSeconds)
		{
			count += lastEvent.count; // Increment count from the last event
			NotableEvents.Remove(lastEvent); // Remove the last event as we'll replace it
		}

		// Regardless of whether we found a matching event within 5 seconds, add the new event
		NotableEvents.Add(
			new EventMessage
			{
				count = count,
				content = message,
				time = time
			}
		);
	}

	public async void EndgameSummarization()
	{
		// Clients receive the finished summary via EventBusRelay; generating here
		// would emit an empty one (AuxCompleteAsync is host-gated).
		if (!Multiplayer.IsServer())
		{
			return;
		}

		string allEvents = "";

		foreach (EventMessage e in NotableEvents)
		{
			allEvents += $"{e.time / 1000} - {e.content} ({e.count} times)\n";
		}

		var additionalGameInfo = "GAME INFORMATION:\n\n";

		additionalGameInfo += Ghost.Call("getStatusStateless").AsString() + "\n\n";
		additionalGameInfo += GetGameInfo() + "\n\n";

		var response = await GhostMind.AuxCompleteAsync(
			new List<LLMMessage>()
			{
				LLMMessage.FromText("system", ENDGAME_SUMMARY_PROMPT),
				LLMMessage.FromText("user", additionalGameInfo),
				LLMMessage.FromText("user", "Events to summarize to follow: "),
				LLMMessage.FromText("user", allEvents)
			}
		);

		Bus.EmitSignal(EventBus.SignalName.EndgameSummary, response);
	}

	public string GetFearFactor()
	{
		var chase = "";

		if (Ghost.Get("chasing").AsBool())
		{
			chase =
				" THE GHOST IS CHASING! YOU **NEED** TO KEEP FEAR FACTOR AT MAXIMUM! EXPLODE LIGHTS, THROW OBJECTS, PULL PLAYER -- ANYTHING!";
		}

		if (FearFactor >= 85)
		{
			return $"‼ FEAR FACTOR: 🚀🚀 {FearFactor} 🚀🚀 (**TERRIFYING.** AMAZING! BE RELENTLESS!)";
		}
		else if (FearFactor >= 60)
		{
			return $"‼ FEAR FACTOR: 💥💥 {FearFactor} 💥💥 (SCARY! THAT'S CRAZY!)" + chase;
		}
		else if (FearFactor >= 40)
		{
			return $"‼ FEAR FACTOR: {FearFactor} (INTENSE. KEEP IT UP!)" + chase;
		}
		else if (FearFactor > 0)
		{
			return $"‼ FEAR FACTOR: {FearFactor} (DANGEROUSLY LOW. FIX THIS, NOW!)" + chase;
		}
		else
		{
			return "‼ FEAR FACTOR: 👎 0 (BORING. USE COMMANDS AT YOUR DISPOSAL!)" + chase;
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override async void _Ready()
	{
		try
		{
			MAX_HISTORY = int.Parse(Config.Get("MAX_HISTORY"));
		}
		catch (Exception e)
		{
			GD.PrintErr("Failed to parse MAX_HISTORY: " + e.Message);
			MAX_HISTORY = 10;
		}

		Bus = EventBus.Get();

		try
		{
			SensorReadInterval = float.Parse(Config.Get("SENSOR_READ_INTERVAL"));
		}
		catch (Exception e)
		{
			GD.PrintErr("Failed to parse SENSOR_READ_INTERVAL: " + e.Message);
		}

		// Players register themselves on spawn (player.gd _ready) and unregister on despawn;
		// the crew is read live from PlayerManager every turn.
		PlayerMgr = PlayerManager.Get();

		AddToGroup("late_join_synced");

		Ghost = GetTree().CurrentScene.GetNode<Node3D>("Ghost");

		if (Ghost == null)
		{
			GD.PrintErr("Failed to find ghost node");
		}

		Integrity = GetNode<NarrativeIntegrity>("/root/NarrativeIntegrity");
		GhostData = GetNode<Node>("/root/GhostData");

		Bus.FearFactorChanged += (int value) =>
		{
			FearFactor = value;
		};

		Bus.ChaseEnded += () =>
		{
			LastTimeChased = Time.GetTicksMsec();
		};

		Bus.GhostTalked += (message) =>
		{
			NotableEvents.Add(
				new()
				{
					content = "Ghost interaction: speakAsGhost(" + message + ")",
					count = 1,
					time = Time.GetTicksMsec()
				}
			);
		};

		Bus.PlayerTalked += (message) =>
		{
			NotableEvents.Add(
				new()
				{
					content = "🗣 (IMPORTANT!) PLAYER TALKED TO GHOST: " + message,
					count = 1,
					time = Time.GetTicksMsec()
				}
			);
		};

		Bus.NotableEventOccurredSpecificTime += (message, time) =>
		{
			OnNotableEventOccurred(message, time);
		};

		Bus.NotableEventOccurred += (message) =>
		{
			OnNotableEventOccurred(message, Time.GetTicksMsec());
		};

		Bus.SystemFeedback += (message) =>
		{
			var time = Time.GetTicksMsec();
			const ulong FiveSeconds = 5 * 1000; // Assuming time is in milliseconds

			int count = 1; // Starting with 1 for the current event

			// Find the last event of the same type
			EventMessage lastEvent =
				new()
				{
					content = "",
					count = 0,
					time = 0
				};

			try
			{
				SystemFeedback.FindLast(e => e.content.Contains(message));
			}
			catch (Exception)
			{
				//
			}

			// Check if the last event exists and is within 5 seconds
			if (lastEvent.time != 0 && (time - lastEvent.time) <= FiveSeconds)
			{
				count += lastEvent.count; // Increment count from the last event
				SystemFeedback.Remove(lastEvent); // Remove the last event as we'll replace it
			}

			// Regardless of whether we found a matching event within 5 seconds, add the new event
			SystemFeedback.Add(
				new EventMessage
				{
					count = count,
					content = message,
					time = time
				}
			);

			SystemFeedback = SystemFeedback.TakeLast(7).ToList();
		};

		Bus.AmendSystemFeedback += (message) =>
		{
			var words = message.Split(" ");

			if (words.Length == 0)
			{
				return;
			}

			SystemFeedback.RemoveAll(
				_event => words.Any(word => _event.content.ToLower().Contains(word.ToLower()))
			);
		};

		Bus.ChaseStarted += () =>
		{
			LastTimeChased = Time.GetTicksMsec();
		};

		Bus.GameWon += (string message) =>
		{
			GameEnded = true;
		};

		Bus.GameLost += (string message) =>
		{
			GameEnded = true;
			EndgameSummarization();
		};

		Bus.OperatorNote += (string message) =>
		{
			if (!Multiplayer.IsServer())
			{
				return;
			}

			EnsureMind();
			Entity.AddMessage(
				LLMMessage.FromText("user", "🎙 OPERATOR NOTE (out-of-game director): " + message)
			);
		};

		await ToSignal(GetTree().CreateTimer(1), "timeout");

		if (OS.HasFeature("standalone"))
		{
			GenerateBackstory();
		}
	}

	public async void GenerateBackstory()
	{
		// Host-only: clients get the sanitized result via EventBusRelay, or via the
		// late-join sync below if they connect after it was generated.
		if (!Multiplayer.IsServer())
		{
			return;
		}

		var backstoryPrompt = FileAccess
			.Open(
				"res://DeclarativeGameInterface/prompts/BackstoryPrompt.txt",
				FileAccess.ModeFlags.Read
			)
			.GetAsText();

		var backstoryForPlayerPrompt = FileAccess
			.Open(
				"res://DeclarativeGameInterface/prompts/BackstoryForPlayerPrompt.txt",
				FileAccess.ModeFlags.Read
			)
			.GetAsText();

		GhostBackstory = await GhostMind.AuxCompleteAsync(
			new List<LLMMessage>
			{
				LLMMessage.FromText("system", backstoryPrompt),
				LLMMessage.FromText("user", Ghost.Call("getStatusStateless").ToString())
			}
		);

		var sanitizedBackstory = await GhostMind.AuxCompleteAsync(
			new List<LLMMessage>
			{
				LLMMessage.FromText("system", backstoryForPlayerPrompt),
				LLMMessage.FromText("user", GhostBackstory)
			}
		);

		sanitizedBackstory = GhostData.Call("StripGhostTypes", sanitizedBackstory).AsString();

		SanitizedBackstoryForPlayers = sanitizedBackstory;
		Bus.EmitSignal(EventBus.SignalName.GhostBackstory, sanitizedBackstory);
	}

	private string SanitizedBackstoryForPlayers = "";

	// Group-protocol name (see MultiplayerManager._late_join_sync): the backstory is
	// generated and emitted before any client connects, so joiners get it pushed here.
	public void late_join_sync(int peerId)
	{
		if (SanitizedBackstoryForPlayers != "")
		{
			RpcId(peerId, MethodName.ApplyBackstory, SanitizedBackstoryForPlayers);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority)]
	private void ApplyBackstory(string sanitizedBackstory)
	{
		Bus.EmitSignal(EventBus.SignalName.GhostBackstory, sanitizedBackstory);
	}

	public string EventMessagesToNaturalLanguageSimple(List<EventMessage> messages)
	{
		messages = new List<EventMessage>(messages);
		messages.Sort((m1, m2) => m1.time.CompareTo(m2.time));

		string naturalLanguage = "";

		foreach (var m in messages)
		{
			string count = m.count > 1 ? $" (x{m.count})" : "";
			naturalLanguage += "\t" + m.content + count + "\n";
		}

		if (naturalLanguage == "")
		{
			naturalLanguage += "None yet.";
		}

		return naturalLanguage;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Input.IsActionJustPressed("GenerateBackstory"))
		{
			GenerateBackstory();
		}

		if (Input.IsActionJustPressed("ToggleAI"))
		{
			AIEnabled = !AIEnabled;

			Bus.EmitSignal(
				EventBus.SignalName.ToastNotification,
				AIEnabled ? "AI Enabled" : "AI Disabled"
			);
		}

		var anyoneInsideHouse = PlayerMgr
			.GetAllPlayerStats()
			.Values
			.Any(s => GodotObject.IsInstanceValid(s) && s.HasPlayerSteppedInsideHouse);

		if (anyoneInsideHouse && !PerformedInitialSilentAIEnable)
		{
			if (OS.HasFeature("standalone"))
			{
				AIEnabled = true;
				PerformedInitialSilentAIEnable = true;
				GD.Print("Silently enabling AI because a player has stepped inside the house.");
			}
			else
			{
				GD.Print(
					"Would have silently enabled AI because a player has stepped inside the house, but didn't (test build)"
				);
				PerformedInitialSilentAIEnable = true;
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Entity?.Process(delta);

		TickElapsed += delta;
		SensorReadElapsed += delta;

		if (DebugView != null && TickElapsed >= TickInterval)
		{
			DebugView.Text = GetNextPromptWithPlayerAndGhostStatus();
			TickElapsed = 0;
		}

		if (
			SensorReadElapsed >= SensorReadInterval
			|| (Ghost.Get("chasing").AsBool() && SensorReadElapsed >= (SensorReadInterval * 0.5))
		)
		{
			SensorReadElapsed = 0;

			if (!AIEnabled)
			{
				GD.Print("AI disabled, skipping sensor read.");
				SensorReadElapsed = SensorReadInterval - 1;
				return;
			}

			if (PlayerMgr.GetPlayerCount() == 0)
			{
				GD.Print("No players available, skipping sensor read.");
				SensorReadElapsed = SensorReadInterval - 1;
				return;
			}

			if (PlayerMgr.GetLivingPlayers().Count == 0)
			{
				GD.Print("All players dead, skipping sensor read.");
				SensorReadElapsed = SensorReadInterval - 1;
				return;
			}

			if (GameEnded)
			{
				GD.Print("Game ended, skipping sensor read.");
				SensorReadElapsed = SensorReadInterval - 1;
				return;
			}

			if (!LoopCompleted)
			{
				GD.Print("Loop not completed yet, skipping sensor read.");
				SensorReadElapsed = SensorReadInterval - 1;
				return;
			}

			PrepareTurnAndThink();
		}
	}

	public string GetContextualAttentionMarkers()
	{
		string markers = "";

		if (Ghost.Get("chasing").AsBool())
		{
			markers +=
				"### 💥 A HUNT IS RUNNING ON YOUR INSTINCTS — the reflex script controls the pursuit. "
				+ "Add ATMOSPHERE (sounds, lights, speech), do NOT micromanage movement. "
				+ "If the instincts poke you with a problem, fix the script. 💥 ###\n";
		}

		if (Hunt != null && !Hunt.HasCompiledScript)
		{
			markers +=
				"### 🧠 NO HUNT INSTINCTS INSTALLED — you CANNOT hunt until you write them. "
				+ "Install update(ctx, api, inputs, state) via setHuntScript (see HUNT_INSTINCTS_API). ###\n";
		}
		else if (
			!Ghost.Get("chasing").AsBool()
			&& (Time.GetTicksMsec() - LastTimeChased) < 30000
			&& LastTimeChased != 0
		)
		{
			markers +=
				"### 🛑 A CHASE HAS JUST ENDED - COOL OFF AND LET THE PLAYER BREATH FOR A MOMENT 🛑 ###\n";
		}

		var living = PlayerMgr.GetLivingPlayers();
		var outside = living
			.Where(p => p.GetNode("Locator").Get("Room").AsString() == "None")
			.ToList();

		if (living.Count > 0 && outside.Count == living.Count)
		{
			markers +=
				"### 🤚 ALL PLAYERS ARE OUTSIDE THE HOUSE - GHOST CANNOT CHASE OUTSIDE THE HOUSE - BE SUBTLER, MAKE THE HOUSE MORE APPEALING, LURE THEM BACK IN, DON'T LOCK ENTRANCE DOOR ✋ ###\n";
		}
		else
		{
			foreach (var player in outside)
			{
				markers +=
					$"### 🤚 Player {player.Get("player_number").AsInt32()} is outside the house - they cannot be chased or targeted out there ✋ ###\n";
			}
		}

		return markers;
	}
}
