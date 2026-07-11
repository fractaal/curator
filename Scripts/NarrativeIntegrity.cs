using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class NarrativeIntegrity : Node
{
	private Node GhostData;

	private string SanitizerPrompt = FileAccess
		.Open(
			"res://DeclarativeGameInterface/prompts/SanitizerPrompt.txt",
			FileAccess.ModeFlags.Read
		)
		.GetAsText();

	private EventBus Bus;

	private List<string> SuspiciousSubstrings = new List<string>()
	{
		"=",
		"*",
		";",
		":",
		"[",
		"]",
		"/",
		"\\",
		"+",
		"-",
		"<",
		">",
		"volume",
		"filter",
		"distorted",
		"voice",
		"room",
		"resolve",
		"target",
		"arg",
		"param",
		"whisper",
		"message",
		"roleplay",
		"universe",
		"language",
		"vague",
		"nonsensical",
		"context",
		"natural"
	};

	public override void _Ready()
	{
		GhostData = GetNode<Node>("/root/GhostData");
		Bus = EventBus.Get();

		GD.Print(SanitizerPrompt);
	}

	public async Task<string> CheckIntegrityForAudio(string message, string action)
	{
		// Only the server should make LLM requests in multiplayer
		if (!Multiplayer.IsServer())
		{
			GD.Print("NarrativeIntegrity: Skipping integrity check - not server, returning original message");
			return message ?? "";
		}

		var rooms = GetTree().GetNodesInGroup("rooms").Select(room => room.Name.ToString());
		var ghostTypes = GhostData.Call("GetGhostTypes").AsStringArray();

		var substrings = new List<string>(SuspiciousSubstrings);
		substrings.AddRange(rooms);
		substrings.AddRange(ghostTypes);

		if (message == null)
		{
			GD.PushWarning("Integrity check received null message.");
			return "";
		}

		if (message.Length < 1)
		{
			GD.PushWarning("Integrity check received empty message.");
			return "";
		}

		List<string> problemSubstrings = new List<string>();

		foreach (string substring in substrings)
		{
			if (message.ToLower().Contains(substring.ToLower()))
			{
				GD.PushWarning(
					$"Message contains suspicious substring: {substring} - amending message."
				);

				problemSubstrings.Add(substring);
			}
		}

		if (problemSubstrings.Count > 0)
		{
			var sanitized = await GhostMind.AuxCompleteAsync(
				new List<LLMMessage>()
				{
					LLMMessage.FromText("system", SanitizerPrompt),
					LLMMessage.FromText(
						"user",
						"\"I see you.\", voice=distorted, filter=low, distant."
					),
					LLMMessage.FromText(
						"assistant",
						"VERDICT: Hallucinated configuration parameters. Text should *just be natural language.*\nI see you."
					),
					LLMMessage.FromText("user", "Whispered message"),
					LLMMessage.FromText(
						"assistant",
						"VERDICT: Vague, roleplaying, descriptive prose - nonsensical or out of context in the context of in-universe speech."
					),
					LLMMessage.FromText("user", "Low, growling, whispered message"),
					LLMMessage.FromText(
						"assistant",
						"VERDICT: Vague, roleplaying, descriptive prose - nonsensical or out of context in the context of in-universe speech."
					),
					LLMMessage.FromText("user", "*Laughing.* You think you can catch me?"),
					LLMMessage.FromText(
						"assistant",
						"VERDICT: Roleplaying - nonsensical or out of context in the context of in-universe speech.\nYou think you can catch me?"
					),
					LLMMessage.FromText("user", "Mark Walker"),
					LLMMessage.FromText("assistant", "Mark Walker"),
					LLMMessage.FromText("user", "My name? David Requinton."),
					LLMMessage.FromText("assistant", "My name? David Requinton."),
					LLMMessage.FromText("user", "I died in this room."),
					LLMMessage.FromText("assistant", "I died in this room."),
					LLMMessage.FromText("user", "I died 200 years ago."),
					LLMMessage.FromText("assistant", "I died 200 years ago."),
					LLMMessage.FromText("user", "I am a demon."),
					LLMMessage.FromText(
						"assistant",
						"VERDICT: **Divulged ghost type**! Breaks narrative integrity by revealing ghost type.\nHow pitiful."
					),
					LLMMessage.FromText("user", "Do you feel my gaze upon you?"),
					LLMMessage.FromText("assistant", "Do you feel my gaze upon you?"),
					LLMMessage.FromText("user", message)
				}
			);

			string reason = "";
			string sanitizedMessage = "";

			foreach (string line in sanitized.Split("\n"))
			{
				if (line.Contains("VERDICT"))
				{
					reason = line.Split(":")[1].Trim();
				}
				else
				{
					sanitizedMessage += line + "\n";
				}
			}

			if (reason == "")
			{
				GD.PushWarning("Sanitizer did not return a verdict.");
				return sanitizedMessage;
			}

			if (sanitizedMessage == "")
			{
				Bus.EmitSignal(
					EventBus.SignalName.SystemFeedback,
					$"NARRATIVE INTEGRITY FAILURE: During {action}, you generated -- \"{message}\" -- as output."
						+ $"This is not allowed, because: **{reason}.** Your message has been completely removed."
						+ "To prevent future removals, abide by narrative integrity, and respect the game objective."
				);
			}
			else
			{
				Bus.EmitSignal(
					EventBus.SignalName.SystemFeedback,
					$"NARRATIVE INTEGRITY FAILURE: During {action}, you generated -- \"{message}\" -- as output."
						+ $"This is not allowed, because: **{reason}.** Your message has been amended to -- \"{sanitizedMessage}\" -- "
						+ "Please take note of this in the future and abide by narrative integrity, the example of the amended message,"
						+ "and respect the game objective."
				);
			}

			return sanitizedMessage;
		}

		return message;
	}
}
