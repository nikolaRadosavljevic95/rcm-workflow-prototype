using System.Diagnostics.CodeAnalysis;

namespace Rcm.Api.Domain;

public enum ErrorKind
{
    Invalid,
    Forbidden,
    NotFound,
    Conflict,
}

public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error Invalid(string message) => new(ErrorKind.Invalid, message);
    public static Error Forbidden(string message) => new(ErrorKind.Forbidden, message);
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);
    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
}

// Business rules return a Result instead of throwing. Callers map the error kind to a response.
public sealed class Result<T>
{
    private readonly T? value;

    private Result(T value) => this.value = value;

    private Result(Error error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? value! : throw new InvalidOperationException($"No value: {Error.Message}");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
