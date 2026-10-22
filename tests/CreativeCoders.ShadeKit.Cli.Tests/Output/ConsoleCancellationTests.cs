using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public class ConsoleCancellationTests
{
    [Fact]
    public void Token_NoCancelKeyPress_IsNotCanceled()
    {
        // Arrange
        using var cancellation = new ConsoleCancellation();

        // Act
        var token = cancellation.Token;

        // Assert
        token.IsCancellationRequested
            .Should().BeFalse();

        token.CanBeCanceled
            .Should().BeTrue();
    }

    [Fact]
    public void HandleCancelKeyPress_FirstCall_CancelsTokenAndKeepsProcessAlive()
    {
        // Arrange
        using var cancellation = new ConsoleCancellation();
        var token = cancellation.Token;

        // Act
        var keepAlive = cancellation.HandleCancelKeyPress();

        // Assert
        keepAlive
            .Should().BeTrue();

        token.IsCancellationRequested
            .Should().BeTrue();
    }

    [Fact]
    public void HandleCancelKeyPress_SecondCall_LetsDefaultHandlingEndProcess()
    {
        // Arrange
        using var cancellation = new ConsoleCancellation();
        cancellation.HandleCancelKeyPress();

        // Act
        var keepAlive = cancellation.HandleCancelKeyPress();

        // Assert
        keepAlive
            .Should().BeFalse();

        cancellation.Token.IsCancellationRequested
            .Should().BeTrue();
    }

    [Fact]
    public void HandleCancelKeyPress_FirstCall_RunsCallbacksOfToken()
    {
        // Arrange
        using var cancellation = new ConsoleCancellation();
        var callbackCalls = 0;
        using var registration = cancellation.Token.Register(() => callbackCalls++);

        // Act
        cancellation.HandleCancelKeyPress();
        cancellation.HandleCancelKeyPress();

        // Assert
        callbackCalls
            .Should().Be(1);
    }

    [Fact]
    public void HandleCancelKeyPress_AfterDispose_DoesNothingAndLetsDefaultHandlingEndProcess()
    {
        // Arrange
        var cancellation = new ConsoleCancellation();
        cancellation.Dispose();

        // Act
        var keepAlive = cancellation.HandleCancelKeyPress();

        // Assert
        keepAlive
            .Should().BeFalse();
    }

    [Fact]
    public void ListenToCancelKeyPress_CalledTwiceThenDisposed_DoesNotThrow()
    {
        // Arrange
        var cancellation = new ConsoleCancellation();

        // Act
        var act = () =>
        {
            cancellation.ListenToCancelKeyPress();
            cancellation.ListenToCancelKeyPress();
            cancellation.Dispose();
            cancellation.Dispose();
        };

        // Assert
        act
            .Should().NotThrow();
    }

    [Fact]
    public void ListenToCancelKeyPress_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cancellation = new ConsoleCancellation();
        cancellation.Dispose();

        // Act
        var act = cancellation.ListenToCancelKeyPress;

        // Assert
        act
            .Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task HandleCancelKeyPress_ConcurrentCalls_OnlyOneCancelsTokenAndCallbackRunsOnce()
    {
        // Arrange
        using var cancellation = new ConsoleCancellation();
        var callbackCalls = 0;
        using var registration = cancellation.Token.Register(() => Interlocked.Increment(ref callbackCalls));
        using var start = new ManualResetEventSlim();

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                start.Wait(TestContext.Current.CancellationToken);

                return cancellation.HandleCancelKeyPress();
            }, TestContext.Current.CancellationToken))
            .ToArray();

        // Act
        start.Set();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Count(x => x)
            .Should().Be(1);

        callbackCalls
            .Should().Be(1);
    }

    [Fact]
    public void ListenToCancelKeyPress_WithoutKeyPress_DoesNotCancelToken()
    {
        // Arrange
        using var cancellation = new ConsoleCancellation();

        // Act
        cancellation.ListenToCancelKeyPress();

        // Assert
        cancellation.Token.IsCancellationRequested
            .Should().BeFalse();

        cancellation.HandleCancelKeyPress()
            .Should().BeTrue();
    }

    [Fact]
    public void Token_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var cancellation = new ConsoleCancellation();
        cancellation.Dispose();

        // Act
        var act = () => cancellation.Token;

        // Assert
        act
            .Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_TokenTakenBefore_KeepsItsState(bool canceled)
    {
        // Arrange
        var cancellation = new ConsoleCancellation();
        var token = cancellation.Token;

        if (canceled)
        {
            cancellation.HandleCancelKeyPress();
        }

        // Act
        cancellation.Dispose();

        // Assert
        token.IsCancellationRequested
            .Should().Be(canceled);
    }
}
