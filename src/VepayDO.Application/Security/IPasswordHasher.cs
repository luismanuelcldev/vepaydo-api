namespace VepayDO.Application.Security;

/// <summary>
/// Port for password hashing (AGENTS.md section 13). Implemented with argon2id
/// by <c>Argon2idPasswordHasher</c>. Kept behind an interface so tests can
/// substitute a fast fake and so the algorithm never leaks into the domain.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password into a self-describing PHC string.</summary>
    string Hash(string plainPassword);

    /// <summary>
    /// Verifies a plaintext password against a stored hash. Returns
    /// <c>false</c> for a malformed or unsupported hash instead of throwing.
    /// </summary>
    bool Verify(string passwordHash, string plainPassword);
}
