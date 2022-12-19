using System.ComponentModel.DataAnnotations;

namespace EasyForNet.EntityFramework.Identity;

public class RegisterUserInput
{
    [Required]
    [StringLength(1024)]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string Password { get; set; }
}
