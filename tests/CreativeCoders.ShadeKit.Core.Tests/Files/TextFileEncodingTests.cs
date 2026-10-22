using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class TextFileEncodingTests
{
    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x42 }, new byte[] { 0x41, 0x42 })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF }, new byte[0])]
    [InlineData(new byte[] { 0x41, 0x42 }, new byte[] { 0x41, 0x42 })]
    [InlineData(new byte[] { 0xEF, 0xBB }, new byte[] { 0xEF, 0xBB })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF }, new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData(new byte[0], new byte[0])]
    public void SkipUtf8Bom_Content_RemovesOnlyOneLeadingUtf8Bom(byte[] content, byte[] expected)
    {
        // Act
        var result = TextFileEncoding.SkipUtf8Bom(content).ToArray();

        // Assert
        result
            .Should().Equal(expected);
    }

    [Fact]
    public void Utf8WithoutBom_GetBytes_EmitsNoByteOrderMark()
    {
        // Act
        var preamble = TextFileEncoding.Utf8WithoutBom.GetPreamble();
        var bytes = TextFileEncoding.Utf8WithoutBom.GetBytes("ä");

        // Assert
        preamble
            .Should().BeEmpty();

        bytes
            .Should().Equal(0xC3, 0xA4);
    }

    [Fact]
    public void Utf8WithoutBom_GetStringWithInvalidBytes_ReplacesThemInsteadOfThrowing()
    {
        // Act
        var text = TextFileEncoding.Utf8WithoutBom.GetString([0x41, 0xE4, 0x42]);

        // Assert
        text
            .Should().Be("A\uFFFDB");
    }

    [Theory]
    [InlineData(new byte[] { 0x23, 0x20, 0xC3, 0xA4 }, "# \u00E4")]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x23, 0x20, 0xC3, 0xA4 }, "# \u00E4")]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF }, "")]
    [InlineData(new byte[0], "")]
    public void DecodeUtf8_ValidUtf8_ReturnsTextWithoutByteOrderMark(byte[] content, string expected)
    {
        // Act
        var text = TextFileEncoding.DecodeUtf8(content, (_, _) => new InvalidOperationException());

        // Assert
        text
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x23, 0x00, 0x00, 0x00 }, "UTF-32 LE")]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x23 }, "UTF-32 BE")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x23, 0x00 }, "UTF-16 LE")]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x23 }, "UTF-16 BE")]
    public void DecodeUtf8_OtherUnicodeByteOrderMark_ThrowsCreatedExceptionWithEncodingName(byte[] content,
        string encodingName)
    {
        // Act
        var act = () => TextFileEncoding.DecodeUtf8(content,
            (reason, innerException) => new InvalidOperationException(reason, innerException));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"The file is encoded as {encodingName}, not as UTF-8. Save the file as UTF-8 and run the " +
                         "command again.")
            .Which.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void DecodeUtf8_NulByteWithoutByteOrderMark_ThrowsCreatedExceptionAboutNulByte()
    {
        // Act
        var act = () => TextFileEncoding.DecodeUtf8([0x23, 0x00, 0x20, 0x00],
            (reason, innerException) => new InvalidOperationException(reason, innerException));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The file contains a NUL byte, so it is not UTF-8 text; it may be UTF-16 or UTF-32 without " +
                         "byte order mark, or a binary file. Save the file as UTF-8 and run the command again.")
            .Which.InnerException
            .Should().BeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0x23, 0x20, 0x4C, 0xE4, 0x6E, 0x67, 0x65 })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3 })]
    [InlineData(new byte[] { 0xEF, 0xBB })]
    public void DecodeUtf8_InvalidUtf8_ThrowsCreatedExceptionWithDecoderInnerException(byte[] content)
    {
        // Act
        var act = () => TextFileEncoding.DecodeUtf8(content,
            (reason, innerException) => new InvalidOperationException(reason, innerException));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("The file contains bytes that are not valid UTF-8. Save the file as UTF-8 and run the " +
                         "command again.")
            .WithInnerException<DecoderFallbackException>();
    }

    [Fact]
    public void DecodeUtf8_NullCreateException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => TextFileEncoding.DecodeUtf8([0x41], null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("createException");
    }
}
