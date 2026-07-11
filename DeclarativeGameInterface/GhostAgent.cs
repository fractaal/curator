using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// The ghost's IAgenticBehavior: assembles the per-turn context from Sensors (persona + game
/// info at the head, rolling history in the middle, the volatile status/feedback tail) and
/// exposes GhostTools through reflection.
///
/// Also owns the cycle-end housekeeping the old Interpreter did in its last-chunk handler:
/// repetition warnings, unused-command nudges, and the evidence/chase cadence counters that
/// keep the AI director honest. Same feedback strings, delivered through the same
/// SystemFeedback accumulator.
/// </summary>
public class GhostAgent : IAgenticBehavior
{
	private readonly Sensors Sensors;
	private readonly GhostTools Tools;
	private readonly IToolSource ToolSource;

	public AgenticEntity Agentic { get; set; }

	private readonly List<string> RecentCommands = new();
	private int Cycles = 0;

	private bool GhostDepositedEvidence = false;
	private int CyclesSinceLastEvidenceDeposit = 0;

	private bool GhostHasChased = false;
	private int CyclesSinceLastChase = 0;

	// Legacy lowercase verb vocabulary, for the periodic "you haven't used these" nudge.
	private static readonly List<string> NudgeableVerbs =
		new()
		{
			"turnofflights",
			"flickerlights",
			"explodelights",
			"restorelights",
			"turnonlights",
			"turnonradios",
			"turnoffradios",
			"playfreakymusiconradios",
			"stopradios",
			"opendoors",
			"closedoors",
			"lockdoors",
			"unlockdoors",
			"shiftobjects",
			"joltobjects",
			"throwobjects",
			"moveasghost",
			"movetoasghost",
			"chaseplayerasghost",
			"speakasghost",
			"appearasghost",
			"depositevidenceasghost",
			"emitsoundasghost",
			"emitsoundinroom",
			"chimeclockwestminster",
			"chimeclock"
		};

	public GhostAgent(Sensors sensors, GhostTools tools)
	{
		Sensors = sensors;
		Tools = tools;
		ToolSource = new ReflectionToolSource(tools);

		Tools.CommandExecuted += OnCommandExecuted;
	}

	private void OnCommandExecuted(string verb)
	{
		RecentCommands.Add(verb);

		while (RecentCommands.Count > 10)
		{
			RecentCommands.RemoveAt(0);
		}

		if (verb == "depositevidenceasghost")
		{
			GhostDepositedEvidence = true;
			CyclesSinceLastEvidenceDeposit = 0;
		}

		if (verb == "chaseplayerasghost")
		{
			GhostHasChased = true;
			CyclesSinceLastChase = 0;
		}
	}

	public List<LLMMessage> BuildEphemeralContext(List<LLMMessage> persistentContext)
	{
		var messages = new List<LLMMessage>
		{
			// The persona is the only fully stable prefix (game info below it mutates with
			// room state), so the provider-side prompt cache breakpoint sits here. The message
			// is freshly built each turn, so marking it never mutates shared history.
			LLMMessage.FromText("system", Sensors.SystemPrompt).WithCacheBreakpoint(),
			LLMMessage.FromText("user", Sensors.GetGameInfo()),
		};

		// This turn's system feedback is already the last persistent message (Sensors persists
		// it right before Think), which lands it in the same slot the old assembler used —
		// re-adding it here would double it within the turn.
		messages.AddRange(persistentContext);

		messages.Add(LLMMessage.FromText("user", Sensors.BehaviorPrompt));
		messages.Add(LLMMessage.FromText("user", Sensors.CurrentTurnStatusPrompt));
		messages.Add(LLMMessage.FromText("user", Sensors.CurrentTurnAttentionMarkers));

		return messages;
	}

	public List<Tool> GetAvailableTools()
	{
		return ToolSource.GetTools(null);
	}

	public async Task<ToolCallResult> ExecuteToolCall(ToolCall toolCall)
	{
		return await ToolSource.Invoke(toolCall, null);
	}

	// The old Interpreter's "AI director performed no commands!" SystemFeedback is deliberately
	// NOT reproduced here: AgenticCore's WarnOnNoToolCalls (left on in EnsureMind) injects the
	// equivalent nag directly into the context, with better delivery (same turn, not next).
	public void OnThinkingCompleted(bool wasInterrupted = false)
	{
		if (wasInterrupted)
		{
			return;
		}

		var bus = EventBus.Get();

		var frequencies = new Dictionary<string, int>();

		foreach (var command in RecentCommands)
		{
			frequencies[command] = frequencies.TryGetValue(command, out var n) ? n + 1 : 1;
		}

		if (frequencies.Count > 0)
		{
			var mostFrequent = frequencies.Aggregate((l, r) => l.Value > r.Value ? l : r);

			if (mostFrequent.Value > 2)
			{
				bus.EmitSignal(
					EventBus.SignalName.SystemFeedback,
					$"WARNING: You are using {mostFrequent.Key} too often ({mostFrequent.Value} times recently). "
						+ "You are becoming predictable. VARY your commands, utilize what is available to you!"
				);

				RecentCommands.RemoveAll(match => match.Contains(mostFrequent.Key));
			}
		}

		if (Cycles > 0 && Cycles % 10 == 0)
		{
			var unusedVerbs = NudgeableVerbs.Where(verb => !RecentCommands.Contains(verb)).ToList();

			if (unusedVerbs.Count > 0)
			{
				bus.EmitSignal(
					EventBus.SignalName.SystemFeedback,
					$"WARNING: You have not used the following commands recently: {string.Join(", ", unusedVerbs)}. "
						+ "DIVERSIFY your command usage for a more engaging experience."
				);
			}
		}

		if (!GhostDepositedEvidence)
		{
			CyclesSinceLastEvidenceDeposit++;
		}

		if (!GhostHasChased)
		{
			CyclesSinceLastChase++;
		}

		if (CyclesSinceLastEvidenceDeposit > 20)
		{
			bus.EmitSignal(
				EventBus.SignalName.SystemFeedback,
				"WARNING: ⚠ AI director has not deposited evidence in a while. This UNDERMINES PLAYER AGENCY! "
					+ "Please deposit evidence to progress the game."
			);
			CyclesSinceLastEvidenceDeposit = 0;
			GhostDepositedEvidence = false;
		}

		if (CyclesSinceLastChase > 10)
		{
			bus.EmitSignal(
				EventBus.SignalName.SystemFeedback,
				"### ❗❗❗ AI director did not start ghost chase in a while. This MAKES THE GAME BORING! "
					+ "Use `chasePlayerAsGhost` to reinforce the horror element! ❗❗❗ ###"
			);
			CyclesSinceLastChase = 0;
			GhostHasChased = false;
		}

		Cycles++;
	}
}
