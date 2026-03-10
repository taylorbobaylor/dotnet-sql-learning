using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

using SqlDemos.Shared;
using SqlDemosApi;

var builder = WebApplication.CreateBuilder(args);

// ── Connection string ────────────────────────────────────────────────────────
// Local dev: credentials are NOT committed to appsettings.json.
// Set via dotnet user-secrets:  dotnet user-secrets set "ConnectionStrings:InterviewDemo" "Server=..."
// Kubernetes: set the ConnectionStrings__InterviewDemo environment variable in the pod spec.
var connectionString = builder.Configuration.GetConnectionString("InterviewDemo") is { Length: > 0 } cs
    ? cs
    : throw new InvalidOperationException(
        "Connection string 'InterviewDemo' is missing or empty. " +
        "Set it via dotnet user-secrets or the ConnectionStrings__InterviewDemo environment variable. " +
        "See README.md for setup instructions.");

// ── Scenario catalog ─────────────────────────────────────────────────────────
// Built once at startup and shared across all consumers (list endpoint + BenchmarkService)
// so definitions never diverge and Year - 1 is computed from a single consistent point in time.
var catalog = ScenarioCatalog.Build(DateTimeOffset.UtcNow.Year - 1);

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));
builder.Services.AddSingleton<IProcTimer, ProcTimer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(catalog); // shared ScenarioDefinition[] injected into BenchmarkService
builder.Services.AddSingleton<IBenchmarkService, BenchmarkService>();

builder.Services.AddOpenApi();

// RFC 7807 problem-details for all unhandled exceptions.
builder.Services.AddProblemDetails();

// CORS — allows the Angular dashboard (default port 4200) to call the API.
// For production/Kubernetes, override AllowedOrigins in appsettings.json or environment variables.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
    options.AddPolicy("Dashboard", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()));

// Rate-limit the benchmark endpoints — each run hits the database.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("benchmark", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0; // reject immediately when limit is hit; don't queue benchmark requests
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
// UseExceptionHandler must come before routing so it wraps all endpoint exceptions.
app.UseExceptionHandler();
// Only redirect to HTTPS when running outside a container (the container only exposes HTTP on 8080).
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseCors("Dashboard");
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference(opts =>
{
    opts.Title = ".NET SQL Demos API";
});

// ── Health ───────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow,
}))
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Liveness / readiness probe used by Kubernetes");

// ── Scenarios — list (static metadata, no DB) ────────────────────────────────
// Derived from ScenarioCatalog so names and proc names stay in sync with what runs.
// Pre-built once at startup; returned as-is on every request (no per-request allocation).
var scenarioListResult = Results.Ok(
    catalog.Select(s => new ScenarioInfo(
        s.Id,
        s.Name,
        s.Antipattern,
        s.Fix,
        s.Runs.First(r => r.IsBad && !r.IsWarmup).ProcName.Replace("dbo.", ""),
        s.Runs.First(r => !r.IsBad && !r.IsWarmup).ProcName.Replace("dbo.", "")))
    .ToArray());

app.MapGet("/scenarios", () => scenarioListResult)
.WithName("ListScenarios")
.WithTags("Scenarios")
.WithSummary("List all 6 benchmark scenarios (no DB calls — metadata only)");

// ── Scenarios — run all ───────────────────────────────────────────────────────
app.MapGet("/scenarios/all", async (IBenchmarkService benchmarkService, CancellationToken cancellationToken) =>
{
    var result = await benchmarkService.RunAllAsync(cancellationToken);
    return Results.Ok(result);
})
.WithName("RunAllScenarios")
.WithTags("Scenarios")
.WithSummary("Run all 6 bad-vs-fixed stored procedure pairs and return timing results")
.WithDescription(
    "Executes all 6 scenario pairs concurrently. Each pair runs the 'bad' stored procedure " +
    "then the 'fixed' version(s) and returns elapsed milliseconds, row counts, and an improvement factor. " +
    "Scenario 2 includes a cache-poisoning warmup call to demonstrate the problem realistically.")
.RequireRateLimiting("benchmark");

// ── Scenarios — run one ───────────────────────────────────────────────────────
// Pre-allocated; avoids Enumerable.Range allocation on every bad-request response.
var validScenarioIds = Enumerable.Range(1, ScenarioCatalog.Count).ToArray();

app.MapGet("/scenarios/{id:int}", async (int id, IBenchmarkService benchmarkService, CancellationToken cancellationToken) =>
{
    if (id is < 1 or > ScenarioCatalog.Count)
    {
        return Results.BadRequest(new
        {
            error = $"Scenario id must be between 1 and {ScenarioCatalog.Count}. Got: {id}.",
            valid = validScenarioIds,
        });
    }

    var result = await benchmarkService.RunScenarioAsync(id, cancellationToken);
    return Results.Ok(result);
})
.WithName("RunScenario")
.WithTags("Scenarios")
.WithSummary($"Run a single bad-vs-fixed scenario pair (1–{ScenarioCatalog.Count})")
.WithDescription("Valid ids: 1=Cursor, 2=ParameterSniffing, 3=NonSargable, 4=SelectStar, 5=KeyLookup, 6=ScalarUdf")
.RequireRateLimiting("benchmark");

app.Run();

// Enables WebApplicationFactory<Program> in integration tests.
public partial class Program;

