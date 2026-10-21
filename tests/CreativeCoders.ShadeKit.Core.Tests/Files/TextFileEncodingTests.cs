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
}
