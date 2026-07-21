using System.ComponentModel.DataAnnotations;

namespace AlfarQuest.Shared.Auth;

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "Confirm your current password.")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Choose a new password.")]
    [StringLength(128, MinimumLength = PasswordRules.MinLength,
        ErrorMessage = "Passwords are at least 10 characters.")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Repeat the new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

/// <summary>Requesting a reset link. The endpoint answers identically whether or
/// not the address is registered, so this is also the shape of a probe that
/// learns nothing.</summary>
public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    public string Email { get; set; } = "";
}
