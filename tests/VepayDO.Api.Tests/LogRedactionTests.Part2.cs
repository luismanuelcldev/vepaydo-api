using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using VepayDO.Infrastructure.Logging;

namespace VepayDO.Api.Tests;

public sealed partial class LogRedactionTests
{

    [Fact]
    public void Token_and_password_fields_are_masked_case_insensitively()
    {
        var sink = new CollectingSink();

        using (var logger = new LoggerConfiguration()
                   .Destructure.With<RedactingDestructuringPolicy>()
                   .WriteTo.Sink(sink)
                   .CreateLogger())
        {
            logger.Information(
                "Session {@Session}",
                new
                {
                    TokenHash = "deadbeef",
                    RefreshToken = "rotating-value",
                    JwtAccessSecret = "super-secret",
                    UserId = Guid.Empty,
                });
        }

        var session = (StructureValue)sink.Events.Single().Properties["Session"];

        foreach (var property in session.Properties)
        {
            var value = ((ScalarValue)property.Value).Value as string;
            if (property.Name is "TokenHash" or "RefreshToken" or "JwtAccessSecret")
            {
                value.Should().Be(
                    RedactingDestructuringPolicy.Placeholder,
                    $"{property.Name} must be masked");
            }
        }
    }

    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events => _events;

        public void Emit(LogEvent logEvent) => _events.Add(logEvent);
    }
}
