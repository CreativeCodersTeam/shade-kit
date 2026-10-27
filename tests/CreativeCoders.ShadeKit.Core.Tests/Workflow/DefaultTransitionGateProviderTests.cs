using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

public class DefaultTransitionGateProviderTests
{
    [Theory]
    [InlineData(WorkflowPhaseNames.Refine)]
    [InlineData(WorkflowPhaseNames.Plan)]
    [InlineData(WorkflowPhaseNames.Finish)]
    public async Task GetGateAsync_AnyPhase_ReturnsDefaultGateWithApprovalAndWithoutChecklist(string targetPhase)
    {
        // Arrange
        var provider = new DefaultTransitionGateProvider();

        // Act
        var gate = await provider.GetGateAsync(A.Fake<IKitStore>(), BuiltInWorkflowProfiles.Feature, targetPhase,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().Be(new TransitionGate(true, null));
    }

    [Fact]
    public async Task GetGateAsync_AnyPhase_ReadsNothingFromStore()
    {
        // Arrange
        var store = A.Fake<IKitStore>(options => options.Strict());

        // Act
        var gate = await new DefaultTransitionGateProvider().GetGateAsync(store, BuiltInWorkflowProfiles.Quick,
            WorkflowPhaseNames.Quick, TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Fact]
    public async Task GetGateAsync_StoreNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultTransitionGateProvider().GetGateAsync(null!, BuiltInWorkflowProfiles.Quick,
            WorkflowPhaseNames.Quick, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetGateAsync_ProfileNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultTransitionGateProvider().GetGateAsync(A.Fake<IKitStore>(), null!,
            WorkflowPhaseNames.Quick, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetGateAsync_TargetPhaseNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultTransitionGateProvider().GetGateAsync(A.Fake<IKitStore>(),
            BuiltInWorkflowProfiles.Quick, null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
