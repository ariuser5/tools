namespace DCiuve.Gcp.Mailflow.Cli;
public static class Utils
{
	/// <summary>
	/// Parses a comma-separated Gmail labels string into a string array.
	/// Trims whitespace from each label.
	/// </summary>
	/// <param name="labels">A comma-separated string of Gmail labels.</param>
	/// <returns>Array of label strings.</returns>
	public static string[] ParseLabels(string labels)
	{
		if (string.IsNullOrWhiteSpace(labels))
			return [];

		return [.. labels
			.Split([','], StringSplitOptions.RemoveEmptyEntries)
			.Select(label => label.Trim())
			.Where(label => !string.IsNullOrEmpty(label))
		];
	}
}