namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Represents the git identity of the user, as configured with <c>user.name</c> and <c>user.email</c>.
/// </summary>
/// <param name="Name">The configured <c>user.name</c>, without leading or trailing whitespace.</param>
/// <param name="Email">The configured <c>user.email</c>, without leading or trailing whitespace.</param>
public sealed record GitIdentity(string Name, string Email);
