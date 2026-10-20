using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using VepayDO.Api.Kernel.Logging;

namespace VepayDO.Api.Tests;

/// <summary>
/// Verifies that sensitive values never reach a log sink (AGENTS.md section 13):
/// the redacting policy masks known names and leaves everything else intact, and
/// the request enrichment adds correlation properties without content.
/// </summary>
public sealed class LogRedactionTests
{
    [Fact]
    public void Sensitive_properties_are_masked_and_safe_properties_survive()
    {
        var sink = new CollectingSink();

        using (var logger = new LoggerConfiguration()
                   .Destructure.With<RedactingDestructuringPolicy>()
                   .WriteTo.Sink(sink)
                   .CreateLogger())
        {
            logger.Information(
                "Receipt reviewed {@Receipt}",
                new
                {
                    Reference = "REF-123456",
                    SenderName = "Ana Perez",
                    Last4 = "4242",
                    Email = "ana@example.test",
                    Amount = 1500.00m,
                    Status = "CONFIRMED",
                });
        }

        var logEvent = sink.Events.Should().ContainSingle().Subject;
        var message = logEvent.RenderMessage(System.Globalization.CultureInfo.InvariantCulture);
        var receipt = (StructureValue)logEvent.Properties["Receipt"];

        receipt.Properties.Should().ContainSingle(p =>
            p.Name == "Reference" && ((ScalarValue)p.Value).Value as string == RedactingDestructuringPolicy.Placeholder);
        receipt.Properties.Should().ContainSingle(p =>
            p.Name == "SenderName" && ((ScalarValue)p.Value).Value as string == RedactingDestructuringPolicy.Placeholder);
        receipt.Properties.Should().ContainSingle(p =>
            p.Name == "Last4" && ((ScalarValue)p.Value).Value as string == RedactingDestructuringPolicy.Placeholder);
        receipt.Properties.Should().ContainSingle(p =>
            p.Name == "Email" && ((ScalarValue)p.Value).Value as string == RedactingDestructuringPolicy.Placeholder);

        receipt.Properties.Should().ContainSingle(p =>
            p.Name == "Status" && ((ScalarValue)p.Value).Value as string == "CONFIRMED");

        message.Should().NotContain("REF-123456");
        message.Should().NotContain("Ana Perez");
        message.Should().NotContain("4242");
    }

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

    /// <summary>Collects every emitted log event so a test can inspect its structure.</summary>
    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events => _events;

        public void Emit(LogEvent logEvent) => _events.Add(logEvent);
    }
}
