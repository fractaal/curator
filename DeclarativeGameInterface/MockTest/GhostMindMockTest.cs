using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Headless end-to-end proof of the AgenticCore swap, no network and no game scene needed:
/// a scripted MockLLMClient drives the real AgenticEntity + GhostTools + EventBus path and
/// asserts the legacy signal contract held. Run with:
///
///   godot --headless res://DeclarativeGameInterface/MockTest/GhostMindMockTest.tscn
///
/// Exits 0 on PASS, 1 on FAIL (10s timeout).
/// </summary>
/// <summary>Stands in for player.gd: exposes the two script properties PlayerManager
/// reads off real players (dead, player_number).</summary>
public partial class FakeCrewPlayer : Node3D
{
	public bool dead = false;
	public int player_number = 2;
}

public partial class GhostMindMockTest : Node
{
	private class TestBehavior : IAgenticBehavior
	{
		private readonly IToolSource Source;

		public AgenticEntity Agentic { get; set; }

		public TestBehavior(IToolSource source)
		{
			Source = source;
		}

		public List<LLMMessage> BuildEphemeralContext(List<LLMMessage> persistentContext)
		{
			var messages = new List<LLMMessage>
			{
				LLMMessage.FromText("system", "Mock test harness.").WithCacheBreakpoint()
			};
			messages.AddRange(persistentContext);
			return messages;
		}

		public List<Tool> GetAvailableTools() => Source.GetTools(null);

		public async Task<ToolCallResult> ExecuteToolCall(ToolCall toolCall) =>
			await Source.Invoke(toolCall, null);
	}

	private AgenticEntity Entity;
	private bool Finished = false;
	private double Elapsed = 0;

	private bool SawFlickerKitchen = false;
	private bool SawGhostTalked = false;
	private bool SawTargetFailFeedback = false;
	private bool SawThrowAtPlayer2 = false;
	private int RecognizedCount = 0;

	public override void _Ready()
	{
		var bus = EventBus.Get();

		// A fake room so TargetResolution recognizes "kitchen".
		var kitchen = new Node3D { Name = "Kitchen" };
		AddChild(kitchen);
		kitchen.AddToGroup("rooms");

		// A fake interactable: acknowledges light interactions in the kitchen, inline,
		// exactly like GameLight.gd does.
		bus.ObjectInteraction += (verb, objectType, target) =>
		{
			if (objectType == "lights" && target == "kitchen")
			{
				bus.EmitSignal(
					EventBus.SignalName.ObjectInteractionAcknowledged,
					verb,
					objectType,
					target
				);
			}
		};

		bus.ObjectInteraction += (verb, objectType, target) =>
		{
			if (verb == "flicker" && objectType == "lights" && target == "kitchen")
			{
				SawFlickerKitchen = true;
			}
		};

		bus.GhostTalked += (message) =>
		{
			if (message.Contains("Cold"))
			{
				SawGhostTalked = true;
			}
		};

		bus.SystemFeedback += (message) =>
		{
			if (message.Contains("TARGET DOESN'T EXIST"))
			{
				SawTargetFailFeedback = true;
			}
		};

		bus.InterpreterCommandRecognized += (_) =>
		{
			RecognizedCount++;
		};

		bus.PlayerEffect += (verb, arguments, targetPeerId) =>
		{
			if (verb == "throwplayeraround" && targetPeerId == 2)
			{
				SawThrowAtPlayer2 = true;
			}
		};

		// A fake registered crew member so player-targeted effects can resolve "2".
		var fakePlayer = new FakeCrewPlayer { Name = "Player_2" };
		AddChild(fakePlayer);
		PlayerManager.Get().RegisterPlayer(2, fakePlayer);

		var integrity = GetNode<NarrativeIntegrity>("/root/NarrativeIntegrity");
		var tools = new GhostTools(bus, integrity);
		var behavior = new TestBehavior(new ReflectionToolSource(tools));

		var script = new List<MockLLMClient.Step>
		{
			MockLLMClient.MakeToolCall(
				"flickerLights",
				new JsonObject { ["target"] = "kitchen" },
				assistantMsg: "Flickering the kitchen lights."
			),
			MockLLMClient.MakeToolCall(
				"flickerLights",
				new JsonObject { ["target"] = "basement" },
				assistantMsg: "Trying the basement."
			),
			MockLLMClient.MakeToolCall(
				"speakAsGhost",
				new JsonObject { ["message"] = "Cold. Cold. Cold." },
				assistantMsg: ""
			),
			MockLLMClient.MakeToolCall(
				"throwPlayerAround",
				new JsonObject { ["target"] = "2" },
				assistantMsg: "Tormenting Player 2."
			),
			MockLLMClient.MakeToolCall(
				"pullPlayerToGhost",
				new JsonObject { ["target"] = "7" },
				assistantMsg: "Trying a player that doesn't exist."
			),
			MockLLMClient.MakeAssistant("Done."),
		};

		Entity = new AgenticEntity(behavior, new MockLLMClient(script));
		behavior.Agentic = Entity;

		Entity.Config = new AgenticEntityConfig
		{
			OptimalTurnaroundTime = 0.0,
			InitialStartDelay = 0.0,
			WarnOnNoToolCalls = false,
		};

		Entity.ThinkingFinished += (_) => Finished = true;

		Entity.AddMessage(LLMMessage.FromText("user", "Begin the haunt."));

		GD.Print("[MockTest] Starting scripted think cycle...");
		_ = Entity.Think();
	}

	public override void _Process(double delta)
	{
		Entity?.Process(delta);

		Elapsed += delta;

		if (Finished)
		{
			Conclude();
		}
		else if (Elapsed > 10.0)
		{
			GD.PrintErr("[MockTest] TIMEOUT — think cycle did not complete in 10s");
			Conclude();
		}
	}

	private void Conclude()
	{
		SetProcess(false);

		var toolMessages = Entity
			.PersistentContext
			.Where(m => m != null && m.Role == "tool")
			.Select(m => GhostMind.ExtractText(m))
			.ToList();

		bool flickerSucceeded = toolMessages.Any(t => t.Contains("flicker") && t.Contains("kitchen"));
		bool basementFailed = toolMessages.Any(t => t.Contains("TARGET DOESN'T EXIST"));
		bool badPlayerFailed = toolMessages.Any(t => t.Contains("no such player"));

		var checks = new List<(string name, bool pass)>
		{
			("ObjectInteraction(flicker, lights, kitchen) emitted", SawFlickerKitchen),
			("flickerLights(kitchen) tool result returned", flickerSucceeded),
			("flickerLights(basement) failed with TARGET DOESN'T EXIST tool result", basementFailed),
			("bad target also emitted legacy SystemFeedback", SawTargetFailFeedback),
			("GhostTalked emitted for speakAsGhost", SawGhostTalked),
			("throwPlayerAround(2) emitted PlayerEffect targeted at peer 2", SawThrowAtPlayer2),
			("pullPlayerToGhost(7) failed with no-such-player tool result", badPlayerFailed),
			("InterpreterCommandRecognized emitted per tool call (>=5)", RecognizedCount >= 5),
			("think cycle completed", Finished),
		};

		bool allPass = true;

		foreach (var (name, pass) in checks)
		{
			GD.Print($"[MockTest] {(pass ? "PASS" : "FAIL")} — {name}");
			allPass &= pass;
		}

		GD.Print(allPass ? "[MockTest] === ALL PASS ===" : "[MockTest] === FAILED ===");

		GetTree().Quit(allPass ? 0 : 1);
	}
}
