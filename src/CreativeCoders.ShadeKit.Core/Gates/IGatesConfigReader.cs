using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Reads the gate configuration <c>.shadekit/gates.json</c> of a SHADE kit store once per store.
/// </summary>
public interface IGatesConfigReader
{
    /// <summary>
    /// Reads the gate configuration of the specified store, or returns the result of an earlier successful read of the
    /// same store instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is read with <see cref="Formats.FormatAccess.Read" />. A missing file or a missing <c>profiles</c> field
    /// gives the profiles of <see cref="GatesConfig.CreateDefault" /> and a <see cref="GatesDefaultsUsedWarning" />;
    /// the other fields of an existing file are kept. An explicit empty <c>profiles</c> field is used as it is, without
    /// defaults and without a warning. Every entry of the whole file that names an unknown profile, a phase that its
    /// profile does not have, or the first phase of its profile gives a <see cref="GateEntryIgnoredWarning" />, in
    /// file order; the entry stays in the configuration.
    /// </para>
    /// <para>
    /// The result is cached per store instance: a later call with the same store returns the same result, warnings
    /// included, without reading the file again, and a call with another store instance reads the file again. Callers
    /// must not change the returned configuration. Calls that run while a read of the same store is in progress share
    /// that read and its outcome; the read runs with the cancellation token of the call that started it. A failed or
    /// canceled read is not cached, so a call after it reads the file again.
    /// </para>
    /// </remarks>
    /// <param name="store">The store whose <c>gates.json</c> is read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the gate configuration and the
    /// warnings of reading it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    /// <exception cref="InvalidKitFileException"><c>gates.json</c>, or <c>config.json</c> when it is read for the setting, is invalid.</exception>
    /// <exception cref="FormatTooNewException">
    /// <c>gates.json</c>, or <c>config.json</c> when it is read for the setting, uses a format version newer than the
    /// engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// <c>gates.json</c> uses an older format version and <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    Task<GatesConfigReadResult> ReadAsync(IKitStore store, CancellationToken cancellationToken = default);
}
