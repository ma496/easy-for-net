namespace Efn.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime Now => DateTime.UtcNow;
}

public interface IDateTime
{
    DateTime Now { get; }
}

