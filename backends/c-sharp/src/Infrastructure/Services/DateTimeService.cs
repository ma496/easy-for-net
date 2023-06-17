using EasyForNet.Application.Common.Interfaces;

namespace EasyForNet.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime Now => DateTime.Now;
}
