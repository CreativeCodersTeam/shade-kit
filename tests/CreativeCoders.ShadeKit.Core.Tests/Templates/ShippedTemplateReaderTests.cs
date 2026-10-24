using System.Text;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Templates;

namespace CreativeCoders.ShadeKit.Core.Tests.Templates;

public class ShippedTemplateReaderTests
{
    private static readonly ShippedTemplate SampleTemplate =
        new ShippedTemplate("checks/sample.template.md", "templates/checks/sample.md");

    public static TheoryData<string> ShippedResourcePaths =>
        new TheoryData<string>(ShippedTemplates.All.Select(x => x.ResourcePath));

    [Theory]
    [MemberData(nameof(ShippedResourcePaths))]
    public void ReadContent_ShippedTemplate_ReturnsMarkdownContentWithLfLineEndings(string resourcePath)
    {
        // Arrange
        var template = ShippedTemplates.All.Single(x => x.ResourcePath == resourcePath);
        var reader = new ShippedTemplateReader();

        // Act
        var content = reader.ReadContent(template);

        // Assert
        content
            .Should().StartWith("# ")
            .And.NotContain("\r\n")
            .And.NotStartWith("﻿");
    }

    [Fact]
    public void ReadContent_ResourceWithCrlfLineEndings_ReturnsContentWithLfLineEndings()
    {
        // Arrange
        var reader = new ShippedTemplateReader(_ => CreateStream("# Title\r\n\r\nLine one\r\nLine two\r\n"));

        // Act
        var content = reader.ReadContent(SampleTemplate);

        // Assert
        content
            .Should().Be("# Title\n\nLine one\nLine two\n");
    }

    [Fact]
    public void ReadContent_ResourceWithMixedLineEndings_ReturnsContentWithoutCrlf()
    {
        // Arrange
        var reader = new ShippedTemplateReader(_ => CreateStream("one\r\ntwo\nthree\r\n"));

        // Act
        var content = reader.ReadContent(SampleTemplate);

        // Assert
        content
            .Should().Be("one\ntwo\nthree\n");
    }

    [Theory]
    [InlineData("one\r\r\ntwo", "one\n\ntwo")]
    [InlineData("one\rtwo\r", "one\ntwo\n")]
    [InlineData("\r\n\r\n", "\n\n")]
    public void ReadContent_ResourceWithCarriageReturns_ReturnsContentWithoutCrlf(string resourceContent,
        string expectedContent)
    {
        // Arrange
        var reader = new ShippedTemplateReader(_ => CreateStream(resourceContent));

        // Act
        var content = reader.ReadContent(SampleTemplate);

        // Assert
        content
            .Should().Be(expectedContent)
            .And.NotContain("\r\n");
    }

    [Fact]
    public void ReadContent_EmptyResource_ReturnsEmptyString()
    {
        // Arrange
        var reader = new ShippedTemplateReader(_ => CreateStream(string.Empty));

        // Act
        var content = reader.ReadContent(SampleTemplate);

        // Assert
        content
            .Should().BeEmpty();
    }

    [Fact]
    public void ReadContent_ResourceWithUtf8Bom_ReturnsContentWithoutBom()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("# Title\n")).ToArray();
        var reader = new ShippedTemplateReader(_ => new MemoryStream(bytes));

        // Act
        var content = reader.ReadContent(SampleTemplate);

        // Assert
        content
            .Should().Be("# Title\n");
    }

    [Fact]
    public void ReadContent_ResourceWithNonAsciiCharacters_ReturnsContentDecodedAsUtf8()
    {
        // Arrange
        var reader = new ShippedTemplateReader(_ => CreateStream("Feature: `{{feature-id}}` · Spec – done\n"));

        // Act
        var content = reader.ReadContent(SampleTemplate);

        // Assert
        content
            .Should().Be("Feature: `{{feature-id}}` · Spec – done\n");
    }

    [Fact]
    public void ReadContent_Template_OpensResourceWithTemplatesPrefixAndForwardSlashes()
    {
        // Arrange
        var openedNames = new List<string>();
        var reader = new ShippedTemplateReader(name =>
        {
            openedNames.Add(name);
            return CreateStream("# Title\n");
        });

        // Act
        reader.ReadContent(SampleTemplate);

        // Assert
        openedNames
            .Should().Equal("templates/checks/sample.template.md");
    }

    [Fact]
    public void ReadContent_Template_DisposesResourceStream()
    {
        // Arrange
        var stream = CreateStream("# Title\n");
        var reader = new ShippedTemplateReader(_ => stream);

        // Act
        reader.ReadContent(SampleTemplate);

        // Assert
        stream.CanRead
            .Should().BeFalse();
    }

    [Fact]
    public void ReadContent_ResourceMissing_ThrowsInvalidOperationExceptionNamingResource()
    {
        // Arrange
        var reader = new ShippedTemplateReader(_ => null);

        // Act
        var act = () => reader.ReadContent(SampleTemplate);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'templates/checks/sample.template.md'*");
    }

    [Fact]
    public void ReadContent_TemplateNotInCoreAssembly_ThrowsInvalidOperationException()
    {
        // Arrange
        var reader = new ShippedTemplateReader();

        // Act
        var act = () => reader.ReadContent(SampleTemplate);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'templates/checks/sample.template.md'*");
    }

    [Fact]
    public void ReadContent_NullTemplate_ThrowsArgumentNullException()
    {
        // Arrange
        var reader = new ShippedTemplateReader();

        // Act
        var act = () => reader.ReadContent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("shippedTemplate");
    }

    [Fact]
    public void Ctor_NullOpenResource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ShippedTemplateReader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("openResource");
    }

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(new UTF8Encoding(false).GetBytes(content));
    }
}
