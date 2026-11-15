using System.Text;
using Auth.Api.Data;
using Auth.Api.Models;
using Auth.Api.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Рядок підключення приходить від Aspire (WithReference(identityDb)) як ConnectionStrings__IdentityDb
var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' is missing");

builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString));

// ASP.NET Identity: вимоги до пароля та lockout
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Токени підтвердження email і скидання пароля мають обмежений термін дії
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

// JWT
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwt = jwtSection.Get<JwtSettings>() ?? throw new InvalidOperationException("Section 'Jwt' is missing");
if (jwt.SecretKey.Length < 32)
    throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters (HS256)");
builder.Services.Configure<JwtSettings>(jwtSection);

// Увага: AddIdentity виставляє схему за замовчуванням = cookie, тому Default* перевизначаємо на JWT,
// інакше [Authorize] робитиме редірект на логін замість 401.
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Міграції + ролі + тестовий адмін
using (var scope = app.Services.CreateScope())
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);

app.UseCorrelationId();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();