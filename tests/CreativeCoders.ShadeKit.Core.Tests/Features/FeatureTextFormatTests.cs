using System.Globalization;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class FeatureTextFormatTests
{
    [Fact]
    public void FormatCreatedAt_TimeWithFractionalSecondsUnderOtherCulture_ReturnsIsoSecondsWithOffset()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 10, 20, 15, 30, 45, 123, TimeSpan.FromHours(2));
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");

        try
        {
            // Act
            var text = FeatureTextFormat.FormatCreatedAt(createdAt);

            // Assert
            text
                .Should().Be("2026-10-20T15:30:45+02:00");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FormatCreatedAt_NegativeOffset_ReturnsOffsetWithMinusSign()
    {
        // Act
        var text = FeatureTextFormat.FormatCreatedAt(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5)));

        // Assert
        text
            .Should().Be("2026-01-02T03:04:05-05:00");
    }

    [Fact]
    public void FormatCreated_Feature_ReturnsTimeByNameAndEmail()
    {
        // Arrange
        var feature = new FeatureDocument
        {
            Id = "gh-142-login-sso",
            Title = "Login SSO",
            Profile = "feature",
            Source = new FeatureSource { Kind = FeatureSourceKind.Text },
            CreatedAt = new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.Zero),
            CreatedBy = new FeatureCreator { Name = "Jane Doe", Email = "jane@example.com" }
        };

        // Act
        var text = FeatureTextFormat.FormatCreated(feature);

        // Assert
        text
            .Should().Be("2026-10-20T15:30:45+00:00 by Jane Doe <jane@example.com>");
    }

    [Fact]
    public void FormatCreated_NullFeature_ThrowsArgumentNullException()
    {
        // Act
        var act = () => FeatureTextFormat.FormatCreated(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("feature");
    }
}
