namespace VepayDO.Domain.Common;

/// <summary>
/// Prototype pattern contract (AGENTS.md section 8.4). Implementations must
/// return a deep, independent copy: mutating the clone never affects the
/// original, including nested objects and decimals.
/// </summary>
/// <typeparam name="T">The concrete type returned by <see cref="Clone"/>.</typeparam>
public interface ICloneable<out T>
{
    T Clone();
}
