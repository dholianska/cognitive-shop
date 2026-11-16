using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.IdentityServer;
using Platform.IdentityServer.Data;
using Platform.IdentityServer.Models;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Окрема БД IdentityServerDb (на окремому SQL Server, див. AppHost): ізоляція безпеки від бізнес-сервісів
var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityServerDb' is missing");
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddRazorPages();   // Quickstart UI

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddIdentityServer(options =>
{
    options.IssuerUri = builder.Configuration["IdentityServer:Authority"];   // стабільний issuer = Authority сервісів
    options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;            // розробка на HTTP: SameSite=None без Secure браузер відкине
    options.EmitStaticAudienceClaim = false;                                 // aud беремо з ApiResources
})
    .AddAspNetIdentity<ApplicationUser>()
    .AddConfigurationStore(options => options.ConfigureDbContext = b =>
        b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)))
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
        options.EnableTokenCleanup = true;
    });

var app = builder.Build();

// міграції трьох контекстів + початкові дані (ресурси, clients, користувачі)
using (var scope = app.Services.CreateScope())
    await SeedData.InitializeAsync(scope.ServiceProvider);

app.UseCorrelationId();
app.MapDefaultEndpoints();

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages().RequireAuthorization();

app.Run();