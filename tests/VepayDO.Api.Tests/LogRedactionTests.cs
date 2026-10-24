using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using VepayDO.Infrastructure.Logging;

namespace VepayDO.Api.Tests;

public sealed partial class LogRedactionTests
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
}
