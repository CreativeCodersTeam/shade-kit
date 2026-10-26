using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Warnings;
using CreativeCoders.ShadeKit.Core.Processes;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

public class GitLineEndingResolverTests
{
    private const string RepositoryRootPath = "/repo";

    private const string RelativePath = "docs/readme.md";

    private const string Unspecified = "unspecified";

    private static readonly string FullRepositoryRootPath = Path.GetFullPath(RepositoryRootPath);

    private static readonly string[] CheckAttrArguments =
        ["check-attr", "eol", "text", "crlf", "--", RelativePath];

    private static readonly string[] AutoCrLfArguments = ["config", "core.autocrlf"];

    private static readonly string[] EolArguments = ["config", "core.eol"];

    private readonly IGitCommandRunner _gitCommandRunner = A.Fake<IGitCommandRunner>();

    private readonly GitLineEndingResolver _sut;

    public GitLineEndingResolverTests()
    {
        // Every git call that a test does not set up fails, so a forgotten setup cannot pass by accident; both
        // configuration keys are unset unless a test sets them.
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(128, "", "fatal: unexpected call"));
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(1, "", ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(1, "", ""));
        _sut = new GitLineEndingResolver(_gitCommandRunner);
    }

    [Fact]
    public void Constructor_NullGitCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitLineEndingResolver(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitCommandRunner");
    }

    [Fact]
    public void InternalConstructor_NullGitCommandRunner_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitLineEndingResolver(null!, LineEnding.Lf);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gitCommandRunner");
    }

    [Theory]
    [InlineData("lf", LineEnding.Lf)]
    [InlineData("crlf", LineEnding.CrLf)]
    public async Task GetLineEndingAsync_EolAttribute_ReturnsAttributeLineEnding(string eol, LineEnding expected)
    {
        // Arrange
        SetUpAttributes(eol, "set", Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Fact]
    public async Task GetLineEndingAsync_EolAttribute_DoesNotReadConfiguration()
    {
        // Arrange
        SetUpAttributes("lf", "set", Unspecified);

        // Act
        await GetLineEndingAsync(_sut);

        // Assert
        AssertConfigurationNotRead();
    }

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    public async Task GetLineEndingAsync_EolAttributeAndTextUnset_ReturnsNullWithoutReadingConfiguration(string eol)
    {
        // Arrange
        SetUpAttributes(eol, "unset", Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();

        AssertConfigurationNotRead();
    }

    [Theory]
    [InlineData("auto", "lf", LineEnding.Lf)]
    [InlineData("auto", "crlf", LineEnding.CrLf)]
    [InlineData(Unspecified, "lf", LineEnding.Lf)]
    [InlineData(Unspecified, "crlf", LineEnding.CrLf)]
    public async Task GetLineEndingAsync_EolAttributeAndTextAutoOrUnspecified_ReturnsAttributeLineEnding(
        string text, string eol, LineEnding expected)
    {
        // Arrange
        SetUpAttributes(eol, text, Unspecified);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("LF")]
    [InlineData("cr")]
    [InlineData("set")]
    [InlineData("unset")]
    public async Task GetLineEndingAsync_EolAttributeOtherValue_IgnoresAttribute(string eol)
    {
        // Arrange
        SetUpAttributes(eol, Unspecified, Unspecified);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public async Task GetLineEndingAsync_TextUnset_ReturnsNullWithoutReadingConfiguration()
    {
        // Arrange
        SetUpAttributes(Unspecified, "unset", Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();

        AssertConfigurationNotRead();
    }

    [Fact]
    public async Task GetLineEndingAsync_BinaryMacroOverriddenByText_TreatsFileAsText()
    {
        // Arrange
        // "*.md binary" followed by "*.md text": the macro sets -text, the later text attribute sets it again, so
        // check-attr reports text as set; the binary attribute itself is not requested.
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Contains("binary"), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(Unspecified)]
    [InlineData("custom")]
    public async Task GetLineEndingAsync_CrLfUnsetAndTextUnspecifiedOrUnknown_ReturnsNullWithoutReadingConfiguration(
        string text)
    {
        // Arrange
        SetUpAttributes("crlf", text, "unset");
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();

        AssertConfigurationNotRead();
    }

    [Theory]
    [InlineData("set", "lf\n", LineEnding.Lf)]
    [InlineData("set", "crlf\n", LineEnding.CrLf)]
    [InlineData("auto", "crlf\n", LineEnding.CrLf)]
    public async Task GetLineEndingAsync_CrLfSetOrAutoAndTextUnspecified_AppliesCoreEolAsForText(string crlf,
        string eol, LineEnding expected)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, crlf);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("auto")]
    public async Task GetLineEndingAsync_CrLfSetOrAutoAndTextUnknown_AppliesCoreEolAsForText(string crlf)
    {
        // Arrange
        SetUpAttributes(Unspecified, "custom", crlf);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Theory]
    [InlineData("INPUT")]
    [InlineData("Auto")]
    [InlineData("Set")]
    public async Task GetLineEndingAsync_CrLfValueInOtherCase_TreatsCrLfAsUnknown(string crlf)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, crlf);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public async Task GetLineEndingAsync_CrLfInputAndTextUnspecified_ReturnsLfWithoutReadingConfiguration()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, "input");
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);

        AssertConfigurationNotRead();
    }

    [Theory]
    [InlineData("set", "unset")]
    [InlineData("auto", "unset")]
    [InlineData("auto", "input")]
    public async Task GetLineEndingAsync_TextSetOrAutoAndCrLfUnsetOrInput_TextWins(string text, string crlf)
    {
        // Arrange
        SetUpAttributes(Unspecified, text, crlf);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("input")]
    public async Task GetLineEndingAsync_TextUnsetAndCrLfSetOrInput_TextWins(string crlf)
    {
        // Arrange
        SetUpAttributes("lf", "unset", crlf);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("input", Unspecified)]
    [InlineData(Unspecified, "input")]
    [InlineData("custom", "input")]
    public async Task GetLineEndingAsync_TextOrCrLfInput_ReturnsLfWithoutReadingConfiguration(string text, string crlf)
    {
        // Arrange
        SetUpAttributes(Unspecified, text, crlf);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);

        AssertConfigurationNotRead();
    }

    [Theory]
    [InlineData("input", Unspecified)]
    [InlineData(Unspecified, "input")]
    public async Task GetLineEndingAsync_TextOrCrLfInputAndEolCrLf_ReturnsCrLf(string text, string crlf)
    {
        // Arrange
        // git lets the eol attribute override the LF of text=input / crlf=input.
        SetUpAttributes("crlf", text, crlf);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Theory]
    [InlineData("INPUT")]
    [InlineData("Auto")]
    public async Task GetLineEndingAsync_TextValueInOtherCase_TreatsTextAsUnknown(string text)
    {
        // Arrange
        // Attribute values are case-sensitive in git, so the value counts as unknown and core.eol does not apply.
        SetUpAttributes(Unspecified, text, Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("On")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("10")]
    [InlineData("0x1")]
    [InlineData("0X10")]
    [InlineData("-0x1")]
    [InlineData("010")]
    [InlineData("1k")]
    [InlineData("1M")]
    [InlineData("1g")]
    [InlineData("2097151k")]
    [InlineData("2147483647")]
    [InlineData("-2147483647")]
    public async Task GetLineEndingAsync_AutoCrLfTrue_ReturnsCrLf(string autoCrLf)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf + "\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Theory]
    [InlineData("input")]
    [InlineData("Input")]
    public async Task GetLineEndingAsync_AutoCrLfInput_ReturnsLf(string autoCrLf)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf + "\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Theory]
    [InlineData("set", "true\n", "lf\n", LineEnding.CrLf)]
    [InlineData("auto", "true\n", "lf\n", LineEnding.CrLf)]
    [InlineData("set", "input\n", "crlf\n", LineEnding.Lf)]
    [InlineData("auto", "input\n", "crlf\n", LineEnding.Lf)]
    public async Task GetLineEndingAsync_AutoCrLfTrueOrInputAndCoreEol_IgnoresCoreEol(string text, string autoCrLf,
        string eol, LineEnding expected)
    {
        // Arrange
        SetUpAttributes(Unspecified, text, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf, ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(EolArguments), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("false\n")]
    [InlineData("no\n")]
    [InlineData("off\n")]
    [InlineData("OFF\n")]
    [InlineData("0\n")]
    [InlineData("00\n")]
    [InlineData("-0\n")]
    [InlineData("")]
    public async Task GetLineEndingAsync_AutoCrLfFalseAndTextUnspecified_ReturnsNull(string autoCrLf)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("set", "\n", "lf\n", LineEnding.Lf)]
    [InlineData("set", "\n", "crlf\n", LineEnding.CrLf)]
    [InlineData("auto", "\n", "crlf\n", LineEnding.CrLf)]
    [InlineData("set", "\r\n", "crlf\r\n", LineEnding.CrLf)]
    public async Task GetLineEndingAsync_AutoCrLfWithoutValue_TreatsAutoCrLfAsNotSet(string text, string autoCrLf,
        string eol, LineEnding expected)
    {
        // Arrange
        // git config prints an empty line for a key without a value, which git itself treats as true.
        SetUpAttributes(Unspecified, text, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf, ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("0x0")]
    [InlineData("0k")]
    [InlineData("00")]
    [InlineData("08")]
    [InlineData("0x")]
    [InlineData("1x")]
    [InlineData("k")]
    [InlineData("+-1")]
    [InlineData("1.0")]
    [InlineData("1kk")]
    [InlineData("2147483648")]
    [InlineData("-2147483648")]
    [InlineData("2097152k")]
    [InlineData("3g")]
    [InlineData("99999999999999999999")]
    public async Task GetLineEndingAsync_AutoCrLfZeroOrInvalidNumber_TreatsAutoCrLfAsNotTrue(string autoCrLf)
    {
        // Arrange
        // git_parse_int rejects these values (out of the int range, an invalid digit or unit) or reads them as 0.
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf + "\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public async Task GetLineEndingAsync_AutoCrLfUnsetAndTextUnspecified_ReturnsNull()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("lf\n")]
    [InlineData("crlf\n")]
    [InlineData("native\n")]
    public async Task GetLineEndingAsync_CoreEolAndTextUnspecified_IgnoresCoreEolWithoutReadingIt(string eol)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));
        var sut = new GitLineEndingResolver(_gitCommandRunner, LineEnding.CrLf);

        // Act
        var lineEnding = await GetLineEndingAsync(sut);

        // Assert
        lineEnding
            .Should().BeNull();

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(EolArguments), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData("set", "lf\n", LineEnding.Lf)]
    [InlineData("auto", "lf\n", LineEnding.Lf)]
    [InlineData("set", "crlf\n", LineEnding.CrLf)]
    [InlineData("auto", "crlf\n", LineEnding.CrLf)]
    [InlineData("set", "CRLF\n", LineEnding.CrLf)]
    [InlineData("auto", "Lf\n", LineEnding.Lf)]
    public async Task GetLineEndingAsync_CoreEolAndTextSetOrAuto_ReturnsCoreEolLineEnding(string text, string eol,
        LineEnding expected)
    {
        // Arrange
        SetUpAttributes(Unspecified, text, Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("false\n")]
    [InlineData(null)]
    public async Task GetLineEndingAsync_CoreEolWithAutoCrLfFalseOrUnset_ReturnsCoreEolLineEnding(string? autoCrLf)
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments,
            autoCrLf is null ? new ExternalProcessResult(1, "", "") : new ExternalProcessResult(0, autoCrLf, ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public async Task GetLineEndingAsync_CoreEolNative_ReturnsNativeLineEnding(LineEnding nativeLineEnding)
    {
        // Arrange
        SetUpAttributes(Unspecified, "auto", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "native\n", ""));
        var sut = new GitLineEndingResolver(_gitCommandRunner, nativeLineEnding);

        // Act
        var lineEnding = await GetLineEndingAsync(sut);

        // Assert
        lineEnding
            .Should().Be(nativeLineEnding);
    }

    [Fact]
    public async Task GetLineEndingAsync_CoreEolNativeWithPublicConstructor_ReturnsOperatingSystemLineEnding()
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "native\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(OperatingSystem.IsWindows() ? LineEnding.CrLf : LineEnding.Lf);
    }

    [Theory]
    [InlineData("cr\n")]
    [InlineData("\n")]
    public async Task GetLineEndingAsync_CoreEolOtherValue_ReturnsNull(string eol)
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Theory]
    [InlineData("true\r\n", "lf\r\n", LineEnding.CrLf)]
    [InlineData("input\r\n", "crlf\r\n", LineEnding.Lf)]
    [InlineData("false\r\n", "crlf\r\n", LineEnding.CrLf)]
    [InlineData("false\r\n", "lf\r\n", LineEnding.Lf)]
    public async Task GetLineEndingAsync_ConfigOutputWithCrLf_ParsesValues(string autoCrLf, string eol,
        LineEnding expected)
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, autoCrLf, ""));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, eol, ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(expected);
    }

    [Fact]
    public async Task GetLineEndingAsync_CoreEolUnsetAndTextSet_ReturnsNull()
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public async Task GetLineEndingAsync_TextAttributeOtherValue_IgnoresCoreEol()
    {
        // Arrange
        SetUpAttributes(Unspecified, "custom", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();
    }

    [Fact]
    public async Task GetLineEndingAsync_AnyPath_RunsCheckAttrInRepositoryRoot()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);

        // Act
        await GetLineEndingAsync(_sut);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(FullRepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("./docs/readme.md")]
    [InlineData("docs/../docs/readme.md")]
    [InlineData("docs//readme.md")]
    public async Task GetLineEndingAsync_NonNormalizedRelativePath_PassesNormalizedPathToGit(string relativePath)
    {
        // Arrange
        SetUpAttributes("lf", Unspecified, Unspecified);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut, RepositoryRootPath, relativePath,
            TestContext.Current.CancellationToken);

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public async Task GetLineEndingAsync_FileNameStartsWithTwoDots_PassesPathToGit()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(["check-attr", "eol", "text", "crlf", "--", "..hidden.md"],
            new ExternalProcessResult(0, "..hidden.md: eol: crlf\n..hidden.md: text: set\n..hidden.md: crlf: unspecified\n",
                ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut, RepositoryRootPath, "..hidden.md",
            TestContext.Current.CancellationToken);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public async Task GetLineEndingAsync_RelativePathWithOperatingSystemSeparator_PassesForwardSlashPathToGit()
    {
        // Arrange
        SetUpAttributes("crlf", Unspecified, Unspecified);

        // Act
        var lineEnding = await GetLineEndingAsync(_sut, RepositoryRootPath,
            Path.Combine("docs", "readme.md"), TestContext.Current.CancellationToken);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public async Task GetLineEndingAsync_PathContainsAttributeSeparator_ParsesAttributesFromLineEnd()
    {
        // Arrange
        const string relativePath = "notes: eol: crlf.md";
        _gitCommandRunner.SetUpResult(["check-attr", "eol", "text", "crlf", "--", relativePath],
            new ExternalProcessResult(0,
                $"{relativePath}: eol: lf\n{relativePath}: text: set\n{relativePath}: crlf: unspecified\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut, RepositoryRootPath, relativePath,
            TestContext.Current.CancellationToken);

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public async Task GetLineEndingAsync_CheckAttrOutputWithCrLf_ParsesAttributes()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CheckAttrArguments,
            new ExternalProcessResult(0,
                $"{RelativePath}: eol: crlf\r\n{RelativePath}: text: set\r\n{RelativePath}: crlf: unspecified\r\n",
                ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage\n")]
    [InlineData("docs/readme.md: eol\n")]
    [InlineData("docs/readme.md: other: crlf\n")]
    public async Task GetLineEndingAsync_CheckAttrOutputWithoutKnownAttributes_TreatsAttributesAsUnspecified(
        string standardOutput)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CheckAttrArguments, new ExternalProcessResult(0, standardOutput, ""));
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "input\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public async Task GetLineEndingAsync_GitNotFoundOnCheckAttr_ReturnsNullWithoutWarning()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .Throws(new GitNotFoundException(CheckAttrArguments, "git was not found."));

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(null, null));
    }

    [Fact]
    public async Task GetLineEndingAsync_GitCannotBeStartedOnCheckAttr_ReturnsNullAndWarning()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .Throws(new GitUnavailableException(CheckAttrArguments, "git could not be started."));

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(null,
                new GitAttributesUnavailableWarning(FullRepositoryRootPath,
                    "git check-attr", "git could not be started.")));
    }

    [Fact]
    public async Task GetLineEndingAsync_GitUnavailableOnConfig_ReturnsNullAndWarning()
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .Throws(new GitUnavailableException(AutoCrLfArguments, "git could not be started."));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(null,
                new GitAttributesUnavailableWarning(FullRepositoryRootPath, "git config",
                    "git could not be started.")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(-1)]
    public async Task GetLineEndingAsync_CheckAttrFails_ReturnsNullAndWarningWithExitCodeAndStandardError(
        int exitCode)
    {
        // Arrange
        _gitCommandRunner.SetUpResult(CheckAttrArguments,
            new ExternalProcessResult(exitCode, $"{RelativePath}: eol: lf\n", "fatal: not a git repository"));

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(null,
                new GitAttributesUnavailableWarning(FullRepositoryRootPath,
                    "git check-attr",
                    $"git exited with the unexpected exit code {exitCode}: fatal: not a git repository.")));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(128)]
    public async Task GetLineEndingAsync_AutoCrLfReadFails_ReturnsNullAndWarning(int exitCode)
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(exitCode, "", "error: invalid config"));
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<GitAttributesUnavailableWarning>()
            .Which.Command.Should().Be("git config");
    }

    [Fact]
    public async Task GetLineEndingAsync_CoreEolReadFails_ReturnsNullAndWarning()
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(128, "", "fatal: bad config line"));

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<GitAttributesUnavailableWarning>()
            .Which.Command.Should().Be("git config");
    }

    [Fact]
    public async Task GetLineEndingAsync_GitFailedForRepository_ReturnsSameWarningForLaterFileWithoutRunningGit()
    {
        // Arrange
        SetUpAttributes("other.md", "lf", Unspecified, Unspecified);
        var firstResult = await ResolveAsync(_sut);
        Fake.ClearRecordedCalls(_gitCommandRunner);

        // Act
        var secondResult = await ResolveAsync(_sut, RepositoryRootPath, "other.md",
            TestContext.Current.CancellationToken);

        // Assert
        firstResult.Warning
            .Should().BeOfType<GitAttributesUnavailableWarning>();

        secondResult
            .Should().Be(new GitLineEndingResult(null, firstResult.Warning));

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetLineEndingAsync_GitNotFoundForRepository_RunsNoFurtherGitAndReturnsNoWarning()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Throws(new GitNotFoundException(CheckAttrArguments, "git was not found."));
        await ResolveAsync(_sut);

        // Act
        var result = await ResolveAsync(_sut, RepositoryRootPath, "other.md", TestContext.Current.CancellationToken);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(null, null));

        A.CallTo(_gitCommandRunner)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_GitFailedForOtherRepository_StillRunsGitForThisRepository()
    {
        // Arrange
        const string otherRepositoryRootPath = "/other";
        SetUpAttributes("lf", Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(Path.GetFullPath(otherRepositoryRootPath),
                A<IReadOnlyList<string>>._, A<CancellationToken>._))
            .Returns(new ExternalProcessResult(128, "", "fatal: detected dubious ownership"));
        await ResolveAsync(_sut, otherRepositoryRootPath, RelativePath, TestContext.Current.CancellationToken);

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(LineEnding.Lf, null));
    }

    [Fact]
    public async Task GetLineEndingAsync_CheckAttrCanceled_KeepsNoFailureAndRunsGitAgain()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .Throws(new OperationCanceledException()).Once()
            .Then.Returns(new ExternalProcessResult(0,
                $"{RelativePath}: eol: crlf\n{RelativePath}: text: unspecified\n{RelativePath}: crlf: unspecified\n",
                ""));
        var firstAct = () => ResolveAsync(_sut);
        await firstAct.Should().ThrowAsync<OperationCanceledException>();

        // Act
        var result = await ResolveAsync(_sut);

        // Assert
        result
            .Should().Be(new GitLineEndingResult(LineEnding.CrLf, null));
    }

    [Fact]
    public async Task GetLineEndingAsync_CancellationToken_PassesTokenToEveryGitCall()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpAttributes(Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "lf\n", ""));

        // Act
        await GetLineEndingAsync(_sut, RepositoryRootPath, RelativePath, cancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._, A<IReadOnlyList<string>>._, cancellationToken))
            .MustHaveHappened(3, Times.Exactly);
    }

    [Fact]
    public async Task GetLineEndingAsync_GitCallCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => GetLineEndingAsync(_sut);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("core.autocrlf")]
    [InlineData("core.eol")]
    public async Task GetLineEndingAsync_ConfigCallCanceled_ThrowsOperationCanceledException(string key)
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);
        string[] configArguments = ["config", key];
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(configArguments), A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        // Act
        var act = () => GetLineEndingAsync(_sut);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetLineEndingAsync_RunnerThrowsUnexpectedException_PropagatesException()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .Throws(new InvalidOperationException("unexpected"));

        // Act
        var act = () => GetLineEndingAsync(_sut);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetLineEndingAsync_SeveralFilesOfOneRepository_ReadsEachConfigurationValueOnce()
    {
        // Arrange
        string[] relativePaths = ["docs/readme.md", "docs/setup.md", ".shadekit/config.json"];

        foreach (var relativePath in relativePaths)
        {
            SetUpAttributes(relativePath, Unspecified, "set", Unspecified);
        }

        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "crlf\n", ""));

        // Act
        var lineEndings = new List<LineEnding?>();

        foreach (var relativePath in relativePaths)
        {
            lineEndings.Add(await GetLineEndingAsync(_sut, RepositoryRootPath, relativePath,
                TestContext.Current.CancellationToken));
        }

        // Assert
        lineEndings
            .Should().AllBeEquivalentTo(LineEnding.CrLf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[0] == "check-attr"), A<CancellationToken>._))
            .MustHaveHappened(3, Times.Exactly);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(EolArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_ConfigurationValueChangesAfterFirstRead_KeepsFirstValueForInstance()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .ReturnsNextFromSequence(new ExternalProcessResult(0, "true\n", ""), new ExternalProcessResult(0, "input\n", ""));

        // Act
        var firstLineEnding = await GetLineEndingAsync(_sut);
        var secondLineEnding = await GetLineEndingAsync(_sut);

        // Assert
        firstLineEnding
            .Should().Be(LineEnding.CrLf);

        secondLineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public async Task GetLineEndingAsync_ConfigurationValueNotSet_KeepsNotSetAndReadsItOnce()
    {
        // Arrange
        SetUpAttributes(Unspecified, "set", Unspecified);

        // Act
        await GetLineEndingAsync(_sut);
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().BeNull();

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[0] == "config"), A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task GetLineEndingAsync_TwoRepositoryRoots_ReadsConfigurationForEachRoot()
    {
        // Arrange
        const string otherRepositoryRootPath = "/other";
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(FullRepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .Returns(new ExternalProcessResult(0, "true\n", ""));
        A.CallTo(() => _gitCommandRunner.RunAsync(Path.GetFullPath(otherRepositoryRootPath),
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .Returns(new ExternalProcessResult(0, "input\n", ""));

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);
        var otherLineEnding = await GetLineEndingAsync(_sut, otherRepositoryRootPath, RelativePath,
            TestContext.Current.CancellationToken);
        var lineEndingAgain = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);

        otherLineEnding
            .Should().Be(LineEnding.Lf);

        lineEndingAgain
            .Should().Be(LineEnding.CrLf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }

    [Theory]
    [InlineData("/repo/")]
    [InlineData("/repo/../repo")]
    [InlineData("/repo/.")]
    public async Task GetLineEndingAsync_OtherSpellingOfRepositoryRoot_SharesConfigurationValues(
        string otherSpelling)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        _gitCommandRunner.SetUpResult(AutoCrLfArguments, new ExternalProcessResult(0, "true\n", ""));

        // Act
        await GetLineEndingAsync(_sut);
        var lineEnding = await GetLineEndingAsync(_sut, otherSpelling, RelativePath,
            TestContext.Current.CancellationToken);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_SeparateInstances_ReadConfigurationAgain()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .ReturnsNextFromSequence(new ExternalProcessResult(0, "true\n", ""), new ExternalProcessResult(0, "input\n", ""));
        var firstSut = new GitLineEndingResolver(_gitCommandRunner);
        var secondSut = new GitLineEndingResolver(_gitCommandRunner);

        // Act
        var firstLineEnding = await GetLineEndingAsync(firstSut);
        var secondLineEnding = await GetLineEndingAsync(secondSut);

        // Assert
        firstLineEnding
            .Should().Be(LineEnding.CrLf);

        secondLineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Fact]
    public async Task GetLineEndingAsync_SeparateServiceContainers_ReadConfigurationAgain()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .ReturnsNextFromSequence(new ExternalProcessResult(0, "true\n", ""), new ExternalProcessResult(0, "input\n", ""));

        // Act
        var firstLineEnding = await GetLineEndingFromNewContainerAsync();
        var secondLineEnding = await GetLineEndingFromNewContainerAsync();

        // Assert
        firstLineEnding
            .Should().Be(LineEnding.CrLf);

        secondLineEnding
            .Should().Be(LineEnding.Lf);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(128)]
    public async Task GetLineEndingAsync_ConfigurationReadFailed_KeepsFailureAndDoesNotReadAgain(int exitCode)
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .ReturnsNextFromSequence(new ExternalProcessResult(exitCode, "", "error"), new ExternalProcessResult(0, "true\n", ""));

        // Act
        var firstResult = await ResolveAsync(_sut);
        var secondResult = await ResolveAsync(_sut);

        // Assert
        firstResult.LineEnding
            .Should().BeNull();

        secondResult
            .Should().Be(firstResult);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_CoreEolNeededOnlyForLaterFile_ReadsItOnceAndAutoCrLfNotAgain()
    {
        // Arrange
        const string textFilePath = "docs/setup.md";
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        SetUpAttributes(textFilePath, Unspecified, "set", Unspecified);
        _gitCommandRunner.SetUpResult(EolArguments, new ExternalProcessResult(0, "lf\n", ""));

        // Act
        var firstLineEnding = await GetLineEndingAsync(_sut);
        var secondLineEnding = await GetLineEndingAsync(_sut, RepositoryRootPath, textFilePath,
            TestContext.Current.CancellationToken);

        // Assert
        firstLineEnding
            .Should().BeNull();

        secondLineEnding
            .Should().Be(LineEnding.Lf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(EolArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetLineEndingAsync_ConfigurationReadCanceled_ReadsAgainOnNextCall()
    {
        // Arrange
        SetUpAttributes(Unspecified, Unspecified, Unspecified);
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(AutoCrLfArguments), A<CancellationToken>._))
            .Throws(new OperationCanceledException()).Once()
            .Then.Returns(new ExternalProcessResult(0, "true\n", ""));
        var firstAct = () => GetLineEndingAsync(_sut);
        await firstAct.Should().ThrowAsync<OperationCanceledException>();

        // Act
        var lineEnding = await GetLineEndingAsync(_sut);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);
    }

    [Fact]
    public async Task GetLineEndingAsync_NullFile_ThrowsArgumentNullExceptionWithoutRunningGit()
    {
        // Act
        var act = () => _sut.GetLineEndingAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("file");

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PreloadAttributesAsync_SeveralPaths_RunsOneCheckAttrForAllFiles()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(["check-attr", "eol", "text", "crlf", "--", "a.md", "b.md"],
            new ExternalProcessResult(0,
                "a.md: eol: lf\na.md: text: unspecified\na.md: crlf: unspecified\n" +
                "b.md: eol: crlf\nb.md: text: unspecified\nb.md: crlf: unspecified\n", ""));

        // Act
        await _sut.PreloadAttributesAsync(Files(["a.md", "b.md"]), TestContext.Current.CancellationToken);
        var first = await GetLineEndingAsync(_sut, RepositoryRootPath, "a.md", TestContext.Current.CancellationToken);
        var second = await GetLineEndingAsync(_sut, RepositoryRootPath, "b.md", TestContext.Current.CancellationToken);

        // Assert
        first
            .Should().Be(LineEnding.Lf);

        second
            .Should().Be(LineEnding.CrLf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[0] == "check-attr"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreloadAttributesAsync_FileMissingFromOutput_ReadsItsAttributesOnItsOwn()
    {
        // Arrange
        _gitCommandRunner.SetUpResult(["check-attr", "eol", "text", "crlf", "--", "a.md", "b.md"],
            new ExternalProcessResult(0, "a.md: eol: lf\na.md: text: unspecified\na.md: crlf: unspecified\n", ""));
        SetUpAttributes("b.md", "crlf", Unspecified, Unspecified);
        string[] singleFileArguments = ["check-attr", "eol", "text", "crlf", "--", "b.md"];

        // Act
        await _sut.PreloadAttributesAsync(Files(["a.md", "b.md"]), TestContext.Current.CancellationToken);
        var lineEnding = await GetLineEndingAsync(_sut, RepositoryRootPath, "b.md",
            TestContext.Current.CancellationToken);

        // Assert
        lineEnding
            .Should().Be(LineEnding.CrLf);

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(singleFileArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreloadAttributesAsync_CheckAttrFails_KeepsFailureForRepositoryWithoutThrowing()
    {
        // Arrange
        SetUpAttributes("lf", Unspecified, Unspecified);

        // Act
        await _sut.PreloadAttributesAsync(Files([RelativePath, "other.md"]), TestContext.Current.CancellationToken);
        var result = await ResolveAsync(_sut);

        // Assert
        result.LineEnding
            .Should().BeNull();

        result.Warning
            .Should().BeOfType<GitAttributesUnavailableWarning>()
            .Which.Command.Should().Be("git check-attr");

        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PreloadAttributesAsync_NoPaths_RunsNoGit()
    {
        // Act
        await _sut.PreloadAttributesAsync(Files([]), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PreloadAttributesAsync_SameFileInSeveralSpellings_PassesItOnceNormalized()
    {
        // Act
        await _sut.PreloadAttributesAsync(Files([RelativePath, "./docs/readme.md", Path.Combine("docs", "readme.md")]), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(FullRepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(CheckAttrArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreloadAttributesAsync_MoreThanOneHundredFiles_RunsCheckAttrForEachHundred()
    {
        // Arrange
        string[] paths = [.. Enumerable.Range(0, 150).Select(x => $"file{x}.md")];
        string[] firstArguments = ["check-attr", "eol", "text", "crlf", "--", .. paths[..100]];
        string[] secondArguments = ["check-attr", "eol", "text", "crlf", "--", .. paths[100..]];
        _gitCommandRunner.SetUpResult(firstArguments, new ExternalProcessResult(0, "", ""));
        _gitCommandRunner.SetUpResult(secondArguments, new ExternalProcessResult(0, "", ""));

        // Act
        await _sut.PreloadAttributesAsync(Files(paths), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(firstArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                    A<IReadOnlyList<string>>.That.IsSameSequenceAs(secondArguments), A<CancellationToken>._))
                .MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task PreloadAttributesAsync_CheckAttrCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[0] == "check-attr"), A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = () => _sut.PreloadAttributesAsync(Files([RelativePath]), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PreloadAttributesAsync_NullFiles_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.PreloadAttributesAsync(null!, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("files");
    }

    [Fact]
    public async Task PreloadAttributesAsync_FilesContainNull_ThrowsArgumentNullExceptionWithoutRunningGit()
    {
        // Act
        var act = () => _sut.PreloadAttributesAsync([.. Files([RelativePath]), null!],
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<ArgumentNullException>())
            .WithParameterName("files");

        A.CallTo(_gitCommandRunner)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task PreloadAttributesAsync_FilesOfTwoRepositories_RunsCheckAttrInEachRoot()
    {
        // Arrange
        const string otherRepositoryRootPath = "/other";
        RepositoryFile[] files =
        [
            RepositoryFile.Resolve(RepositoryRootPath, "a.md"),
            RepositoryFile.Resolve(otherRepositoryRootPath, "b.md")
        ];

        string[] firstArguments = ["check-attr", "eol", "text", "crlf", "--", "a.md"];
        string[] secondArguments = ["check-attr", "eol", "text", "crlf", "--", "b.md"];

        // Act
        await _sut.PreloadAttributesAsync(files, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(FullRepositoryRootPath,
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(firstArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _gitCommandRunner.RunAsync(Path.GetFullPath(otherRepositoryRootPath),
                A<IReadOnlyList<string>>.That.IsSameSequenceAs(secondArguments), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PreloadAttributesAsync_FirstChunkFails_RunsNoFurtherChunk()
    {
        // Arrange
        string[] paths = [.. Enumerable.Range(0, 150).Select(x => $"file{x}.md")];

        // Act
        await _sut.PreloadAttributesAsync(Files(paths), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[0] == "check-attr"), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private void AssertConfigurationNotRead()
    {
        A.CallTo(() => _gitCommandRunner.RunAsync(A<string>._,
                A<IReadOnlyList<string>>.That.Matches(x => x[0] == "config"), A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private async Task<LineEnding?> GetLineEndingFromNewContainerAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_gitCommandRunner);
        services.AddShadeKitCore();

        await using var serviceProvider = services.BuildServiceProvider();

        var result = await serviceProvider.GetRequiredService<IGitLineEndingResolver>()
            .GetLineEndingAsync(RepositoryFile.Resolve(RepositoryRootPath, RelativePath),
                TestContext.Current.CancellationToken);

        return result.LineEnding;
    }

    private static Task<LineEnding?> GetLineEndingAsync(GitLineEndingResolver sut)
    {
        return GetLineEndingAsync(sut, RepositoryRootPath, RelativePath, TestContext.Current.CancellationToken);
    }

    private static async Task<LineEnding?> GetLineEndingAsync(GitLineEndingResolver sut, string repositoryRootPath,
        string relativePath, CancellationToken cancellationToken)
    {
        var result = await ResolveAsync(sut, repositoryRootPath, relativePath, cancellationToken);

        return result.LineEnding;
    }

    private static Task<GitLineEndingResult> ResolveAsync(GitLineEndingResolver sut)
    {
        return ResolveAsync(sut, RepositoryRootPath, RelativePath, TestContext.Current.CancellationToken);
    }

    private static Task<GitLineEndingResult> ResolveAsync(GitLineEndingResolver sut, string repositoryRootPath,
        string relativePath, CancellationToken cancellationToken)
    {
        return sut.GetLineEndingAsync(RepositoryFile.Resolve(repositoryRootPath, relativePath), cancellationToken);
    }

    private static RepositoryFile[] Files(IEnumerable<string> relativePaths)
    {
        return [.. relativePaths.Select(relativePath => RepositoryFile.Resolve(RepositoryRootPath, relativePath))];
    }

    private void SetUpAttributes(string eol, string text, string crlf)
    {
        SetUpAttributes(RelativePath, eol, text, crlf);
    }

    private void SetUpAttributes(string path, string eol, string text, string crlf)
    {
        _gitCommandRunner.SetUpResult(["check-attr", "eol", "text", "crlf", "--", path],
            new ExternalProcessResult(0, $"{path}: eol: {eol}\n{path}: text: {text}\n{path}: crlf: {crlf}\n", ""));
    }
}
