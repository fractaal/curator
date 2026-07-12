using System;
using System.Collections.Generic;
using System.Text;

public enum HuntLintSeverity
{
	Warning,
}

public sealed class HuntLintWarning
{
	public string Code { get; }
	public string Title { get; }
	public string Message { get; }

	public HuntLintWarning(string code, string title, string message)
	{
		Code = code;
		Title = title;
		Message = message;
	}
}

/// <summary>
/// Advisory checks on a HuntTick script, run at install/patch time and reported in
/// the tool result. Structural pattern follows Emergent-Agentics' ComputerCoreLinter,
/// but the tactical doctrine checks don't transfer — these are the hunt-domain
/// analogues. Compile errors are handled by HuntCore, not here.
/// </summary>
public static class HuntScriptLinter
{
	public static List<HuntLintWarning> Lint(string source)
	{
		var warnings = new List<HuntLintWarning>();
		if (string.IsNullOrWhiteSpace(source))
		{
			return warnings;
		}

		var cleaned = StripStringsAndComments(source);

		if (CountUpdateDefinitions(cleaned) > 1)
		{
			warnings.Add(
				new HuntLintWarning(
					"HNT00",
					"Multiple update() definitions.",
					"Only the last update() is executed. Submit exactly one update(ctx, api, inputs, state) definition."
				)
			);
		}

		if (!cleaned.Contains("api.", StringComparison.Ordinal))
		{
			warnings.Add(
				new HuntLintWarning(
					"HNT01",
					"Script never acts.",
					"update() makes no api calls at all — the ghost will stand still for the whole hunt. "
						+ "Use api.moveToward / api.lungeAt / api.<tool> to actually hunt."
				)
			);
		}

		bool readsSenses =
			cleaned.Contains("stimuli", StringComparison.Ordinal)
			|| cleaned.Contains("lineOfSight", StringComparison.Ordinal)
			|| cleaned.Contains("lastKnown", StringComparison.Ordinal);
		if (!readsSenses)
		{
			warnings.Add(
				new HuntLintWarning(
					"HNT02",
					"Blind hunting.",
					"update() never reads ctx.stimuli, ctx.lineOfSight, or ctx.lastKnown. "
						+ "You are hunting without senses — write heuristics that react to what the ghost hears and sees, "
						+ "not a fixed patrol. **This will not catch anyone.**"
				)
			);
		}

		if (!cleaned.Contains("api.log", StringComparison.Ordinal))
		{
			warnings.Add(
				new HuntLintWarning(
					"OBS01",
					"No api.log() calls in update function.",
					"Your script has no observable logging. api.log() lines become your post-hunt debrief — "
						+ "without them you cannot see what your instincts did, and silent failures are invisible."
				)
			);
		}

		return warnings;
	}

	public static string BuildWarningText(IReadOnlyList<HuntLintWarning> warnings)
	{
		if (warnings == null || warnings.Count == 0)
		{
			return string.Empty;
		}

		var sb = new StringBuilder(256);
		sb.AppendLine("⚠️ INSTINCT QUALITY WARNINGS (script installed, but improve it soon):");
		foreach (var warning in warnings)
		{
			sb.AppendLine($"{warning.Code} {warning.Title}");
			sb.AppendLine(warning.Message);
			sb.AppendLine("");
		}
		return sb.ToString().TrimEnd();
	}

	// "function update(" declarations plus "update = function(" assignments,
	// counted on comment/string-stripped source.
	private static int CountUpdateDefinitions(string cleaned)
	{
		int count = 0;
		int index = 0;
		while ((index = cleaned.IndexOf("function", index, StringComparison.Ordinal)) >= 0)
		{
			int nameStart = index + "function".Length;
			while (nameStart < cleaned.Length && char.IsWhiteSpace(cleaned[nameStart]))
			{
				nameStart++;
			}
			if (nameStart + "update".Length <= cleaned.Length
				&& cleaned.AsSpan(nameStart).StartsWith("update")
				&& (nameStart + "update".Length >= cleaned.Length
					|| !char.IsLetterOrDigit(cleaned[nameStart + "update".Length])))
			{
				count++;
			}
			index += "function".Length;
		}

		index = 0;
		while ((index = cleaned.IndexOf("update", index, StringComparison.Ordinal)) >= 0)
		{
			bool startBoundary = index == 0 || !char.IsLetterOrDigit(cleaned[index - 1]);
			int after = index + "update".Length;
			while (after < cleaned.Length && char.IsWhiteSpace(cleaned[after]))
			{
				after++;
			}
			if (startBoundary && after < cleaned.Length && cleaned[after] == '=')
			{
				int rhs = after + 1;
				while (rhs < cleaned.Length && char.IsWhiteSpace(cleaned[rhs]))
				{
					rhs++;
				}
				if (rhs < cleaned.Length && cleaned.AsSpan(rhs).StartsWith("function"))
				{
					count++;
				}
			}
			index += "update".Length;
		}

		return count;
	}

	private static string StripStringsAndComments(string source)
	{
		var sb = new StringBuilder(source.Length);
		bool inLineComment = false;
		bool inBlockComment = false;
		char stringQuote = '\0';

		for (int i = 0; i < source.Length; i++)
		{
			char c = source[i];
			char next = i + 1 < source.Length ? source[i + 1] : '\0';

			if (inLineComment)
			{
				if (c == '\n')
				{
					inLineComment = false;
					sb.Append(c);
				}
				continue;
			}
			if (inBlockComment)
			{
				if (c == '*' && next == '/')
				{
					inBlockComment = false;
					i++;
				}
				continue;
			}
			if (stringQuote != '\0')
			{
				if (c == '\\')
				{
					i++;
				}
				else if (c == stringQuote)
				{
					stringQuote = '\0';
				}
				continue;
			}

			if (c == '/' && next == '/')
			{
				inLineComment = true;
				i++;
				continue;
			}
			if (c == '/' && next == '*')
			{
				inBlockComment = true;
				i++;
				continue;
			}
			if (c == '"' || c == '\'' || c == '`')
			{
				stringQuote = c;
				continue;
			}

			sb.Append(c);
		}

		return sb.ToString();
	}
}
