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

    .Destructure.With<RedactingDestructuringPolicy>()
    .WriteTo.Console(new JsonFormatter(renderMessage: false)));

var appConfig = AppConfigFactory.Create(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(new WorkerSettings(appConfig));
builder.Services.AddInfrastructure(appConfig);
builder.Services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
builder.Services.AddHostedService<ReceiptProcessingWorker>();

await builder.Build().RunAsync().ConfigureAwait(false);
