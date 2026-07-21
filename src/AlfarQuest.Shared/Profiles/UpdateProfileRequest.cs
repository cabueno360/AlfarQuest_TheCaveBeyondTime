using System.ComponentModel.DataAnnotations;

namespace AlfarQuest.Shared.Profiles;

/// <summary>The editable slice of a profile. Email is absent on purpose: changing
/// it has to go through verification, and leaving it out means no route exists to
/// change it before that lands.</summary>
public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "A name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "A name is between 2 and 80 characters.")]
    public string FullName { get; set; } = "";

    [StringLength(56)] public string? Country { get; set; }

    [StringLength(32)]
    [RegularExpression(@"^[0-9 ()+\-]*$", ErrorMessage = "Digits, spaces and + ( ) - only.")]
    public string? PhoneNumber { get; set; }
}
