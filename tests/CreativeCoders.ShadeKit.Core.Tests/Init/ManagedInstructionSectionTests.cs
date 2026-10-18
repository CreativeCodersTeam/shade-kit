using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Init;

namespace CreativeCoders.ShadeKit.Core.Tests.Init;

public class ManagedInstructionSectionTests
{
    [Fact]
    [Trait("Requirement", "FP02-AC5")]
    public void Text_Always_IsTheManagedSectionWithMarkers()
    {
        // Act
        var text = ManagedInstructionSection.Text;

        // Assert
        text
            .Should().Be(
                "<!-- shade-kit:begin - managed by shade-kit, content between the markers is replaced on " +
                "upgrade -->\n" +
                "## SHADE kit\n" +
                "\n" +
                "This repository uses SHADE kit. Its data lives in `.shadekit/` (configuration, templates, features) " +
                "and is committed with the code.\n" +
                "\n" +
                "- To start or continue work on a feature, use the `shade-kit-next-phase` skill.\n" +
                "- Call the engine as `shade-kit <command> --json` and act on its JSON result.\n" +
                "- Change feature state only through `shade-kit` commands, never by editing `.shadekit/features/` " +
                "by hand.\n" +
                "<!-- shade-kit:end -->");
    }

    [Fact]
    public void Text_Always_StartsWithBeginMarkerPrefixAndEndsWithEndMarker()
    {
        // Act
        var text = ManagedInstructionSection.Text;

        // Assert
        text
            .Should().StartWith(ManagedInstructionSection.BeginMarkerPrefix)
            .And.EndWith(ManagedInstructionSection.EndMarker);
    }

    [Fact]
    public void Markers_Always_AreTheShadeKitHtmlComments()
    {
        // Act
        var markers = (ManagedInstructionSection.BeginMarkerPrefix, ManagedInstructionSection.EndMarker);

        // Assert
        markers
            .Should().Be(("<!-- shade-kit:begin", "<!-- shade-kit:end -->"));
    }
}
