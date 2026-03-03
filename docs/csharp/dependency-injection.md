# Dependency Injection

DI is core to .NET — it's built into the framework. You should be able to explain lifetimes confidently and know when to use each one.

---

## What Is Dependency Injection?

DI is a pattern where a class's dependencies are **provided to it** (injected) rather than the class creating them itself. In .NET Core / ASP.NET Core, the built-in IoC container manages this automatically.

```csharp
// ❌ Without DI — class creates its own dependency (tight coupling)
public class OrderService
{
    private readonly IOrderRepository _repo = new SqlOrderRepository();  // ← hardcoded
}

// ✅ With DI — dependency injected via constructor
public class OrderService
{
    private readonly IOrderRepository _repo;

    public OrderService(IOrderRepository repo)  // ← injected
    {
        _repo = repo;
    }
}
```

---

## Registering Services in .NET

```csharp
// Program.cs (.NET 6+)
var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<IReportGenerator, PdfReportGenerator>();

// Register with a factory (when you need conditional logic)
builder.Services.AddScoped<IOrderRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return config["Database:Type"] == "InMemory"
        ? new InMemoryOrderRepository()
        : new SqlOrderRepository(config.GetConnectionString("Default"));
});

var app = builder.Build();
```

---

## Service Lifetimes

This is the most important DI concept to understand for interviews.

### Singleton

**One instance for the entire application lifetime.**

```csharp
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
```

- Created once, shared by everything
- Thread-safe: must be written to be used from multiple threads simultaneously
- **Good for:** Configuration objects, caches, HTTP clients (`IHttpClientFactory`), logging
- **Bad for:** Database contexts, anything with per-request state

### Scoped

**One instance per request (in web apps) / one per scope.**

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<AppDbContext>();  // EF Core registers itself as Scoped
```

- Created once per HTTP request, disposed at end of request
- All classes in the same request share the same instance
- **Good for:** Database contexts (EF Core `DbContext`), unit of work, services with request-level state
- **Bad for:** Background services (no HTTP request scope — need to create a scope manually)

### Transient

**New instance every time it's requested from the container.**

```csharp
builder.Services.AddTransient<IReportGenerator, PdfReportGenerator>();
```

- Always fresh, never shared
- **Good for:** Lightweight, stateless services with no shared state
- **Bad for:** Heavy objects (creates a new one every injection, GC pressure)

---

## The Captive Dependency Problem

**Never inject a shorter-lived service into a longer-lived service.**

```csharp
// ❌ Captive dependency — Singleton holds a reference to a Scoped service
//    The Scoped service never gets disposed — it lives for the app lifetime
public class MyBackgroundService  // Singleton
{
    private readonly IOrderRepository _repo;  // Scoped — will be captured!

    public MyBackgroundService(IOrderRepository repo)  // ← Bad!
    {
        _repo = repo;
    }
}

// ✅ Fix — inject IServiceScopeFactory and create a scope manually
public class MyBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MyBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        await repo.DoWorkAsync();
    }
}
```

.NET will throw a `InvalidOperationException` at runtime if you try to inject a Scoped service into a Singleton (in development mode with validation enabled).

---

## Injection Types

### Constructor Injection (preferred)

```csharp
public class OrderService
{
    private readonly IOrderRepository _repo;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository repo, ILogger<OrderService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }
}
```

Always prefer constructor injection — dependencies are explicit and required.

### Property Injection

Not natively supported in .NET's DI but available in third-party containers (Autofac). Avoid in most cases.

### Method Injection (via `[FromServices]` in controllers)

```csharp
[HttpGet]
public IActionResult GetOrders([FromServices] IOrderService orderService)
{
    return Ok(orderService.GetAll());
}
```

Useful for injecting a service into a single action method without putting it in the constructor.

---

## Resolving Services Manually

```csharp
// In Program.cs before app.Run()
var service = app.Services.GetRequiredService<IOrderService>();  // throws if not registered
var service = app.Services.GetService<IOrderService>();          // returns null if not registered

// In a factory / background service
var scope   = _serviceScopeFactory.CreateScope();
var service = scope.ServiceProvider.GetRequiredService<IOrderService>();
// ... don't forget to dispose the scope
```

---

## Lifetime Quick Reference

| Lifetime | New instance | Scope | Use for |
|---|---|---|---|
| **Singleton** | Once per app | Shared by all | Config, cache, HTTP clients |
| **Scoped** | Once per request | Shared within request | DbContext, unit of work |
| **Transient** | Every injection | Never shared | Stateless utilities |
