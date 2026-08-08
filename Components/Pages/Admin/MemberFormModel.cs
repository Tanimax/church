using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Models;

namespace ChurchAttendance.Components.Pages.Admin;

// Shared by MemberAdd.razor and MemberEdit.razor.
public class MemberFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Le prénom est requis.")]
    [StringLength(100, ErrorMessage = "Le prénom ne peut pas dépasser 100 caractères.")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Le nom est requis.")]
    [StringLength(100, ErrorMessage = "Le nom ne peut pas dépasser 100 caractères.")]
    public string LastName { get; set; } = "";

    // Validated manually in Validate() below instead of via [Phone]/[EmailAddress]:
    // those attributes reject an empty string (as opposed to null), but Blazor's
    // InputText always posts "" for a blank optional field, never null.
    public string? Phone { get; set; }

    public string? Email { get; set; }

    public Gender? Sexe { get; set; }
    public bool IsBaptized { get; set; }
    public MaritalStatus? EtatMatrimonial { get; set; }

    [Range(1, 31, ErrorMessage = "Le jour de naissance doit être entre 1 et 31.")]
    public int? BirthDay { get; set; }

    [Range(1, 12, ErrorMessage = "Le mois de naissance doit être entre 1 et 12.")]
    public int? BirthMonth { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Phone) && !new PhoneAttribute().IsValid(Phone))
        {
            yield return new ValidationResult("Numéro de téléphone invalide.", [nameof(Phone)]);
        }

        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("Adresse courriel invalide.", [nameof(Email)]);
        }

        if (BirthDay.HasValue != BirthMonth.HasValue)
        {
            yield return new ValidationResult(
                "Le jour et le mois de naissance doivent être renseignés ensemble.",
                [nameof(BirthDay), nameof(BirthMonth)]);
            yield break;
        }

        if (BirthDay is { } day && BirthMonth is { } month && month is >= 1 and <= 12)
        {
            var daysInMonth = DateTime.DaysInMonth(2000, month); // leap year reference so Feb 29 is allowed
            if (day < 1 || day > daysInMonth)
            {
                yield return new ValidationResult(
                    $"Le jour {day} n'existe pas pour ce mois.",
                    [nameof(BirthDay)]);
            }
        }
    }
}
