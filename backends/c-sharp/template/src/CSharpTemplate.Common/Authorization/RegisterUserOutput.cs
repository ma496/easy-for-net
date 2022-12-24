using System.Collections.Generic;

namespace EasyForNet.EntityFramework.Identity;

public class RegisterUserOutput
{
    public string Message { get; set; }
    public bool IsSuccess { get; set; }
    public IEnumerable<string> Errors { get; set; }
}
