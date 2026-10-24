using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Output;
using Spectre.Console.Testing;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

public sealed class ConsoleConfirmationTests : IDisposable
{
    private readonly TestConsole _console = new TestConsole();

    [Fact]
    public void CanAsk_InteractiveConsoleWithoutRedirectedInput_ReturnsTrue()
    {
        // Arrange
        var sut = new ConsoleConfirmation(_console.Interactive(), () => false);

        // Act
        var canAsk = sut.CanAsk;

        // Assert
        canAsk
            .Should().BeTrue();
    }

    [Fact]
    public void CanAsk_RedirectedInput_ReturnsFalse()
    {
        // Arrange
        var sut = new ConsoleConfirmation(_console.Interactive(), () => true);

        // Act
        var canAsk = sut.CanAsk;

        // Assert
        canAsk
            .Should().BeFalse();
    }

    [Fact]
    public void CanAsk_NonInteractiveConsole_ReturnsFalse()
    {
        // Arrange
        var sut = new ConsoleConfirmation(_console, () => false);

        // Act
        var canAsk = sut.CanAsk;

        // Assert
        canAsk
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("n", false)]
    [InlineData("N", false)]
    [InlineData("", false)]
    public async Task ConfirmAsync_Answer_ReturnsWhetherUserAnsweredYes(string answer, bool expected)
    {
        // Arrange
        _console.Interactive().Input.PushTextWithEnter(answer);
        var sut = new ConsoleConfirmation(_console, () => false);

        // Act
        var confirmed = await sut.ConfirmAsync("Drop feature login?", TestContext.Current.CancellationToken);

        // Assert
        confirmed
            .Should().Be(expected);
    }

    [Fact]
    public async Task ConfirmAsync_Question_ShowsQuestionWithNoAsDefault()
    {
        // Arrange
        _console.Interactive().Input.PushTextWithEnter("n");
        var sut = new ConsoleConfirmation(_console, () => false);

        // Act
        await sut.ConfirmAsync("Drop feature login?", TestContext.Current.CancellationToken);

        // Assert
        _console.Output
            .Should().Contain("Drop feature login? [y/n] (n):");
    }

    [Fact]
    public async Task ConfirmAsync_QuestionWithMarkupCharacters_ShowsQuestionAsPlainText()
    {
        // Arrange
        _console.Interactive().Input.PushTextWithEnter("n");
        var sut = new ConsoleConfirmation(_console, () => false);

        // Act
        await sut.ConfirmAsync("Drop feature [red]x[/]?", TestContext.Current.CancellationToken);

        // Assert
        _console.Output
            .Should().Contain("Drop feature [red]x[/]?");
    }

    [Fact]
    public async Task ConfirmAsync_InvalidAnswerThenYes_AsksAgainAndReturnsTrue()
    {
        // Arrange
        _console.Interactive();
        _console.Input.PushTextWithEnter("maybe");
        _console.Input.PushTextWithEnter("y");
        var sut = new ConsoleConfirmation(_console, () => false);

        // Act
        var confirmed = await sut.ConfirmAsync("Drop feature login?", TestContext.Current.CancellationToken);

        // Assert
        confirmed
            .Should().BeTrue();

        _console.Output
            .Should().Contain("Please select one of the available options");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ConfirmAsync_CannotAsk_ThrowsInvalidOperationExceptionWithoutAsking(bool interactive,
        bool inputRedirected)
    {
        // Arrange
        if (interactive)
        {
            _console.Interactive();
        }

        var sut = new ConsoleConfirmation(_console, () => inputRedirected);

        // Act
        var act = () => sut.ConfirmAsync("Drop feature login?", TestContext.Current.CancellationToken);

        // Assert
        await act
            .Should().ThrowAsync<InvalidOperationException>();

        _console.Output
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        _console.Interactive();
        var sut = new ConsoleConfirmation(_console, () => false);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => sut.ConfirmAsync("Drop feature login?", cancellationTokenSource.Token);

        // Assert
        await act
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void CanAsk_InputRedirectionChanges_ReflectsCurrentState()
    {
        // Arrange
        var inputRedirected = false;
        var sut = new ConsoleConfirmation(_console.Interactive(), () => inputRedirected);
        var before = sut.CanAsk;

        // Act
        inputRedirected = true;

        // Assert
        before
            .Should().BeTrue();

        sut.CanAsk
            .Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmAsync_NullQuestion_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = new ConsoleConfirmation(_console.Interactive(), () => false);

        // Act
        var act = () => sut.ConfirmAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        await act
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("question");
    }

    [Fact]
    public void Constructor_NullConsole_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ConsoleConfirmation(null!);

        // Assert
        act
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("console");
    }

    [Fact]
    public void Constructor_NullIsInputRedirected_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ConsoleConfirmation(_console, null!);

        // Assert
        act
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("isInputRedirected");
    }

    public void Dispose()
    {
        _console.Dispose();
    }
}
