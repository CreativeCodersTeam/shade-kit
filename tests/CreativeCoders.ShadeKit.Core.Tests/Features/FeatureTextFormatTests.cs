using System.Globalization;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

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

    [Theory]
    [InlineData(PhaseHistoryAction.Start, "new", "refine", null,
        "2026-10-21T09:00:00+02:00 start new -> refine by Jane Doe <jane@example.com>")]
    [InlineData(PhaseHistoryAction.Reject, "refine", "map", "DOR-04 failed",
        "2026-10-21T09:00:00+02:00 reject refine -> map by Jane Doe <jane@example.com>, note: DOR-04 failed")]
    [InlineData(PhaseHistoryAction.Approve, "refine", "map", "approval off",
        "2026-10-21T09:00:00+02:00 approve refine -> map by Jane Doe <jane@example.com>, note: approval off")]
    [InlineData(PhaseHistoryAction.Reopen, "plan", "refine", null,
        "2026-10-21T09:00:00+02:00 reopen plan -> refine by Jane Doe <jane@example.com>")]
    [InlineData(PhaseHistoryAction.Complete, "finish", null, null,
        "2026-10-21T09:00:00+02:00 complete finish by Jane Doe <jane@example.com>")]
    [InlineData(PhaseHistoryAction.Start, null, "refine", null,
        "2026-10-21T09:00:00+02:00 start -> refine by Jane Doe <jane@example.com>")]
    [InlineData(PhaseHistoryAction.Start, null, null, null,
        "2026-10-21T09:00:00+02:00 start by Jane Doe <jane@example.com>")]
    [InlineData(PhaseHistoryAction.Reject, "refine", "map", "",
        "2026-10-21T09:00:00+02:00 reject refine -> map by Jane Doe <jane@example.com>, note: ")]
    [InlineData(PhaseHistoryAction.Reject, "refine", "map", "DOR-04 failed\nsee plan.md",
        "2026-10-21T09:00:00+02:00 reject refine -> map by Jane Doe <jane@example.com>, note: DOR-04 failed\nsee plan.md")]
    public void FormatHistoryEntry_Entry_ReturnsOneLineWithTransitionAndNote(PhaseHistoryAction action, string? from,
        string? to, string? note, string expected)
    {
        // Arrange
        var entry = new PhaseHistoryEntry
        {
            At = new DateTimeOffset(2026, 10, 21, 9, 0, 0, 500, TimeSpan.FromHours(2)),
            By = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" },
            Action = action,
            From = from,
            To = to,
            Note = note
        };

        // Act
        var text = FeatureTextFormat.FormatHistoryEntry(entry);

        // Assert
        text
            .Should().Be(expected);
    }

    [Fact]
    public void FormatHistoryEntry_Null_ThrowsArgumentNullException()
    {
        // Act
        var act = () => FeatureTextFormat.FormatHistoryEntry(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("entry");
    }
}
