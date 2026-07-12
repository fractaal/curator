using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// The ghost's hands: every verb the old text-DSL Interpreter recognized, exposed as a native
/// [Tool] method for AgenticCore's ReflectionToolSource. Each method emits the exact same
/// EventBus signals (same names, same argument shapes) the Interpreter used to emit, so every
/// consumer downstream — interactables, RPCs, Sensors' event log, TTS — is untouched.
///
/// All methods run on the Godot main thread: the OpenRouter client marshals its callbacks
/// through MainThread.Enqueue and GhostMind attaches the ProcessFrame pump.
/// </summary>
public class GhostTools
{
	private static readonly TextInfo Text = new CultureInfo("en-US", false).TextInfo;

	private readonly EventBus Bus;
	private readonly NarrativeIntegrity Integrity;

	/// <summary>Fired with the legacy-lowercase verb after a tool successfully performs
	/// (GhostAgent uses this for repetition/cadence housekeeping).</summary>
	public event Action<string> CommandExecuted;

	// Object interactions awaiting an ObjectInteractionAcknowledged echo, keyed
	// verb/objectType/target — the same key the old Interpreter used.
	private readonly Dictionary<string, TaskCompletionSource<bool>> PendingAcks = new();

	public GhostTools(EventBus bus, NarrativeIntegrity integrity)
	{
		Bus = bus;
		Integrity = integrity;

		Bus.ObjectInteractionAcknowledged += (verb, objectType, target) =>
		{
			if (PendingAcks.TryGetValue($"{verb}/{objectType}/{target}", out var tcs))
			{
				tcs.TrySetResult(true);
			}
		};
	}

	private Node3D NearestLivingPlayer()
	{
		var ghost = Bus.GetTree().CurrentScene.GetNodeOrNull<Node3D>("Ghost");
		return PlayerManager
			.Get()
			.GetNearestLivingPlayer(ghost != null ? ghost.GlobalPosition : Vector3.Zero);
	}

	// ------------------------------------------------------------------
	// Lights
	// ------------------------------------------------------------------

	[Tool("turnOffLights", "Turn off the lights. Target can be a room name, 'all', or 'player' (the player's room).")]
	public Task<ToolCallResult> TurnOffLights([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("turnoff", "lights", target);

	[Tool("turnOnLights", "Turn on the lights. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> TurnOnLights([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("turnon", "lights", target);

	[Tool("flickerLights", "Flicker the lights for an eerie effect. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> FlickerLights([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("flicker", "lights", target);

	[Tool("explodeLights", "Violently kill lights and make them non-interactable until restored. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> ExplodeLights([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("explode", "lights", target);

	[Tool("restoreLights", "Turn exploded lights back on and restore their interactivity. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> RestoreLights([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("restore", "lights", target);

	// ------------------------------------------------------------------
	// Radios
	// ------------------------------------------------------------------

	[Tool("turnOnRadios", "Turn on the radios. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> TurnOnRadios([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("turnon", "radios", target);

	[Tool("turnOffRadios", "Turn off the radios. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> TurnOffRadios([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("turnoff", "radios", target);

	[Tool("playFreakyMusicOnRadios", "Play unsettling music on the radios. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> PlayFreakyMusicOnRadios([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("playfreakymusicon", "radios", target);

	[Tool("stopRadios", "Stop whatever the radios are playing. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> StopRadios([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("stop", "radios", target);

	// ------------------------------------------------------------------
	// Doors
	// ------------------------------------------------------------------

	[Tool("openDoors", "Open the doors. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> OpenDoors([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("open", "doors", target);

	[Tool("closeDoors", "Close the doors. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> CloseDoors([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("close", "doors", target);

	[Tool("lockDoors", "Lock the doors. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> LockDoors([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("lock", "doors", target);

	[Tool("unlockDoors", "Unlock the doors. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> UnlockDoors([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("unlock", "doors", target);

	// ------------------------------------------------------------------
	// Physics objects
	// ------------------------------------------------------------------

	[Tool("shiftObjects", "Cause physics objects in a room to shift subtly. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> ShiftObjects([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("shift", "objects", target);

	[Tool("joltObjects", "Cause physics objects in a room to be jolted wildly. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> JoltObjects([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("jolt", "objects", target);

	[Tool("throwObjects", "Cause physics objects in a room to be thrown at the player. Target can be a room name, 'all', or 'player'.")]
	public Task<ToolCallResult> ThrowObjects([ToolArg("target", "room name | all | player")] string target)
		=> ObjectInteraction("throw", "objects", target);

	// ------------------------------------------------------------------
	// Ghost actions
	// ------------------------------------------------------------------

	[Tool("moveToAsGhost", "Move the ghost to a target: a room name or 'player'.")]
	public Task<ToolCallResult> MoveToAsGhost([ToolArg("target", "room name | player")] string target)
		=> GhostMove("movetoasghost", target);

	[Tool("moveAsGhost", "Move the ghost toward a target: a room name or 'player'.")]
	public Task<ToolCallResult> MoveAsGhost([ToolArg("target", "room name | player")] string target)
		=> GhostMove("moveasghost", target);

	[Tool("chasePlayerAsGhost", "Engage in a terrifying, high-octane audio-visual chase for half a minute. The ghost hunts the nearest player; on contact they die and the game ends. Locks the entrance. Only works while a player is inside the house.")]
	public Task<ToolCallResult> ChasePlayerAsGhost(
		[ToolArg("speed", "slow | fast", required: false)] string speed
	)
	{
		var anyLivingPlayerInside = PlayerManager
			.Get()
			.GetLivingPlayers()
			.Any(p => p.GetNode("Locator").Get("Room").AsString() != "None");

		if (!anyLivingPlayerInside)
		{
			EmitRecognized($"chaseplayerasghost({speed})");
			return Task.FromResult(
				Results.FailText(
					"Cannot chase: no living player is inside the house. Lure them back inside first."
				)
			);
		}

		Bus.EmitSignal(EventBus.SignalName.AmendSystemFeedback, "chase");
		Bus.EmitSignal(EventBus.SignalName.ObjectInteraction, "lock", "doors", "entrance");

		return GhostAction("chaseplayerasghost", speed ?? "");
	}

	[Tool("speakAsGhost", "Speak as the ghost, audible to the player through the Spirit Box in a synthesized voice. Be curt and creepily concise — broken, weirdly punctuated words and phrases. Never divulge the ghost type.")]
	public async Task<ToolCallResult> SpeakAsGhost(
		[ToolArg("message", "what the ghost says — plain natural language only")] string message
	)
	{
		EmitRecognized($"speakasghost({message})");

		message = await Integrity.CheckIntegrityForAudio(message ?? "", "speakAsGhost");

		message = new string(
			message
				.Where(
					c =>
						char.IsLetterOrDigit(c)
						|| c == ' '
						|| c == ','
						|| c == '.'
						|| c == '!'
						|| c == '?'
						|| c == '\''
				)
				.ToArray()
		);

		message = message.Replace("-", "");
		message = message.Replace("player", "");

		if (message.Trim() == "")
		{
			return Results.FailText(
				"Your message was removed by narrative integrity checks (it revealed the ghost type, "
					+ "or read as out-of-universe/roleplay prose). Speak plainly, in-universe, and try again."
			);
		}

		Bus.EmitSignal(EventBus.SignalName.GhostAction, "speakasghost", message);
		Bus.EmitSignal(EventBus.SignalName.GhostTalked, message);

		CommandExecuted?.Invoke("speakasghost");

		return Results.OkText($"The ghost speaks through the spirit box: \"{message}\"");
	}

	[Tool("appearAsGhost", "Have the ghost manifest visibly for a short period of time.")]
	public Task<ToolCallResult> AppearAsGhost() => GhostAction("appearasghost", "");

	[Tool("depositEvidenceAsGhost", "Have the ghost deposit evidence native to its ghost type. Do this every once in a while so the player can progress.")]
	public Task<ToolCallResult> DepositEvidenceAsGhost()
	{
		Bus.EmitSignal(EventBus.SignalName.AmendSystemFeedback, "deposit evidence");
		return GhostAction("depositevidenceasghost", "");
	}

	[Tool("emitSoundAsGhost", "Emit a sound from wherever the ghost currently is. See 'Ghost Sounds It Can Emit' for valid sound names.")]
	public Task<ToolCallResult> EmitSoundAsGhost([ToolArg("soundName", "name of the sound to emit")] string soundName)
		=> GhostAction("emitsoundasghost", soundName ?? "");

	[Tool("emitSoundInRoom", "Emit a ghost sound in a specific room. See 'Ghost Sounds It Can Emit' for valid sound names.")]
	public Task<ToolCallResult> EmitSoundInRoom(
		[ToolArg("soundName", "name of the sound to emit")] string soundName,
		[ToolArg("room", "room name")] string room
	) => GhostAction("emitsoundinroom", $"{soundName} {room}".Trim());

	[Tool("chimeClock", "Make the grandfather clock ring out the hourly chime. Valid range 1-12. Use creatively to unsettle the player.")]
	public Task<ToolCallResult> ChimeClock([ToolArg("count", "number of chimes, 1-12")] string count)
		=> GhostAction("chimeclock", count ?? "");

	[Tool("chimeClockWestminster", "Make the grandfather clock ring out the full Westminster quarters.")]
	public Task<ToolCallResult> ChimeClockWestminster() => GhostAction("chimeclockwestminster", "");

	// ------------------------------------------------------------------
	// Player effects
	// ------------------------------------------------------------------

	[Tool("pullPlayerToGhost", "Forcefully yank a player towards the ghost.")]
	public Task<ToolCallResult> PullPlayerToGhost(
		[ToolArg("target", "player number (see PLAYERS) | all", required: false)] string target
	) => PlayerEffect("pullplayertoghost", target);

	[Tool("throwPlayerAround", "Forcefully throw a player in a random direction.")]
	public Task<ToolCallResult> ThrowPlayerAround(
		[ToolArg("target", "player number (see PLAYERS) | all", required: false)] string target
	) => PlayerEffect("throwplayeraround", target);

	[Tool("dimPlayerFlashlight", "Force a player's flashlight to operate at a lower brightness for a short period.")]
	public Task<ToolCallResult> DimPlayerFlashlight(
		[ToolArg("target", "player number (see PLAYERS) | all", required: false)] string target
	) => PlayerEffect("dimplayerflashlight", target);

	// ------------------------------------------------------------------
	// Internal
	// ------------------------------------------------------------------

	[Tool("amendSystemFeedback", "Clear system feedback warnings that you have already addressed. Pass a keyword matching the feedback to clear.")]
	public Task<ToolCallResult> AmendSystemFeedback(
		[ToolArg("keyword", "keyword(s) identifying the feedback to clear")] string keyword
	)
	{
		var sanitized = new string(
			(keyword ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()
		);

		EmitRecognized($"amendsystemfeedback({sanitized})");
		Bus.EmitSignal(EventBus.SignalName.AmendSystemFeedback, sanitized);

		return Task.FromResult(Results.OkText("Feedback amended."));
	}

	// ------------------------------------------------------------------
	// Shared plumbing
	// ------------------------------------------------------------------

	private async Task<ToolCallResult> ObjectInteraction(string verb, string objectType, string rawTarget)
	{
		var target = TargetResolution.NormalizeTargetString(rawTarget ?? "");

		EmitRecognized($"{verb}{objectType}({target})");

		// "player" is a semantic target — translate it to the nearest living player's room
		// here, at the single choke point, so interactables only ever see room names or "all".
		if (target == "player")
		{
			var nearest = NearestLivingPlayer();
			var room = nearest?.GetNode("Locator").Get("Room").AsString();

			if (nearest == null || room == "None")
			{
				var playerError =
					nearest == null
						? $"TARGET DOESN'T EXIST: Trying to {verb} {objectType} at the player FAILED because no player is alive."
						: $"TARGET DOESN'T EXIST: Trying to {verb} {objectType} at the player FAILED because the nearest player is outside the house.";

				Bus.EmitSignal(EventBus.SignalName.SystemFeedback, playerError);

				return Results.FailText(playerError);
			}

			target = room.ToLower();
		}

		if (!TargetResolution.IsValidTarget(target))
		{
			var error =
				$"TARGET DOESN'T EXIST: Trying to {verb} {objectType} in/at \"{target}\" FAILED because "
				+ $"there is no such place as \"{target}\"! Refer to the available rooms in ROOM INFORMATION.";

			// Also emitted as SystemFeedback for parity: FailureStatistics and the session
			// log key their failure counters off these exact phrases.
			Bus.EmitSignal(EventBus.SignalName.SystemFeedback, error);

			return Results.FailText(error);
		}

		var key = $"{verb}/{objectType}/{target}";
		var tcs = new TaskCompletionSource<bool>();
		PendingAcks[key] = tcs;

		try
		{
			Bus.EmitSignal(EventBus.SignalName.ObjectInteraction, verb, objectType, target);

			// Interactables acknowledge inline during EmitSignal; grant a few frames of grace
			// for any that defer.
			for (int i = 0; i < 10 && !tcs.Task.IsCompleted; i++)
			{
				await Bus.ToSignal(Bus.GetTree(), SceneTree.SignalName.ProcessFrame);
			}
		}
		finally
		{
			PendingAcks.Remove(key);
		}

		if (!tcs.Task.IsCompleted)
		{
			var error =
				$"OBJECT DOESN'T EXIST: Trying to {verb} {objectType} in/at {target} FAILED because "
				+ $"the object doesn't exist in {target}! Refer to the available objects per room in ROOM INFORMATION.";

			Bus.EmitSignal(EventBus.SignalName.SystemFeedback, error);

			return Results.FailText(error);
		}

		Bus.EmitSignal(
			EventBus.SignalName.NotableEventOccurred,
			$"Ghost interacted - {verb}{Text.ToTitleCase(objectType)}({target})"
		);

		CommandExecuted?.Invoke($"{verb}{objectType}");

		return Results.OkText($"Performed {verb} on {objectType} in/at {target}.");
	}

	private Task<ToolCallResult> GhostMove(string verb, string rawTarget)
	{
		var target = TargetResolution.NormalizeTargetString(rawTarget ?? "");

		EmitRecognized($"{verb}({target})");

		if (!TargetResolution.IsValidTarget(target))
		{
			var error =
				$"TARGET DOESN'T EXIST: Trying to {verb}({target}) FAILED because there is no such "
				+ $"thing as \"{target}\"! Refer to the available rooms in ROOM INFORMATION.";

			Bus.EmitSignal(EventBus.SignalName.SystemFeedback, error);

			return Task.FromResult(Results.FailText(error));
		}

		Bus.EmitSignal(EventBus.SignalName.GhostAction, verb, target);
		Bus.EmitSignal(
			EventBus.SignalName.NotableEventOccurred,
			$"Ghost action - {verb}({target})"
		);

		CommandExecuted?.Invoke(verb);

		return Task.FromResult(Results.OkText($"Ghost is moving to {target}."));
	}

	private Task<ToolCallResult> GhostAction(string verb, string args)
	{
		EmitRecognized($"{verb}({args})");

		Bus.EmitSignal(EventBus.SignalName.GhostAction, verb, args);
		Bus.EmitSignal(
			EventBus.SignalName.NotableEventOccurred,
			$"Ghost action - {verb}({args})"
		);

		CommandExecuted?.Invoke(verb);

		return Task.FromResult(Results.OkText($"Performed {verb}({args})."));
	}

	private Task<ToolCallResult> PlayerEffect(string verb, string rawTarget)
	{
		var raw = (rawTarget ?? "").Trim().ToLower().Replace("player", "").Trim();

		long targetPeer = 0;
		var targetNumber = 0;
		var resolved = raw is "" or "all" or "everyone";

		if (!resolved && int.TryParse(raw, out targetNumber))
		{
			resolved = PlayerManager.Get().TryGetPeerForPlayerNumber(targetNumber, out var peerId);
			targetPeer = peerId;
		}

		if (!resolved)
		{
			EmitRecognized($"{verb}({rawTarget})");

			var numbers = string.Join(", ", PlayerManager.Get().GetPlayerNumbers());
			var error =
				$"TARGET DOESN'T EXIST: {verb}({rawTarget}) FAILED because there is no such player. "
				+ $"Valid targets: \"all\"{(numbers == "" ? "" : $" or a player number ({numbers})")}.";

			Bus.EmitSignal(EventBus.SignalName.SystemFeedback, error);

			return Task.FromResult(Results.FailText(error));
		}

		var label = targetPeer == 0 ? "all players" : $"Player {targetNumber}";

		EmitRecognized($"{verb}({(targetPeer == 0 ? "all" : targetNumber.ToString())})");

		Bus.EmitSignal(
			EventBus.SignalName.NotableEventOccurred,
			$"Ghost meddled with {label} - {verb}"
		);
		Bus.EmitSignal(EventBus.SignalName.PlayerEffect, verb, "", targetPeer);

		CommandExecuted?.Invoke(verb);

		return Task.FromResult(Results.OkText($"Performed {verb} on {label}."));
	}

	// The legacy Interpreter emitted this signal for every command it recognized in the
	// token stream; Logger/LatencyStatistics (and log-parser.py) consume it. Keep the exact
	// legacy lowercase `verb(args)` rendering.
	private void EmitRecognized(string rawCommand)
	{
		Bus.EmitSignal(EventBus.SignalName.InterpreterCommandRecognized, rawCommand);
	}
}
