namespace ChurchAttendance.Domain;

// A domain rule was broken (e.g. a user without a username). Input validation is supposed to
// prevent this, so it signals a bug or an unvalidated path, not a normal business case: those
// are returned as a Result instead. The API answers it with 400, the message being French.
public sealed class DomainException(string message) : Exception(message);
