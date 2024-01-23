namespace Efn.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime Now => DateTime.Now;
}

public interface IDateTime
{
    DateTime Now { get; }
}

