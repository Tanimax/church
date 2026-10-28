namespace ChurchAttendance.SharedKernel.Results;

// An expected business failure (not a crash): a stable code the caller can branch on, and a
// message in French that can be shown to the user as is. See docs/gestion-des-erreurs.md.
public sealed record Error(string Code, string Message);
