using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Autoload that bootstraps the AgenticCore runtime for this Godot host and exposes one-shot
/// auxiliary completions (backstory, endgame summary, speech sanitizer) on the AUX model.
/// Replaces the old LLMInterface autoload: the agentic loop itself lives in AgenticEntity
/// (owned by Sensors); this node wires the engine seams and the aux path.
/// </summary>
public partial class GhostMind : Node
{
	private static GhostMind Instance;

	private LLMClient AuxClient;

	public override void _EnterTree()
	{
		Instance = this;

		AgentLog.Current = new GodotAgentLogger();
		AgentConfig.Current = new CuratorConfigProvider();
		AgentLLM.Factory = new CuratorLLMFactory();
		MainThread.AttachPump(drain => GetTree().ProcessFrame += () => drain());
	}

	public override void _Ready()
	{
		LogManager.UpdateLog(
			"llmModel",
			"[color=\"0000FF\"]Using model [b]" + Config.Get("MODEL") + "[/b][/color]"
		);

		// The main scene doesn't exist yet while autoloads ready up.
		CallDeferred(nameof(ShowSettingsWarningIfMissing));
	}

	private void ShowSettingsWarningIfMissing()
	{
		if (!Config.SettingsFileMissing)
		{
			return;
		}

		var warning = GetTree()
			.CurrentScene
			?.GetNodeOrNull<RichTextLabel>("CenterContainer/SettingsFileMissingWarning");

		if (warning != null)
		{
			warning.Visible = true;
		}
	}

	/// <summary>
	/// One-shot completion on the AUX model (no tools, no persistent context). Returns "" on
	/// timeout or when called on a non-server peer — the same failure shape as the old
	/// LLMInterface.SendIsolated.
	/// </summary>
	public static async Task<string> AuxCompleteAsync(
		List<LLMMessage> messages,
		double timeoutSeconds = 90
	)
	{
		if (Instance == null)
		{
			GD.PrintErr("GhostMind: AuxCompleteAsync called before autoload ready");
			return "";
		}

		if (!Instance.Multiplayer.IsServer())
		{
			GD.Print("GhostMind: Skipping aux LLM request - not server");
			return "";
		}

		Instance.AuxClient ??= new RateLimitedLLMClient(
			new OpenRouterLLMClient(
				modelOverride: Config.Get("AUX_MODEL"),
				temperatureOverride: ParseFloatOrNull(Config.Get("AUX_MODEL_TEMPERATURE"))
			)
		);

		var tcs = new TaskCompletionSource<string>();

		_ = Instance.AuxClient.SendWithIndefiniteRetry(
			messages,
			null,
			onComplete: message => tcs.TrySetResult(ExtractText(message)),
			onToolCalls: (_, message) => tcs.TrySetResult(ExtractText(message))
		);

		var finished = await Task.WhenAny(
			tcs.Task,
			Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))
		);

		if (finished != tcs.Task)
		{
			GD.PushWarning($"GhostMind: Aux completion timed out after {timeoutSeconds}s");
			return "";
		}

		return await tcs.Task;
	}

	public static string ExtractText(LLMMessage message)
	{
		if (message?.Content is string text)
		{
			return text;
		}

		if (message?.Content is List<ContentPart> parts)
		{
			return ContentPartUtils.RenderText(parts);
		}

		return "";
	}

	private static float? ParseFloatOrNull(string raw)
	{
		if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
		{
			return value;
		}

		return null;
	}
}

public sealed class GodotAgentLogger : IAgentLogger
{
	public void Info(string message) => GD.Print(message);

	public void Warn(string message) => GD.PushWarning(message);

	public void Error(string message) => GD.PrintErr(message);
}

/// <summary>
/// Bridges AgenticCore's config seam onto curator's settings.txt (the Config autoload), so the
/// player-facing config file keeps its historical key names. Unknown keys pass through by name,
/// then fall back to environment variables.
/// </summary>
public sealed class CuratorConfigProvider : IConfigProvider
{
	// settings.txt is loaded once at boot and never changes at runtime.
	public event Action<string, string> Changed
	{
		add { }
		remove { }
	}

	private static string MapKey(string key)
	{
		switch (key)
		{
			case "OPEN_ROUTER_API_KEY":
				return "API_KEY";
			case "TEMPERATURE":
				return "MODEL_TEMPERATURE";
			default:
				return key;
		}
	}

	private static string Raw(string key)
	{
		var value = Config.Get(MapKey(key));

		if (string.IsNullOrEmpty(value))
		{
			value = System.Environment.GetEnvironmentVariable(key);
		}

		return value;
	}

	public string GetString(string key, string fallback = null)
	{
		var value = Raw(key);
		return string.IsNullOrEmpty(value) ? fallback : value;
	}

	public float GetFloat(string key, float fallback = 0f)
	{
		return float.TryParse(
			Raw(key),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var value
		)
			? value
			: fallback;
	}

	public int GetInt(string key, int fallback = 0)
	{
		return int.TryParse(
			Raw(key),
			NumberStyles.Integer,
			CultureInfo.InvariantCulture,
			out var value
		)
			? value
			: fallback;
	}

	public bool GetBool(string key, bool fallback = false)
	{
		var value = Raw(key);

		if (string.IsNullOrWhiteSpace(value))
		{
			return fallback;
		}

		switch (value.Trim().ToLowerInvariant())
		{
			case "true":
			case "1":
			case "yes":
			case "on":
				return true;
			case "false":
			case "0":
			case "no":
			case "off":
				return false;
			default:
				return fallback;
		}
	}

	public void SetValue(string key, string value, bool persist = true)
	{
		Config.Set(MapKey(key), value);
	}
}

public sealed class CuratorLLMFactory : ILLMClientFactory
{
	public LLMClient Create() => new RateLimitedLLMClient(new OpenRouterLLMClient());
}
