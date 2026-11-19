using Rcm.Api.Domain;
using static Rcm.Api.Tests.TestSupport.TestOrg;

namespace Rcm.Api.Tests.Domain;

public class ReviewerRulesTests
{
    [Fact]
    public void ReviewerOf_Biller_IsTheirSupervisor()
    {
        Assert.Equal(Sam, ReviewerRules.ReviewerOf(Alex, Chart));
    }

    [Fact]
    public void ReviewerOf_Supervisor_IsTheirManager()
    {
        Assert.Equal(Quinn, ReviewerRules.ReviewerOf(Casey, Chart));
    }

    [Fact]
    public void ReviewerOf_OwnerReportingToNoOne_IsEmpty()
    {
        Assert.Null(ReviewerRules.ReviewerOf(Morgan, Chart));
    }

    [Fact]
    public void IsReviewer_ReviewerOrTheirCover_IsTrue()
    {
        Assert.True(ReviewerRules.IsReviewer(Actor(Sam), Alex, Chart));
        Assert.True(ReviewerRules.IsReviewer(Actor(Taylor, Sam), Alex, Chart));
    }

    [Fact]
    public void IsReviewer_ManagerOutsideReportingLine_IsFalse()
    {
        Assert.False(ReviewerRules.IsReviewer(Actor(Morgan), Alex, Chart));
    }

    [Fact]
    public void IsReviewer_OwnerReportingToNoOne_OtherManagersOnly()
    {
        Assert.True(ReviewerRules.IsReviewer(Actor(Quinn), Morgan, Chart));
        Assert.False(ReviewerRules.IsReviewer(Actor(Morgan), Morgan, Chart));
        Assert.False(ReviewerRules.IsReviewer(Actor(Sam), Morgan, Chart));
    }
}
