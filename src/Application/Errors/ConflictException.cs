namespace ChurchAttendance.Application.Errors;

// A write was rejected because it conflicts with data saved meanwhile (a unique constraint:
// same username, same session date, same check-in). Infrastructure translates the
// database's error into this so Application never depends on Npgsql. A use case that expects
// the conflict catches it and turns it into a Result ("déjà pris") or a success ("déjà
// enregistré"); otherwise the API answers 409.
public sealed class ConflictException(string? constraintName, Exception innerException)
    : Exception($"La donnée entre en conflit avec une donnée existante ({constraintName}).", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
