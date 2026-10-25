using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Trackers;
using CreativeCoders.ShadeKit.Core.Web;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

// The reading and the wrapping of the reader failures are covered through FeatureIdFactoryTests.
public class FeatureSourceReaderTests
{
    [Fact]
    public void Ctor_NullTrackerIssueReader_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureSourceReader(null!, A.Fake<IUrlContentReader>());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("trackerIssueReader");
    }

    [Fact]
    public void Ctor_NullUrlContentReader_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureSourceReader(A.Fake<ITrackerIssueReader>(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("urlContentReader");
    }
}
