namespace VepayDO.Domain.Time;

public interface IClock
{

    DateTimeOffset UtcNow { get; }
}
