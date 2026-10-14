using System.Reflection;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Engine;

/// <summary>
/// Provides the engine version taken from the assembly attributes of the SHADE kit core assembly.
/// </summary>
/// <remarks>
/// The version is the <see cref="AssemblyInformationalVersionAttribute" /> without its build metadata, such as the
/// source revision after the first <c>+</c> sign. If the attribute is missing or yields an empty or whitespace
/// version, the assembly version is used instead.
/// </remarks>
public sealed class EngineVersionProvider : IEngineVersionProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EngineVersionProvider" /> class for the SHADE kit core assembly.
    /// </summary>
    public EngineVersionProvider()
        : this(typeof(EngineVersionProvider).Assembly)
    {
    }

    internal EngineVersionProvider(Assembly assembly)
    {
        Version = GetVersion(Ensure.NotNull(assembly));
    }

    /// <inheritdoc />
    public string Version { get; }

    private static string GetVersion(Assembly assembly)
    {
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+', 2)[0];

        return string.IsNullOrWhiteSpace(version)
            ? assembly.GetName().Version?.ToString() ?? string.Empty
            : version;
    }
}
