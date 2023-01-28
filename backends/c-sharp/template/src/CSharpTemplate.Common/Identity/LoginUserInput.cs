using System.ComponentModel.DataAnnotations;

namespace CSharpTemplate.Common.Identity;

public class LoginUserInput
{
    [Required]
    [StringLength(1024)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}
