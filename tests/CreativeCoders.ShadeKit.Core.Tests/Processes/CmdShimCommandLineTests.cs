using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Tests.Processes;

// The check and the command line are pure string logic, so they run on every operating system; the Windows run of a
// .cmd shim itself is Unverified.
public class CmdShimCommandLineTests
{
    private const string ShimPath = @"C:\Users\jane\AppData\Roaming\npm\copilot.cmd";

    private const string ShimPathWithSpace = @"C:\Users\Jane Doe\AppData\Roaming\npm\copilot.cmd";

    [Theory]
    [InlineData(@"C:\npm\copilot.cmd")]
    [InlineData(@"C:\npm\COPILOT.CMD")]
    [InlineData(@"C:\npm\copilot.Cmd")]
    [InlineData("/opt/npm/copilot.cmd")]
    public void IsCmdShim_CmdExtension_ReturnsTrue(string executablePath)
    {
        // Act
        var result = CmdShimCommandLine.IsCmdShim(executablePath);

        // Assert
        result
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\npm\copilot.exe")]
    [InlineData(@"C:\npm\copilot.bat")]
    [InlineData(@"C:\npm\copilot.ps1")]
    [InlineData(@"C:\npm\copilot.cmd.exe")]
    [InlineData("/usr/local/bin/copilot")]
    public void IsCmdShim_OtherExtension_ReturnsFalse(string executablePath)
    {
        // Act
        var result = CmdShimCommandLine.IsCmdShim(executablePath);

        // Assert
        result
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    [InlineData(@"C:\WINDOWS\system32\CMD.EXE")]
    [InlineData("C:/Windows/System32/cmd.exe")]
    [InlineData(@"\\server\share\cmd.exe")]
    [InlineData(@"\\?\C:\Windows\System32\cmd.exe")]
    public void IsValidComSpec_FullyQualifiedCmdExe_ReturnsTrue(string comSpec)
    {
        // Act
        var result = CmdShimCommandLine.IsValidComSpec(comSpec, ShimPath, out var problem);

        // Assert
        result
            .Should().BeTrue();

        problem
            .Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidComSpec_NotSet_ReturnsFalseWithNotSetProblem(string? comSpec)
    {
        // Act
        var result = CmdShimCommandLine.IsValidComSpec(comSpec, ShimPath, out var problem);

        // Assert
        result
            .Should().BeFalse();

        problem
            .Should().Be($"The ComSpec environment variable is not set, so the .cmd shim '{ShimPath}' cannot be " +
                         "run through cmd.exe.");
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData(@"C:cmd.exe")]
    [InlineData(@"\Windows\System32\cmd.exe")]
    [InlineData(@"System32\cmd.exe")]
    [InlineData(@"""C:\Windows\System32\cmd.exe""")]
    public void IsValidComSpec_RelativePath_ReturnsFalseWithRelativeProblem(string comSpec)
    {
        // Act
        var result = CmdShimCommandLine.IsValidComSpec(comSpec, ShimPath, out var problem);

        // Assert
        result
            .Should().BeFalse();

        problem
            .Should().Be($"The ComSpec environment variable '{comSpec}' is not a fully qualified path, so the .cmd " +
                         $"shim '{ShimPath}' cannot be run through cmd.exe.");
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    [InlineData(@"C:\Windows\System32\cmd.exe.bak")]
    [InlineData(@"C:\tools\mycmd.exe")]
    [InlineData(@"C:\Windows\System32\")]
    public void IsValidComSpec_NotCmdExe_ReturnsFalseWithNotCmdExeProblem(string comSpec)
    {
        // Act
        var result = CmdShimCommandLine.IsValidComSpec(comSpec, ShimPath, out var problem);

        // Assert
        result
            .Should().BeFalse();

        problem
            .Should().Be($"The ComSpec environment variable '{comSpec}' does not name cmd.exe, so the .cmd shim " +
                         $"'{ShimPath}' cannot be run through cmd.exe.");
    }

    [Fact]
    public void FindRefusal_PlainArguments_ReturnsNull()
    {
        // Act
        var result = CmdShimCommandLine.FindRefusal(ShimPathWithSpace,
            ["plugin", "marketplace", "add", "CreativeCodersTeam/shade-kit#v0.8.0", @"C:\my plugins\kit\", ""]);

        // Assert
        result
            .Should().BeNull();
    }

    [Theory]
    [InlineData("\"", "'\"'")]
    [InlineData("%", "'%'")]
    [InlineData("!", "'!'")]
    [InlineData("^", "'^'")]
    [InlineData("&", "'&'")]
    [InlineData("|", "'|'")]
    [InlineData("<", "'<'")]
    [InlineData(">", "'>'")]
    [InlineData("(", "'('")]
    [InlineData(")", "')'")]
    [InlineData("\r", @"carriage return (\r)")]
    [InlineData("\n", @"line feed (\n)")]
    [InlineData("\0", @"NUL (\0)")]
    public void FindRefusal_ArgumentWithRefusedCharacter_NamesPositionAndCharacter(string character,
        string expectedCharacter)
    {
        // Arrange
        var argument = $"a{character}b";

        // Act
        var result = CmdShimCommandLine.FindRefusal(ShimPath, ["plugin", argument]);

        // Assert
        result
            .Should().NotBeNull()
            .And.StartWith("Argument 2 '")
            .And.Contain($"' contains the character {expectedCharacter}, which is refused: ")
            .And.Contain("a .cmd shim is run through cmd.exe")
            .And.Contain("refused instead of escaped")
            .And.EndWith(".");
    }

    [Theory]
    [InlineData("a\rb", @"'a\rb'")]
    [InlineData("a\nb", @"'a\nb'")]
    [InlineData("a\0b", @"'a\0b'")]
    [InlineData("a%\u0007b", @"'a%\u0007b'")]
    public void FindRefusal_ArgumentWithControlCharacter_RendersItAsEscape(string argument, string expectedRendering)
    {
        // Act
        var result = CmdShimCommandLine.FindRefusal(ShimPath, [argument]);

        // Assert
        result
            .Should().StartWith($"Argument 1 {expectedRendering} contains the character ");
    }

    [Fact]
    public void FindRefusal_SeveralRefusedArguments_ReportsTheFirstArgumentAndItsFirstCharacter()
    {
        // Act
        var result = CmdShimCommandLine.FindRefusal(ShimPath, ["ok", "x&y|z", "%PATH%"]);

        // Assert
        result
            .Should().StartWith("Argument 2 'x&y|z' contains the character '&', which is refused: ");
    }

    [Fact]
    public void FindRefusal_PathWithRefusedCharacter_NamesThePath()
    {
        // Arrange
        const string shimPath = @"C:\Program Files (x86)\npm\copilot.cmd";

        // Act
        var result = CmdShimCommandLine.FindRefusal(shimPath, ["plugin", "a&b"]);

        // Assert
        result
            .Should().StartWith($"The path of the .cmd shim '{shimPath}' contains the character '(', which is " +
                                "refused: a .cmd shim is run through cmd.exe");
    }

    [Theory]
    [InlineData(@"C:\npm%\copilot.cmd", "'%'")]
    [InlineData(@"C:\a&b\copilot.cmd", "'&'")]
    [InlineData(@"C:\jane!\copilot.cmd", "'!'")]
    public void FindRefusal_PathWithRefusedCharacter_NamesTheCharacter(string shimPath, string expectedCharacter)
    {
        // Act
        var result = CmdShimCommandLine.FindRefusal(shimPath, []);

        // Assert
        result
            .Should().StartWith($"The path of the .cmd shim '{shimPath}' contains the character {expectedCharacter}");
    }

    [Fact]
    public void Create_PathWithSpaceAndPlainArguments_QuotesPathInsideOuterQuotes()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPathWithSpace, ["plugin", "list", "--json"]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPathWithSpace}\" plugin list --json\"");
    }

    [Fact]
    public void Create_ArgumentWithSpace_QuotesOnlyThatArgument()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath,
            ["plugin", "marketplace", "add", @"C:\my plugins\shade-kit"]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\" plugin marketplace add \"C:\\my plugins\\shade-kit\"\"");
    }

    [Fact]
    public void Create_QuotedArgumentEndsWithBackslashes_DoublesTheTrailingBackslashes()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath, [@"C:\my plugins\\"]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\" \"C:\\my plugins\\\\\\\\\"\"");
    }

    [Fact]
    public void Create_UnquotedArgumentEndsWithBackslash_KeepsItUnchanged()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath, [@"C:\plugins\"]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\" C:\\plugins\\\"");
    }

    [Fact]
    public void Create_EmptyArgument_PassesEmptyQuotes()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath, ["--ref", ""]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\" --ref \"\"\"");
    }

    [Fact]
    public void Create_WhitespaceOnlyArgument_QuotesTheArgument()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath, [" "]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\" \" \"\"");
    }

    [Fact]
    public void Create_ArgumentWithTab_QuotesTheArgument()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath, ["a\tb"]);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\" \"a\tb\"\"");
    }

    [Fact]
    public void Create_NoArguments_QuotesOnlyThePath()
    {
        // Act
        var result = CmdShimCommandLine.Create(ShimPath, []);

        // Assert
        result
            .Should().Be($"/d /s /c \"\"{ShimPath}\"\"");
    }

    [Fact]
    public void Create_RefusedArgument_ThrowsArgumentException()
    {
        // Act
        var act = () => CmdShimCommandLine.Create(ShimPath, ["a&b"]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("arguments");
    }

    [Fact]
    public void Create_RefusedPath_ThrowsArgumentException()
    {
        // Act
        var act = () => CmdShimCommandLine.Create(@"C:\npm%\copilot.cmd", ["plugin"]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("shimPath");
    }
}
