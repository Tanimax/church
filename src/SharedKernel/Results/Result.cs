using System.Diagnostics.CodeAnalysis;

namespace ChurchAttendance.SharedKernel.Results;

// The outcome of a use case whose failure is a normal business case (username taken, card
// unknown...). Unexpected failures stay exceptions and are handled at the app's boundaries.
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(Error error) : base(error)
    {
    }

    // Only read it after checking IsSuccess.
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Pas de valeur : le résultat est l'erreur « {Error.Code} ».");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
