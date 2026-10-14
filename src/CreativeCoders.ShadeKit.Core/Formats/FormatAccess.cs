namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Specifies whether a command reads a versioned file only, reads it in order to change it, or migrates it to the
/// current format.
/// </summary>
public enum FormatAccess
{
    /// <summary>
    /// The command only reads the file. A file in an older format is handled according to the
    /// <c>outdatedFormatOnRead</c> setting.
    /// </summary>
    Read,

    /// <summary>
    /// The command reads the file in order to write it. A file in an older format stops the command, because only
    /// <c>shade-kit upgrade</c> may migrate files.
    /// </summary>
    Write,

    /// <summary>
    /// The command migrates the file to the current format, as <c>shade-kit upgrade</c> does. A file in an older
    /// format is migrated in memory whatever the <c>outdatedFormatOnRead</c> setting says, and may be replaced by a
    /// file in the current format.
    /// </summary>
    Upgrade
}
