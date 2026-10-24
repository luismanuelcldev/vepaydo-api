namespace VepayDO.Application.Security;

public interface IPasswordHasher
{

    string Hash(string plainPassword);

    bool Verify(string passwordHash, string plainPassword);
}
