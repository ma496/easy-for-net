using System.ComponentModel.DataAnnotations;

namespace CSharpTemplate.Common.Identity.Dto;

public class RegisterUserInput
{
    [Required]
    [StringLength(1024)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(16)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [StringLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}
