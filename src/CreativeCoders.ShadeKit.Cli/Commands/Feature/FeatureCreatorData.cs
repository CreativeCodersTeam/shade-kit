using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature;

/// <summary>
/// Represents the <c>createdBy</c> object in the JSON output of the feature commands.
/// </summary>
/// <param name="Name">The git user name of the person who created the feature.</param>
/// <param name="Email">The git user email of the person who created the feature.</param>
public sealed record FeatureCreatorData(string Name, string Email)
{
    /// <summary>
    /// Creates the data from the creator of a feature.
    /// </summary>
    /// <param name="creator">The creator of the feature.</param>
    /// <returns>The data with the name and the email of <paramref name="creator" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="creator" /> is <see langword="null" />.</exception>
    public static FeatureCreatorData FromCreator(FeatureCreator creator)
    {
        Ensure.NotNull(creator);

        return new FeatureCreatorData(creator.Name, creator.Email);
    }
}
