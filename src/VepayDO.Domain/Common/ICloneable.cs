namespace VepayDO.Domain.Common;

public interface ICloneable<out T>
{
    T Clone();
}
