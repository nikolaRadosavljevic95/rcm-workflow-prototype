using Rcm.Api.Domain;

namespace Rcm.Api.Tests.Domain;

public class CommentRulesTests
{
    [Fact]
    public void Check_OpenMonthWithText_ReturnsTrimmedText()
    {
        Assert.Equal("Called the facility.", CommentRules.Check(true, "  Called the facility. ").Value);
    }

    [Fact]
    public void Check_ClosedMonth_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CommentRules.Check(false, "Late note").Error?.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Check_NoText_ReturnsInvalid(string? text)
    {
        Assert.Equal(ErrorKind.Invalid, CommentRules.Check(true, text).Error?.Kind);
    }

    [Fact]
    public void Check_TextOverLimit_ReturnsInvalid()
    {
        Assert.Equal(ErrorKind.Invalid, CommentRules.Check(true, new string('x', WorkflowRules.MaxTextLength + 1)).Error?.Kind);
    }
}
