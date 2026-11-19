using Microsoft.AspNetCore.Http.HttpResults;
using Rcm.Api.Domain;
using Rcm.Api.Features;

namespace Rcm.Api.Tests.Features;

public class ResultMappingTests
{
    [Theory]
    [InlineData(ErrorKind.Invalid, 400)]
    [InlineData(ErrorKind.Forbidden, 403)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Conflict, 409)]
    public void ToHttp_Error_ReturnsProblemWithStatus(ErrorKind kind, int status)
    {
        var problem = Assert.IsType<ProblemHttpResult>(new Error(kind, "Message").ToHttp());

        Assert.Equal(status, problem.StatusCode);
        Assert.Equal("Message", problem.ProblemDetails.Detail);
    }

    [Fact]
    public void ToHttp_Success_ReturnsOkWithValue()
    {
        Result<string> result = "Value";

        var ok = Assert.IsType<Ok<string>>(result.ToHttp());

        Assert.Equal("Value", ok.Value);
    }
}
