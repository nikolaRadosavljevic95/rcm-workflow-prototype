using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Rcm.Api.Auth;
using Rcm.Api.Data;
using Rcm.Api.Domain;
using Rcm.Api.Features;
using Rcm.Api.Features.Absences;
using Rcm.Api.Features.Cycles;
using Rcm.Api.Features.Demo;
using Rcm.Api.Features.Me;
using Rcm.Api.Features.Users;
using Rcm.Api.Features.WorkItems;

var builder = WebApplication.CreateBuilder(args);

// The demo sign-in is the only authentication, and it exists only in Development.
// Anywhere else every request would fail, so refuse to start instead.
if (!builder.Environment.IsDevelopment())
{
    Console.Error.WriteLine(
        $"Rcm.Api has no sign-in for the '{builder.Environment.EnvironmentName}' environment. " +
        "Run it in Development, where the demo sign-in is registered. Production sign-in (docs/05, Auth) is not built yet.");
    return 1;
}

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "rcm.db");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));

var timeZone = TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["Business:TimeZone"] ?? "America/New_York");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(services => new BusinessCalendar(services.GetRequiredService<TimeProvider>(), timeZone));

builder.Services.AddProblemDetails();

// Development otherwise throws on a malformed body, which the exception handler turns into a 500.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services
    .AddAuthentication(DemoAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthHandler>(DemoAuthHandler.SchemeName, null);
builder.Services.AddOpenApi();

builder.Services.AddScoped<RequestContextLoader>();
builder.Services.AddScoped<GetMeHandler>();
builder.Services.AddScoped<GetUsersHandler>();
builder.Services.AddScoped<ResetDemoDataHandler>();
builder.Services.AddScoped<GetCyclesHandler>();
builder.Services.AddScoped<OpenCycleHandler>();
builder.Services.AddScoped<CloseCycleHandler>();
builder.Services.AddScoped<WorkItemReader>();
builder.Services.AddScoped<GetMyWorkHandler>();
builder.Services.AddScoped<GetOverviewHandler>();
builder.Services.AddScoped<GetWorkItemHandler>();
builder.Services.AddScoped<TransitionWorkItemHandler>();
builder.Services.AddScoped<AddCommentHandler>();
builder.Services.AddScoped<GetAbsencesHandler>();
builder.Services.AddScoped<CreateAbsenceHandler>();
builder.Services.AddScoped<UpdateAbsenceHandler>();
builder.Services.AddScoped<DeleteAbsenceHandler>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(db, scope.ServiceProvider.GetRequiredService<BusinessCalendar>());
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapDemoEndpoints();
app.MapMeEndpoints();
app.MapUsersEndpoints();
app.MapCyclesEndpoints();
app.MapWorkItemsEndpoints();
app.MapAbsencesEndpoints();

app.Run();
return 0;
