using System.ComponentModel.DataAnnotations;

namespace AlfarQuest.Shared.Auth;

public sealed class SignInRequest
{
    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "A password is required.")]
    public string Password { get; set; } = "";

    /// <summary>Lengthens the session rather than storing the password anywhere.</summary>
    public bool RememberMe { get; set; }
}
