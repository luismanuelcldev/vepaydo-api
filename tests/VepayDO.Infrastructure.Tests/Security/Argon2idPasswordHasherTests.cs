using FluentAssertions;
using VepayDO.Infrastructure.Security;

namespace VepayDO.Infrastructure.Tests.Security;

public sealed class Argon2idPasswordHasherTests
{

    private const string ReferenceHash =
        "$argon2id$v=19$m=19456,t=2,p=1$Kgx7Zq8w5CgyYsZmOQhFYQ$" +
        "gUskzqnbJeT7fjDPAIGCynJJaRHh7+4aLdY9kQkQbQw";

    private readonly Argon2idPasswordHasher _hasher = new();

    [Fact]
    public void Verifies_a_hash_produced_by_the_reference_implementation()
    {
        _hasher.Verify(ReferenceHash, "correct horse battery staple").Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_reference_hash_for_the_wrong_password()
    {
        _hasher.Verify(ReferenceHash, "correct horse battery stapl").Should().BeFalse();
    }

    [Fact]
    public void Produces_the_phc_format_with_the_configured_parameters()
    {
        var encoded = _hasher.Hash("correct horse battery staple");

        encoded.Should().StartWith("$argon2id$v=19$m=19456,t=2,p=1$");
        encoded.Split('$').Should().HaveCount(6);
    }

    [Fact]
    public void Verifies_its_own_hash()
    {
        var encoded = _hasher.Hash("s3cret-passw0rd");

        _hasher.Verify(encoded, "s3cret-passw0rd").Should().BeTrue();
        _hasher.Verify(encoded, "s3cret-passw0rD").Should().BeFalse();
    }

    [Fact]
    public void Uses_a_fresh_salt_for_every_hash()
    {
        _hasher.Hash("same-password").Should().NotBe(_hasher.Hash("same-password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=16$m=19456,t=2,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=999999999,t=2,p=1$c2FsdA$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$!!!$aGFzaA")]
    public void Rejects_malformed_hashes_without_throwing(string encoded)
    {
        _hasher.Invoking(h => h.Verify(encoded, "password")).Should().NotThrow();
        _hasher.Verify(encoded, "password").Should().BeFalse();
    }

    [Fact]
    public void Rejects_empty_inputs()
    {
        _hasher.Verify(string.Empty, "password").Should().BeFalse();
        _hasher.Verify(ReferenceHash, string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Refuses_to_hash_an_empty_password()
    {
        _hasher.Invoking(h => h.Hash(string.Empty)).Should().Throw<ArgumentException>();
    }
}
