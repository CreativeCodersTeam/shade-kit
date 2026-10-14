namespace CreativeCoders.ShadeKit.Core.Config;

/// <summary>
/// Specifies what a read command does with a versioned file in an older format version.
/// </summary>
public enum OutdatedFormatOnReadPolicy
{
    /// <summary>
    /// The file is migrated in memory, the command returns its result, and an <c>upgrade-needed</c> warning is
    /// reported. Stored as <c>migrate-and-warn</c>.
    /// </summary>
    MigrateAndWarn,

    /// <summary>
    /// The command stops and reports that <c>shade-kit upgrade</c> is needed, like a write command. Stored as
    /// <c>stop</c>.
    /// </summary>
    Stop
}
