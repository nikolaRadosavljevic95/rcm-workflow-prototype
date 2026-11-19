using Rcm.Api.Domain;

namespace Rcm.Api.Features;

public static class ResultMapping
{
    public static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToHttp();

    public static IResult ToHttp(this Error error) =>
        TypedResults.Problem(detail: error.Message, statusCode: StatusFor(error.Kind));

    public static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };
}
