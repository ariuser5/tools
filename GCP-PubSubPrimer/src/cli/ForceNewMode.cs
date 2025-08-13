namespace DCiuve.Tools.Gcp.PubSub.Cli;

/// <summary>
/// Defines the behavior for creating new watches when one may already exist.
/// </summary>
public enum ForceNewMode
{
    /// <summary>
    /// Never force new watches. Reuse existing watches if they exist and are valid.
    /// </summary>
    False = 0,
    
    /// <summary>
    /// Force new watch only on the first execution, then reuse for subsequent calls.
    /// Useful for repeated execution where you want to ensure a fresh start but then maintain the same watch.
    /// </summary>
    First = 1,
    
    /// <summary>
    /// Always force new watches on every execution.
    /// Useful when you want to ensure fresh watches for reliability or testing.
    /// </summary>
    Always = 2
}
