using System;
using System.Text;

public static class ScriptPatcher {
	public static bool TryPatch(
		string source,
		string oldString,
		string newString,
		bool replaceAll,
		out string patched,
		out string error
	) {
		patched = null;
		error = string.Empty;

		// Line-ending normalization: we match and rewrite in LF so that a script
		// stored with CRLF (e.g. pasted from a Windows clipboard) still accepts
		// an LF-only old_string from the LLM. Otherwise a CRLF-vs-LF mismatch
		// silently fails with "not found" on text that looks identical to the LLM.
		source = NormalizeLineEndings(source);
		oldString = NormalizeLineEndings(oldString);
		newString = NormalizeLineEndings(newString);

		if (string.IsNullOrEmpty(oldString)) {
			error = "old_string is required and must be non-empty.";
			return false;
		}

		int firstIndex = source.IndexOf(oldString, StringComparison.Ordinal);
		if (firstIndex < 0) {
			error = "old_string not found in current script source.";
			return false;
		}

		if (!replaceAll) {
			int secondIndex = source.IndexOf(oldString, firstIndex + 1, StringComparison.Ordinal);
			if (secondIndex >= 0) {
				int total = CountOccurrences(source, oldString);
				error = $"old_string is ambiguous: matched {total} locations. Either add more surrounding context so it matches exactly one location, or pass replace_all=true for a global rename.";
				return false;
			}
			patched = string.Concat(
				source.Substring(0, firstIndex),
				newString,
				source.Substring(firstIndex + oldString.Length)
			);
			return true;
		}

		var sb = new StringBuilder(source.Length);
		int cursor = 0;
		while (cursor < source.Length) {
			int hit = source.IndexOf(oldString, cursor, StringComparison.Ordinal);
			if (hit < 0) {
				sb.Append(source, cursor, source.Length - cursor);
				break;
			}
			sb.Append(source, cursor, hit - cursor);
			sb.Append(newString);
			cursor = hit + oldString.Length;
		}
		patched = sb.ToString();
		return true;
	}

	public static int CountOccurrences(string source, string needle) {
		if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(needle)) return 0;
		int count = 0;
		int cursor = 0;
		while (cursor <= source.Length - needle.Length) {
			int hit = source.IndexOf(needle, cursor, StringComparison.Ordinal);
			if (hit < 0) break;
			count++;
			cursor = hit + needle.Length;
		}
		return count;
	}

	private static string NormalizeLineEndings(string value) {
		if (value == null) return string.Empty;
		if (value.IndexOf('\r') < 0) return value;
		return value.Replace("\r\n", "\n").Replace("\r", "\n");
	}

	public static string BuildNumberedSource(string source) {
		if (string.IsNullOrEmpty(source)) return "(empty)";
		var lines = source.Replace("\r\n", "\n").Split('\n');
		int width = lines.Length.ToString().Length;
		var sb = new StringBuilder(source.Length + lines.Length * (width + 3));
		for (int i = 0; i < lines.Length; i++) {
			string lineNumber = (i + 1).ToString().PadLeft(width);
			sb.Append(lineNumber);
			sb.Append(" | ");
			sb.Append(lines[i]);
			if (i < lines.Length - 1) sb.Append('\n');
		}
		return sb.ToString();
	}
}
