using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Init;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class ShadeKitMarkupExtensionsTests
{
    [Fact]
    public void ToMutedMarkup_Markup_WrapsMarkupInGrey()
    {
        // Act
        var markup = "[bold]none[/]".ToMutedMarkup();

        // Assert
        markup
            .Should().Be("[grey][bold]none[/][/]");
    }

    [Fact]
    public void ToCommandMarkup_Markup_WrapsMarkupInCyan()
    {
        // Act
        var markup = "shade-kit phase next".ToCommandMarkup();

        // Assert
        markup
            .Should().Be("[cyan]shade-kit phase next[/]");
    }

    [Theory]
    [InlineData(PhaseState.Pending, "[grey]pending[/]")]
    [InlineData(PhaseState.InProgress, "[blue]in-progress[/]")]
    [InlineData(PhaseState.AwaitingApproval, "[yellow]awaiting-approval[/]")]
    [InlineData(PhaseState.Approved, "[green]approved[/]")]
    public void ToMarkup_PhaseState_ReturnsJsonNameColoredByMeaning(PhaseState state, string expected)
    {
        // Act
        var markup = state.ToMarkup();

        // Assert
        markup
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(FeatureStatus.Active, "[blue]active[/]")]
    [InlineData(FeatureStatus.Done, "[green]done[/]")]
    public void ToMarkup_FeatureStatus_ReturnsJsonNameColoredByMeaning(FeatureStatus status, string expected)
    {
        // Act
        var markup = status.ToMarkup();

        // Assert
        markup
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(InitializedFileAction.Created, "[green]created[/]")]
    [InlineData(InitializedFileAction.Appended, "[yellow]appended[/]")]
    [InlineData(InitializedFileAction.Updated, "[yellow]updated[/]")]
    public void ToMarkup_InitializedFileAction_ReturnsJsonNameColoredByMeaning(InitializedFileAction action,
        string expected)
    {
        // Act
        var markup = action.ToMarkup();

        // Assert
        markup
            .Should().Be(expected);
    }
}
