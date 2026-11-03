using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Gates.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates.Exceptions;

public class ChecklistTemplateNotFoundExceptionTests
{
    [Fact]
    public void Constructor_TemplatePath_MessageNamesTemplatePath()
    {
        // Act
        var exception = new ChecklistTemplateNotFoundException(".shadekit/templates/checks/definition-of-ready.md");

        // Assert
        exception.Message
            .Should().Be("The checklist template '.shadekit/templates/checks/definition-of-ready.md' does not exist.");

        exception.TemplatePath
            .Should().Be(".shadekit/templates/checks/definition-of-ready.md");
    }
}
