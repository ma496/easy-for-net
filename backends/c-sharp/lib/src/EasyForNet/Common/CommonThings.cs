using AutoMapper;
using System;

namespace EasyForNet.Common;

public abstract class CommonThings
{
    public IServiceProvider ServiceProvider { get; set; }
    public IMapper Mapper { get; set; }
    public ICurrentUser CurrentUser { get; set; }
}