using Serilog;
using Serilog.Formatting.Json;
using VepayDO.Application.Receipts;
using VepayDO.Infrastructure;
using VepayDO.Infrastructure.Configuration;
using VepayDO.Infrastructure.Logging;
using VepayDO.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    // Never let a sensitive field (sender, reference, token, password, email)
    // reach a sink, even if something logs a whole object (AGENTS.md section 13).
    .Destructure.With<RedactingDestructuringPolicy>()
    .WriteTo.Console(new JsonFormatter(renderMessage: false)));

// The worker binds the same configuration as the API: a misconfigured process
// fails at boot instead of on the first job.
var appConfig = AppConfigFactory.Create(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(new WorkerSettings(appConfig));
builder.Services.AddInfrastructure(appConfig);
builder.Services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
builder.Services.AddHostedService<ReceiptProcessingWorker>();

await builder.Build().RunAsync().ConfigureAwait(false);
