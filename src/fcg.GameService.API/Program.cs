using fcg.GameService.API.Middlewares;
// using fcg.GameService.API.Workers;
using fcg.GameService.Application;
using fcg.GameService.Application.Interfaces;
using fcg.GameService.Application.Mappers;
using fcg.GameService.Infrastructure;
using fcg.GameService.Infrastructure.Adapters;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
// using fcg.GameService.Infrastructure.Configurations;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Inicializando aplicação");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {TraceId} {SpanId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.OpenTelemetry();
    });

    builder.Services.AddSingleton(typeof(IAppLogger<>), typeof(MicrosoftLogAdapter<>));

    builder.Services.AddEndpointsApiExplorer();
    var pathBase = Environment.GetEnvironmentVariable("PATH_BASE");
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "FCG Games",
            Version = "v1",
            Description = "Microsserviço responsável pelo controle dos jogos da Fiap Cloud Games."
        });
        
        if (!string.IsNullOrEmpty(pathBase))
        {
            options.AddServer(new OpenApiServer
            {
                Url = pathBase.TrimEnd('/')
            });
        }
    });

    builder.Services.AddProblemDetails();

    // Worker não é mais necessário pois o masstransit faz esse trabalho.
    // builder.Services.AddHostedService<GamePurchaseWorker>();

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();
    MappingConfig.RegisterMappings();

    builder.Services.Configure<JsonOptions>(options =>
    {
        options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.SerializerOptions.WriteIndented = true;
    });

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

    WebApplication app = builder.Build();

    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });

    app.UseSwaggerUI(options =>
    {
        var swaggerPath = string.IsNullOrEmpty(pathBase) 
            ? "./v1/swagger.json" 
            : $"{pathBase.TrimEnd('/')}/swagger/v1/swagger.json";
        options.SwaggerEndpoint(swaggerPath, "FCG Games v1");
        options.RoutePrefix = "swagger";
    });

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<BodyValidationMiddleware>();

    app.MapControllers();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(x => new
                {
                    name = x.Key,
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    exception = x.Value.Exception?.Message,
                    duration = x.Value.Duration
                })
            };
            await context.Response.WriteAsJsonAsync(response);
        }
    });

    Log.Information("Aplicação inicializada com sucesso.");

    // Configura para exibir URLs quando o servidor iniciar
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() =>
    {
        Console.WriteLine("\nServidor iniciado com sucesso!");
        Console.WriteLine("URLs disponíveis:");
        Console.WriteLine("   • http://localhost:5002");
        Console.WriteLine("   • https://localhost:7127");
        Console.WriteLine("Swagger UI: http://localhost:5002/swagger");
        Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Erro ao inicializar a aplicação.");
}
finally
{
    Log.CloseAndFlush();
}

