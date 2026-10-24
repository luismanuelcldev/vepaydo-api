using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Formatting.Json;
using VepayDO.Api.Extensions;
using VepayDO.Api.Kernel.Http;
using VepayDO.Api.Kernel.Routing;
using VepayDO.Infrastructure.Configuration;
using VepayDO.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Destructure.With<RedactingDestructuringPolicy>()
    .WriteTo.Console(new JsonFormatter(renderMessage: false)));

var appConfig = AppConfigFactory.Create(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddVepayDOServices(appConfig);
builder.Services.AddVepayDOAuthentication(appConfig);
builder.Services.AddVepayDORateLimiter(builder.Configuration);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationActionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddMvcOptions(options =>
    {
        options.Conventions.Add(new RoutePrefixConvention("api/v1"));
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

if (!builder.Environment.IsProduction())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();
app.UseVepayDOPipeline();

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
