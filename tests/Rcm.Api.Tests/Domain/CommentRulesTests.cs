using Rcm.Api.Domain;
using static Rcm.Api.Tests.TestSupport.TestOrg;

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

    [Fact]
    public void OnBehalfOf_CoverOfOwner_ReturnsOwner()
    {
        Assert.Equal(Blake, CommentRules.OnBehalfOf(Actor(Alex, Blake), Blake, Chart));
    }

    [Fact]
    public void OnBehalfOf_CoverOfOwnersSupervisor_ReturnsSupervisor()
    {
        Assert.Equal(Taylor, CommentRules.OnBehalfOf(Actor(Sam, Taylor), Charlie, Chart));
    }

    [Fact]
    public void OnBehalfOf_SeesItemWithoutCover_ReturnsNull()
    {
        Assert.Null(CommentRules.OnBehalfOf(Actor(Alex), Alex, Chart));
        Assert.Null(CommentRules.OnBehalfOf(Actor(Sam, Alex), Alex, Chart));
        Assert.Null(CommentRules.OnBehalfOf(Actor(Morgan, Sam), Alex, Chart));
    }
}
