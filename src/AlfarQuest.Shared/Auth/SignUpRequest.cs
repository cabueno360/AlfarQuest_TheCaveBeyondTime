using System.ComponentModel.DataAnnotations;

namespace AlfarQuest.Shared.Auth;

/// <summary>What the sign-up form sends. The annotations are the contract both
/// sides validate against: the Blazor form for instant feedback, the API again
/// because a browser is not a trustworthy validator.</summary>
public sealed class SignUpRequest
{
    [Required(ErrorMessage = "Tell us what to call you.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "A name is between 2 and 80 characters.")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(128, MinimumLength = PasswordRules.MinLength,
        ErrorMessage = "Passwords are at least 10 characters.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Repeat the password.")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";

    // Optional. Empty string and null mean the same thing to the API.
    [StringLength(56)] public string? Country { get; set; }
    [StringLength(32)]
    [RegularExpression(@"^[0-9 ()+\-]*$", ErrorMessage = "Digits, spaces and + ( ) - only.")]
    public string? PhoneNumber { get; set; }
}
