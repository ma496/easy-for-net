namespace CSharpTemplate.Common.Identity;

public class RegisterUserOutput
{
    public string Message { get; set; }
    public bool IsSuccess { get; set; }
    public IEnumerable<string> Errors { get; set; }
}
