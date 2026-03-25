using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);
ValidateProductionConfiguration(builder);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHostedService<MyApp.Services.EmailRoundWatcher>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<PlayerService>();
builder.Services
    .AddOptions<RoundNotificationEmailOptions>()
    .Bind(builder.Configuration.GetSection("RoundNotificationEmail"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<RoundOperationsOptions>()
    .Bind(builder.Configuration.GetSection("RoundOperations"));
builder.Services.AddScoped<IRoundNotificationEmailService, RoundNotificationEmailService>();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    });

var app = builder.Build();

// Fail fast if the deployed database is not aligned with the compiled EF Core model.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var pendingMigrations = db.Database.GetPendingMigrations().ToArray();
    if (pendingMigrations.Length > 0)
    {
        throw new InvalidOperationException(
            "Database schema is not up to date. Run EF Core migrations during deployment before starting the app. " +
            $"Pending migrations: {string.Join(", ", pendingMigrations)}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

static void ValidateProductionConfiguration(WebApplicationBuilder builder)
{
    if (!builder.Environment.IsProduction())
    {
        return;
    }

    var missing = new List<string>();
    var configuration = builder.Configuration;

    static void RequireValue(IConfiguration configuration, string key, ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
        {
            missing.Add(key.Replace(':', '_'));
        }
    }

    RequireValue(configuration, "ConnectionStrings:DefaultConnection", missing);
    RequireValue(configuration, "Authentication:Google:ClientId", missing);
    RequireValue(configuration, "Authentication:Google:ClientSecret", missing);
    RequireValue(configuration, "RoundNotificationEmail:CredentialsFilePath", missing);
    RequireValue(configuration, "RoundNotificationEmail:TokenDirectoryPath", missing);
    RequireValue(configuration, "RoundNotificationEmail:SenderUserId", missing);
    RequireValue(configuration, "RoundNotificationEmail:FromAddress", missing);
    RequireValue(configuration, "RoundNotificationEmail:ApplicationName", missing);
    RequireValue(configuration, "RoundNotificationEmail:SiteUrl", missing);

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString)
        && (connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("mssqllocaldb", StringComparison.OrdinalIgnoreCase)))
    {
        missing.Add("ConnectionStrings__DefaultConnection (must reference a real SQL Server instance, not LocalDB)");
    }

    if (missing.Count == 0)
    {
        return;
    }

    throw new InvalidOperationException(
        $"Missing required production configuration values: {string.Join(", ", missing)}. " +
        "Provide these values through environment variables or your host secret manager.");
}
