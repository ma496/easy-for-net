using System.Collections.Generic;
using System;

namespace EasyForNet.EntityFramework.Identity;

public class LoginUserOutput
{
    public string Message { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime? ExpireDate { get; set; }
    public string Token { get; set; }
}
