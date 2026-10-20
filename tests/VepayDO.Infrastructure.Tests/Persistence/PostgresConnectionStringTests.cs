using FluentAssertions;
using VepayDO.Infrastructure.Persistence;

namespace VepayDO.Infrastructure.Tests.Persistence;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void Converts_a_url_into_a_keyword_connection_string()
    {
        var normalized = PostgresConnectionString.Normalize(
            "postgresql://app_user:s3cret@db.internal:5433/vepaydo?sslmode=require");

        normalized.Should().Be(
            "Host=db.internal;Port=5433;Database=vepaydo;Username=app_user;Password=s3cret;SSL Mode=Require");
    }

    [Fact]
    public void Accepts_the_legacy_postgres_scheme()
    {
        PostgresConnectionString.Normalize("postgres://localhost/vepaydo")
            .Should().Be("Host=localhost;Database=vepaydo");
    }

    [Fact]
    public void Decodes_url_escaped_credentials()
    {
        PostgresConnectionString.Normalize("postgresql://user%40mail:p%40ss@host/db")
            .Should().Contain("Username=user@mail")
            .And.Contain("Password=p@ss");
    }

    [Theory]
    [InlineData("Host=localhost;Database=vepaydo")]
    [InlineData("Server=localhost;Port=5432;Database=vepaydo;User Id=app;Password=x")]
    public void Passes_a_keyword_connection_string_through_untouched(string value)
    {
        PostgresConnectionString.Normalize(value).Should().Be(value);
    }

    [Fact]
    public void Empty_and_null_values_normalize_to_empty()
    {
        PostgresConnectionString.Normalize(null).Should().BeEmpty();
        PostgresConnectionString.Normalize("   ").Should().BeEmpty();
    }
}
