using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Application.SundayClasses;

public class SundayClassFormModel
{
    [Required(ErrorMessage = "Le nom de la classe est requis.")]
    [StringLength(100, ErrorMessage = "Le nom ne peut pas dépasser 100 caractères.")]
    public string Name { get; set; } = "";
}
